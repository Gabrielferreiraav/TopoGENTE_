using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGENTE.Domain.Exceptions;
using TopoGente.Core.Strategies;

namespace TopoGENTE.Test
{
    public class ProcessamentoPoligonalTests
    {
        private static LevantamentoProcessor CriarProcessor()
        {
            return new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
        }

        [Fact]
        public void Processar_PoligonalFechadaConforme_CompensaCoordenadasPeloMetodoBowditch()
        {
            // Arrange
            var processor = CriarProcessor();
            var e1 = new Estacao { Nome = "E1" };
            e1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P1", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0 });
            var p1 = new Estacao { Nome = "P1" };
            p1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P2", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0 });
            var p2 = new Estacao { Nome = "P2" };
            p2.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P3", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0 });
            var p3 = new Estacao { Nome = "P3" };
            p3.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "E1", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90.0, AnguloVertical = 90.0, DistanciaInclinada = 100.0 });

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
            res.Perimetro.Should().BeApproximately(400.0, 0.001);
            res.ErroLinear.Should().BeApproximately(0.0, 0.001);
            res.Poligonal.Should().HaveCount(5, "sequência fechada de 4 vértices retorna 5 pontos incluindo o fechamento no ponto de partida");
        }

        [Fact]
        public void Processar_PoligonalFechadaComErroAngularExtrapolado_ReprovaLevantamento()
        {
            // Arrange
            var processor = CriarProcessor();
            
            var e1 = new Estacao { Nome = "E1" };
            // Aberracao intencional: 15 graus off!
            e1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P1", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 105, DistanciaInclinada = 100 });
            
            var p1 = new Estacao { Nome = "P1" };
            p1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P2", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90, DistanciaInclinada = 100 });
            
            var p2 = new Estacao { Nome = "P2" };
            p2.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "E1", Tipo = TipoLeitura.Poligonal, Purpose = "vante", AnguloHorizontal = 90, DistanciaInclinada = 100 });
            
            var todasEstacoes = new List<Estacao> { e1, p1, p2 };
            
            var metadados = new MetadadosCenario 
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };

            // Act
            var res = processor.Processar(new List<SequenciaPoligonal> { new SequenciaPoligonal { EhPrincipal = true, Metadados = metadados } }, todasEstacoes, new Dictionary<string, PontoCoordenada>());
            
            // Assert
            // AprovadoNorma == false devido ao enorme erro angular
            Assert.False(res.AprovadoNorma);
        }

        [Fact]
        public void GerarEsbocoBruto_CalculaProjecaoTrigonometricaExata_DePontosIrradiadosPlanimetricosEAltimetricos()
        {
            // Arrange
            var processor = CriarProcessor();

            var e1 = new Estacao { Nome = "E1", AlturaInstrumento = 1.50 };
            // Visada de poligonal para E2
            e1.AdicionarVisada(new LeituraEstacaoTotal 
            { 
                PontoVisado = "E2", 
                Tipo = TipoLeitura.Poligonal, 
                Purpose = "vante",
                AnguloHorizontal = 0.0, 
                AnguloVertical = 90.0, 
                DistanciaInclinada = 100.0,
                AlturaInstrumento = 1.50,
                AlturaPrisma = 1.50
            });
            // Irradiação 1: horizontal (desnível zero -> cota = cotaEstacao)
            e1.AdicionarVisada(new LeituraEstacaoTotal 
            { 
                PontoVisado = "DET_HORIZ", 
                Tipo = TipoLeitura.Irradiacao, 
                AnguloHorizontal = 45.0, 
                AnguloVertical = 90.0, 
                DistanciaInclinada = 50.0,
                AlturaInstrumento = 1.50,
                AlturaPrisma = 1.50
            });
            // Irradiação 2: inclinada (zenital 60° -> cos(60°)=0.5 -> deltaZ = 100 * 0.5 + 1.5 - 1.5 = +50.0m)
            e1.AdicionarVisada(new LeituraEstacaoTotal 
            { 
                PontoVisado = "DET_INCLINADO", 
                Tipo = TipoLeitura.Irradiacao, 
                AnguloHorizontal = 90.0, 
                AnguloVertical = 60.0, 
                DistanciaInclinada = 100.0,
                AlturaInstrumento = 1.50,
                AlturaPrisma = 1.50
            });

            var e2 = new Estacao { Nome = "E2", AlturaInstrumento = 1.50 };
            e2.AdicionarVisada(new LeituraEstacaoTotal 
            { 
                PontoVisado = "E1", 
                Tipo = TipoLeitura.Poligonal, 
                Purpose = "vante",
                AnguloHorizontal = 180.0, 
                AnguloVertical = 90.0, 
                DistanciaInclinada = 100.0,
                AlturaInstrumento = 1.50,
                AlturaPrisma = 1.50
            });

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
            detHoriz.Z.Should().BeApproximately(100.0, 0.001, "com visada horizontal V=90 e hi=hp, a cota deve ser idêntica à estação ocupada");
            
            // Validação de projeção analítica no plano cartesiano (NBR 13.133)
            double esperadoXHoriz = 1000.0 + (50.0 * Math.Sin(Math.PI / 4.0));
            double esperadoYHoriz = 1000.0 + (50.0 * Math.Cos(Math.PI / 4.0));
            detHoriz.XBruto.Should().BeApproximately(esperadoXHoriz, 0.001, "coordenada X bruta da irradiação deve seguir a projeção senoidal do azimute 45°");
            detHoriz.YBruto.Should().BeApproximately(esperadoYHoriz, 0.001, "coordenada Y bruta da irradiação deve seguir a projeção cossenoidal do azimute 45°");

            var detInclinado = res.Irradiacoes.First(i => i.Nome == "DET_INCLINADO");
            detInclinado.Z.Should().BeApproximately(150.0, 0.001, "com V=60°, cos(60°)=0.5 e di=100m, o desnível trigonométrico é de +50m sobre a cota de 100m da estação");
        }
    }
}