using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class CalculoTopograficoService
    {
        /// <summary>
        /// Calcula o próximo azimute com base no anterior e no ângulo lido.
        /// </summary>
        public double CalcularProximoAzimute(double azimuteAnterior, double anguloHorizontal, SentidoAngulo sentido = SentidoAngulo.Horario)
        {
            // Az(n) = Az(n-1) + Angulo(n) - 180°

            double novoAzimute = azimuteAnterior + anguloHorizontal;

            // Se a soma for maior que 180, subtraímos 180.
            // Se for menor, somamos 180.
            if (novoAzimute >= 180)
            {
                novoAzimute -= 180;
            }
            else
            {
                novoAzimute += 180;
            }

            // Normaliza para ficar sempre entre 0 e 360
            novoAzimute = novoAzimute % 360;
            if (novoAzimute < 0) novoAzimute += 360;

            return novoAzimute;
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
        public (double x, double y) CalcularCoordenada(double xAnterior, double yAnterior, double distancia, double azimute)
        {
            var (deltaX, deltaY) = CalcularProjecao(distancia, azimute);

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
        /// Calculo para o Desnível.
        /// DN = DI * Cos(Zenite)
        /// </summary>
        public double CalcularDesnivel(double distanciaInclinada, double anguloVerticalGraus)
        {
            double radianos = ConversorAngulos.ParaRadianos(anguloVerticalGraus);
            return distanciaInclinada * Math.Cos(radianos);
        }
        /// <summary>
        /// Soma um ângulo ao azimute base, apenas normalizando para 0-360.
        /// Usado para Irradiação onde já temos o Azimute da Linha de Ré.
        /// </summary>
        private double SomarAngulos(double azimuteBase, double anguloLido)
        {
            double soma = azimuteBase + anguloLido;
            soma = soma % 360;
            if (soma < 0) soma += 360;
            return soma;
        }
        /// <summary>
        /// Calcula a correção de Curvatura e Refração para distâncias longas.
        /// </summary>
        public double CalcularCorrecaoCurvaturaRefracao(double distHorizontal)
        {
            if (distHorizontal <= 500)
            {
                return 0;
            }

            double k = 0.13; // Coeficiente de refração padrão 
            double R = 6370000; // Raio da Terra em metros

            // (1-k) * DH² / 2R
            double correcao = ((1 - k) * Math.Pow(distHorizontal, 2)) / (2 * R);

            return correcao;
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
                precisaoRelativa = perimetroTotal / erroLinearTotal;
            }

            return (erroX, erroY, erroLinearTotal, precisaoRelativa);
        }
        /// <summary>
        /// Recebe os objetos e devolve um novo Ponto calculado.
        /// </summary>
        public PontoCoordenada CalcularPontoIrradiado(PontoCoordenada estacao, LeituraEstacaoTotal leitura, double azimuteRe)
        {
            double azimuteVante = SomarAngulos(azimuteRe, leitura.AnguloHorizontal);

            double distHorizontal = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);

            // Calcular as Coordenadas
            var (novoX, novoY) = CalcularCoordenada(estacao.X, estacao.Y, distHorizontal, azimuteVante);

            // Calcular Cota (Z) - Nivelamento Trigonométrico
            // Z_novo = Z_ant + hi + dn - hp
            // dn (Desnível) = DI * Cosseno(Zenite)
            double desnivel = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical);
            double correcaoCR = CalcularCorrecaoCurvaturaRefracao(distHorizontal);

            double novoZ = estacao.Z + leitura.AlturaInstrumento + desnivel + correcaoCR - leitura.AlturaPrisma;

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
        /// Calcula uma sequência de pontos.
        /// </summary>
        /// <param name="pontoPartida">Coordenada do primeiro ponto (ex: Marcos Geodésico).</param>
        /// <param name="azimuteInicial">Azimute de partida (Orientação inicial).</param>
        /// <param name="leituras">Lista ordenada da poligonal.</param>
        /// <returns>Lista contendo o ponto de partida e todos os pontos calculados.</returns>
        public List<PontoCoordenada> CalcularPoligonal(PontoCoordenada pontoPartida, double azimuteInicial, List<LeituraEstacaoTotal> leituras)
        {
            var pontosCalculados = new List<PontoCoordenada>();

            // O ponto de partida via azimute inicial (ou é a referência dele)
            pontoPartida.AzimuteChegada = azimuteInicial;
            pontosCalculados.Add(pontoPartida);

            PontoCoordenada estacaoAtual = pontoPartida;
            double azimuteAnterior = azimuteInicial;

            foreach (var leitura in leituras)
            {
                // Calcular Azimute
                double azimuteVante = CalcularProximoAzimute(azimuteAnterior, leitura.AnguloHorizontal);

                // Cálculos geométricos
                double distHorizontal = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double desnivel = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double correcaoCR = CalcularCorrecaoCurvaturaRefracao(distHorizontal);
                var (novoX, novoY) = CalcularCoordenada(estacaoAtual.X, estacaoAtual.Y, distHorizontal, azimuteVante);
                double novoZ = estacaoAtual.Z + leitura.AlturaInstrumento + desnivel + correcaoCR - leitura.AlturaPrisma;

                // Criar Ponto (AZIMUTE CHEGADA)
                var novoPonto = new PontoCoordenada
                {
                    Nome = leitura.PontoVisado,
                    X = novoX,
                    Y = novoY,
                    Z = novoZ,
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimuteVante 
                };

                pontosCalculados.Add(novoPonto);

                // Atualiza loop
                estacaoAtual = novoPonto;
                azimuteAnterior = azimuteVante;
            }

            return pontosCalculados;
        }

        /// <summary>
        /// Aplica a Compensação de Bowditch na poligonal. (Bowditch)
        /// </summary>
        public List<PontoCoordenada> CompensarPoligonal(List<PontoCoordenada> poligonalOriginal, double erroX, double erroY, double perimetroTotal)
        {
            if (perimetroTotal == 0 || (Math.Abs(erroX) < 0.0001 && Math.Abs(erroY) < 0.0001))
            {
                return new List<PontoCoordenada>(poligonalOriginal);
            }

            var poligonalAjustada = new List<PontoCoordenada>();

            // O primeiro ponto (M1) é fixo
            var pInicial = poligonalOriginal[0];
            poligonalAjustada.Add(new PontoCoordenada
            {
                Nome = pInicial.Nome,
                X = pInicial.X,
                Y = pInicial.Y,
                Z = pInicial.Z,
                EhPontoPoligonal = true,
                AzimuteChegada = pInicial.AzimuteChegada
            });

            // Acumuladores de correção
            double correcaoAcumuladaX = 0;
            double correcaoAcumuladaY = 0;

            //  ajustamos até o fim para garantir que o último ponto fique igual ao primeiro matematicamente.

            for (int i = 1; i < poligonalOriginal.Count; i++)
            {
                var pAnterior = poligonalOriginal[i - 1];
                var pAtual = poligonalOriginal[i];

                // d = sqrt((X2-X1)² + (Y2-Y1)²)
                double dx = pAtual.X - pAnterior.X;
                double dy = pAtual.Y - pAnterior.Y;
                double distPerna = Math.Sqrt(dx * dx + dy * dy);

                // Correção Unitária para esta perna
                // cx = -ErroTotalX * (dist / Perimetro)
                double cx = -erroX * (distPerna / perimetroTotal);
                double cy = -erroY * (distPerna / perimetroTotal);

                correcaoAcumuladaX += cx;
                correcaoAcumuladaY += cy;

                //  ponto ajustado
                var novoPonto = new PontoCoordenada
                {
                    Nome = pAtual.Nome,
                    X = pAtual.X + correcaoAcumuladaX, 
                    Y = pAtual.Y + correcaoAcumuladaY,
                    Z = pAtual.Z,  // mantemos Z original.
                    EhPontoPoligonal = true,
                    AzimuteChegada = pAtual.AzimuteChegada
                };

                poligonalAjustada.Add(novoPonto);
            }

            return poligonalAjustada;
        }
    }
}
