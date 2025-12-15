using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Entities
{
    public class Estacao
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nome { get; set; } = string.Empty;
        public double AlturaInstrumento { get; set; } 

        public PontoCoordenada? CoordenadaConhecida { get; set; } = null;
        public List<LeituraEstacaoTotal> Leituras { get; set; } = new List<LeituraEstacaoTotal>();

        public override string ToString()
        {
            return $"{Nome} (HI: {AlturaInstrumento} m, Leituras: {Leituras.Count})";
        }
    }
}
