using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;
using TopoGENTE.Test.Builders;

namespace TopoGente.Tests.CaixaBranca
{
    public sealed record CompensacaoEnquadradaCaixaBrancaTests
    {
        private static CompensacaoPoligonalInputDTO CriarEntradaBase()
        {
            return new PoligonalTestBuilder()
                .ComoEnquadrada("E0", (0, 0, 0), "E4", (400, 0, 0), azimutePartida: 270.0)
                .ComAzimuteChegada(270.0)
                .ComEstacao("E0", hi: 0).VisandoVante("E1", anguloHz: 180, distanciaInclinada: 100, anguloVt: 90, hp: 0)
                .ComEstacao("E1", hi: 0).VisandoVante("E2", anguloHz: 180, distanciaInclinada: 100, anguloVt: 90, hp: 0)
                .ComEstacao("E2", hi: 0).VisandoVante("E3", anguloHz: 180, distanciaInclinada: 100, anguloVt: 90, hp: 0)
                .ComEstacao("E3", hi: 0).VisandoVante("E4", anguloHz: 180, distanciaInclinada: 100, anguloVt: 90, hp: 0)
                .BuildDto();
        }

        [Fact]
        public void CT01_Deve_Abortar_Quando_Leituras_Ausentes()
        {
            var entrada = CriarEntradaBase() with { Leituras = new List<LeituraEstacaoTotal>() };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeFalse();
            resultado.AlertaReprovacao.Should().Be("Nenhuma leitura fornecida. Compensação não realizada.");
        }

        [Fact]
        public void CT02_Deve_Abortar_Quando_Coordenadas_Chegada_Ausentes()
        {
            var entrada = CriarEntradaBase() with
            {
                Metadados = new MetadadosCenario { NomeChegada = "E4" }
            };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeFalse();
            resultado.AlertaReprovacao.Should().Be("Poligonal enquadrada exige coordenadas de chegada (X, Y, Z).");
        }

        [Fact]
        public void CT03_Deve_Abortar_Quando_Erro_Angular_Supera_Tolerancia()
        {
            var entrada = CriarEntradaBase() with { AzimuteChegada = 0.0 };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeFalse();
            resultado.AlertaReprovacao.Should().Contain("Erro Angular");
        }

        [Fact]
        public void CT04_Deve_Abortar_Quando_Precisao_Linear_E_Inferior_A_Norma()
        {
            var entrada = CriarEntradaBase();
            entrada.Metadados.ChegadaX = 1000.0;
            entrada.Metadados.ChegadaY = 0.0;
            entrada.Metadados.ChegadaZ = 0.0;

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeFalse();
            resultado.AlertaReprovacao.Should().Contain("Precisão Linear");
        }

        [Fact]
        public void CT05_Deve_Abortar_Quando_Erro_Altimetrico_Supera_Tolerancia()
        {
            var entrada = CriarEntradaBase();
            entrada.Metadados.ChegadaZ = 1000.0;

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeFalse();
            resultado.AlertaReprovacao.ToLowerInvariant().Should().Contain("altimétrico");
        }

        [Fact]
        public void CT06_Deve_Aprovar_Quando_Todos_Os_Criterios_Sao_Atendidos()
        {
            var entrada = CriarEntradaBase();

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            resultado.AprovadoNorma.Should().BeTrue();
            resultado.AlertaReprovacao.Should().BeEmpty();
            resultado.PoligonalCompensada[1].X.Should().BeApproximately(100.0, 0.001);
            resultado.PoligonalCompensada[1].Y.Should().BeApproximately(0.0, 0.001);
            resultado.PoligonalCompensada[1].Z.Should().BeApproximately(0.0, 0.001);
            resultado.PoligonalCompensada[4].X.Should().BeApproximately(400.0, 0.001);
            resultado.PoligonalCompensada[4].Y.Should().BeApproximately(0.0, 0.001);
            resultado.PoligonalCompensada[4].Z.Should().BeApproximately(0.0, 0.001);
        }
    }
}
