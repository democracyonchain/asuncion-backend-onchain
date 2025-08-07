using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using asuncion_cardano_api.Models;
using asuncion_cardano_api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsuncionCardanoApi.Services
{
    public class CardanoTransactionService
    {
        private readonly ILogger<CardanoTransactionService> _logger;
        private readonly string _authorizedAddress;
        private readonly string _networkParam;
        private readonly string _keyDirectory;
        private readonly string _senderAddress;
        private const long MontoADA = 2_000_000;
        private const long FEE_MARGIN = 1000; // Adding a small margin to prevent fee-too-small errors

        public CardanoTransactionService(
            ILogger<CardanoTransactionService> logger,
            IOptions<CardanoSettings> settings)
        {
            _logger = logger;
            _authorizedAddress = settings.Value.AuthorizedAddress;

            var config = settings.Value;

            _networkParam = $"--testnet-magic {config.NetworkMagic}";
            _keyDirectory = config.KeyDirectory;
            _senderAddress = config.AuthorizedAddress;
        }


        public async Task<string> ExecuteTransaction(string recipientAddress, long amountLovelace, string metadataFile)
        {
            _logger.LogInformation($"Iniciando transacción a {recipientAddress} por {amountLovelace} lovelace...");

            var utxos = await ObtenerUtxosDisponibles();
            var utxo = SeleccionarUtxoSuficiente(utxos, amountLovelace);
            if (utxo == null)
                throw new Exception("No se encontraron UTXOs con saldo suficiente.");

            await ExecuteCommandAsync($"cardano-cli query protocol-parameters {_networkParam} --out-file /tmp/protocol.json");

            // Paso 1: construir borrador con fee = 0 y salida vacía para cambio
            await BuildTransactionDraft(utxo, recipientAddress, amountLovelace, metadataFile);

            // Paso 2: calcular fee con un margen adicional
            long fee = await CalcularFee();

            // Paso 3: calcular cambio exacto
            long change = utxo.Amount - amountLovelace - fee;
            if (change < 0)
                throw new Exception($"Fondos insuficientes: cambio negativo {change}.");

            // Paso 4: construir transacción final con cambio y fee correcto
            await BuildFinalTransaction(utxo, recipientAddress, amountLovelace, change, fee, metadataFile);

            // Paso 5: firmar y enviar
            await SignTransaction();

            // Validar la transacción antes de enviarla
            await ValidateTransaction();

            await SubmitTransaction();

            string txId = await GetTransactionId("/tmp/tx.signed");
            return $"Transacción enviada con éxito. TxID: {txId}";
        }

        public async Task<string> EjecutarTransaccionMetadataAsync(Acta acta)
        {
            try
            {
                string tempFilePath = Path.Combine("/tmp", $"acta-{acta.Codigo}.metadata.json");

                var metadata = new Dictionary<string, object>
                {
                    ["674"] = acta
                };

                /*string json = JsonSerializer.Serialize(metadata);
                await System.IO.File.WriteAllTextAsync(tempFilePath, json);*/
                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                };

                string json = JsonSerializer.Serialize(metadata, jsonOptions);
                await System.IO.File.WriteAllTextAsync(tempFilePath, json);

                _logger.LogInformation("Usando transacción normal");
                    return await ExecuteTransaction(
                        _authorizedAddress,
                        2_000_000,
                        tempFilePath
                    );
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar transacción con metadata");
                throw;
            }
        }
        private async Task<List<UtxoInfo>> ObtenerUtxosDisponibles()
        {
            string command = $"cardano-cli query utxo --address {_senderAddress} {_networkParam}";
            string output = await ExecuteCommandAsync(command);

            _logger.LogInformation("UTXOs disponibles:\n" + output);
            return ParseUtxoOutput(output);
        }

        private UtxoInfo? SeleccionarUtxoSuficiente(List<UtxoInfo> utxos, long monto)
        {
            // Incrementando el margen requerido para tener en cuenta los posibles errores de fee
            return utxos.Find(utxo => utxo.Amount >= (monto + 600_000));
        }

        private async Task BuildTransactionDraft(UtxoInfo utxo, string recipient, long amount, string metadata)
        {
            string command =
                $"cardano-cli conway transaction build-raw " +
                $"--tx-in {utxo.TxHash}#{utxo.TxIx} " +
                $"--tx-out {recipient}+{amount} " +
                $"--tx-out {_senderAddress}+0 " +
                $"--fee 0 " +
                $"--metadata-json-file {metadata} " +
                $"--out-file /tmp/tx.draft";
            await ExecuteCommandAsync(command);
        }

        private async Task<long> CalcularFee()
        {
            string command =
                $"cardano-cli conway transaction calculate-min-fee " +
                $"--tx-body-file /tmp/tx.draft " +
                $"--tx-in-count 1 --tx-out-count 2 " +
                $"{_networkParam} --witness-count 1 " +
                $"--protocol-params-file /tmp/protocol.json";

            string output = await ExecuteCommandAsync(command);
            long baseFee = ExtractFee(output);

            // Agregamos un margen para evitar el error de fee insuficiente
            long adjustedFee = baseFee + FEE_MARGIN;
            _logger.LogInformation($"Fee calculado: {baseFee}, Fee ajustado: {adjustedFee}");

            return adjustedFee;
        }

        private async Task BuildFinalTransaction(UtxoInfo utxo, string recipient, long amount, long change, long fee, string metadata)
        {
            string command =
                $"cardano-cli conway transaction build-raw " +
                $"--tx-in {utxo.TxHash}#{utxo.TxIx} " +
                $"--tx-out {recipient}+{amount} " +
                $"--tx-out {_senderAddress}+{change} " +
                $"--fee {fee} " +
                $"--metadata-json-file {metadata} " +
                $"--out-file /tmp/tx.raw";

            await ExecuteCommandAsync(command);
        }

       

        private async Task SignTransaction()
        {
            string command =
                $"cardano-cli conway transaction sign " +
                $"--tx-body-file /tmp/tx.raw " +
                $"--signing-key-file {_keyDirectory}/payment.skey " +
                $"{_networkParam} " +
                $"--out-file /tmp/tx.signed";
            await ExecuteCommandAsync(command);
        }

        private async Task ValidateTransaction()
        {
            try
            {
                string command = $"cardano-cli conway transaction view --tx-file /tmp/tx.signed";
                string output = await ExecuteCommandAsync(command);
                _logger.LogInformation("Validación de transacción:\n" + output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error al validar la transacción: {ex.Message}");
                // Continuamos de todos modos, ya que esto es solo informativo
            }
        }

        private async Task SubmitTransaction()
        {
            string command = $"cardano-cli conway transaction submit --tx-file /tmp/tx.signed {_networkParam}";
            await ExecuteCommandAsync(command);
        }

        private async Task<string> GetTransactionId(string signedTxFile)
        {
            string command = $"cardano-cli conway transaction txid --tx-file {signedTxFile}";
            string output = await ExecuteCommandAsync(command);
            return output.Trim();
        }

        private List<UtxoInfo> ParseUtxoOutput(string output)
        {
            var utxos = new List<UtxoInfo>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("TxHash") || line.StartsWith("-")) continue;

                var parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length >= 3)
                {
                    utxos.Add(new UtxoInfo
                    {
                        TxHash = parts[0],
                        TxIx = int.Parse(parts[1]),
                        Amount = long.Parse(parts[2])
                    });
                }
            }

            return utxos;
        }

        private long ExtractFee(string feeOutput)
        {
            var match = Regex.Match(feeOutput, @"(\d+)\s+Lovelace");
            return match.Success ? long.Parse(match.Groups[1].Value) : 200_000;
        }

        private async Task<string> ExecuteCommandAsync(string command)
        {
            _logger.LogInformation($"Ejecutando comando: {command}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError($"Error ejecutando comando. Código: {process.ExitCode}, Error: {error}");
                throw new Exception($"Error ejecutando comando: {error}");
            }

            return output.Trim();
        }
    }

    public class UtxoInfo
    {
        public string? TxHash { get; set; }
        public int TxIx { get; set; }
        public long Amount { get; set; }
    }
}