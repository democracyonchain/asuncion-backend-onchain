using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace asuncion_cardano_api.Models
{
    public class Acta
    {
        /*public string Id { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public int Estado { get; set; } // 1: Escaneado, 2: OCR, 3: Digitado, 4: Verificado
        public string ImagenActa { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string HashAnterior { get; set; } = string.Empty;
        public List<Segmento> Segmentos { get; set; } = new();
        public List<DatoVoto> Datos { get; set; } = new();*/
        public int Codigo { get; set; }
        public int Seguridad { get; set; }
        public int Provincia { get; set; }
        public int Canton { get; set; }
        public int Parroquia { get; set; }
        public int Zona { get; set; }
        public int Junta { get; set; }
        public string Sexo { get; set; }
        public int Dignidad { get; set; }
        public int Pagina { get; set; }
        [JsonPropertyName("numero_paginas")]
        public int Paginas { get; set; }
        public string? Path { get; set; }
        public List<Pagina> paginas { get; set; }
        public int Estado { get; set; }
        public int Sufragantes { get; set; }
        public int Blancos { get; set; }
        public int Nulos { get; set; }
        public string? TxIcr { get; set; }
    }
}
