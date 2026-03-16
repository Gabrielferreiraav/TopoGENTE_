using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class CalculoTopograficoService
    {
        private const double ToleranciaAngularBaseGraus = 0.0; // NBR 13.133 (em graus)(sem bloqueio atualmente)

        /// <summary>
        /// Normaliza ângulo para o intervalo [0, 360).
        /// </summary>
        private static double Normalizar360(double angulo)
        {
            angulo %= 360.0;
            if (angulo < 0) angulo += 360.0;
            return angulo;
        }

        private static double NormalizarErroAngular(double erroGraus)
        {
            erroGraus %= 360.0;
            if (erroGraus <= -180.0) erroGraus += 360.0;
            if (erroGraus > 180.0) erroGraus -= 360.0;
            return erroGraus;
        }

        /// <summary>
        /// Calcula o azimute inverso (retorno) adicionando 180° e normalizando.
        /// </summary>
        private static double AzimuteInverso(double azimute)
            => Normalizar360(azimute + 180.0);

        /// <summary>
        /// Fórmula clássica de transporte de azimute (Aula06):
        ///   Az(n) = Az(n-1) + AngH(n) ± 180°
        /// Se (Az_anterior + AngH) >= 180 → subtrai 180.
        /// Se (Az_anterior + AngH) &lt; 180 → soma 180.
        /// Resultado normalizado em [0, 360).
        /// </summary>
        private static double TransportarAzimute(double azimuteAnterior, double anguloHorizontal)
        {
            double soma = azimuteAnterior + anguloHorizontal;

            return Normalizar360(soma);
        }

        /// <summary>
        /// Calcula o próximo azimute com base no anterior e no ângulo lido.
        /// Usa a fórmula clássica: Az(n) = Az(n-1) + AngH(n) ± 180°.
        /// </summary>
        public double CalcularProximoAzimute(double azimuteAnterior, double anguloHorizontal, SentidoAngulo sentido = SentidoAngulo.Horario)
        {
            return TransportarAzimute(azimuteAnterior, anguloHorizontal);
        }

        /// <summary>
        /// Calcula o Azimute de Orientação entre a Estação e um Ponto de Ré conhecido.
        /// Usa Atan2 para resolver o quadrante correto (0 a 360).
        /// </summary>
        public double CalcularAzimutePorCoordenadas(double xEstacao, double yEstacao, double xRe, double yRe)
        {
            double deltaX = xRe - xEstacao;
            double deltaY = yRe - yEstacao;

            // Math.Atan2 retorna em radianos entre -PI e +PI
            double azimuteRad = Math.Atan2(deltaX, deltaY);

            // Converter para Graus
            double azimuteGraus = azimuteRad * 180.0 / Math.PI;

            // Normalizar para 0-360
            if (azimuteGraus < 0)
            {
                azimuteGraus += 360;
            }

            return azimuteGraus;
        }
        /// <summary>
        /// Calcula as projeções (delta X e delta Y) de um lado da poligonal.
        /// </summary>
        public (double deltaX, double deltaY) CalcularProjecao(double distancia, double azimuteDecimal)
        {
            // Delta X (Leste) = Distância * Seno(Azimute)
            // Delta Y (Norte) = Distância * Cosseno(Azimute)
            double radianos = ConversorAngulos.ParaRadianos(azimuteDecimal);

            double deltaX = distancia * Math.Sin(radianos);
            double deltaY = distancia * Math.Cos(radianos);

            return (deltaX, deltaY);
        }
        /// <summary>
        /// Calcula a coordenada final a partir de um ponto inicial, distância e azimute.
        /// Retorna uma tupla (X, Y).
        /// </summary>
        public (double x, double y) CalcularCoordenada(double xAnterior, double yAnterior, double deltaX, double deltaY)
        {
            return (xAnterior + deltaX, yAnterior + deltaY);
        }
        /// <summary>
        /// Converte Distância Inclinada em Horizontal baseada no ângulo vertical (Zênite).
        /// </summary>
        public double CalcularDistanciaHorizontal(double distanciaInclinada, double anguloVerticalGraus)
        {
            // Assumindo que o aparelho lê Zênite (0° apontando para cima, 90° no horizonte).
            // DH = DI * Seno(Zenite)
            double radianos = ConversorAngulos.ParaRadianos(anguloVerticalGraus);
            return distanciaInclinada * Math.Sin(radianos);
        }
        /// <summary>
        /// DI * cos(Zenite) + Hi - Hp  calcular a diferença de nível real (Δh) entre o terreno
        /// da estação ocupada e o terreno do ponto visado.
        /// </summary>
        public double CalcularDesnivel(double distanciaInclinada, double anguloVerticalGraus, double hi, double hp)
        {
            double radianos = ConversorAngulos.ParaRadianos(anguloVerticalGraus);
            return ((distanciaInclinada * Math.Cos(radianos)) + hi - hp);
        }
        /// <summary>
        /// Calcula os erros de fechamento linear da poligonal.
        /// </summary>
        /// <param name="pontoCalculado">O último ponto calculado pela poligonal.</param>
        /// <param name="pontoConhecido">O ponto onde deveria fechar (geralmente o de partida).</param>
        /// <param name="perimetroTotal">Soma das distâncias horizontais da poligonal.</param>
        public (double erroX, double erroY, double erroLinearTotal, double precisaoRelativa)
            CalcularErroFechamento(PontoCoordenada pontoCalculado, PontoCoordenada pontoConhecido, double perimetroTotal)
        {
            // Diferença entre onde cheguei e onde deveria chegar
            double erroX = pontoCalculado.X - pontoConhecido.X;
            double erroY = pontoCalculado.Y - pontoConhecido.Y;

            // Teorema de Pitágoras para achar o erro linear total
            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            // Precisão Relativa (1:M)
            double precisaoRelativa = 0;
            if (erroLinearTotal > 0.0001) // Evitar divisão por zero
            {
                precisaoRelativa = erroLinearTotal / perimetroTotal;
            }

            return (erroX, erroY, erroLinearTotal, precisaoRelativa);
        }
        /// <summary>
        /// Recebe os objetos e devolve um novo Ponto calculado (Irradiação).
        /// Fórmula Z (): Z_vante = Z_estação + (DI×cos(Zênite)) + Hi − Hp
        /// </summary>
        public PontoCoordenada CalcularPontoIrradiado(PontoCoordenada estacao, LeituraEstacaoTotal leitura, double azimuteRe)
        {
            // Para irradiação: AngH é referenciado à Ré (Ré = 0°), então
            // Az(Vante) = Az(Estação→Ré) + AngH, normalizado em [0,360).
            double azimuteVante = Normalizar360(azimuteRe + leitura.AnguloHorizontal);

            double distHorizontal = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);

            // Calcular as Coordenadas
            var (deltaX, deltaY) = CalcularProjecao(distHorizontal, azimuteVante);

            var (novoX, novoY) = CalcularCoordenada(estacao.X, estacao.Y, deltaX, deltaY);

            // Calcular Cota (Z) - Nivelamento Trigonométrico
            // Z_vante = Z_estação + DN + Hi - Hp
            // DN (Desnível) = DI × cos(Zênite)
            double desnivel = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

            double novoZ = estacao.Z + desnivel;

            System.Diagnostics.Debug.WriteLine(
                    $" PONTO IRRADIADO {leitura.PontoVisado} | X={novoX:F3} | Y={novoY:F3} | Z={novoZ:F3} | AzVAnte={azimuteVante:F4}| AzRe={azimuteRe:F4}°");

            return new PontoCoordenada
            {
                Nome = leitura.PontoVisado,
                X = novoX,
                Y = novoY,
                Z = novoZ,
                EhPontoPoligonal = false
            };
        }
        /// <summary>
        /// Calcula a poligonal bruta que é a base obrigatória para o cálculo de erros de fechamento nos cenários 1 e 2, ou o resultado final definitivo no cenário 3.

        /// </summary>
        /// <param name="pontoPartida">Coordenada do primeiro ponto .</param>
        /// <param name="azimuteInicial">Azimute do alinhamento de partida (Estação → Ré).</param>
        /// <param name="leituras">Lista ordenada de leituras da poligonal.</param>
        /// <returns>Lista contendo o ponto de partida e todos os pontos calculados.</returns>
        public List<PontoCoordenada> CalcularPoligonal(PontoCoordenada pontoPartida, double azimuteInicial, List<LeituraEstacaoTotal> leituras)
        {
            var pontosCalculados = new List<PontoCoordenada>();

            pontoPartida.AzimuteChegada = azimuteInicial;
            pontosCalculados.Add(pontoPartida);

            if (leituras == null || leituras.Count == 0)
            {
                return pontosCalculados;
            }

            // Propagar azimutes brutos para obter erro angular (ea)
            PontoCoordenada estacaoAtual = pontoPartida;
            double azimuteAnterior = azimuteInicial;

            for (int i = 0; i < leituras.Count; i++)
            {
                var leitura = leituras[i];

                //Reducao das observacoes
                double distanciaHorizontal = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double desnivel = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                // Az(n) = Az(n-1) + AngH(n) ± 180°
                double azimuteAtual = Normalizar360(azimuteAnterior + leitura.AnguloHorizontal);

                var (deltaX, deltaY) = CalcularProjecao(distanciaHorizontal, azimuteAtual);

                //Acumulo de coordenadas brutas
                double novoX = estacaoAtual.X + deltaX;
                double novoY = estacaoAtual.Y + deltaY;
                double novoZ = estacaoAtual.Z + desnivel;


                var novoPonto = new PontoCoordenada
                {
                    Nome = leitura.PontoVisado,
                    X = novoX,
                    Y = novoY,
                    Z = novoZ,
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimuteAtual
                };

                pontosCalculados.Add(novoPonto);

                System.Diagnostics.Debug.WriteLine($"Estação {i + 1}: Az_anterior={azimuteAnterior:F4}° + AngH={leitura.AnguloHorizontal:F4}° → Az_atual={azimuteAtual:F4}°");

                estacaoAtual = novoPonto;
                azimuteAnterior = Normalizar360(azimuteAtual + 180); ;
            }

            return pontosCalculados;
        }

        public List<PontoCoordenada> CompensarPoligonal(
            PontoCoordenada pontoPartida, PontoCoordenada pontoChegada,
            double azimuteInicial, double? azimuteChegada,
            List<LeituraEstacaoTotal> leituras, List<PontoCoordenada> poligonalBruta,
            TipoCenarioPoligonal tipoCenario, double anguloFechamento, out double erroAngular, out double erroX, out double erroY,
            out double erroLinearTotal, out double precisaoRelativa, out double erroAltimetrico)
        {
            erroAngular = 0; erroX = 0; erroY = 0; erroAngular = 0; erroLinearTotal = 0; precisaoRelativa = 0; erroAltimetrico = 0;

            if (leituras == null || leituras.Count == 0)
            {
                return new List<PontoCoordenada> { pontoPartida };
            }

            int nEstacoes = leituras.Count;



            if (tipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                // Compensacao Angular
                double azimuteCalculadoFinal = poligonalBruta[^1].AzimuteChegada;

                double azimuteReRetorno = Normalizar360(azimuteCalculadoFinal + 180);

                double azimuteCalculadoChegada = Normalizar360(azimuteReRetorno + anguloFechamento);

                erroAngular = NormalizarErroAngular(azimuteCalculadoChegada - azimuteChegada.Value);

            }
            else if (tipoCenario == TipoCenarioPoligonal.Fechada)
            {

                double azimuteCalculadoFinal = poligonalBruta[^1].AzimuteChegada;
                double azimuteReRetorno = Normalizar360(azimuteCalculadoFinal + 180);
                double azimuteCalculadoRetorno = Normalizar360(azimuteReRetorno + anguloFechamento);

                erroAngular = NormalizarErroAngular(azimuteCalculadoRetorno - azimuteInicial);

            }

            // Verificacao de tolerancia ainda nao implementada 
            // Tolerancia_Angular = (3 * precisao_equipamento * sqrt(n_estacoes)) + 10
            double precisaoEquipamentoSegundos = 5.0;
            double toleranciaSegundos = (3 * precisaoEquipamentoSegundos * Math.Sqrt(nEstacoes)) + 10;
            double toleranciaGraus = toleranciaSegundos / 3600.0;

            if (Math.Abs(erroAngular)>toleranciaGraus)
            {
                System.Diagnostics.Debug.WriteLine($"[FALHA] Erro Angular ({erroAngular:F4}°) superou a tolerância ({toleranciaGraus:F4}°). Compensação abortada.");

                // Retorna os erros brutos para a tela, mas devolve a malha torta (sem Bowditch)
                erroX =  poligonalBruta[^1].X - pontoChegada.X; // Ajuste simplificado pro log
                erroY =  poligonalBruta[^1].Y - pontoChegada.Y; // Ajuste simplificado pro log
                erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));
                precisaoRelativa = 0;
                
                return poligonalBruta;
            }

            // Correcao Linear
            double correcaoAngularUnitaria = -erroAngular / nEstacoes;

            // Aplicar azimutes compensados
            // Azimute_Compensado_J = Azimute_Calculado_J + (J * corr_ang)
            var azimutesCompensados = new double[nEstacoes];
            for (int i = 0; i < nEstacoes; i++)
            {
                double azimuteCalculado = poligonalBruta[i + 1].AzimuteChegada;
                azimutesCompensados[i] = Normalizar360(azimuteCalculado + ((i + 1) * correcaoAngularUnitaria));
            }

            // Recalcular as projecoes parciais
            var deltaX = new double[nEstacoes];
            var deltaY = new double[nEstacoes];
            var distanciasHorizontais = new double[nEstacoes];
            var desniveis = new double[nEstacoes];

            double perimetroTotal = 0;

            for (int i = 0; i < nEstacoes; i++)
            {
                var leitura = leituras[i];

                double dh = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                // Dn_Calculado = DI * cos(Zênite), Hi e hP ja estão incluidos
                double dn = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                distanciasHorizontais[i] = dh;
                desniveis[i] = dn;
                perimetroTotal += dh;

                var (dx, dy) = CalcularProjecao(dh, azimutesCompensados[i]);
                deltaX[i] = dx; deltaY[i] = dy;
            }

            // Compensacao Linear (Bowditch)
            double somaDeltasX = 0; double somaDeltasY = 0;

            for (int j = 0; j < nEstacoes; j++)
            {
                somaDeltasX += deltaX[j];
                somaDeltasY += deltaY[j];
            }

            if (tipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                erroX = (somaDeltasX + pontoPartida.X) - pontoChegada.X;
                erroY = (somaDeltasY + pontoPartida.Y) - pontoChegada.Y;
            }
            else if (tipoCenario == TipoCenarioPoligonal.Fechada)
            {
                erroX = somaDeltasX;
                erroY = somaDeltasY;
            }

            // Erro linear total e precisão relativa
            erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            if (perimetroTotal > 0.0001)
            {
                precisaoRelativa = erroLinearTotal / perimetroTotal;
            }

            double precisaoMinima = 1.0 / 12000.0; // 1 : 12000
            if (precisaoRelativa > precisaoMinima)
            {
                System.Diagnostics.Debug.WriteLine($"[FALHA] Precisão Linear (1:{(1 / precisaoRelativa):F0}) inferior ao exigido (1:12000). Compensação abortada.");
                return poligonalBruta;
            }

            // Coeficientes de Correcao (Bowditch)
            double coefX = perimetroTotal > 0 ? -erroX / perimetroTotal : 0;
            double coefY = perimetroTotal > 0 ? -erroY / perimetroTotal : 0;

            // Aplicacao dos coeficientes de correcao para obter coordenadas compensadas
            var deltasXCompensados = new double[nEstacoes];
            var deltasYCompensados = new double[nEstacoes];

            for (int j = 0; j < nEstacoes; j++)
            {
                deltasXCompensados[j] = deltaX[j] + (coefX * distanciasHorizontais[j]);
                deltasYCompensados[j] = deltaY[j] + (coefY * distanciasHorizontais[j]);
            }

            // Calculo das coordenadas Finais Compensadas
            var poligonalCompensada = new List<PontoCoordenada>();

            poligonalCompensada.Add(new PontoCoordenada
            {
                Nome = pontoPartida.Nome,
                X = pontoPartida.X,
                Y = pontoPartida.Y,
                Z = pontoPartida.Z,
                EhPontoPoligonal = true,
                AzimuteChegada = azimuteInicial
            });

            double xAtual = pontoPartida.X;
            double yAtual = pontoPartida.Y;

            for (int j = 0; j < nEstacoes; j++)
            {
                xAtual += deltasXCompensados[j];
                yAtual += deltasYCompensados[j];

                var novoPonto = new PontoCoordenada
                {
                    Nome = leituras[j].PontoVisado,
                    X = xAtual,
                    Y = yAtual,
                    Z = 0, // Cota compensada ainda não calculada
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimutesCompensados[j]
                };

                poligonalCompensada.Add(novoPonto);
            }


            //Calculo do Z altimétrico compensado
            double somaDn = 0;
            for (int j = 0; j < nEstacoes; j++)
            {
                somaDn += desniveis[j];
            }

            if (tipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                erroAltimetrico = (pontoPartida.Z + somaDn) - pontoChegada.Z;
            }
            else if (tipoCenario == TipoCenarioPoligonal.Fechada)
            {
                erroAltimetrico = somaDn;
            }

            // Tolerancia Altimetrica ainda nao aplicada
            // Tolerancia = Constante * sqrt(Perimetro_em_KM)

            // Correcao Z = - erroAltimetrico  / n
            double corrZ = -erroAltimetrico / nEstacoes;

            // Para cada estação: DN_Corrigido = DN_Calculado + CorrZ
            // Z_Novo = Z_Anterior + DN_Corrigido
            double zAtual = pontoPartida.Z;

            for (int i = 0; i < nEstacoes; i++)
            {
                double dnCorrigido = desniveis[i] + corrZ;

                zAtual += dnCorrigido;

                poligonalCompensada[i + 1].Z = zAtual; // i+1,primeiro ponto é o de partida
            }

            return poligonalCompensada;
        }


    }
}
