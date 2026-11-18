using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using Xunit;

namespace TopoGente.Test
{
    public class CalculoIrradiacaoOrientacaoReTests
    {
        private const double ToleranciaCoordenada = 0.001;

        private static PontoCoordenada CriarPonto(string nome, double x, double y, double z = 100.0, bool ehPoligonal = true) =>
            new PontoCoordenada
            {
                Nome = nome,
                X = x,
                Y = y,
                Z = z,
                EhPontoPoligonal = ehPoligonal
            };

        private static LeituraEstacaoTotal CriarLeituraRe(
            string estacao,
            string visado,
            double anguloHorizontal = 0.0,
            double distancia = 100.0,
            TipoLeitura tipo = TipoLeitura.Re) =>
            new LeituraEstacaoTotal
            {
                EstacaoOcupada = estacao,
                PontoVisado = visado,
                Tipo = tipo,
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = 90.0,
                DistanciaInclinada = distancia,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

        private static LeituraEstacaoTotal CriarLeituraIrradiacao(
            string estacao,
            string visado,
            double anguloHorizontal,
            double distancia = 50.0,
            string observacao = "") =>
            new LeituraEstacaoTotal
            {
                EstacaoOcupada = estacao,
                PontoVisado = visado,
                Tipo = TipoLeitura.Irradiacao,
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = 90.0,
                DistanciaInclinada = distancia,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5,
                Observacao = observacao
            };

        private static Estacao CriarEstacao(string nome, params LeituraEstacaoTotal[] leituras)
        {
            var estacao = new Estacao { Nome = nome };
            foreach (var leitura in leituras)
            {
                estacao.AdicionarVisada(leitura);
            }
            return estacao;
        }

        [Fact]
        public void VisitarEstacao_ComReEmZeroGraus_MantemCompatibilidadeRegressiva()
        {
            // Arrange
            var e1Compensado = CriarPonto("E1", 1000.0, 1000.0, 100.0);
            var refPonto = CriarPonto("REF", 1000.0, 1100.0, 100.0);

            var poligonal = new List<PontoCoordenada> { e1Compensado };
            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["REF"] = refPonto
            };

            var visitor = new CalculoIrradiacaoVisitor(poligonal, pontosConhecidos, azimuteInicial: 0.0);

            var estacaoE1 = CriarEstacao("E1",
                CriarLeituraRe("E1", "REF", anguloHorizontal: 0.0, distancia: 100.0),
                CriarLeituraIrradiacao("E1", "IRR1", anguloHorizontal: 90.0, distancia: 50.0, observacao: "Cerca"));

            // Act
            estacaoE1.Accept(visitor);

            // Assert
            visitor.IrradiacoesCalculadas.Should().HaveCount(1);
            var ponto = visitor.IrradiacoesCalculadas[0];

            ponto.Nome.Should().Be("IRR1");
            ponto.X.Should().BeApproximately(1050.0, ToleranciaCoordenada);
            ponto.Y.Should().BeApproximately(1000.0, ToleranciaCoordenada);
            ponto.Z.Should().BeApproximately(100.0, ToleranciaCoordenada);
            ponto.XBruto.Should().BeApproximately(ponto.X, ToleranciaCoordenada);
            ponto.YBruto.Should().BeApproximately(ponto.Y, ToleranciaCoordenada);
            ponto.ZBruto.Should().BeApproximately(ponto.Z, ToleranciaCoordenada);
            ponto.Descricao.Should().Be("Cerca");
            ponto.EhPontoPoligonal.Should().BeFalse();
            estacaoE1.PontosCalculados.Should().ContainSingle(p => p.Nome == "IRR1");
        }

        [Fact]
        public void VisitarEstacao_EstacaoIntermediariaComReLocal_CalculaAzimutePorCoordenadasCompensadas()
        {
            // Arrange
            var e1Compensado = CriarPonto("E1", 1000.0, 1000.0, 100.0);
            var e2Compensado = CriarPonto("E2", 1000.0, 1100.0, 100.0);

            var poligonal = new List<PontoCoordenada> { e1Compensado, e2Compensado };
            var visitor = new CalculoIrradiacaoVisitor(poligonal, null, azimuteInicial: 0.0);

            // Na estacao E2, a visada para E1 eh uma ReLocal amarrada ao vertice anterior
            // Irradiacao deflete 90 graus a partir da re (azimute de E2 para E1 eh 180; com 90 vira 270 graus, direcao Oeste)
            var estacaoE2 = CriarEstacao("E2",
                CriarLeituraRe("E2", "E1", anguloHorizontal: 0.0, distancia: 100.0, tipo: TipoLeitura.ReLocal),
                CriarLeituraIrradiacao("E2", "IRR_E2", anguloHorizontal: 90.0, distancia: 50.0, observacao: "Poste"));

            // Act
            estacaoE2.Accept(visitor);

            // Assert
            visitor.IrradiacoesCalculadas.Should().HaveCount(1);
            var ponto = visitor.IrradiacoesCalculadas[0];

            ponto.Nome.Should().Be("IRR_E2");
            ponto.X.Should().BeApproximately(950.0, ToleranciaCoordenada);
            ponto.Y.Should().BeApproximately(1100.0, ToleranciaCoordenada);
            ponto.Z.Should().BeApproximately(100.0, ToleranciaCoordenada);
            ponto.Descricao.Should().Be("Poste");
        }

        [Fact]
        public void VisitarEstacao_ComReEmAnguloArbitrario_DescontaAnguloReEDefleteEm45Graus()
        {
            // Arrange
            // Estacao E1 em (1000, 1000), Re REF em (1000, 1100) -> Azimute verdadeiro da Re = 0.0 graus
            var e1Compensado = CriarPonto("E1", 1000.0, 1000.0, 100.0);
            var refPonto = CriarPonto("REF", 1000.0, 1100.0, 100.0);

            var poligonal = new List<PontoCoordenada> { e1Compensado };
            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["REF"] = refPonto
            };

            var visitor = new CalculoIrradiacaoVisitor(poligonal, pontosConhecidos, azimuteInicial: 0.0);

            // Operador visou o marco REF com limbo em 45.0000 graus
            // Operador visou o ponto irradiado com limbo em 90.0000 graus -> deflexao liquida horaria = 45.0000 graus
            var estacaoE1 = CriarEstacao("E1",
                CriarLeituraRe("E1", "REF", anguloHorizontal: 45.0, distancia: 100.0),
                CriarLeituraIrradiacao("E1", "IRR_45", anguloHorizontal: 90.0, distancia: 100.0, observacao: "Ponto 45 graus"));

            // Act
            estacaoE1.Accept(visitor);

            // Assert
            visitor.IrradiacoesCalculadas.Should().HaveCount(1);
            var ponto = visitor.IrradiacoesCalculadas[0];

            // Azimute resultante = 0.0 + (90.0 - 45.0) = 45.0 graus
            double deltaEsperado = 100.0 * Math.Sin(Math.PI / 4.0);
            ponto.X.Should().BeApproximately(1000.0 + deltaEsperado, ToleranciaCoordenada);
            ponto.Y.Should().BeApproximately(1000.0 + deltaEsperado, ToleranciaCoordenada);
            ponto.Z.Should().BeApproximately(100.0, ToleranciaCoordenada);
        }

        [Fact]
        public void VisitarEstacao_ComReEmAnguloArbitrarioEPassagemPor360_DescontaAnguloReComExatidao()
        {
            // Arrange
            // Estacao E1 em (500, 500), Re REF em (500, 600) -> Azimute verdadeiro da Re = 0.0 graus
            var e1Compensado = CriarPonto("E1", 500.0, 500.0, 50.0);
            var refPonto = CriarPonto("REF", 500.0, 600.0, 50.0);

            var poligonal = new List<PontoCoordenada> { e1Compensado };
            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["REF"] = refPonto
            };

            var visitor = new CalculoIrradiacaoVisitor(poligonal, pontosConhecidos, azimuteInicial: 0.0);

            // Operador visou o marco REF com limbo em 350.0000 graus
            // Operador visou ponto irradiado em 20.0000 graus -> deflexao liquida horaria = (20 - 350) normalizado = 30.0000 graus
            var estacaoE1 = CriarEstacao("E1",
                CriarLeituraRe("E1", "REF", anguloHorizontal: 350.0, distancia: 100.0),
                CriarLeituraIrradiacao("E1", "IRR_30", anguloHorizontal: 20.0, distancia: 100.0, observacao: "Ponto 30 graus"));

            // Act
            estacaoE1.Accept(visitor);

            // Assert
            visitor.IrradiacoesCalculadas.Should().HaveCount(1);
            var ponto = visitor.IrradiacoesCalculadas[0];

            // Azimute resultante = 0.0 + 30.0 = 30.0 graus
            double deltaXEsperado = 100.0 * Math.Sin(30.0 * Math.PI / 180.0); // 50.0
            double deltaYEsperado = 100.0 * Math.Cos(30.0 * Math.PI / 180.0); // 86.6025
            ponto.X.Should().BeApproximately(500.0 + deltaXEsperado, ToleranciaCoordenada);
            ponto.Y.Should().BeApproximately(500.0 + deltaYEsperado, ToleranciaCoordenada);
            ponto.Z.Should().BeApproximately(50.0, ToleranciaCoordenada);
        }

        [Fact]
        public void VisitarEstacao_MultiplasEstacoes_DescartaOrientacaoAnteriorAoOcuparNovaEstacao()
        {
            // Arrange
            var e1Compensado = CriarPonto("E1", 1000.0, 1000.0, 100.0);
            var e2Compensado = CriarPonto("E2", 1100.0, 1000.0, 100.0);
            var refPonto = CriarPonto("REF", 1000.0, 1100.0, 100.0);

            var poligonal = new List<PontoCoordenada> { e1Compensado, e2Compensado };
            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["REF"] = refPonto
            };

            var visitor = new CalculoIrradiacaoVisitor(poligonal, pontosConhecidos, azimuteInicial: 0.0);

            var estacaoE1 = CriarEstacao("E1",
                CriarLeituraRe("E1", "REF", anguloHorizontal: 45.0, distancia: 100.0),
                CriarLeituraIrradiacao("E1", "IRR_E1", anguloHorizontal: 90.0, distancia: 100.0));

            // Na estacao E2, a ré é E1 (Azimute de E2 para E1 = 270 graus). Limbo da ré zerado em 0 graus.
            // Irradiacao em 90 graus a partir da ré -> Azimute = 270 + 90 = 360 = 0 graus (Norte)
            var estacaoE2 = CriarEstacao("E2",
                CriarLeituraRe("E2", "E1", anguloHorizontal: 0.0, distancia: 100.0, tipo: TipoLeitura.ReLocal),
                CriarLeituraIrradiacao("E2", "IRR_E2", anguloHorizontal: 90.0, distancia: 50.0));

            // Act
            estacaoE1.Accept(visitor);
            estacaoE2.Accept(visitor);

            // Assert
            visitor.IrradiacoesCalculadas.Should().HaveCount(2);

            var pontoE2 = visitor.IrradiacoesCalculadas[1];
            pontoE2.Nome.Should().Be("IRR_E2");
            pontoE2.X.Should().BeApproximately(1100.0, ToleranciaCoordenada);
            pontoE2.Y.Should().BeApproximately(1050.0, ToleranciaCoordenada);
            pontoE2.Z.Should().BeApproximately(100.0, ToleranciaCoordenada);
        }

        [Fact]
        public void VisitarEstacao_PreservaCoordenadasBrutasESemantica()
        {
            // Arrange
            var e1Compensado = CriarPonto("E1", 200.0, 300.0, 50.0);
            var poligonal = new List<PontoCoordenada> { e1Compensado };
            var visitor = new CalculoIrradiacaoVisitor(poligonal, null, azimuteInicial: 45.0);

            var estacaoE1 = CriarEstacao("E1",
                CriarLeituraIrradiacao("E1", "P_DETALHE", anguloHorizontal: 0.0, distancia: 10.0, observacao: "Bordo de pista"));

            // Act
            estacaoE1.Accept(visitor);

            // Assert
            var ponto = visitor.IrradiacoesCalculadas.Should().ContainSingle().Subject;
            ponto.XBruto.Should().BeApproximately(ponto.X, ToleranciaCoordenada);
            ponto.YBruto.Should().BeApproximately(ponto.Y, ToleranciaCoordenada);
            ponto.ZBruto.Should().BeApproximately(ponto.Z, ToleranciaCoordenada);
            ponto.Descricao.Should().Be("Bordo de pista");
            ponto.EhPontoPoligonal.Should().BeFalse();
        }
    }
}
