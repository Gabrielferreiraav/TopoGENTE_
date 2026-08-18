using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;

namespace TopoGENTE.Test
{
    public class AuxiliarTests
    {
        [Fact]
        public void PrecisaoRelativa_Deve_Bater_Com_Calculadora_Sem_Truncamento()
        {
            // O cálculo exigido é:
            // erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY))
            // precisaoRelativa = perimetroTotal > 0.0001 ? erroLinearTotal / perimetroTotal : 0
            
            var resultado = new ResultadoLevantamento
            {
                Precisao = 1.0 / 12850.4, // Precisao = erro / perimetro, ex: 1 / 12850.4
                AprovadoNorma = true
            };

            // Testamos a lógica de formatação de forma idêntica ao ViewModel (já que não temos ref pra UI aqui)
            string FormatarPrecisao(double precisao)
            {
                if (precisao <= 0) return "1:∞";
                double denominador = 1.0 / precisao;
                long arredondado = (long)Math.Round(denominador, MidpointRounding.AwayFromZero);
                return $"1:{arredondado:N0}";
            }

            var expected = $"1:{(long)12850:N0}";
            Assert.Equal(expected, FormatarPrecisao(resultado.Precisao));

            var resultadoInf = new ResultadoLevantamento { Precisao = 0 };
            Assert.Equal("1:∞", FormatarPrecisao(resultadoInf.Precisao));
        }

        [Fact]
        public void PoligonalSecundaria_Deve_Usar_Estacao_Compensada_Da_Principal()
        {
            // Arrange
            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "P1", CoordenadaConhecida = new PontoCoordenada { Nome = "P1", X = 100, Y = 100, Z = 10 } },
                new Estacao { Nome = "P2" }
            };

            estacoes[0].AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "REF",
                AnguloHorizontal = 0,
                AnguloVertical = 90,
                Purpose = "re",
                Tipo = TipoLeitura.Re
            });
            estacoes[0].AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "P2",
                DistanciaInclinada = 100,
                AnguloHorizontal = 90,
                AnguloVertical = 90,
                Purpose = "vante",
                Tipo = TipoLeitura.Poligonal
            });
            
            estacoes[1].AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "P1",
                AnguloHorizontal = 0,
                AnguloVertical = 90,
                Purpose = "re",
                Tipo = TipoLeitura.Re
            });
            estacoes[1].AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "AUX1",
                DistanciaInclinada = 50,
                AnguloHorizontal = 90,
                AnguloVertical = 90,
                Purpose = "vante",
                Tipo = TipoLeitura.Auxiliar
            });

            var metadadosPrin = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                PartidaX = 100, PartidaY = 100, PartidaZ = 10,
                UsarCoordenadaRe = true,
                ReX = 100, ReY = 200, ReZ = 10, // REF no norte (Az = 0)
                SequenciaEstacoesSelecionadas = new List<string> { "P1", "P2" }
            };
            
            var metadadosSec = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                PartidaX = 0, PartidaY = 0,
                UsarCoordenadaRe = false,
                AzimutePartida = 0,
                SequenciaEstacoesSelecionadas = new List<string> { "P2", "AUX1" }
            };

            var sequencias = new List<SequenciaPoligonal>
            {
                new SequenciaPoligonal { Nome = "Principal", EhPrincipal = true, Metadados = metadadosPrin, Estacoes = metadadosPrin.SequenciaEstacoesSelecionadas },
                new SequenciaPoligonal { Nome = "Secundaria", EhPrincipal = false, EstacaoAncoragemNome = "P2", Metadados = metadadosSec, Estacoes = metadadosSec.SequenciaEstacoesSelecionadas }
            };

            var processador = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
            
            // Act
            var resultado = processador.Processar(sequencias, estacoes, new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase) { { "P1", estacoes[0].CoordenadaConhecida! } });

            // Assert
            Assert.NotNull(resultado);
            var p2 = resultado.Poligonal.First(p => p.Nome == "P2");
            Assert.Equal(200, p2.X, 3);
            Assert.Equal(100, p2.Y, 3);

            var aux1 = resultado.Poligonal.FirstOrDefault(p => p.Nome == "AUX1");
            Assert.NotNull(aux1);
            Assert.Equal(250, aux1.X, 3);
            Assert.Equal(100, aux1.Y, 3);
        }

        [Fact]
        public void Calculador_Deve_Lancar_Excecao_Se_Ancoragem_Nao_Existir_No_Grafo()
        {
            // Arrange
            var metadadosPrin = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                PartidaX = 100, PartidaY = 100, PartidaZ = 10,
                SequenciaEstacoesSelecionadas = new List<string> { "P1", "P2" }
            };
            
            var metadadosSec = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                SequenciaEstacoesSelecionadas = new List<string> { "P99", "AUX1" }
            };

            var sequencias = new List<SequenciaPoligonal>
            {
                new SequenciaPoligonal { Nome = "Principal", EhPrincipal = true, Metadados = metadadosPrin, Estacoes = metadadosPrin.SequenciaEstacoesSelecionadas },
                new SequenciaPoligonal { Nome = "Secundaria", EhPrincipal = false, EstacaoAncoragemNome = "P99", Metadados = metadadosSec, Estacoes = metadadosSec.SequenciaEstacoesSelecionadas }
            };

            var processador = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());
            var estacoes = new List<Estacao> { new Estacao { Nome = "P1" } };

            // Act & Assert
            Assert.Throws<DadosInsuficientesException>(() => processador.Processar(sequencias, estacoes, new Dictionary<string, PontoCoordenada>()));
        }

        [Fact]
        public void GerarEsbocoBruto_Deve_Calcular_Irradiacoes_E_Desvios_Brutos_Antes_De_Compensar()
        {
            // Arrange: Configura cenário com uma estação E1 e uma irradiação feita a partir dela
            var sequencias = new List<SequenciaPoligonal> {
                new SequenciaPoligonal {
                    EhPrincipal = true,
                    Metadados = new MetadadosCenario {
                        TipoCenario = TipoCenarioPoligonal.Fechada,
                        PartidaX = 1000.0, PartidaY = 1000.0, PartidaZ = 100.0,
                        AzimutePartida = 0.0,
                        SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2", "E1" }
                    }
                }
            };

            var estacao1 = new Estacao { Nome = "E1", AlturaInstrumento = 1.50 };
            estacao1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "E2", Tipo = TipoLeitura.Poligonal, DistanciaInclinada = 100.0, AnguloVertical = 90.0, AnguloHorizontal = 0.0 });
            estacao1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "IRR1", Tipo = TipoLeitura.Irradiacao, DistanciaInclinada = 50.0, AnguloVertical = 90.0, AnguloHorizontal = 45.0, AlturaPrisma = 1.50 });

            var estacao2 = new Estacao { Nome = "E2", AlturaInstrumento = 1.50 };
            estacao2.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "E1", Tipo = TipoLeitura.Poligonal, DistanciaInclinada = 100.0, AnguloVertical = 90.0, AnguloHorizontal = 180.0 });

            var estacoes = new List<Estacao> { estacao1, estacao2 };

            var processor = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());

            // Act
            var resultado = processor.GerarEsbocoBruto(sequencias, estacoes);

            // Assert
            Assert.NotNull(resultado);
            Assert.NotEmpty(resultado.PoligonalBruta);
            Assert.NotEmpty(resultado.Irradiacoes);
            
            // Verifica se calculou a irradiação bruta de forma coerente no plano cartesiano
            var irr1 = resultado.Irradiacoes.First(i => i.Nome == "IRR1");
            Assert.True(irr1.XBruto > 1000.0);
            Assert.True(irr1.YBruto > 1000.0);
        }
    }
}
