using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Strategies
{
    public sealed class CompensacaoFechadaStrategy : ICompensacaoPoligonalStrategy
    {
        private const double PrecisaoEquipamentoSegundos = 5.0;
        private const double AngularEpsilonGraus = 1e-10;
        private const double PrecisaoRelativaEpsilon = 1e-12;

        public ResultadoCompensacaoDTO Compensar(CompensacaoPoligonalInputDTO entrada)
        {
            if (entrada.Leituras == null || entrada.Leituras.Count == 0)
            {
                return new ResultadoCompensacaoDTO
                {
                    AprovadoNorma = false,
                    AlertaReprovacao = "Nenhuma leitura fornecida. Compensação não realizada.",
                    PoligonalCompensada = new List<PontoCoordenada> { entrada.PontoPartida }
                };
            }

            int nEstacoes = entrada.Leituras.Count;
            double erroAngular = CalcularErroAngularFechada(entrada);
            double toleranciaGraus = CalcularToleranciaAngularGraus(nEstacoes);

            if (Ultrapassa(Math.Abs(erroAngular), toleranciaGraus, AngularEpsilonGraus))
            {
                double erroXAngular = entrada.PoligonalBruta[^1].X - entrada.PontoChegada.X;
                double erroYAngular = entrada.PoligonalBruta[^1].Y - entrada.PontoChegada.Y;
                double erroLinearAngular = Math.Sqrt((erroXAngular * erroXAngular) + (erroYAngular * erroYAngular));

                return new ResultadoCompensacaoDTO
                {
                    ErroAngular = erroAngular,
                    ErroX = erroXAngular,
                    ErroY = erroYAngular,
                    ErroLinearTotal = erroLinearAngular,
                    PrecisaoRelativa = 0,
                    ErroAltimetrico = 0,
                    AprovadoNorma = false,
                    AlertaReprovacao = $"Erro Angular ({erroAngular:F4}°) superou a tolerância da NBR 13.133 ({toleranciaGraus:F4}°).",
                    PoligonalCompensada = entrada.PoligonalBruta
                };
            }

            double[] azimutesCompensados = CalcularAzimutesCompensados(entrada.PoligonalBruta, erroAngular, nEstacoes);
            var elementos = CalcularElementos(entrada.Leituras, azimutesCompensados);

            double erroX = Somar(elementos.DeltaX);
            double erroY = Somar(elementos.DeltaY);
            double erroAltimetrico = elementos.SomaDesnivel;
            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));
            double precisaoRelativa = elementos.PerimetroTotal > 0.0001 ? erroLinearTotal / elementos.PerimetroTotal : 0;

            const double precisaoMinima = 1.0 / 12000.0;
            if (Ultrapassa(precisaoRelativa, precisaoMinima, PrecisaoRelativaEpsilon))
            {
                return ReprovarPorPrecisaoLinear(erroAngular, erroX, erroY, erroLinearTotal, precisaoRelativa, erroAltimetrico, entrada.PoligonalBruta);
            }

            double toleranciaAltimetrica = CalcularToleranciaAltimetrica(elementos.PerimetroTotal);
            if (Ultrapassa(Math.Abs(erroAltimetrico), toleranciaAltimetrica, PrecisaoRelativaEpsilon))
            {
                return new ResultadoCompensacaoDTO
                {
                    ErroAngular = erroAngular,
                    ErroX = erroX,
                    ErroY = erroY,
                    ErroLinearTotal = erroLinearTotal,
                    PrecisaoRelativa = precisaoRelativa,
                    ErroAltimetrico = erroAltimetrico,
                    AprovadoNorma = false,
                    AlertaReprovacao = $"Erro Altimétrico ({Math.Abs(erroAltimetrico):F4} m) superou a tolerância da NBR 13.133 ({toleranciaAltimetrica:F4} m).",
                    PoligonalCompensada = entrada.PoligonalBruta
                };
            }

            var poligonalCompensada = ConstruirPoligonalCompensada(entrada, azimutesCompensados, elementos, erroX, erroY, erroAltimetrico);

            return new ResultadoCompensacaoDTO
            {
                ErroAngular = erroAngular,
                ErroX = erroX,
                ErroY = erroY,
                ErroLinearTotal = erroLinearTotal,
                PrecisaoRelativa = precisaoRelativa,
                ErroAltimetrico = erroAltimetrico,
                AprovadoNorma = true,
                AlertaReprovacao = string.Empty,
                PoligonalCompensada = poligonalCompensada
            };
        }

        private static double CalcularErroAngularFechada(CompensacaoPoligonalInputDTO entrada)
        {
            double azimuteCalculadoFinal = entrada.PoligonalBruta[^1].AzimuteChegada;
            double azimuteReRetorno = GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180);
            double azimuteCalculadoRetorno = GeometriaTopograficaHelper.Normalizar360(azimuteReRetorno + entrada.AnguloFechamento);
            return GeometriaTopograficaHelper.NormalizarErroAngular(azimuteCalculadoRetorno - entrada.AzimuteInicial);
        }

        private static double[] CalcularAzimutesCompensados(List<PontoCoordenada> poligonalBruta, double erroAngular, int nEstacoes)
        {
            double correcaoAngularUnitaria = -erroAngular / nEstacoes;
            var azimutesCompensados = new double[nEstacoes];

            for (int i = 0; i < nEstacoes; i++)
            {
                double azimuteCalculado = poligonalBruta[i + 1].AzimuteChegada;
                azimutesCompensados[i] = GeometriaTopograficaHelper.Normalizar360(azimuteCalculado + ((i + 1) * correcaoAngularUnitaria));
            }

            return azimutesCompensados;
        }

        private static ElementosPoligonal CalcularElementos(List<LeituraEstacaoTotal> leituras, double[] azimutesCompensados)
        {
            int nEstacoes = leituras.Count;
            var deltaX = new double[nEstacoes];
            var deltaY = new double[nEstacoes];
            var distanciasHorizontais = new double[nEstacoes];
            var desniveis = new double[nEstacoes];
            double perimetroTotal = 0;
            double somaDesnivel = 0;

            for (int i = 0; i < nEstacoes; i++)
            {
                var leitura = leituras[i];
                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                distanciasHorizontais[i] = dh;
                desniveis[i] = dn;
                perimetroTotal += dh;
                somaDesnivel += dn;

                var (dx, dy) = GeometriaTopograficaHelper.CalcularProjecao(dh, azimutesCompensados[i]);
                deltaX[i] = dx;
                deltaY[i] = dy;
            }

            return new ElementosPoligonal(deltaX, deltaY, distanciasHorizontais, desniveis, perimetroTotal, somaDesnivel);
        }

        private static List<PontoCoordenada> ConstruirPoligonalCompensada(
            CompensacaoPoligonalInputDTO entrada,
            double[] azimutesCompensados,
            ElementosPoligonal elementos,
            double erroX,
            double erroY,
            double erroAltimetrico)
        {
            var poligonalCompensada = new List<PontoCoordenada>
            {
                new PontoCoordenada
                {
                    Nome = entrada.PontoPartida.Nome,
                    X = entrada.PontoPartida.X,
                    Y = entrada.PontoPartida.Y,
                    Z = entrada.PontoPartida.Z,
                    EhPontoPoligonal = true,
                    AzimuteChegada = entrada.AzimuteInicial
                }
            };

            double xAtual = entrada.PontoPartida.X;
            double yAtual = entrada.PontoPartida.Y;
            double zAtual = entrada.PontoPartida.Z;

            for (int j = 0; j < entrada.Leituras.Count; j++)
            {
                var (dxCompensado, dyCompensado) = CompensarPlanimetriaBowditch(
                    elementos.DeltaX[j],
                    elementos.DeltaY[j],
                    elementos.DistanciasHorizontais[j],
                    erroX,
                    erroY,
                    elementos.PerimetroTotal);

                double dzCompensado = CompensarAltimetriaSimples(
                    elementos.Desniveis[j],
                    erroAltimetrico,
                    entrada.Leituras.Count);

                xAtual += dxCompensado;
                yAtual += dyCompensado;
                zAtual += dzCompensado;

                poligonalCompensada.Add(new PontoCoordenada
                {
                    Nome = entrada.Leituras[j].PontoVisado,
                    X = xAtual,
                    Y = yAtual,
                    Z = zAtual,
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimutesCompensados[j]
                });
            }

            return poligonalCompensada;
        }

        private static (double dxCompensado, double dyCompensado) CompensarPlanimetriaBowditch(
            double deltaX,
            double deltaY,
            double distanciaHorizontal,
            double erroX,
            double erroY,
            double perimetroTotal)
        {
            if (perimetroTotal <= 0)
            {
                return (deltaX, deltaY);
            }

            double fator = distanciaHorizontal / perimetroTotal;
            return (deltaX - (erroX * fator), deltaY - (erroY * fator));
        }

        private static double CompensarAltimetriaSimples(double desnivel, double erroAltimetrico, int nEstacoes)
            => desnivel - (erroAltimetrico / nEstacoes);

        private static ResultadoCompensacaoDTO ReprovarPorPrecisaoLinear(
            double erroAngular,
            double erroX,
            double erroY,
            double erroLinearTotal,
            double precisaoRelativa,
            double erroAltimetrico,
            List<PontoCoordenada> poligonalBruta)
        {
            double denominadorPrecisao = precisaoRelativa > 0 ? (1.0 / precisaoRelativa) : 0.0;
            double denominadorArredondado = Math.Round(denominadorPrecisao, 0, MidpointRounding.AwayFromZero);
            return new ResultadoCompensacaoDTO
            {
                ErroAngular = erroAngular,
                ErroX = erroX,
                ErroY = erroY,
                ErroLinearTotal = erroLinearTotal,
                PrecisaoRelativa = precisaoRelativa,
                ErroAltimetrico = erroAltimetrico,
                AprovadoNorma = false,
                AlertaReprovacao = $"Precisão Linear (1:{denominadorArredondado}) inferior ao exigido (1:12000).",
                PoligonalCompensada = poligonalBruta
            };
        }

        private static double CalcularToleranciaAngularGraus(int nEstacoes)
            => ((3 * PrecisaoEquipamentoSegundos * Math.Sqrt(nEstacoes)) + 10) / 3600.0;

        private static double CalcularToleranciaAltimetrica(double perimetroTotal)
            => 0.15 * Math.Sqrt(perimetroTotal / 1000.0);

        private static bool Ultrapassa(double valor, double limite, double epsilon)
            => valor > limite + epsilon;

        private static double Somar(double[] valores)
        {
            double soma = 0;
            for (int i = 0; i < valores.Length; i++)
            {
                soma += valores[i];
            }

            return soma;
        }

        private sealed record ElementosPoligonal(
            double[] DeltaX,
            double[] DeltaY,
            double[] DistanciasHorizontais,
            double[] Desniveis,
            double PerimetroTotal,
            double SomaDesnivel);
    }
}
