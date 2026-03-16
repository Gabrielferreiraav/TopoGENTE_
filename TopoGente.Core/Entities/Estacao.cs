using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Entities
{
    public class Estacao : IGrafoElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nome { get; set; } = string.Empty;
        public double AlturaInstrumento { get; set; }

        // O compilador sabe em tempo de execução que 'this' é uma Estação.
        // Ele despacha para o método VisitEstacao do Visitor.
        public void Accept(ITopografiaVisitor visitor)
        {
            visitor.VisitarEstacao(this);

            foreach (var leitura in Leituras)
            {
                leitura.Accept(visitor);
            }
        }

        public PontoCoordenada? CoordenadaConhecida { get; set; } = null;
        public List<LeituraEstacaoTotal> Leituras { get; set; } = new List<LeituraEstacaoTotal>();
        public List<PontoCoordenada> PontosCalculados { get; set; } = new List<PontoCoordenada>();

        public override string ToString()
        {
            return $"{Nome} (HI: {AlturaInstrumento} m, Leituras: {Leituras.Count})";
        }
    }
}
