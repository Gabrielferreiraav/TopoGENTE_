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

        public void SubstituirLeitura(string leituraIdAntiga, string novoPontoVisado, double novoAngH, double novoAngV, double novaDistI, double novaAltPrisma, string novaObservacao)
        {
            var leituraExistente = _leituras.FirstOrDefault(l => l.Id == leituraIdAntiga);
            if (leituraExistente == null) return; // Retorno silencioso: bloqueia o "Double-Fire" fantasma do WPF.

            _leituras.Remove(leituraExistente);

            var novaLeitura = new LeituraEstacaoTotal
            {
                Id = Guid.NewGuid().ToString(), // Garantia de novo Fato Físico
                SetupId = leituraExistente.SetupId,
                TimeStamp = leituraExistente.TimeStamp,
                EstacaoOcupada = leituraExistente.EstacaoOcupada,
                AlturaInstrumento = leituraExistente.AlturaInstrumento,
                OrdemArquivo = leituraExistente.OrdemArquivo,
                Tipo = leituraExistente.Tipo,
                Purpose = leituraExistente.Purpose,
                
                // Novos valores alterados na UI
                PontoVisado = novoPontoVisado,
                AnguloHorizontal = novoAngH,
                AnguloVertical = novoAngV,
                DistanciaInclinada = novaDistI,
                AlturaPrisma = novaAltPrisma,
                Observacao = novaObservacao ?? string.Empty
            };

            _leituras.Add(novaLeitura);
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
