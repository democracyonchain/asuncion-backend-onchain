namespace asuncion_cardano_api.Models
{
    public class CardanoSettings
    {
        public string AuthorizedAddress { get; set; }
        public string KeyDirectory { get; set; }
        public int NetworkMagic { get; set; }
        public string SocketPath { get; set; }
    }
}
