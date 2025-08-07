using System.Text.Json;
using asuncion_cardano_api.Models;

namespace asuncion_cardano_api.Services
{
    public class DatumBuilderService
    {
        private readonly ILogger<DatumBuilderService> _logger;
        private readonly string _outputFolder;

        public DatumBuilderService(ILogger<DatumBuilderService> logger)
        {
            _logger = logger;
            _outputFolder = "/tmp"; // Carpeta donde guardaremos los datums
        }

        public async Task<string> CrearDatumParaActaAsync(Acta acta)
        {
            try
            {
                _logger.LogInformation("Generando datum.json para el acta {ActaId}", acta.Codigo);

                var datum = new
                {
                    constructor = 0,
                    fields = new object[]
                    {
                        new { bytes = acta.Codigo },
                        /*new { int_ = acta.Timestamp },
                        new { int_ = acta.Estado },
                        new { bytes = acta.ImagenActa },
                        new { int_ = acta.UserId },
                        new { bytes = acta.HashAnterior },*/

                        // segmentos: List<IpfsCandidatoUrl>
                        /*new {
                            list = acta.Segmentos?.Select(s => new {
                                constructor = 0,
                                fields = new object[] {
                                    new { int_ = s.Candidato },
                                    new { bytes = s.IpfsSeccion }
                                }
                            }).ToArray() ?? Array.Empty<object>()
                        },

                        // votos: List<Votos>
                        new {
                            list = acta.Datos?.Select(v => new {
                                constructor = 0,
                                fields = new object[] {
                                    new { int_ = v.Candidato },
                                    new { int_ = v.Voto }
                                }
                            }).ToArray() ?? Array.Empty<object>()
                        }*/
                    }
                };

                string jsonContent = JsonSerializer.Serialize(datum, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                string filePath = Path.Combine(_outputFolder, $"datum-{acta.Codigo}.json");
                await File.WriteAllTextAsync(filePath, jsonContent);

                _logger.LogInformation("Datum generado correctamente en: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando datum.json para acta {ActaId}", acta.Codigo);
                throw;
            }
        }
    }
}
