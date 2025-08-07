using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using asuncion_cardano_api.Models;

namespace asuncion_cardano_api.Services
{
    public class ValidatorService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ValidatorService> _logger;
        private const string ScriptCacheKey = "plutus_script_cache";
        private readonly string _scriptFilePath = Path.Combine("Resources", "plutus.json");

        public ValidatorService(IMemoryCache cache, ILogger<ValidatorService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public ValidatorResult GetValidatorPayload(Acta acta, string scriptTitle)
        {
            var script = GetScriptByName(scriptTitle);

            var datumJson = GenerarDatumAikenCompatible(acta);
            var redeemerJson = JsonSerializer.Serialize(new
            {
                constructor = 0,
                fields = new object[] { new { @int = acta.Codigo } }
            }, new JsonSerializerOptions { PropertyNamingPolicy = null });

            return new ValidatorResult
            {
                ScriptName = scriptTitle,
                ScriptHex = script,
                Datum = datumJson,
                Redeemer = acta.Codigo
            };
        }

        private string GetScriptByName(string scriptTitle)
        {
            var validators = LoadValidatorScripts();
            var script = validators?.FirstOrDefault(v => v.Title == scriptTitle);
            return script?.CompiledCode ?? string.Empty;
        }

        private List<ValidatorScript>? LoadValidatorScripts()
        {
            if (_cache.TryGetValue(ScriptCacheKey, out List<ValidatorScript>? cachedValidators))
            {
                return cachedValidators;
            }

            if (!File.Exists(_scriptFilePath))
            {
                _logger.LogError("Archivo plutus.json no encontrado en ruta {Path}", _scriptFilePath);
                return null;
            }

            try
            {
                var json = File.ReadAllText(_scriptFilePath);
                var root = JsonNode.Parse(json);
                var validatorsArray = root?["validators"]?.AsArray();

                if (validatorsArray == null)
                {
                    _logger.LogError("No se encontraron validadores en plutus.json");
                    return null;
                }

                var scripts = validatorsArray
                    .Where(v => v?["title"] != null && v?["compiledCode"] != null)
                    .Select(v => new ValidatorScript
                    {
                        Title = v?["title"]?.ToString() ?? string.Empty,
                        CompiledCode = v?["compiledCode"]?.ToString() ?? string.Empty
                    })
                    .ToList();

                if (scripts.Any())
                {
                    _cache.Set(ScriptCacheKey, scripts, TimeSpan.FromMinutes(10));
                }

                return scripts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al leer plutus.json");
                return null;
            }
        }

        private string GenerarDatumAikenCompatible(Acta acta)
        {
            var datum = new
            {
                constructor = 0,
                fields = new object[]
                {
                    new
                    {
                        constructor = 0,
                        fields = new object[]
                        {
                            new { bytes = ByteArrayToHex(acta.Codigo.ToString()) },
                           /* new { @int = acta.Timestamp },
                            new { @int = acta.Estado },
                            new { bytes = ByteArrayToHex(acta.ImagenActa) },
                            new { @int = acta.UserId },
                            new { bytes = ByteArrayToHex(acta.HashAnterior) },
                            new
                            {
                                list = acta.Segmentos.Select(seg => new
                                {
                                    constructor = 0,
                                    fields = new object[]
                                    {
                                        new { @int = seg.Candidato },
                                        new { bytes = ByteArrayToHex(seg.IpfsSeccion) }
                                    }
                                }).ToArray()
                            },
                            new
                            {
                                list = acta.Datos.Select(d => new
                                {
                                    constructor = 0,
                                    fields = new object[]
                                    {
                                        new { @int = d.Candidato },
                                        new { @int = d.Voto }
                                    }
                                }).ToArray()
                            }*/
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(datum, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null
            });
        }

        private string ByteArrayToHex(string input)
        {
            return Convert.ToHexString(Encoding.UTF8.GetBytes(input));
        }
    }

    public class ValidatorScript
    {
        public string Title { get; set; } = string.Empty;
        public string CompiledCode { get; set; } = string.Empty;
    }

    public class ValidatorResult
    {
        public string ScriptName { get; set; } = string.Empty;
        public string ScriptHex { get; set; } = string.Empty;
        public string Datum { get; set; } = string.Empty;
        public int Redeemer { get; set; } 
    }

}
