using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;

namespace TopoGente.Core.Tests
{
    public class LevantamentoProcessorTests
    {
        [Fact]
        public void Processar_PoligonalFechada_ComErroAngularAcimaDaTolerancia_DeveAbortarCompensacao()
        {
            
            // 1. ARRANGE (Injeção de Dependências e Cenário)
            
            var classificador = new ClassificadorGrafo();
            var processador = new LevantamentoProcessor(classificador);

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = 1000.0,
                PartidaY = 1000.0,
                PartidaZ = 100.0,
                UsarCoordenadaRe = false,
                AzimutePartida = 90.0,
                NomeRe = "E2"
            };

            var estacoes = new List<Estacao>
    {
        new Estacao {
            Nome = "E0",
            Leituras = new List<LeituraEstacaoTotal> {
                new LeituraEstacaoTotal { EstacaoOcupada = "E0", PontoVisado = "E2", AnguloHorizontal = 0.0, DistanciaInclinada = 141.42, AnguloVertical = 90.0 },
                new LeituraEstacaoTotal { EstacaoOcupada = "E0", PontoVisado = "E1", AnguloHorizontal = 0.0, DistanciaInclinada = 100.0, AnguloVertical = 90.0 }
            }
        },
        new Estacao {
            Nome = "E1",
            Leituras = new List<LeituraEstacaoTotal> {
                new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E0", AnguloHorizontal = 0.0, DistanciaInclinada = 100.0, AnguloVertical = 90.0 },
                new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E2", AnguloHorizontal = 90.0, DistanciaInclinada = 100.0, AnguloVertical = 90.0 }
            }
        },
        new Estacao {
            Nome = "E2",
            Leituras = new List<LeituraEstacaoTotal> {
                new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E1", AnguloHorizontal = 0.0, DistanciaInclinada = 100.0, AnguloVertical = 90.0 },
                new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E0", AnguloHorizontal = 90.0, DistanciaInclinada = 141.42, AnguloVertical = 90.0 }
            }
        },
        new Estacao {
            Nome = "E0", // Ocupando E0 novamente para o fechamento
            Leituras = new List<LeituraEstacaoTotal> {
                // ERRO GROSSEIRO INJETADO (15 graus) COM CHAVES ESTRITAS
                new LeituraEstacaoTotal { EstacaoOcupada = "E0", PontoVisado = "E2", AnguloHorizontal = 15.0, DistanciaInclinada = 141.42, AnguloVertical = 90.0 }
            }
        }
    };

            var pontosConhecidos = new Dictionary<string, PontoCoordenada>();

            
            // 2. ACT

            var resultado = processador.Processar(metadados, estacoes, pontosConhecidos);

            
            // 3. ASSERT
            
            Assert.True(resultado.PoligonalFechada, "Falhou em reconhecer o fechamento físico.");

            Assert.True(Math.Abs(resultado.ErroAngular) > 1.0,
                $"Erro Angular foi {resultado.ErroAngular}°. Deveria ser superior a 1° devido ao erro injetado.");

            Assert.Equal(0, resultado.Precisao);

            var pontoFinalBruto = resultado.PoligonalBruta.Last();
            var pontoFinalExposto = resultado.Poligonal.Last();
            Assert.Equal(pontoFinalBruto.X, pontoFinalExposto.X);
            Assert.Equal(pontoFinalBruto.Y, pontoFinalExposto.Y);
        }
    }
}