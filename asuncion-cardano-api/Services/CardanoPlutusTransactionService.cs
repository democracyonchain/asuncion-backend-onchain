using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using asuncion_cardano_api.Models;
using AsuncionCardanoApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace asuncion_cardano_api.Services
{
    public class CardanoPlutusTransactionService
    {
        private readonly ILogger<CardanoPlutusTransactionService> _logger;
        private readonly string _networkParam;
        private readonly string _keyDirectory;
        private readonly string _senderAddress;
        private readonly ValidatorService _validatorService;
        private readonly UtxoFinderService _utxoFinderService;

        public CardanoPlutusTransactionService(
            ILogger<CardanoPlutusTransactionService> logger,
            IOptions<CardanoSettings> settings,
            ValidatorService validatorService,
            UtxoFinderService utxoFinderService)
        {
            _logger = logger;
            var config = settings.Value;
            _networkParam = $"--testnet-magic {config.NetworkMagic}";
            _keyDirectory = config.KeyDirectory;
            _senderAddress = config.AuthorizedAddress;
            _validatorService = validatorService;
            _utxoFinderService = utxoFinderService;
        }

        public async Task<string> EjecutarPlutusTransactionDesdeActa(Acta acta, string validatorName)
        {
            var scriptUtxo = await _utxoFinderService.BuscarUtxoEnScriptAddress(validatorName);
            var collateralUtxo = await _utxoFinderService.BuscarUtxoConMayorAdaEnPaymentAddress();
            var validatorPayload = _validatorService.GetValidatorPayload(acta, validatorName);

            string scriptFile = Path.Combine("Resources", "script_plutus", $"{validatorName.Replace('.', '_')}.plutus");

            if (!File.Exists(scriptFile))
                throw new FileNotFoundException($"No se encontró el archivo del validador: {scriptFile}");

            string metadataFile = await GuardarMetadataTemporalAsync(acta);            

            return await ExecutePlutusTransaction(
                scriptUtxo,
                collateralUtxo,
                _senderAddress,
                2_000_000,
                metadataFile,
                scriptFile,
                validatorPayload.Redeemer

            );
        }

        public async Task<string> ExecutePlutusTransaction(
        UtxoInfo scriptUtxo,
        UtxoInfo collateralUtxo,
        string recipientAddress,
        long amountLovelace,
        string metadataFile,
        string scriptFile,
        int redeemerValue)
        {
            _logger.LogInformation($"Iniciando transacción Plutus a {recipientAddress}...");

            long estimatedFee = 250_000;
            if (collateralUtxo.Amount < (amountLovelace + estimatedFee))
            {
                throw new Exception($"El UTxO de colateral ({collateralUtxo.Amount} lovelace) no cubre la salida de {amountLovelace} + fee estimado {estimatedFee}.");
            }

            await BuildPlutusTransactionDraft(scriptUtxo, collateralUtxo, recipientAddress, amountLovelace, metadataFile, scriptFile, redeemerValue);

            await SignTransaction();
            await ValidateTransaction();
            await SubmitTransaction();

            string txId = await GetTransactionId("/tmp/tx.signed");
            return $"Plutus Transacción enviada con éxito. TxID: {txId}";
        }


        private async Task BuildPlutusTransactionDraft(
        UtxoInfo scriptUtxo,
        UtxoInfo collateralUtxo,
        string recipient,
        long amount,
        string metadata,
        string scriptFile,
        int redeemerValue)
        {
            string redeemerPath = SaveRedeemerToFile(redeemerValue, scriptUtxo.TxHash);
            
            string command =
                $"cardano-cli conway transaction build " +
                $"--tx-in {scriptUtxo.TxHash}#{scriptUtxo.TxIx} " +
                $"--tx-in-collateral {collateralUtxo.TxHash}#{collateralUtxo.TxIx} " +
                $"--tx-in-script-file {scriptFile} " +
                $"--tx-in-inline-datum-present " +
                $"--tx-in-redeemer-file {redeemerPath} " +
                $"--tx-out {recipient}+{amount} " +
                $"--change-address {_senderAddress} " +
                $"--metadata-json-file {metadata} " +
                $"{_networkParam} " +
                $"--out-file /tmp/tx.draft";

            await ExecuteCommandAsync(command);
        }

        // $"--protocol-params-file /tmp/protocol.json " +
        private async Task SignTransaction()
        {
            string command =
                $"cardano-cli conway transaction sign " +
                $"--tx-body-file /tmp/tx.draft " +
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
                _logger.LogInformation("Validación de transacción Plutus:\n" + output);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error al validar la transacción Plutus: {ex.Message}");
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
        private string SaveRedeemerToFile(int redeemerValue, string txId)
        {
            var redeemer = new
            {
                constructor = 0,
                fields = new List<Dictionary<string, object>>
            {
            new Dictionary<string, object> { { "int", redeemerValue } }
            }
            };

            string path = $"/tmp/redeemer-{txId}.json";
            var options = new JsonSerializerOptions { WriteIndented = false };
            var json = JsonSerializer.Serialize(redeemer, options);
            File.WriteAllText(path, json);

            _logger.LogInformation("Redeemer generado en ruta: {Path}", path);
            return path;
        }



        private async Task<string> GuardarMetadataTemporalAsync(Acta acta)
        {
            string filePath = $"/tmp/acta-{acta.Codigo}.metadata.json";

            var metadata = new Dictionary<string, object>
            {
                ["674"] = acta
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            string json = JsonSerializer.Serialize(metadata, options);
            await File.WriteAllTextAsync(filePath, json);

            return filePath;
        }
    }
}
