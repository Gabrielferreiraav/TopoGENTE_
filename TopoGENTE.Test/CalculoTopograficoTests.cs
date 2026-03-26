using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace TopoGente.Tests
{
    public class CalculoTopograficoTests
    {
        private readonly CalculoTopograficoService _servico;
        private readonly ITestOutputHelper _output;

        public CalculoTopograficoTests(ITestOutputHelper output)
        {
            _servico = new CalculoTopograficoService();
            _output = output;
        }

        [Fact]
        public void Azimute_Deve_Calcular_Corretamente_Transporte()
        {
            // O serviço (hoje) é uma calculadora polar "burra":
            // Az = Normalizar360(AzAnterior + AngH)
            double azimuteAnterior = 45.0;
            double anguloLido = 200.0;

            double resultado = _servico.CalcularProximoAzimute(azimuteAnterior, anguloLido);

            Assert.Equal(245.0, resultado);
        }

        [Fact]
        public void Projecao_Deve_Calcular_Seno_Cosseno()
        {
            double distancia = 100.0;
            double azimute = 90.0;

            var (dx, dy) = _servico.CalcularProjecao(distancia, azimute);

            Assert.Equal(100.0, dx, precision: 3);
            Assert.Equal(0.0, dy, precision: 3);
        }

        [Fact]
        public void Azimute_AntiHorario_Deve_Ser_Calculado_Corretamente()
        {
            // A implementação atual IGNORA 'sentido' e sempre soma.
            // Logo, mesmo passando AntiHorario, o resultado permanece (90+90)=180.
            double resultado = _servico.CalcularProximoAzimute(90, 90, SentidoAngulo.AntiHorario);

            Assert.Equal(180.0, resultado);
        }

        [Fact]
        public void Deve_Calcular_Nova_Coordenada_Corretamente()
        {
            // CalcularCoordenada recebe deltaX/deltaY (não distância/azimute).
            double xInicial = 1000.0;
            double yInicial = 1000.0;
            double distancia = 100.0;
            double azimute = 45.0;

            var (deltaX, deltaY) = _servico.CalcularProjecao(distancia, azimute);
            var (novoX, novoY) = _servico.CalcularCoordenada(xInicial, yInicial, deltaX, deltaY);

            Assert.Equal(1070.711, novoX, precision: 3);
            Assert.Equal(1070.711, novoY, precision: 3);
        }

        [Fact]
        public void Deve_Calcular_Ponto_Irradiado_Completo_Com_Objetos()
        {
            var estacaoE1 = new PontoCoordenada
            {
                Nome = "E1",
                X = 1000.0,
                Y = 1000.0,
                Z = 500.0
            };

            var leituraP1 = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "E1",
                PontoVisado = "P1",
                AnguloHorizontal = 90.0,
                AnguloVertical = 90.0,
                DistanciaInclinada = 100.0,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            double azimuteRe = 0;

            PontoCoordenada pontoCalculado = _servico.CalcularPontoIrradiado(estacaoE1, leituraP1, azimuteRe);

            Assert.Equal("P1", pontoCalculado.Nome);
            Assert.Equal(1100.0, pontoCalculado.X, precision: 3);
            Assert.Equal(1000.0, pontoCalculado.Y, precision: 3);
            Assert.Equal(500.0, pontoCalculado.Z, precision: 3);
        }

        [Fact]
        public void Deve_Calcular_Poligonal_Em_L_Corretamente()
        {
            var pontoPartida = new PontoCoordenada
            {
                Nome = "M0",
                X = 0,
                Y = 0,
                Z = 100
            };

            double azimuteInicial = 0;

            var leituras = new List<LeituraEstacaoTotal>
            {
                // Com azimuteAnterior=0, para ir ao Norte (Az=0) => AngH=0 (0+0=0)
                new LeituraEstacaoTotal
                {
                    PontoVisado = "P1",
                    AnguloHorizontal = 0.0,
                    DistanciaInclinada = 100.0,
                    AnguloVertical = 90.0,
                    AlturaInstrumento = 1.5,
                    AlturaPrisma = 1.5
                },

                // No loop, azimuteAnterior vira (azimuteAtual + 180).
                // Então aqui azimuteAnterior=180; para ir a Leste (90) => AngH=270 (180+270=450 => 90)
                new LeituraEstacaoTotal
                {
                    PontoVisado = "P2",
                    AnguloHorizontal = 270.0,
                    DistanciaInclinada = 100.0,
                    AnguloVertical = 90.0,
                    AlturaInstrumento = 1.5,
                    AlturaPrisma = 1.5
                }
            };

            var resultado = _servico.CalcularPoligonal(pontoPartida, azimuteInicial, leituras);

            Assert.Equal(3, resultado.Count);

            Assert.Equal("P1", resultado[1].Nome);
            Assert.Equal(0.0, resultado[1].X, precision: 3);
            Assert.Equal(100.0, resultado[1].Y, precision: 3);

            Assert.Equal("P2", resultado[2].Nome);
            Assert.Equal(100.0, resultado[2].X, precision: 3);
            Assert.Equal(100.0, resultado[2].Y, precision: 3);
        }

        [Fact]
        public void Deve_Processar_Orientacao_Da_Irradiacao_Por_Re_Quando_Disponivel_E_Manter_Fallback_Quando_Nao()
        {
            var classificador = new ClassificadorGrafo();
            var processador = new LevantamentoProcessor(classificador);

            var m1 = new PontoCoordenada { Nome = "M1", X = 1000, Y = 1000, Z = 100 };
            double azimuteInicial = 0; // Norte

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                PartidaX = m1.X,
                PartidaY = m1.Y,
                PartidaZ = m1.Z,
                UsarCoordenadaRe = false,
                AzimutePartida = azimuteInicial,
                NomeRe = "RE1"
            };

            var leituraIrrad = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "M1",
                PontoVisado = "P1",
                AnguloHorizontal = 90,
                AnguloVertical = 90,
                DistanciaInclinada = 100,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            var leituraRe = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "M1",
                PontoVisado = "RE1",
                AnguloHorizontal = 0,
                AnguloVertical = 90,
                DistanciaInclinada = 1,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            var estacoes = new List<Estacao>
            {
                new Estacao
                {
                    Nome = "M1",
                    Leituras = new List<LeituraEstacaoTotal> { leituraRe, leituraIrrad }
                }
            };

            var resultadoSemReConhecida = processador.Processar(metadados, estacoes, pontosConhecidos: null);
            var pSem = Assert.Single(resultadoSemReConhecida.Irradiacoes);
            Assert.Equal("P1", pSem.Nome);
            Assert.Equal(1100.0, pSem.X, precision: 3);
            Assert.Equal(1000.0, pSem.Y, precision: 3);

            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["RE1"] = new PontoCoordenada { Nome = "RE1", X = 1100, Y = 1000, Z = 100 }
            };

            var resultadoComReConhecida = processador.Processar(metadados, estacoes, pontosConhecidos);
            var pCom = Assert.Single(resultadoComReConhecida.Irradiacoes);
            Assert.Equal("P1", pCom.Nome);

            Assert.Equal(1000.0, pCom.X, precision: 3);
            Assert.Equal(900.0, pCom.Y, precision: 3);

            Assert.NotEqual(pSem.X, pCom.X);
            Assert.NotEqual(pSem.Y, pCom.Y);
        }

        [Fact]
        public void Deve_Calcular_Erro_De_Fechamento_Em_Poligonal()
        {
            var classificador = new ClassificadorGrafo();
            var processador = new LevantamentoProcessor(classificador);

            double azimuteInicial = 0; // Norte

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Fechada,
                PartidaX = 1000,
                PartidaY = 1000,
                PartidaZ = 100,
                UsarCoordenadaRe = false,
                AzimutePartida = azimuteInicial
            };

            var estacoes = new List<Estacao>
            {
                new Estacao
                {
                    Nome = "M1",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = "M1",
                            PontoVisado = "M4",
                            AnguloHorizontal = 90.0,
                            AnguloVertical = 90.0,
                            DistanciaInclinada = 1.0,
                            AlturaInstrumento = 1.5,
                            AlturaPrisma = 1.5
                        },
                        new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = "M1",
                            PontoVisado = "M2",
                            AnguloHorizontal = 180.0,
                            DistanciaInclinada = 100.0,
                            AnguloVertical = 90.0,
                            AlturaInstrumento = 1.5,
                            AlturaPrisma = 1.5
                        }
                    }
                },
                new Estacao
                {
                    Nome = "M2",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = "M2",
                            PontoVisado = "M3",
                            AnguloHorizontal = 270.0,
                            DistanciaInclinada = 100.0,
                            AnguloVertical = 90.0,
                            AlturaInstrumento = 1.5,
                            AlturaPrisma = 1.5
                        }
                    }
                },
                new Estacao
                {
                    Nome = "M3",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = "M3",
                            PontoVisado = "M4",
                            AnguloHorizontal = 270.0,
                            DistanciaInclinada = 100.0,
                            AnguloVertical = 90.0,
                            AlturaInstrumento = 1.5,
                            AlturaPrisma = 1.5
                        }
                    }
                },
                new Estacao
                {
                    Nome = "M4",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = "M4",
                            PontoVisado = "M1",
                            AnguloHorizontal = 270.01,
                            DistanciaInclinada = 100.0,
                            AnguloVertical = 90.0,
                            AlturaInstrumento = 1.5,
                            AlturaPrisma = 1.5
                        }
                    }
                }
            };

            var resultado = processador.Processar(metadados, estacoes, pontosConhecidos: null);

            _output.WriteLine($"Perímetro: {resultado.Perimetro:F3} m");
            _output.WriteLine($"Erro Linear: {resultado.ErroLinear:F4} m");
            _output.WriteLine($"Precisão: 1:{resultado.Precisao:F0}");

            Assert.Equal(400.0, resultado.Perimetro, precision: 1);
            Assert.True(resultado.ErroLinear > 0);
            Assert.True(resultado.ErroLinear < 0.20);
        }
    }
}