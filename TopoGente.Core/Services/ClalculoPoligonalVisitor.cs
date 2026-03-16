using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    public class CalculoPoligonalVisitor : ITopografiaVisitor
    {
        private PontoCoordenada _estacaoAtual;
        private double _azimuteAtual;

        private readonly CalculoTopograficoService _mathService;

        public List<PontoCoordenada> PontosCalculados { get; private set; } = new();

        public CalculoPoligonalVisitor(PontoCoordenada pontoPartida, double azimuteInicial, CalculoTopograficoService mathService)
        {
            _mathService = mathService;
            _estacaoAtual = pontoPartida;
            _azimuteAtual = azimuteInicial;

            pontoPartida.AzimuteChegada = azimuteInicial;
            PontosCalculados.Add(pontoPartida);
        }

        public void VisitarEstacao(Estacao estacao)
        {
            // A estação em si não tem cálculo, mas pode ser usada para atualizar o estado.

        }

        public void VisitarLeitura(LeituraEstacaoTotal leitura)
        {
            // O algoritmo de Double-Dispatch
            // A matemática só é aplicada em Arestas Vantes
            if (leitura.Tipo == TipoLeitura.Poligonal)
            {
                double dh = _mathService.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = _mathService.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                _azimuteAtual = _mathService.CalcularProximoAzimute(_azimuteAtual, leitura.AnguloHorizontal);

                var (deltaX, deltaY) = _mathService.CalcularProjecao(dh, _azimuteAtual);

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

                _estacaoAtual = novoPonto;
                _azimuteAtual = _mathService.CalcularProximoAzimute(_azimuteAtual, 180.0);
            }

        }
    }
}
