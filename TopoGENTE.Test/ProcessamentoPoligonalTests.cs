using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using Xunit;

namespace TopoGente.Test
{
    public class ProcessamentoPoligonalTests
    {
        private const double AnguloZenitalHorizontalPadrao = 90.0;
        private const double ToleranciaComparacao = 1e-4;

        #region Helpers para Eliminação de Setup Pollution e Magic Numbers

        private static LevantamentoProcessor CriarProcessor()
        {
            return new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
        }

        private static Estacao CriarEstacaoComVisada(
            string nomeEstacao,
            string pontoVisado,
            double anguloHorizontal,
            double distanciaInclinada,
            double anguloVertical = AnguloZenitalHorizontalPadrao,
            TipoLeitura tipo = TipoLeitura.Poligonal,
            string purpose = "vante",
            double alturaInstrumento = 0.0,
            double alturaPrisma = 0.0)
        {
            var estacao = new Estacao
            {
                Nome = nomeEstacao,
                AlturaInstrumento = alturaInstrumento
            };

            estacao.AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = pontoVisado,
                Tipo = tipo,
                Purpose = purpose,
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = anguloVertical,
                DistanciaInclinada = distanciaInclinada,
                AlturaInstrumento = alturaInstrumento,
                AlturaPrisma = alturaPrisma
            });

            return estacao;
        }

        private static Estacao CriarEstacao(
            string nome,
            double alturaInstrumento = 0.0,
            params LeituraEstacaoTotal[] visadas)
        {
            var estacao = new Estacao
            {
                Nome = nome,
                AlturaInstrumento = alturaInstrumento
            };

            foreach (var visada in visadas)
            {
                estacao.AdicionarVisada(visada);
            }

            return estacao;
        }

        private static LeituraEstacaoTotal CriarVisadaVante(
            string pontoVisado,
            double anguloHorizontal,
            double distanciaInclinada,
            double anguloVertical = AnguloZenitalHorizontalPadrao,
            double alturaInstrumento = 0.0,
            double alturaPrisma = 0.0)
        {
            return new LeituraEstacaoTotal
            {
                PontoVisado = pontoVisado,
                Tipo = TipoLeitura.Poligonal,
                Purpose = "vante",
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = anguloVertical,
                DistanciaInclinada = distanciaInclinada,
                AlturaInstrumento = alturaInstrumento,
                AlturaPrisma = alturaPrisma
            };
        }

        private static LeituraEstacaoTotal CriarVisadaIrradiacao(
            string pontoVisado,
            double anguloHorizontal,
            double distanciaInclinada,
            double anguloVertical = AnguloZenitalHorizontalPadrao,
            double alturaInstrumento = 0.0,
            double alturaPrisma = 0.0)
        {
            return new LeituraEstacaoTotal
            {
                PontoVisado = pontoVisado,
                Tipo = TipoLeitura.Irradiacao,
                AnguloHorizontal = anguloHorizontal,
                AnguloVertical = anguloVertical,
                DistanciaInclinada = distanciaInclinada,
                AlturaInstrumento = alturaInstrumento,
                AlturaPrisma = alturaPrisma
            };
        }

        #endregion

        [Fact]
        public void Processar_PoligonalFechadaConforme_CompensaCoordenadasPeloMetodoBowditch()
        {
            // Arrange: Poligonal fechada quadrada de 4 lances de 100m com deflexões de 90°
            var processor = CriarProcessor();

            var e1 = CriarEstacaoComVisada("E1", "P1", anguloHorizontal: 90.0, distanciaInclinada: 100.0);
            var p1 = CriarEstacaoComVisada("P1", "P2", anguloHorizontal: 90.0, distanciaInclinada: 100.0);
            var p2 = CriarEstacaoComVisada("P2", "P3", anguloHorizontal: 90.0, distanciaInclinada: 100.0);
            var p3 = CriarEstacaoComVisada("P3", "E1", anguloHorizontal: 90.0, distanciaInclinada: 100.0);

            var todasEstacoes = new List<Estacao> { e1, p1, p2, p3 };

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = 0.0,
                PartidaY = 0.0,
                PartidaZ = 100.0,
                AzimutePartida = 0.0,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "P3", "E1" }
            };

            // Act
            var res = processor.Processar(
                new List<SequenciaPoligonal> { new SequenciaPoligonal { EhPrincipal = true, Metadados = metadados } },
                todasEstacoes,
                new Dictionary<string, PontoCoordenada>());

            // Assert
            res.Should().NotBeNull();
            res.AprovadoNorma.Should().BeTrue("poligonal fechada perfeitamente quadrada com 360° internos deve ser aprovada na norma");
            res.PoligonalFechada.Should().BeTrue();
            res.Perimetro.Should().BeApproximately(400.0, ToleranciaComparacao);
            res.ErroLinear.Should().BeApproximately(0.0, ToleranciaComparacao);
            res.Poligonal.Should().HaveCount(5, "sequência fechada de 4 vértices retorna 5 pontos incluindo o fechamento no ponto de partida");
        }

        [Fact]
        public void Processar_PoligonalFechadaComErroAngularExtrapolado_ReprovaLevantamento()
        {
            // Arrange: Aberração angular intencional de +15° em E1 extrapolando a tolerância
            var processor = CriarProcessor();

            var e1 = CriarEstacaoComVisada("E1", "P1", anguloHorizontal: 105.0, distanciaInclinada: 100.0);
            var p1 = CriarEstacaoComVisada("P1", "P2", anguloHorizontal: 90.0, distanciaInclinada: 100.0);
            var p2 = CriarEstacaoComVisada("P2", "E1", anguloHorizontal: 90.0, distanciaInclinada: 100.0);

            var todasEstacoes = new List<Estacao> { e1, p1, p2 };

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };

            // Act
            var res = processor.Processar(
                new List<SequenciaPoligonal> { new SequenciaPoligonal { EhPrincipal = true, Metadados = metadados } },
                todasEstacoes,
                new Dictionary<string, PontoCoordenada>());

            // Assert
            res.AprovadoNorma.Should().BeFalse("desvio angular de 15° excede severamente a tolerância normativa NBR 13.133");
        }

        [Fact]
        public void GerarEsbocoBruto_CalculaProjecaoTrigonometricaExata_DePontosIrradiadosPlanimetricosEAltimetricos()
        {
            // Arrange
            var processor = CriarProcessor();
            const double hiPadrao = 1.50;
            const double hpPadrao = 1.50;

            // E1 com visada de poligonal para E2 e 2 irradiações (uma horizontal e uma inclinada Z=60°)
            var e1 = CriarEstacao("E1", alturaInstrumento: hiPadrao,
                CriarVisadaVante("E2", anguloHorizontal: 0.0, distanciaInclinada: 100.0, alturaInstrumento: hiPadrao, alturaPrisma: hpPadrao),
                CriarVisadaIrradiacao("DET_HORIZ", anguloHorizontal: 45.0, distanciaInclinada: 50.0, anguloVertical: AnguloZenitalHorizontalPadrao, alturaInstrumento: hiPadrao, alturaPrisma: hpPadrao),
                CriarVisadaIrradiacao("DET_INCLINADO", anguloHorizontal: 90.0, distanciaInclinada: 100.0, anguloVertical: 60.0, alturaInstrumento: hiPadrao, alturaPrisma: hpPadrao));

            var e2 = CriarEstacaoComVisada("E2", "E1", anguloHorizontal: 180.0, distanciaInclinada: 100.0, alturaInstrumento: hiPadrao, alturaPrisma: hpPadrao);

            var todasEstacoes = new List<Estacao> { e1, e2 };

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = 1000.0,
                PartidaY = 1000.0,
                PartidaZ = 100.0,
                AzimutePartida = 0.0,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2", "E1" }
            };

            var sequencia = new SequenciaPoligonal { EhPrincipal = true, Metadados = metadados };

            // Act
            var res = processor.GerarEsbocoBruto(new List<SequenciaPoligonal> { sequencia }, todasEstacoes);

            // Assert
            res.Irradiacoes.Should().NotBeEmpty();
            var detHoriz = res.Irradiacoes.First(i => i.Nome == "DET_HORIZ");
            detHoriz.Z.Should().BeApproximately(100.0, ToleranciaComparacao,
                "com visada horizontal V=90 e hi=hp, a cota deve ser idêntica à estação ocupada");

            // Validação de projeção analítica no plano cartesiano (NBR 13.133)
            double esperadoXHoriz = 1000.0 + (50.0 * Math.Sin(Math.PI / 4.0));
            double esperadoYHoriz = 1000.0 + (50.0 * Math.Cos(Math.PI / 4.0));
            detHoriz.XBruto.Should().BeApproximately(esperadoXHoriz, ToleranciaComparacao,
                "coordenada X bruta da irradiação deve seguir a projeção senoidal do azimute 45°");
            detHoriz.YBruto.Should().BeApproximately(esperadoYHoriz, ToleranciaComparacao,
                "coordenada Y bruta da irradiação deve seguir a projeção cossenoidal do azimute 45°");

            var detInclinado = res.Irradiacoes.First(i => i.Nome == "DET_INCLINADO");
            detInclinado.Z.Should().BeApproximately(150.0, ToleranciaComparacao,
                "com V=60°, cos(60°)=0.5 e di=100m, o desnível trigonométrico é de +50m sobre a cota de 100m da estação");
        }
    }
}
