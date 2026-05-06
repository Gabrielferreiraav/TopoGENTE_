using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class CalculoIrradiacaoVisitor : ITopografiaVisitor
    {
        // Estado local acumulado durante o percurso da estrutura 
        private PontoCoordenada? _estacaoAtual;
        private double? _azimuteReVigente;
        private readonly double _azimuteInicial;

        private readonly Dictionary<string, PontoCoordenada> _pontosCompensados;
        private readonly Dictionary<string, PontoCoordenada> _pontosConhecidos;

        public List<PontoCoordenada> IrradiacoesCalculadas { get; private set; } = new();

        public CalculoIrradiacaoVisitor(
            List<PontoCoordenada> poligonalCompensada,
            Dictionary<string, PontoCoordenada>? pontosConhecidos,
            double azimuteInicial)
        {
            _pontosCompensados = poligonalCompensada.GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _pontosConhecidos = pontosConhecidos ?? new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);
            _azimuteInicial = azimuteInicial;
        }

        public void VisitarEstacao(Estacao estacao)
        {
            // O visitante acessa no Nó. Atualiza o contexto espacial buscando a coordenada compensada.
            if (_pontosCompensados.TryGetValue(estacao.Nome, out var pontoCalculado))
                _estacaoAtual = pontoCalculado;
            else
                _estacaoAtual = null;

            // A cada nova ocupação de estação, a orientação de ré anterior é descartada
            _azimuteReVigente = null;
        }

        public void VisitarLeitura(LeituraEstacaoTotal leitura)
        {
            //  se a estação atual for inválida, ele não opera
            if (_estacaoAtual == null) return;

            // O Double-Dispatch auto-seleciona a operação.
            if (leitura.Tipo == TipoLeitura.Re)
            {
                DefinirOrientacaoDeRe(leitura);
            }
            else if (leitura.Tipo == TipoLeitura.Irradiacao)
            {
                CalcularIrradiacao(leitura);
            }
        }

        private void DefinirOrientacaoDeRe(LeituraEstacaoTotal leitura)
        {
            PontoCoordenada? pontoRe = null;

            if (_pontosConhecidos.TryGetValue(leitura.PontoVisado, out var pConhecido))
                pontoRe = pConhecido;
            else if (_pontosCompensados.TryGetValue(leitura.PontoVisado, out var pPoligonal))
                pontoRe = pPoligonal;

            if (pontoRe != null)
            {
                _azimuteReVigente = GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(
                    _estacaoAtual!.X, _estacaoAtual.Y, pontoRe.X, pontoRe.Y);
            }
            else if (leitura.EstacaoOcupada.Equals(_pontosCompensados.Values.First().Nome, StringComparison.OrdinalIgnoreCase))
            {
                // Fallback: Se for a primeira estação e a ré não tem coordenadas, assume a orientação inicial
                _azimuteReVigente = _azimuteInicial;
            }
        }

        private void CalcularIrradiacao(LeituraEstacaoTotal leitura)
        {
            double azReUsado;

            if (_azimuteReVigente.HasValue)
            {
                azReUsado = _azimuteReVigente.Value;
            }
            else
            {
                // Fallback teórico do legado para irradiações feitas sem leitura de Ré formal
                azReUsado = _estacaoAtual!.Nome.Equals(_pontosCompensados.Values.First().Nome, StringComparison.OrdinalIgnoreCase)
                    ? _azimuteInicial
                    : (_estacaoAtual.AzimuteChegada < 180 ? _estacaoAtual.AzimuteChegada + 180 : _estacaoAtual.AzimuteChegada - 180);
            }

            var pontoIrradiado = GeometriaTopograficaHelper.CalcularPontoIrradiado(_estacaoAtual!, leitura, azReUsado);
            IrradiacoesCalculadas.Add(pontoIrradiado);
        }
    }
}
