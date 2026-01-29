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
            // Cenário:
            // Azimute Anterior (Ré) = 45°
            // Ângulo Lido (Horário) = 200°
            // Esperado: 45 + 200 = 245. 
            // Regra: Se > 180, subtrai 180 => 245 - 180 = 65°.

            double azimuteAnterior = 45.0;
            double anguloLido = 200.0;

            double resultado = _servico.CalcularProximoAzimute(azimuteAnterior, anguloLido);

            Assert.Equal(65.0, resultado);
        }

        [Fact]
        public void Projecao_Deve_Calcular_Seno_Cosseno()
        {
            // Cenário: Andar 100m com Azimute 90° (Direção Leste exata)

            double distancia = 100.0;
            double azimute = 90.0;

            var (dx, dy) = _servico.CalcularProjecao(distancia, azimute);

            Assert.Equal(100.0, dx, precision: 3); // Tolerância de 3 casas decimais

            Assert.Equal(0.0, dy, precision: 3);
        }

        [Fact]
        public void Azimute_AntiHorario_Deve_Ser_Calculado_Corretamente()
        {
            // Cenário:
            // Estamos olhando para o Norte (Azimute 0°)
            // Viramos 90° para a ESQUERDA (Anti-horário)
            // Se Az=0 (Norte) e viramos 90 à esquerda no vértice...
            // Azimute Ré = 180 (vindo do Sul), Ângulo interno à esquerda = 90.

            double azimuteAnterior = 90; // Olhando pro Leste
            double anguloEsquerda = 90;  // Virou 90 pra esquerda

            double resultado = _servico.CalcularProximoAzimute(90, 90, SentidoAngulo.AntiHorario);

            Assert.Equal(0, resultado);
        }

        [Fact]
        public void Deve_Calcular_Nova_Coordenada_Corretamente()
        {
            // Cenário:
            // Ponto Inicial (E, N) = (1000.00, 1000.00)
            // Azimute = 45 graus
            // Distância = 100 metros

            double xInicial = 1000.0;
            double yInicial = 1000.0;
            double distancia = 100.0;
            double azimute = 45.0;

            var (novoX, novoY) = _servico.CalcularCoordenada(xInicial, yInicial, distancia, azimute);

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
                AnguloHorizontal = 90.0,     // Lemos 90 graus
                AnguloVertical = 90.0,       // 90 graus no Zênite = Horizonte (Nível)
                DistanciaInclinada = 100.0,  // Como é plano, Inclinada = Horizontal
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            // Cenário:
            // Se Ré = 0 (Norte) e li 90 (Dir), Azimute Vante = 90 (Leste).
            double azimuteRe = 0;

            PontoCoordenada pontoCalculado = _servico.CalcularPontoIrradiado(estacaoE1, leituraP1, azimuteRe);

            Assert.Equal("P1", pontoCalculado.Nome);
            Assert.Equal(1100.0, pontoCalculado.X, precision: 3);
            Assert.Equal(1000.0, pontoCalculado.Y, precision: 3);

            // Nivelado (90 graus), hi e hp iguais -> Z deve manter 500.
            Assert.Equal(500.0, pontoCalculado.Z, precision: 3);
        }

        [Fact]
        public void Deve_Calcular_Poligonal_Em_L_Corretamente()
        {
            // 1. ARRANGE
            var pontoPartida = new PontoCoordenada
            {
                Nome = "M0",
                X = 0,
                Y = 0,
                Z = 100
            };

            // Norte (0 graus)
            double azimuteInicial = 0;

            var leituras = new List<LeituraEstacaoTotal>
            {
                // Do M0 para P1. 
                // Queremos continuar indo para o Norte (Azimute 0).
                // Azimute Ant (0) + Leitura (180) - 180 = 0.
                new LeituraEstacaoTotal
                {
                    PontoVisado = "P1",
                    AnguloHorizontal = 180.0,
                    DistanciaInclinada = 100.0,
                    AnguloVertical = 90.0,
                    AlturaInstrumento = 1.5,
                    AlturaPrisma = 1.5
                },

                //  Do P1 para P2. 
                // Ir para Leste - Azimute 90
                // Azimute Ant (0) + Leitura (270) - 180 = 90.
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
            // Objetivo do teste:
            // 1) Quando existe Ré conhecida para a estação, azimuteOrientacao deve ser calculado por coordenadas (CalcularAzimutePorCoordenadas)
            //    e isso deve alterar a posição da irradiação.
            // 2) Quando NÃO existe Ré conhecida, deve cair no comportamento antigo (azimuteInicial / AzimuteChegada±180).
            //
            // Vamos usar 1 estação só (M1), com uma irradiação de 90° (para "leste" relativo à orientação).
            // - Sem ré: orientação = azimuteInicial = 0 => ponto vai para Leste (X+)
            // - Com ré conhecida ao Norte (azimute 0), continuaria igual (não prova mudança)
            // - Então usamos uma ré conhecida ao Leste => azimuteOrientacao=90, e a irradiação 90 vira azimute 180 (Sul), mudando o ponto.

            var processador = new LevantamentoProcessor();

            var m1 = new PontoCoordenada { Nome = "M1", X = 1000, Y = 1000, Z = 100 };
            double azimuteInicial = 0; // Norte

            // Irradiação: 90° a partir da orientação (DI=100, zenith=90 => DH=100)
            var leituraIrrad = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "M1",
                PontoVisado = "P1",
                Tipo = Core.Entities.TipoLeitura.Irradiacao,
                AnguloHorizontal = 90,
                AnguloVertical = 90,
                DistanciaInclinada = 100,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            // Uma leitura de Ré que aponta para "RE1" (nome do ponto de ré)
            var leituraRe = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "M1",
                PontoVisado = "RE1",
                Tipo = Core.Entities.TipoLeitura.Re,
                AnguloHorizontal = 0,
                AnguloVertical = 90,
                DistanciaInclinada = 1, // não usado na orientação por coordenadas, mas evita validator/edge-cases futuros
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            var leituras = new List<LeituraEstacaoTotal> { leituraRe, leituraIrrad };

            // Caso A: sem mapa de pontos conhecidos => fallback (orientação = azimuteInicial = 0)
            var resultadoSemReConhecida = processador.Processar(m1, azimuteInicial, leituras);
            var pSem = Assert.Single(resultadoSemReConhecida.Irradiacoes);
            Assert.Equal("P1", pSem.Nome);
            Assert.Equal(1100.0, pSem.X, precision: 3); // Leste (X+100)
            Assert.Equal(1000.0, pSem.Y, precision: 3);

            // Caso B: com mapa de pontos conhecidos contendo RE1 ao Leste da estação => orientação por coordenadas (az=90)
            // então irradiação 90° resultará em azimute 180 (Sul) => Y-100
            var pontosConhecidos = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase)
            {
                ["RE1"] = new PontoCoordenada { Nome = "RE1", X = 1100, Y = 1000, Z = 100 }
            };

            var resultadoComReConhecida = processador.Processar(m1, azimuteInicial, leituras, pontosConhecidos);
            var pCom = Assert.Single(resultadoComReConhecida.Irradiacoes);
            Assert.Equal("P1", pCom.Nome);

            Assert.Equal(1000.0, pCom.X, precision: 3);
            Assert.Equal(900.0, pCom.Y, precision: 3); // Sul (Y-100)

            // Prova de mudança: coordenadas diferem entre os casos
            Assert.NotEqual(pSem.X, pCom.X);
            Assert.NotEqual(pSem.Y, pCom.Y);
        }

        [Fact]
        public void Deve_Calcular_Erro_De_Fechamento_Em_Poligonal()
        {
            // 1. SETUP: Quadrado de 100x100m
            var processador = new LevantamentoProcessor();
            var m1 = new PontoCoordenada { Nome = "M1", X = 1000, Y = 1000, Z = 100 };
            double azimuteInicial = 0; // Norte

            var leituras = new List<LeituraEstacaoTotal>
            {
                // M1 -> M2 (Norte 100m)
                new LeituraEstacaoTotal { PontoVisado="M2", AnguloHorizontal=180, DistanciaInclinada=100, AnguloVertical=90, Tipo = Core.Entities.TipoLeitura.Poligonal},
                // M2 -> M3 (Leste 100m)
                new LeituraEstacaoTotal { PontoVisado="M3", AnguloHorizontal=270, DistanciaInclinada=100, AnguloVertical=90, Tipo = Core.Entities.TipoLeitura.Poligonal},
                // M3 -> M4 (Sul 100m)
                new LeituraEstacaoTotal { PontoVisado="M4", AnguloHorizontal=270, DistanciaInclinada=100, AnguloVertical=90, Tipo = Core.Entities.TipoLeitura.Poligonal},

                // M4 -> M1 (Oeste 100m) - AQUI VAMOS INTRODUZIR O ERRO
                // Se fosse perfeito seria 270 (curva a direita vindo do Sul).
                // Vamos colocar 270.01 (Erro angular pequeno)
                // Isso vai fazer o ponto final não cair exatamente em 1000,1000
                new LeituraEstacaoTotal {
                    PontoVisado="M1",
                    AnguloHorizontal=270.01, // Erro proposital
                    DistanciaInclinada=100,
                    AnguloVertical=90,
                    Tipo = Core.Entities.TipoLeitura.Poligonal
                }
            };

            // 2. ACT
            var resultado = processador.Processar(m1, azimuteInicial, leituras);

            // 3. ASSERT
            _output.WriteLine($"Perímetro: {resultado.Perimetro:F3} m");
            _output.WriteLine($"Erro Linear: {resultado.ErroLinear:F4} m");
            _output.WriteLine($"Precisão: 1:{resultado.Precisao:F0}");

            // O perímetro deve ser 400m
            Assert.Equal(400.0, resultado.Perimetro, precision: 1);

            // Deve haver erro (não pode ser zero)
            Assert.True(resultado.ErroLinear > 0);

            // O erro deve ser pequeno (centímetros), não metros
            Assert.True(resultado.ErroLinear < 0.20);
        }
    }
}