using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public sealed class QaCheckService : IQaCheckService
    {
        public RelatorioQA GerarRelatorioQaChecks(
            List<Estacao> estacoesOrganizadas,
            ResultadoLevantamento resultado,
            Dictionary<string, PontoCoordenada> pontosConhecidos,
            ParametrosTolerancia tolerancias)
        {
            ArgumentNullException.ThrowIfNull(tolerancias);

            var rel = GerarRelatorioQaChecks(
                estacoesOrganizadas,
                resultado,
                pontosConhecidos,
                tolerancias.ToleranciaCheckDeltaXY,
                tolerancias.ToleranciaCheckDeltaZ);

            if (resultado != null)
            {
                int nEstacoes = resultado.Poligonal != null && resultado.Poligonal.Count > 1 &&
                                resultado.Poligonal[0].Nome.Equals(resultado.Poligonal[^1].Nome, StringComparison.OrdinalIgnoreCase)
                    ? resultado.Poligonal.Count - 1
                    : (resultado.Poligonal?.Count ?? 0);

                double perimetro = resultado.Perimetro;

                rel.ErroAngular = resultado.ErroAngular;
                rel.ErroLinear = resultado.ErroLinear;
                rel.ErroAltimetrico = resultado.ErroFechamentoZ;

                rel.ToleranciasAplicadas = tolerancias;
                rel.ModoAvaliacaoLinearUtilizado = tolerancias.ModoAvaliacaoLinear;

                if (resultado.Memorial?.Ajustamento != null && resultado.Memorial.Ajustamento.ToleranciaAngularSegundos > 0)
                {
                    rel.ToleranciaAngularSegundos = resultado.Memorial.Ajustamento.ToleranciaAngularSegundos;
                    rel.ToleranciaAngularAdmissivel = rel.ToleranciaAngularSegundos / 3600.0;
                    rel.AngularConforme = resultado.Memorial.Ajustamento.AngularAprovado;
                }
                else
                {
                    rel.ToleranciaAngularAdmissivel = tolerancias.CalcularToleranciaAngularGraus(nEstacoes);
                    rel.ToleranciaAngularSegundos = tolerancias.CalcularToleranciaAngularSegundos(nEstacoes);
                    rel.AngularConforme = Math.Abs(rel.ErroAngular) <= rel.ToleranciaAngularAdmissivel + 1e-6;
                }

                rel.ToleranciaLinearAbsolutaMetros = tolerancias.CalcularToleranciaLinearAbsolutaMetros(perimetro);
                rel.ToleranciaLinearRelativaMetros = tolerancias.CalcularToleranciaLinearRelativaMetros(perimetro);
                rel.ToleranciaLinearAdmissivel = tolerancias.CalcularToleranciaLinearMetros(perimetro);
                rel.ToleranciaAltimetricaAdmissivel = tolerancias.CalcularToleranciaAltimetricaMetros(perimetro);

                var (aprovadoGeral, aprovadoRelativo, aprovadoAbsoluto) =
                    tolerancias.AvaliarConformidadeLinear(resultado.ErroLinear, perimetro);

                rel.AprovadoRelativo = aprovadoRelativo;
                rel.AprovadoAbsoluto = aprovadoAbsoluto;
                rel.AprovadoLinearGeral = aprovadoGeral;

                rel.PrecisaoLinearObtidaDenominador = (resultado.ErroLinear > 1e-9 && perimetro > 0.001)
                    ? perimetro / resultado.ErroLinear
                    : 0.0;

                rel.AltimetricoConforme = Math.Abs(rel.ErroAltimetrico) <= rel.ToleranciaAltimetricaAdmissivel + 1e-4;
            }

            return rel;
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
                MemorialRastreabilidade = resultado?.Memorial
            };

            if (resultado?.Poligonal == null || resultado.Poligonal.Count == 0 || estacoesOrganizadas == null)
            {
                return rel;
            }

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
                    .Where(l => string.Equals(l.Purpose, "check", StringComparison.OrdinalIgnoreCase))
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

                    var pObs = GeometriaTopograficaHelper.CalcularPontoIrradiado(pEstacao, check, azimuteOrientacao);

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

        public static RelatorioQA Avaliar(
            double erroAngularGraus,
            double erroLinearMetros,
            double erroAltimetricoMetros,
            double perimetroTotalMetros,
            int numVertices,
            ParametrosTolerancia tolerancias)
        {
            ArgumentNullException.ThrowIfNull(tolerancias);

            int n = Math.Max(1, numVertices);
            double tolAngularGraus = tolerancias.CalcularToleranciaAngularGraus(n);
            double tolAngularSegundos = tolerancias.CalcularToleranciaAngularSegundos(n);
            double tolLinearAbsoluta = tolerancias.CalcularToleranciaLinearAbsolutaMetros(perimetroTotalMetros);
            double tolLinearRelativa = tolerancias.CalcularToleranciaLinearRelativaMetros(perimetroTotalMetros);
            double tolLinearAdmissivel = tolerancias.CalcularToleranciaLinearMetros(perimetroTotalMetros);
            double tolAltimetrica = tolerancias.CalcularToleranciaAltimetricaMetros(perimetroTotalMetros);

            var (aprovadoGeral, aprovadoRelativo, aprovadoAbsoluto) =
                tolerancias.AvaliarConformidadeLinear(erroLinearMetros, perimetroTotalMetros);

            double pObtido = (erroLinearMetros > 1e-9 && perimetroTotalMetros > 0.001)
                ? perimetroTotalMetros / erroLinearMetros
                : 0.0;

            bool angularConforme = Math.Abs(erroAngularGraus) <= tolAngularGraus + 1e-6;
            bool altimetricoConforme = Math.Abs(erroAltimetricoMetros) <= tolAltimetrica + 1e-4;

            return new RelatorioQA
            {
                ErroAngular = erroAngularGraus,
                ErroLinear = erroLinearMetros,
                ErroAltimetrico = erroAltimetricoMetros,
                ToleranciasAplicadas = tolerancias,
                ToleranciaAngularAdmissivel = tolAngularGraus,
                ToleranciaAngularSegundos = tolAngularSegundos,
                ToleranciaLinearAdmissivel = tolLinearAdmissivel,
                ToleranciaLinearAbsolutaMetros = tolLinearAbsoluta,
                ToleranciaLinearRelativaMetros = tolLinearRelativa,
                PrecisaoLinearObtidaDenominador = pObtido,
                ToleranciaAltimetricaAdmissivel = tolAltimetrica,
                AngularConforme = angularConforme,
                AprovadoRelativo = aprovadoRelativo,
                AprovadoAbsoluto = aprovadoAbsoluto,
                AprovadoLinearGeral = aprovadoGeral,
                ModoAvaliacaoLinearUtilizado = tolerancias.ModoAvaliacaoLinear,
                AltimetricoConforme = altimetricoConforme
            };
        }
    }
}
