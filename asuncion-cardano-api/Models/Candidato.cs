namespace asuncion_cardano_api.Models
{
    public class Candidato
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        public int PatidoId { get; set; }
        public int VotosIa { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public int Estado { get; set; }
    }
}
