using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Strategies
{
    public sealed class CompensacaoEnquadradaStrategy : ICompensacaoPoligonalStrategy
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

            var pontoChegada = ObterPontoChegada(entrada);
            if (pontoChegada == null)
            {
                return new ResultadoCompensacaoDTO
                {
                    AprovadoNorma = false,
                    AlertaReprovacao = "Poligonal enquadrada exige coordenadas de chegada (X, Y, Z).",
                    PoligonalCompensada = new List<PontoCoordenada> { entrada.PontoPartida }
                };
            }

            var poligonalBruta = entrada.PoligonalBruta;
            if (poligonalBruta == null || poligonalBruta.Count == 0)
            {
                return new ResultadoCompensacaoDTO
                {
                    AprovadoNorma = false,
                    AlertaReprovacao = "Poligonal bruta ausente. Compensação não realizada.",
                    PoligonalCompensada = new List<PontoCoordenada> { entrada.PontoPartida }
                };
            }

            int nEstacoes = entrada.Leituras.Count;
            double erroAngular = CalcularErroAngularEnquadrada(entrada);
            double toleranciaGraus = CalcularToleranciaAngularGraus(nEstacoes);

            if (Ultrapassa(Math.Abs(erroAngular), toleranciaGraus, AngularEpsilonGraus))
            {
                double erroXAngular = poligonalBruta[^1].X - pontoChegada.X;
                double erroYAngular = poligonalBruta[^1].Y - pontoChegada.Y;
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
                    PoligonalCompensada = poligonalBruta
                };
            }

            double[] azimutesCompensados = CalcularAzimutesCompensados(poligonalBruta, erroAngular, nEstacoes);
            var elementos = CalcularElementos(entrada.Leituras, azimutesCompensados);

            double xFinal = entrada.PontoPartida.X + Somar(elementos.DeltaX);
            double yFinal = entrada.PontoPartida.Y + Somar(elementos.DeltaY);
            double zFinal = entrada.PontoPartida.Z + elementos.SomaDesnivel;

            double erroX = xFinal - pontoChegada.X;
            double erroY = yFinal - pontoChegada.Y;
            double erroAltimetrico = zFinal - pontoChegada.Z;
            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));
            double precisaoRelativa = elementos.PerimetroTotal > 0.0001 ? erroLinearTotal / elementos.PerimetroTotal : 0;

            const double precisaoMinima = 1.0 / 12000.0;
            if (Ultrapassa(precisaoRelativa, precisaoMinima, PrecisaoRelativaEpsilon))
            {
                return ReprovarPorPrecisaoLinear(erroAngular, erroX, erroY, erroLinearTotal, precisaoRelativa, erroAltimetrico, poligonalBruta);
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
                    PoligonalCompensada = poligonalBruta
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

        private static PontoCoordenada? ObterPontoChegada(CompensacaoPoligonalInputDTO entrada)
        {
            if (entrada.Metadados.ChegadaX is not null &&
                entrada.Metadados.ChegadaY is not null &&
                entrada.Metadados.ChegadaZ is not null)
            {
                return new PontoCoordenada
                {
                    Nome = entrada.Metadados.NomeChegada ?? entrada.PontoChegada.Nome,
                    X = entrada.Metadados.ChegadaX.Value,
                    Y = entrada.Metadados.ChegadaY.Value,
                    Z = entrada.Metadados.ChegadaZ.Value,
                    EhPontoPoligonal = true
                };
            }

            bool metadadosDeclararamChegada =
                entrada.Metadados.ChegadaX is not null ||
                entrada.Metadados.ChegadaY is not null ||
                entrada.Metadados.ChegadaZ is not null ||
                !string.IsNullOrWhiteSpace(entrada.Metadados.NomeChegada);

            return metadadosDeclararamChegada ? null : entrada.PontoChegada;
        }

        private static double CalcularErroAngularEnquadrada(CompensacaoPoligonalInputDTO entrada)
        {
            double azimuteCalculadoFinal = entrada.PoligonalBruta[^1].AzimuteChegada;
            double azimuteCalculadoChegada = Math.Abs(entrada.AnguloFechamento) > 0.0001
                ? GeometriaTopograficaHelper.Normalizar360(
                    GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180) + entrada.AnguloFechamento)
                : GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180);

            return GeometriaTopograficaHelper.NormalizarErroAngular(
                azimuteCalculadoChegada - entrada.AzimuteChegada.GetValueOrDefault());
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
            return new ResultadoCompensacaoDTO
            {
                ErroAngular = erroAngular,
                ErroX = erroX,
                ErroY = erroY,
                ErroLinearTotal = erroLinearTotal,
                PrecisaoRelativa = precisaoRelativa,
                ErroAltimetrico = erroAltimetrico,
                AprovadoNorma = false,
                AlertaReprovacao = $"Precisão Linear (1:{(precisaoRelativa > 0 ? (1 / precisaoRelativa) : 0):F0}) inferior ao exigido (1:12000).",
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
