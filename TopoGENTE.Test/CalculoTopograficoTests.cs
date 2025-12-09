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
    public void Deve_Processar_Levantamento_Misto_Com_GMS_E_Gerar_Relatorio()
    {

        // Ponto de Partida (Marco M1)
        var estacaoAtual = new PontoCoordenada { Nome = "M1", X = 5000, Y = 10000, Z = 100 };
        double azimuteOrientacao = 0; // Zerei o aparelho no Norte

        _output.WriteLine($"INÍCIO: Estação {estacaoAtual.Nome} | X={estacaoAtual.X} Y={estacaoAtual.Y}");

        var cadernetaCampo = new List<(TipoLeitura Tipo, string Nome, int G, int M, double S, double Dist)>
            {
                // ESTAÇÃO M1

                // Pontos Irradiados (Poste, Árvore, Cerca)
                (TipoLeitura.Irradiacao, "P1_Poste",  45,  0, 0, 15.50), // 45° à direita
                (TipoLeitura.Irradiacao, "P2_Arvore", 315, 30, 0, 22.10), // 45° à esquerda (360-45)
                
                // Vante para mudar de estação (M1 -> M2) - Anda 100m a 90° (Leste)
                (TipoLeitura.Poligonal,  "M2",        90,  0, 0, 100.00),


                // assumir que o azimute transportado foi mantido.
                // Irradiações a partir de M2
                (TipoLeitura.Irradiacao, "P3_Bueiro", 0,   0, 0,  10.00), // Olhando para frente

                (TipoLeitura.Irradiacao, "P4_Casa",   180, 0, 0,  50.00), // Sul

                // Vante para fechar ou avançar (M2 -> M3) - Anda 100m a 180° (Sul)
                (TipoLeitura.Poligonal,  "M3",        180, 0, 0, 100.00),

                // ESTAÇÃO M3
                // Irradiação
                (TipoLeitura.Irradiacao, "P5_Muro",   270, 0, 0,  12.00), // Oeste

                // Vante para Fechar no M4 (que seria Oeste de M3)
                (TipoLeitura.Poligonal,  "M4",        270, 0, 0, 100.00),
                
                    // Vante para Fechar no INÍCIO (M4 -> M1)
                (TipoLeitura.Poligonal,  "M1_Fech",   0,   0, 0, 100.00),
            };


        var listaPontosCalculados = new List<PontoCoordenada>();
        listaPontosCalculados.Add(estacaoAtual); // Adiciona o M1

        double azimuteAnterior = azimuteOrientacao; // 0

        foreach (var linha in cadernetaCampo)
        {
            // Converter GMS para Decimal usando seu Utilitário
            double anguloDecimal = TopoGente.Core.Utilities.ConversorAngulos.ParaDecimal(linha.G, linha.M, linha.S);

            // Criar o objeto de leitura
            var leitura = new LeituraEstacaoTotal
            {
                PontoVisado = linha.Nome,
                AnguloHorizontal = anguloDecimal,
                DistanciaInclinada = linha.Dist,
                AnguloVertical = 90,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.5
            };

            PontoCoordenada pontoCalculado;

            if (linha.Tipo == TipoLeitura.Irradiacao)
            {
                pontoCalculado = _servico.CalcularPontoIrradiado(estacaoAtual, leitura, azimuteAnterior);

                _output.WriteLine($"[IRRAD] {pontoCalculado.Nome}: X={pontoCalculado.X:F3} Y={pontoCalculado.Y:F3}");
            }
            else // Poligonal
            {
                double azimuteVante = leitura.AnguloHorizontal;

                // Calcula Coord
                double distHz = _servico.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, 90);
                var (nx, ny) = _servico.CalcularCoordenada(estacaoAtual.X, estacaoAtual.Y, distHz, azimuteVante);

                pontoCalculado = new PontoCoordenada
                {
                    Nome = linha.Nome,
                    X = nx,
                    Y = ny,
                    Z = 100,
                    EhPontoPoligonal = true
                };

                _output.WriteLine($"[VANTE] {pontoCalculado.Nome}: X={pontoCalculado.X:F3} Y={pontoCalculado.Y:F3} (Nova Estação)");

                // Atualiza o estado para a próxima iteração
                estacaoAtual = pontoCalculado;
                azimuteAnterior = azimuteVante;
            }

            listaPontosCalculados.Add(pontoCalculado);
        }


        // Verificar o fechamento da poligonal (M1 -> M2 -> M3 -> M4 -> M1)
        // M1 original: 5000, 10000
        var pontoFechamento = listaPontosCalculados.Last();

        Assert.Equal("M1_Fech", pontoFechamento.Nome);

        // Quadrado de 100x100.
        Assert.Equal(5000.0, pontoFechamento.X, precision: 1); // Precisão de decímetro para erros de double
        Assert.Equal(10000.0, pontoFechamento.Y, precision: 1);
    }

    // Enum auxiliar apenas para este teste organizar a "Caderneta"
    private enum TipoLeitura { Poligonal, Irradiacao }

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
                new LeituraEstacaoTotal { PontoVisado="M2", AnguloHorizontal=180, DistanciaInclinada=100, AnguloVertical=90, EhLeituraDePoligonal=true },
                // M2 -> M3 (Leste 100m)
                new LeituraEstacaoTotal { PontoVisado="M3", AnguloHorizontal=270, DistanciaInclinada=100, AnguloVertical=90, EhLeituraDePoligonal=true },
                // M3 -> M4 (Sul 100m)
                new LeituraEstacaoTotal { PontoVisado="M4", AnguloHorizontal=270, DistanciaInclinada=100, AnguloVertical=90, EhLeituraDePoligonal=true },
                
                // M4 -> M1 (Oeste 100m) - AQUI VAMOS INTRODUZIR O ERRO
                // Se fosse perfeito seria 270 (curva a direita vindo do Sul).
                // Vamos colocar 270.01 (Erro angular pequeno)
                // Isso vai fazer o ponto final não cair exatamente em 1000,1000
                new LeituraEstacaoTotal {
                    PontoVisado="M1_Fech",
                    AnguloHorizontal=270.01, // Erro proposital
                    DistanciaInclinada=100,
                    AnguloVertical=90,
                    EhLeituraDePoligonal=true
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