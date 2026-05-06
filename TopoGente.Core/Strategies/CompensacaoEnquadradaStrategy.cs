using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Strategies
{
    public sealed class CompensacaoEnquadradaStrategy : ICompensacaoPoligonalStrategy
    {
        public ResultadoCompensacaoDTO Compensar(CompensacaoPoligonalInputDTO entrada)
        {
            double erroAngular = 0;
            double erroX = 0;
            double erroY = 0;
            double erroLinearTotal = 0;
            double precisaoRelativa = 0;
            double erroAltimetrico = 0;

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

            double azimuteCalculadoFinal = entrada.PoligonalBruta[^1].AzimuteChegada;
            double azimuteReRetorno = GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180);
            double azimuteCalculadoChegada = GeometriaTopograficaHelper.Normalizar360(azimuteReRetorno + entrada.AnguloFechamento);
            erroAngular = GeometriaTopograficaHelper.NormalizarErroAngular(azimuteCalculadoChegada - entrada.AzimuteChegada.GetValueOrDefault());

            double precisaoEquipamentoSegundos = 5.0;
            double toleranciaSegundos = (3 * precisaoEquipamentoSegundos * Math.Sqrt(nEstacoes)) + 10;
            double toleranciaGraus = toleranciaSegundos / 3600.0;
            const double toleranciaAngularEpsilon = 1e-12;

            if (Math.Abs(erroAngular) - toleranciaGraus > toleranciaAngularEpsilon)
            {
                erroX = entrada.PoligonalBruta[^1].X - entrada.PontoChegada.X;
                erroY = entrada.PoligonalBruta[^1].Y - entrada.PontoChegada.Y;
                erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

                return new ResultadoCompensacaoDTO
                {
                    ErroAngular = erroAngular,
                    ErroX = erroX,
                    ErroY = erroY,
                    ErroLinearTotal = erroLinearTotal,
                    PrecisaoRelativa = 0,
                    ErroAltimetrico = 0,
                    AprovadoNorma = false,
                    AlertaReprovacao = $"Erro Angular ({erroAngular:F4}°) superou a tolerância da NBR 13.133 ({toleranciaGraus:F4}°).",
                    PoligonalCompensada = entrada.PoligonalBruta
                };
            }

            double correcaoAngularUnitaria = -erroAngular / nEstacoes;

            var azimutesCompensados = new double[nEstacoes];
            for (int i = 0; i < nEstacoes; i++)
            {
                double azimuteCalculado = entrada.PoligonalBruta[i + 1].AzimuteChegada;
                azimutesCompensados[i] = GeometriaTopograficaHelper.Normalizar360(azimuteCalculado + ((i + 1) * correcaoAngularUnitaria));
            }

            var deltaX = new double[nEstacoes];
            var deltaY = new double[nEstacoes];
            var distanciasHorizontais = new double[nEstacoes];
            var desniveis = new double[nEstacoes];

            double perimetroTotal = 0;
            double somaDn = 0;

            for (int i = 0; i < nEstacoes; i++)
            {
                var leitura = entrada.Leituras[i];

                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                distanciasHorizontais[i] = dh;
                desniveis[i] = dn;
                perimetroTotal += dh;
                somaDn += dn;

                var (dx, dy) = GeometriaTopograficaHelper.CalcularProjecao(dh, azimutesCompensados[i]);
                deltaX[i] = dx;
                deltaY[i] = dy;
            }

            double somaDeltasX = 0;
            double somaDeltasY = 0;

            for (int j = 0; j < nEstacoes; j++)
            {
                somaDeltasX += deltaX[j];
                somaDeltasY += deltaY[j];
            }

            erroX = (somaDeltasX + entrada.PontoPartida.X) - entrada.PontoChegada.X;
            erroY = (somaDeltasY + entrada.PontoPartida.Y) - entrada.PontoChegada.Y;
            erroAltimetrico = (entrada.PontoPartida.Z + somaDn) - entrada.PontoChegada.Z;

            erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            if (perimetroTotal > 0.0001)
            {
                precisaoRelativa = erroLinearTotal / perimetroTotal;
            }

            double precisaoMinima = 1.0 / 12000.0;
            const double toleranciaPrecisao = 1e-12;

            if (precisaoRelativa - precisaoMinima > toleranciaPrecisao)
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
                    PoligonalCompensada = entrada.PoligonalBruta
                };
            }

            double perimetroKm = perimetroTotal / 1000.0;
            double toleranciaAltimetrica = 0.15 * Math.Sqrt(perimetroKm);

            if (Math.Abs(erroAltimetrico) - toleranciaAltimetrica > toleranciaPrecisao)
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

            double coefX = perimetroTotal > 0 ? -erroX / perimetroTotal : 0;
            double coefY = perimetroTotal > 0 ? -erroY / perimetroTotal : 0;
            double corrZ = -erroAltimetrico / nEstacoes;

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

            for (int j = 0; j < nEstacoes; j++)
            {
                xAtual += deltaX[j] + (coefX * distanciasHorizontais[j]);
                yAtual += deltaY[j] + (coefY * distanciasHorizontais[j]);
                zAtual += desniveis[j] + corrZ;

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
    }
}