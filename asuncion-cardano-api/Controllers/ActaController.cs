using Microsoft.AspNetCore.Mvc;
using asuncion_cardano_api.Models;
using asuncion_cardano_api.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AsuncionCardanoApi.Services;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace asuncion_cardano_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActaController : ControllerBase
    {
        private readonly ValidatorService _validatorService;
        private readonly CardanoTransactionService _cardanoTransactionService;
        private readonly CardanoPlutusTransactionService _cardanoPlutusTransactionService;
        private readonly UtxoFinderService _utxoFinderService;
        private readonly LockActaService _lockActaService;
        private readonly ILogger<ActaController> _logger;
        private readonly CardanoSettings _cardanoSettings;

        public ActaController(
            ValidatorService validatorService,
            CardanoTransactionService cardanoTransactionService,
            CardanoPlutusTransactionService cardanoPlutusTransactionService,
            UtxoFinderService utxoFinderService,
            LockActaService lockActaService,
            ILogger<ActaController> logger,
            IOptions<CardanoSettings> cardanoSettings)
        {
            _validatorService = validatorService;
            _cardanoTransactionService = cardanoTransactionService;
            _cardanoPlutusTransactionService = cardanoPlutusTransactionService;
            _utxoFinderService = utxoFinderService;
            _lockActaService = lockActaService;
            _logger = logger;
            _cardanoSettings = cardanoSettings.Value;

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CARDANO_NODE_SOCKET_PATH")))
            {
                Environment.SetEnvironmentVariable("CARDANO_NODE_SOCKET_PATH", _cardanoSettings.SocketPath);
            }
        }

        [HttpGet("{id}")]
        public IActionResult ObtenerActa(string id)
        {
            var acta = new Acta
            {
                Codigo =0,
                Estado =1,
            };
            return Ok(acta);
        }

        [HttpPost("{id}/escaneo")]
        public async Task<IActionResult> EscaneoActa(string id, [FromBody] Acta acta)
        {   
            try
            {
                string txId = await _cardanoTransactionService.EjecutarTransaccionMetadataAsync(acta);

                return Ok(new { transactionId = txId });
            }
            catch (Exception ex) {
                _logger.LogError(ex, "❌ Error en EscaneoActa");
                return StatusCode(500, new { error = ex.Message });
            }

            
        }

        [HttpPost("validadores/exportar")]
        public IActionResult ExportarValidadoresPlutus()
        {
            try
            {
                string basePath = "Resources";
                string jsonPath = Path.Combine(basePath, "plutus.json");
                string plutusOutDir = Path.Combine(basePath, "script_plutus");
                string addrOutDir = Path.Combine(basePath, "script_addr");

                if (!System.IO.File.Exists(jsonPath))
                    return NotFound("No se encontró plutus.json");

                Directory.CreateDirectory(plutusOutDir);
                Directory.CreateDirectory(addrOutDir);

                var json = System.IO.File.ReadAllText(jsonPath);
                var root = JsonNode.Parse(json);
                var validatorsArray = root?["validators"]?.AsArray();

                if (validatorsArray == null)
                    return BadRequest("No se encontraron validadores en plutus.json");

                int totalExportados = 0;
                int totalDirecciones = 0;
                List<string> errores = new();

                foreach (var val in validatorsArray)
                {
                    string? title = val?["title"]?.ToString();
                    string? compiledCode = val?["compiledCode"]?.ToString();

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(compiledCode))
                        continue;

                    string safeTitle = title.Replace('.', '_').Replace('/', '_');
                    string plutusFilePath = Path.Combine(plutusOutDir, $"{safeTitle}.plutus");
                    string addrFilePath = Path.Combine(addrOutDir, $"script_{safeTitle}.addr");

                    var plutusCompatible = new
                    {
                        type = "PlutusScriptV2",
                        description = $"Exportado desde plutus.json - {title}",
                        cborHex = compiledCode
                    };

                    // Escribir archivo .plutus
                    string content = JsonSerializer.Serialize(plutusCompatible, new JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(plutusFilePath, content);
                    totalExportados++;

                    // Solo generar address si es validador .spend
                    if (title.EndsWith(".spend"))
                    {
                        string buildCmd = $"cardano-cli address build --payment-script-file {plutusFilePath} --testnet-magic 2 --out-file {addrFilePath}";
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                Arguments = $"-c \"{buildCmd}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        if (process.ExitCode == 0 && System.IO.File.Exists(addrFilePath))
                        {
                            totalDirecciones++;
                        }
                        else
                        {
                            errores.Add($"Error generando address para {title}: {stderr}");
                        }
                    }
                }

                return Ok(new
                {
                    mensaje = "Exportación completa",
                    validadores_exportados = totalExportados,
                    direcciones_generadas = totalDirecciones,
                    errores
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpPost("{id}/escaneo-plutus")]
        public async Task<IActionResult> EscaneoPlutusActa(string id, [FromBody] Acta acta)
        {
            const string validatorName = "acta_trazabilidad.escaneo_acta.spend";

            try
            {
                var utxosEnScript = await _utxoFinderService.BuscarTodosLosUtxoEnScriptAddress(validatorName);

                if (utxosEnScript == null || !utxosEnScript.Any())
                {
                    _logger.LogInformation("No hay UTxO en el script. Procediendo a bloquear el acta (lock).");

                    try
                    {
                        var txId = await _lockActaService.LockActaAsync(acta);
                        return Ok(new { transactionId = txId, action = "lock" });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Error al bloquear el acta con LockActaAsync.");
                        return StatusCode(500, new
                        {
                            error = "Error al intentar bloquear el acta",
                            detalle = ex.Message,
                            stack = ex.StackTrace
                        });
                    }
                }

                _logger.LogInformation("UTxO encontrado. Ejecutando Plutus...");

                try
                {
                    var txId = await _cardanoPlutusTransactionService.EjecutarPlutusTransactionDesdeActa(acta, validatorName);
                    return Ok(new { transactionId = txId, action = "spend" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error ejecutando transacción Plutus.");
                    return StatusCode(500, new
                    {
                        error = "Error ejecutando la transacción Plutus",
                        detalle = ex.Message,
                        stack = ex.StackTrace
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error inesperado en escaneo-plutus.");
                return StatusCode(500, new
                {
                    error = "Error inesperado en escaneo-plutus",
                    detalle = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }

        [HttpPost("diagnose-datum")]
        public async Task<IActionResult> DiagnoseDatum([FromBody] Acta acta)
        {
            try
            {
                string diagnosisResult = await _lockActaService.DiagnoseDatumIssueAsync(acta);
                return Ok(new { result = diagnosisResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en diagnóstico de datum");
                return StatusCode(500, new { error = ex.Message });
            }
        }


    }
}
