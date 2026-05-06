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

            poligonalBruta[^1].X = pontoChegada.X;
            poligonalBruta[^1].Y = pontoChegada.Y;
            poligonalBruta[^1].Z = pontoChegada.Z;

            var nEstacoes = entrada.Leituras.Count;

            var azimuteCalculadoFinal = poligonalBruta[^1].AzimuteChegada;
            var azimuteReRetorno = GeometriaTopograficaHelper.Normalizar360(azimuteCalculadoFinal + 180);
            var azimuteCalculadoChegada = GeometriaTopograficaHelper.Normalizar360(azimuteReRetorno + entrada.AnguloFechamento);
            var erroAngular = GeometriaTopograficaHelper.NormalizarErroAngular(
                azimuteCalculadoChegada - entrada.AzimuteChegada.GetValueOrDefault());

            const double precisaoEquipamentoSegundos = 5.0;
            var toleranciaSegundos = (3 * precisaoEquipamentoSegundos * Math.Sqrt(nEstacoes)) + 10;
            var toleranciaGraus = toleranciaSegundos / 3600.0;
            const double toleranciaAngularEpsilon = 1e-12;

            if (Math.Abs(erroAngular) - toleranciaGraus > toleranciaAngularEpsilon)
            {
                var erroXAngular = poligonalBruta[^1].X - pontoChegada.X;
                var erroYAngular = poligonalBruta[^1].Y - pontoChegada.Y;
                var erroLinearAngular = Math.Sqrt((erroXAngular * erroXAngular) + (erroYAngular * erroYAngular));

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

            var correcaoAngularUnitaria = -erroAngular / nEstacoes;

            double erroX = 0;
            double erroY = 0;

            for (int i = 0; i < nEstacoes; i++)
            {
                var leitura = entrada.Leituras[i];
                var pesoFrac = leitura.Peso / 100.0;
                var correcao = pesoFrac * correcaoAngularUnitaria;

                erroX += leitura.X - (poligonalBruta[i].X - correcao);
                erroY += leitura.Y - (poligonalBruta[i].Y - correcao);
            }

            erroX /= nEstacoes;
            erroY /= nEstacoes;

            var erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            double somaQuadradosErroX = 0;
            double somaQuadradosErroY = 0;

            for (int i = 0; i < nEstacoes; i++)
            {
                var dx = poligonalBruta[i].X - erroX;
                var dy = poligonalBruta[i].Y - erroY;

                somaQuadradosErroX += dx * dx;
                somaQuadradosErroY += dy * dy;
            }

            var desvioPadraoX = (nEstacoes > 1) ? Math.Sqrt(somaQuadradosErroX / (nEstacoes - 1)) : 0;
            var desvioPadraoY = (nEstacoes > 1) ? Math.Sqrt(somaQuadradosErroY / (nEstacoes - 1)) : 0;

            double somaPesos = 0;
            for (int i = 0; i < nEstacoes; i++)
            {
                somaPesos += entrada.Leituras[i].Peso;
            }

            var precisaoRelativa =
                somaPesos > 0 && erroX != 0 && erroY != 0
                    ? Math.Sqrt(
                        ((desvioPadraoX / erroX) * (desvioPadraoX / erroX) + (desvioPadraoY / erroY) * (desvioPadraoY / erroY)) / somaPesos)
                    : 0;

            var erroAltimetrico = Math.Abs(pontoChegada.Z - poligonalBruta[^1].Z);

            return new ResultadoCompensacaoDTO
            {
                ErroAngular = erroAngular,
                ErroX = erroX,
                ErroY = erroY,
                ErroLinearTotal = erroLinearTotal,
                PrecisaoRelativa = precisaoRelativa,
                ErroAltimetrico = erroAltimetrico,
                AprovadoNorma = true,
                PoligonalCompensada = poligonalBruta
            };
        }
    }
}