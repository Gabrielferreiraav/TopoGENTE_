using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services
{
    public sealed class QaCheckService
    {
        private readonly CalculoTopograficoService _calculo;

        public QaCheckService()
        {
            _calculo = new CalculoTopograficoService();
        }

        public RelatorioQA GerarRelatorioQaChecks(
            List<Estacao> estacoesOrganizadas,
            ResultadoLevantamento resultado,
            Dictionary<string, PontoCoordenada> pontosConhecidos,
            double toleranciaDeltaXY = 0.01,
            double toleranciaDeltaZ = 0.02)
        {
            var rel = new RelatorioQA
            {
                ToleranciaCheckDeltaXY = toleranciaDeltaXY,
                ToleranciaCheckDeltaZ = toleranciaDeltaZ,
            };

            var poligonalPorNome = resultado.Poligonal
                .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            foreach (var estacao in estacoesOrganizadas)
            {
                if (!poligonalPorNome.TryGetValue(estacao.Nome, out var pEstacao))
                {
                    pEstacao = estacao.CoordenadaConhecida;
                }

                if (pEstacao == null) continue;

                double azimuteOrientacao = pEstacao == resultado.Poligonal.First()
                    ? resultado.Poligonal.First().AzimuteChegada
                    : (pEstacao.AzimuteChegada < 180 ? pEstacao.AzimuteChegada + 180 : pEstacao.AzimuteChegada - 180);

                var checks = estacao.Leituras
                    .Where(l => string.Equals(l.Purpose,"check", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var check in checks)
                {
                    if (!pontosConhecidos.TryGetValue(check.PontoVisado, out var pConhecido))
                    {
                        rel.Checks.Add(new EventoQACheck
                        {
                            SetupId = check.SetupId ?? estacao.Id,
                            TargetPoint = check.PontoVisado,
                            TimeStamp = check.TimeStamp,
                            EstacaoOcupada = estacao.Nome,
                            Mensagem = "Ponto conhecido para check não encontrado"
                        });
                        continue;
                    }

                    var pObs = _calculo.CalcularPontoIrradiado(pEstacao, check, azimuteOrientacao);

                    var dx = pObs.X - pConhecido.X;
                    var dy = pObs.Y - pConhecido.Y;
                    var deltaXY = Math.Sqrt(dx * dx + dy * dy);
                    var deltaZ = Math.Abs(pObs.Z - pConhecido.Z);

                    rel.Checks.Add(new EventoQACheck
                    {
                        SetupId = check.SetupId ?? estacao.Id,
                        TargetPoint = check.PontoVisado,
                        TimeStamp = check.TimeStamp,
                        EstacaoOcupada = estacao.Nome,
                        DeltaXY = deltaXY,
                        DeltaZ = deltaZ,
                        ExcedeuDeltaXY = deltaXY > rel.ToleranciaCheckDeltaXY,
                        ExcedeuDeltaZ = deltaZ > rel.ToleranciaCheckDeltaZ,
                        Mensagem = $"Check '{estacao.Nome}' -> '{check.PontoVisado}' :ΔXY={deltaXY:F3}m, ΔZ={deltaZ:F3}m "
                    });
                }
            }

            return rel;
        }
    }
}