using System.Security.Cryptography.Xml;

namespace asuncion_cardano_api.Models
{
    public class Pagina
    {
        public int ActaId { get; set; }
        public int Numero { get; set; }
        public string? Nombre { get; set; }
        public string? Path { get; set; }
        public string? Url { get; set; }
        public string? Hash { get; set; }
        public List<Candidato> candidatos { get; set; }
        public int Estado { get; set; }
    }
}
