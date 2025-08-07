using asuncion_cardano_api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AsuncionCardanoApi.Services
{
    public class LockActaService
    {
        private readonly string _networkParam;
        private readonly string _senderAddress;
        private readonly string _keyDirectory;
        private readonly ILogger<LockActaService> _logger;

        public LockActaService(
            IOptions<CardanoSettings> settings,
            ILogger<LockActaService> logger)
        {
            var config = settings.Value;
            _networkParam = $"--testnet-magic {config.NetworkMagic}";
            _senderAddress = config.AuthorizedAddress;
            _keyDirectory = config.KeyDirectory;
            _logger = logger;
        }

        public async Task<string> LockActaAsync(Acta acta)
        {
            try
            {
                const string validatorName = "acta_trazabilidad.escaneo_acta.spend";

                string scriptFile = Path.Combine("Resources", "script_plutus",
                                                 $"{validatorName.Replace('.', '_')}.plutus");
                string addrFile = Path.Combine("Resources", "script_addr",
                                                 $"script_{validatorName.Replace('.', '_')}.addr");

                if (!File.Exists(scriptFile))
                    throw new FileNotFoundException($"No se encontró el archivo Plutus: {scriptFile}");

                if (!File.Exists(addrFile))
                    throw new FileNotFoundException($"No se encontró el archivo de dirección: {addrFile}");

                string scriptAddress = (await File.ReadAllTextAsync(addrFile)).Trim();

                // 1️⃣  UTxO de pago con más ADA
                string paymentUtxoPath = "/tmp/payment-utxos.json";
                await ExecuteCommandAsync(
                    $"cardano-cli query utxo --address {_senderAddress} {_networkParam} --out-file {paymentUtxoPath}"
                );

                string txIn = GetFirstTxInFromJson(paymentUtxoPath, out long utxoAmount);

                const long salida = 2_000_000;
                const long estimatedFee = 250_000;

                if (utxoAmount < salida + estimatedFee)
                    throw new Exception($"UTxO insuficiente ({utxoAmount} lovelace). Necesita ≥ {salida + estimatedFee}.");

                // 2️⃣  Datum - Try multiple datum formats and log them for debugging
                string basePath = "/tmp";
                string datumPath = $"{basePath}/acta-{acta.Codigo}.datum.json";

                // Try both datum generation methods
                string jsonDatum = GenerateAikenCompatibleDatum(acta);
                // Also save the alternative format for comparison
                string altDatumPath = $"{basePath}/acta-{acta.Codigo}-alt.datum.json";
                string altJsonDatum = GenerarDatumAikenCompatible(acta);

                await File.WriteAllTextAsync(datumPath, jsonDatum);
                await File.WriteAllTextAsync(altDatumPath, altJsonDatum);

                _logger.LogInformation($"✅ Datum guardado en: {datumPath}\n{jsonDatum}");
                _logger.LogInformation($"✅ Datum alternativo guardado en: {altDatumPath}\n{altJsonDatum}");

                // 3️⃣  Build → Sign → Submit
                string draftTx = $"{basePath}/lock-tx.draft";
                await ExecuteCommandAsync(
                    $"cardano-cli conway transaction build " +
                    $"--tx-in {txIn} " +
                    $"--tx-out {scriptAddress}+{salida} " +
                    $"--tx-out-inline-datum-file {datumPath} " +
                    $"--change-address {_senderAddress} " +
                    $"{_networkParam} " +
                    $"--out-file {draftTx}"
                );

                string signedTx = $"{basePath}/lock-tx.signed";
                await ExecuteCommandAsync(
                    $"cardano-cli conway transaction sign " +
                    $"--tx-file {draftTx} " +
                    $"--signing-key-file {_keyDirectory}/payment.skey " +
                    $"{_networkParam} --out-file {signedTx}"
                );

                // Check the transaction before submitting
                string txInfo = await ExecuteCommandAsync(
                    $"cardano-cli conway transaction view --tx-file {signedTx}"
                );
                _logger.LogInformation($"Transaction info before submit:\n{txInfo}");

                await ExecuteCommandAsync(
                    $"cardano-cli conway transaction submit --tx-file {signedTx} {_networkParam}"
                );

                string txId = await ExecuteCommandAsync(
                    $"cardano-cli conway transaction txid --tx-file {signedTx}"
                );

                return txId.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en LockActaAsync");
                throw;
            }
        }

        /* ---------- helpers ---------- */

        public string GetDatumOnly(Acta acta) => GenerateAikenCompatibleDatum(acta);

        // Original method with slight modifications for debugging
        private string GenerarDatumAikenCompatible(Acta acta)
        {
            var actaData = new                      // constructor = 0  (Acta)
            {
                constructor = 0,
                fields = new object[]
                {
                    new { bytes = EncodeToHex("") },
                    /*new { bytes = EncodeToHex(acta.Codigo) },
                    new { @int  = acta.Timestamp },
                    new { constructor = acta.Estado, fields = Array.Empty<object>() },
                    new { bytes = EncodeToHex(acta.ImagenActa) },
                    new { @int  = acta.UserId },
                    new { bytes = EncodeToHex(acta.HashAnterior) },
                    new
                    {
                        list = acta.Segmentos.Select(s => new
                        {
                            constructor = 0,
                            fields = new object[]
                            {
                                new { @int = s.Candidato },
                                new { bytes = EncodeToHex(s.IpfsSeccion) }
                            }
                        }).ToArray()
                    },
                    new
                    {
                        list = acta.Datos.Select(v => new
                        {
                            constructor = 0,
                            fields = new object[]
                            {
                                new { @int = v.Candidato },
                                new { @int = v.Voto }
                            }
                        }).ToArray()
                    }*/
                }
            };

            var someWrapper = new                   // 🔹 constructor = 1  (Some)
            {
                constructor = 1,
                fields = new object[] { actaData }
            };

            return JsonSerializer.Serialize(
                someWrapper,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
            );
        }

        // Alternative method with slightly different structure
        private string GenerateAikenCompatibleDatum(Acta acta)
        {
            // Try with direct wrapping and lowercase property names
            var datum = new
            {
                constructor = 1,  // Some
                fields = new[]
                {
                    new {
                        constructor = 0,  // Acta
                        fields = new object[]
                        {
                            new { bytes = EncodeToHex(acta.Codigo.ToString()) },
                            /*new { @int = acta.Timestamp },
                            new { constructor = acta.Estado, fields = Array.Empty<object>() },
                            new { bytes = EncodeToHex(acta.ImagenActa) },
                            new { @int = acta.UserId },
                            new { bytes = EncodeToHex(acta.HashAnterior) },
                            new {
                                list = acta.Segmentos.Select(s => new {
                                    constructor = 0,
                                    fields = new object[] {
                                        new { @int = s.Candidato },
                                        new { bytes = EncodeToHex(s.IpfsSeccion) }
                                    }
                                }).ToArray()
                            },
                            new {
                                list = acta.Datos.Select(d => new {
                                    constructor = 0,
                                    fields = new object[] {
                                        new { @int = d.Candidato },
                                        new { @int = d.Voto }
                                    }
                                }).ToArray()
                            }*/
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(
                datum,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
            );
        }

        // Encode string to lowercase hexadecimal
        private static string EncodeToHex(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private string GetFirstTxInFromJson(string path, out long amount)
        {
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            amount = 0;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var parts = prop.Name.Split('#');
                if (parts.Length != 2) continue;

                if (prop.Value.TryGetProperty("value", out var v) &&
                    v.TryGetProperty("lovelace", out var lov))
                    amount = lov.GetInt64();

                return $"{parts[0]}#{parts[1]}";
            }
            throw new Exception("No se encontró ningún UTxO disponible.");
        }

        private async Task<string> ExecuteCommandAsync(string cmd)
        {
            _logger.LogInformation($"🛠  {cmd}");

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{cmd}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            p.Start();
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            if (p.ExitCode != 0)
            {
                _logger.LogError($"💥 {stderr}");
                throw new Exception(stderr);
            }
            return stdout;
        }

        // Add a diagnostic method to test datum compatibility
        public async Task<string> DiagnoseDatumIssueAsync(Acta acta)
        {
            try
            {
                // Generate both datum formats
                string datum1 = GenerarDatumAikenCompatible(acta);
                string datum2 = GenerateAikenCompatibleDatum(acta);

                // Save datums to files
                string path1 = "/tmp/datum-original.json";
                string path2 = "/tmp/datum-alternative.json";
                await File.WriteAllTextAsync(path1, datum1);
                await File.WriteAllTextAsync(path2, datum2);

                // Try to validate them with cardano-cli
                string result1 = "";
                string result2 = "";

                try
                {
                    result1 = await ExecuteCommandAsync(
                        $"cardano-cli transaction hash-script-data --script-data-file {path1}"
                    );
                }
                catch (Exception ex)
                {
                    result1 = $"Error: {ex.Message}";
                }

                try
                {
                    result2 = await ExecuteCommandAsync(
                        $"cardano-cli transaction hash-script-data --script-data-file {path2}"
                    );
                }
                catch (Exception ex)
                {
                    result2 = $"Error: {ex.Message}";
                }

                // Return diagnostic info
                return $"Original Datum:\n{datum1}\n\nHash Result: {result1}\n\n" +
                       $"Alternative Datum:\n{datum2}\n\nHash Result: {result2}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error diagnóstico de datum");
                throw;
            }
        }
    }
}