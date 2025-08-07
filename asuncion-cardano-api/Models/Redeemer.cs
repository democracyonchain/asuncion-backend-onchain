namespace asuncion_cardano_api.Models
{
    public class Redeemer
    {
        public int constructor { get; set; } = 0;
        public List<Dictionary<string, object>> fields { get; set; } = new();
    }

}
