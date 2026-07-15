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

            foreach (var leitura in _leituras)
            {
                leitura.Accept(visitor);
            }
        }

        public PontoCoordenada? CoordenadaConhecida { get; set; } = null;
        
        private readonly List<LeituraEstacaoTotal> _leituras = new();
        public IReadOnlyCollection<LeituraEstacaoTotal> Leituras => _leituras.AsReadOnly();

        public void AdicionarVisada(LeituraEstacaoTotal leitura)
        {
            _leituras.Add(leitura);
        }

        public void RemoverVisada(LeituraEstacaoTotal leitura)
        {
            _leituras.Remove(leitura);
        }

        private readonly List<PontoCoordenada> _pontosCalculados = new();
        public IReadOnlyCollection<PontoCoordenada> PontosCalculados => _pontosCalculados.AsReadOnly();

        public void AdicionarPontoCalculado(PontoCoordenada ponto)
        {
            if (ponto != null) _pontosCalculados.Add(ponto);
        }

        public override string ToString()
        {
            return $"{Nome} (HI: {AlturaInstrumento} m, Leituras: {Leituras.Count})";
        }
    }
}
