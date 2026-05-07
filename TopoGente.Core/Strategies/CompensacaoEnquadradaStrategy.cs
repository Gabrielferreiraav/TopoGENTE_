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
            if (entrada.Leituras == null || entrada.Leituras.Count == 0)
            {
                return new ResultadoCompensacaoDTO
                {
                    AprovadoNorma = false,
                    AlertaReprovacao = "Nenhuma leitura fornecida. Compensação não realizada.",
                    PoligonalCompensada = new List<PontoCoordenada> { entrada.PontoPartida }
                };
            }

            if (entrada.Metadados.ChegadaX is null || entrada.Metadados.ChegadaY is null || entrada.Metadados.ChegadaZ is null)
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

            var pontoChegada = new PontoCoordenada
            {
                Nome = entrada.Metadados.NomeChegada ?? "Chegada",
                X = entrada.Metadados.ChegadaX.Value,
                Y = entrada.Metadados.ChegadaY.Value,
                Z = entrada.Metadados.ChegadaZ.Value,
                EhPontoPoligonal = true
            };

            int nEstacoes = entrada.Leituras.Count;

            double azimuteCalculadoFinal = poligonalBruta[^1].AzimuteChegada;
            double azimuteReRetorno = GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180);
            double azimuteCalculadoChegada = GeometriaTopograficaHelper.Normalizar360(azimuteReRetorno + entrada.AnguloFechamento);
            double erroAngular = GeometriaTopograficaHelper.NormalizarErroAngular(
                azimuteCalculadoChegada - entrada.AzimuteChegada.GetValueOrDefault());

            const double precisaoEquipamentoSegundos = 5.0;
            double toleranciaSegundos = (3 * precisaoEquipamentoSegundos * Math.Sqrt(nEstacoes)) + 10;
            double toleranciaGraus = toleranciaSegundos / 3600.0;
            const double toleranciaAngularEpsilon = 1e-12;

            if (Math.Abs(erroAngular) - toleranciaGraus > toleranciaAngularEpsilon)
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

            double correcaoAngularUnitaria = -erroAngular / nEstacoes;

            var azimutesCompensados = new double[nEstacoes];
            for (int i = 0; i < nEstacoes; i++)
            {
                double azimuteCalculado = poligonalBruta[i + 1].AzimuteChegada;
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

            double xFinal = entrada.PontoPartida.X + somaDeltasX;
            double yFinal = entrada.PontoPartida.Y + somaDeltasY;
            double zFinal = entrada.PontoPartida.Z + somaDn;

            double erroX = xFinal - pontoChegada.X;
            double erroY = yFinal - pontoChegada.Y;
            double erroAltimetrico = zFinal - pontoChegada.Z;

            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            double precisaoRelativa = 0;
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
                    PoligonalCompensada = poligonalBruta
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