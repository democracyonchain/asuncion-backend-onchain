using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using asuncion_cardano_api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsuncionCardanoApi.Services
{
    public class UtxoFinderService
    {
        private readonly ILogger<UtxoFinderService> _logger;
        private readonly string _networkParam;
        private readonly string _paymentAddress;

        public UtxoFinderService(
            ILogger<UtxoFinderService> logger,
            IOptions<CardanoSettings> settings)
        {
            _logger = logger;
            var config = settings.Value;
            _networkParam = $"--testnet-magic {config.NetworkMagic}";
            _paymentAddress = config.AuthorizedAddress;
        }

        public async Task<UtxoInfo> BuscarUtxoEnScriptAddress(string validatorName)
        {
            var utxos = await BuscarTodosLosUtxoEnScriptAddress(validatorName);

            if (utxos == null || utxos.Count == 0)
                throw new Exception("No se encontró ningún UTxO en el script address.");

            return utxos.OrderByDescending(u => u.Amount).First(); // ✅ UTxO con más ADA
        }

        public async Task<UtxoInfo> BuscarUtxoConMayorAdaEnPaymentAddress()
        {
            string queryCommand = $"cardano-cli query utxo --address {_paymentAddress} {_networkParam} --out-file /tmp/payment-utxos.json";
            await ExecuteCommandAsync(queryCommand);

            var utxos = await ObtenerTodosLosUtxosDesdeJson("/tmp/payment-utxos.json");

            if (utxos == null || utxos.Count == 0)
                throw new Exception("No se encontraron UTxOs en la dirección de pago.");

            return utxos.OrderByDescending(u => u.Amount).First(); // ✅ el de mayor ADA
        }


        public async Task<List<UtxoInfo>> BuscarTodosLosUtxoEnScriptAddress(string validatorName)
        {
            string safeName = validatorName.Replace(".", "_").Replace("/", "_");
            string fileName = $"script_{safeName}.addr";
            string fullPath = Path.Combine("Resources", "script_addr", fileName);

            if (!File.Exists(fullPath))
                throw new Exception($"No se encontró el archivo {fileName} con la dirección del script.");

            string scriptAddress = await File.ReadAllTextAsync(fullPath);
            scriptAddress = scriptAddress.Trim();

            string queryCommand = $"cardano-cli query utxo --address {scriptAddress} {_networkParam} --out-file /tmp/script-utxos.json";
            await ExecuteCommandAsync(queryCommand);

            return await ObtenerTodosLosUtxosDesdeJson("/tmp/script-utxos.json");
        }

       
        private async Task<List<UtxoInfo>> ObtenerTodosLosUtxosDesdeJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                return new List<UtxoInfo>();

            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            var utxos = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

            var listaUtxos = new List<UtxoInfo>();

            if (utxos != null)
            {
                foreach (var kvp in utxos)
                {
                    var parts = kvp.Key.Split('#');
                    if (parts.Length != 2) continue;

                    if (kvp.Value.TryGetProperty("value", out var valueElement) &&
                        valueElement.TryGetProperty("lovelace", out var lovelaceElement))
                    {
                        var amount = lovelaceElement.GetInt64();

                        listaUtxos.Add(new UtxoInfo
                        {
                            TxHash = parts[0],
                            TxIx = int.Parse(parts[1]),
                            Amount = amount
                        });
                    }
                }
            }

            return listaUtxos;
        }

        private async Task ExecuteCommandAsync(string command)
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
        }
    }
}
