using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class CalculoPoligonalVisitor : ITopografiaVisitor
    {
        private PontoCoordenada _estacaoAtual;
        private Estacao? _estacaoVisitada;
        private double _azimuteAtual;

        public List<PontoCoordenada> PontosCalculados { get; private set; } = new();

        public CalculoPoligonalVisitor(PontoCoordenada pontoPartida, double azimuteInicial)
        {
            _estacaoAtual = pontoPartida;
            _azimuteAtual = azimuteInicial;

            pontoPartida.AzimuteChegada = azimuteInicial;
            PontosCalculados.Add(pontoPartida);
        }

        public void VisitarEstacao(Estacao estacao)
        {
            _estacaoVisitada = estacao;
        }

        public void VisitarLeitura(LeituraEstacaoTotal leitura)
        {
            if (leitura.Tipo == TipoLeitura.Poligonal)
            {
                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                _azimuteAtual = GeometriaTopograficaHelper.Normalizar360(_azimuteAtual + leitura.AnguloHorizontal);

                var (deltaX, deltaY) = GeometriaTopograficaHelper.CalcularProjecao(dh, _azimuteAtual);

                var novoPonto = new PontoCoordenada
                {
                    Nome = leitura.PontoVisado,
                    X = _estacaoAtual.X + deltaX,
                    Y = _estacaoAtual.Y + deltaY,
                    Z = _estacaoAtual.Z + dn,
                    AzimuteChegada = _azimuteAtual,
                    EhPontoPoligonal = true
                };

                PontosCalculados.Add(novoPonto);
                _estacaoVisitada?.AdicionarPontoCalculado(novoPonto);

                _estacaoAtual = novoPonto;
                _azimuteAtual = GeometriaTopograficaHelper.Normalizar360(_azimuteAtual + 180.0);
            }
        }
    }
}
