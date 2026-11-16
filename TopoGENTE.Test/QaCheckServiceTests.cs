using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Services;
using TopoGente.Test.Builders;
using Xunit;

namespace TopoGente.Test
{
    public sealed class QaCheckServiceTests
    {
        [Fact]
        public void GerarRelatorioQaChecks_AoTrocarParaClasseIP_DeveRecalcularToleranciaAngularSemReaproveitarMemorialClasseIVP()
        {
            // Arrange: poligonal fechada com n = 4 estacoes (Classe IV-P Ta = 120", Classe I-P Ta = 12")
            // e erro angular de 18" (aprovado na IV-P, reprovado na I-P).
            var resultado = new ResultadoLevantamentoQaBuilder()
                .ComPoligonalFechada(estacoesDistintas: 4, perimetroMetros: 400.0)
                .ComErroAngularEmSegundos(18.0)
                .ComMemorialAjustamentoHistorico(
                    toleranciaAngularSegundos: 120.0,
                    angularAprovado: true,
                    tipo: TipoEstrategiaAjustamento.Bowditch)
                .Build();

            var qaService = new QaCheckService();
            var toleranciaClasseIP = ParametrosTolerancia.ClasseIP;

            // Act
            RelatorioQA relatorio = qaService.GerarRelatorioQaChecks(
                new List<Estacao>(),
                resultado,
                new Dictionary<string, PontoCoordenada>(),
                toleranciaClasseIP);

            // Assert: oraculo independente pré-calculado manualmente para n = 4 na Classe I-P (6" * sqrt(4) = 12")
            relatorio.ToleranciaAngularSegundos.Should().BeApproximately(12.0, 1e-4);
            relatorio.ToleranciaAngularAdmissivel.Should().BeApproximately(0.0033333333333333335, 1e-9);
            relatorio.AngularConforme.Should().BeFalse();

            // Salvaguarda de integridade: o memorial historico do ajustamento permanece inalterado
            resultado.Memorial.Ajustamento!.ToleranciaAngularSegundos.Should().BeApproximately(120.0, 1e-4);
            resultado.Memorial.Ajustamento.AngularAprovado.Should().BeTrue();
        }
    }
}
