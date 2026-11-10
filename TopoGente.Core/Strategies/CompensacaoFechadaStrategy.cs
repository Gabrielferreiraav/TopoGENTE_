using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
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
                    PoligonalCompensada = new List<PontoCoordenada> { entrada.PontoPartida },
                    Memorial = new MemorialRastreabilidadePericial
                    {
                        Ajustamento = new MemoriaAjustamentoDto
                        {
                            Tipo = TipoEstrategiaAjustamento.SemAjustamento,
                            AngularAprovado = false,
                            LinearAprovado = false,
                            AltimetricoAprovado = false
                        }
                    }
                };
            }

            int nEstacoes = entrada.Leituras.Count;
            double erroAngular = CalcularErroAngularFechada(entrada);
            var tolerancias = entrada.Tolerancias ?? ParametrosTolerancia.Padrao;
            double toleranciaGraus = tolerancias.CalcularToleranciaAngularGraus(nEstacoes);

            if (Ultrapassa(Math.Abs(erroAngular), toleranciaGraus, AngularEpsilonGraus))
            {
                double erroXAngular = entrada.PoligonalBruta[^1].X - entrada.PontoChegada.X;
                double erroYAngular = entrada.PoligonalBruta[^1].Y - entrada.PontoChegada.Y;
                double erroLinearAngular = Math.Sqrt((erroXAngular * erroXAngular) + (erroYAngular * erroYAngular));

                var memorialLances = CriarMemorialLances(entrada);
                var rateiosNulos = CriarRateioNulo(entrada);
                var memoriaAjuste = CriarMemoriaAjustamento(
                    TipoEstrategiaAjustamento.Bowditch,
                    erroAngular,
                    erroLinearAngular,
                    0,
                    0,
                    nEstacoes,
                    memorialLances.PerimetroTotal,
                    tolerancias,
                    rateiosNulos,
                    aprovadoGeral: false);

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
                    PoligonalCompensada = entrada.PoligonalBruta,
                    Memorial = new MemorialRastreabilidadePericial
                    {
                        Lances = memorialLances,
                        Ajustamento = memoriaAjuste
                    }
                };
            }

            double[] azimutesCompensados = CalcularAzimutesCompensados(entrada.PoligonalBruta, erroAngular, nEstacoes);
            var elementos = CalcularElementos(entrada.Leituras, azimutesCompensados);

            double erroX = Somar(elementos.DeltaX);
            double erroY = Somar(elementos.DeltaY);
            double erroAltimetrico = elementos.SomaDesnivel;
            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));
            double precisaoRelativa = elementos.PerimetroTotal > 0.0001 ? erroLinearTotal / elementos.PerimetroTotal : 0;

            if (!tolerancias.AvaliarConformidadeLinear(erroLinearTotal, elementos.PerimetroTotal).AprovadoGeral)
            {
                var memorialLances = CriarMemorialLances(entrada, elementos);
                var rateiosNulos = CriarRateioNulo(entrada);
                var memoriaAjuste = CriarMemoriaAjustamento(
                    TipoEstrategiaAjustamento.Bowditch,
                    erroAngular,
                    erroLinearTotal,
                    erroAltimetrico,
                    precisaoRelativa,
                    nEstacoes,
                    elementos.PerimetroTotal,
                    tolerancias,
                    rateiosNulos,
                    aprovadoGeral: false);

                var dtoReprovado = ReprovarPorPrecisaoLinear(erroAngular, erroX, erroY, erroLinearTotal, precisaoRelativa, erroAltimetrico, entrada.PoligonalBruta, tolerancias);
                dtoReprovado.Memorial = new MemorialRastreabilidadePericial
                {
                    Lances = memorialLances,
                    Ajustamento = memoriaAjuste
                };
                return dtoReprovado;
            }

            double toleranciaAltimetrica = tolerancias.CalcularToleranciaAltimetricaMetros(elementos.PerimetroTotal);
            if (Ultrapassa(Math.Abs(erroAltimetrico), toleranciaAltimetrica, PrecisaoRelativaEpsilon))
            {
                var memorialLances = CriarMemorialLances(entrada, elementos);
                var rateiosNulos = CriarRateioNulo(entrada);
                var memoriaAjuste = CriarMemoriaAjustamento(
                    TipoEstrategiaAjustamento.Bowditch,
                    erroAngular,
                    erroLinearTotal,
                    erroAltimetrico,
                    precisaoRelativa,
                    nEstacoes,
                    elementos.PerimetroTotal,
                    tolerancias,
                    rateiosNulos,
                    aprovadoGeral: false);

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
                    PoligonalCompensada = entrada.PoligonalBruta,
                    Memorial = new MemorialRastreabilidadePericial
                    {
                        Lances = memorialLances,
                        Ajustamento = memoriaAjuste
                    }
                };
            }

            var poligonalCompensada = ConstruirPoligonalCompensada(entrada, azimutesCompensados, elementos, erroX, erroY, erroAltimetrico);
            var memorialLancesAprovado = CriarMemorialLances(entrada, elementos);
            var rateiosAprovados = CriarRateioVertices(entrada, poligonalCompensada, elementos, erroX, erroY, erroAltimetrico);
            var memoriaAjusteAprovado = CriarMemoriaAjustamento(
                TipoEstrategiaAjustamento.Bowditch,
                erroAngular,
                erroLinearTotal,
                erroAltimetrico,
                precisaoRelativa,
                nEstacoes,
                elementos.PerimetroTotal,
                tolerancias,
                rateiosAprovados,
                aprovadoGeral: true);

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
                PoligonalCompensada = poligonalCompensada,
                Memorial = new MemorialRastreabilidadePericial
                {
                    Lances = memorialLancesAprovado,
                    Ajustamento = memoriaAjusteAprovado
                }
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
            List<PontoCoordenada> poligonalBruta,
            ParametrosTolerancia tolerancias)
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
                AlertaReprovacao = $"Precisão Linear (1:{denominadorArredondado}) ou Fechamento Absoluto ({erroLinearTotal * 1000.0:F1} mm) fora dos limites da NBR 13.133.",
                PoligonalCompensada = poligonalBruta
            };
        }

        private static MemorialLancesDto CriarMemorialLances(
            CompensacaoPoligonalInputDTO entrada,
            ElementosPoligonal? elementos = null)
        {
            var lances = new List<PassoLanceGeometricoDto>();
            var poligonalBruta = entrada.PoligonalBruta;
            if (poligonalBruta == null || poligonalBruta.Count < 2 || entrada.Leituras == null)
            {
                return new MemorialLancesDto();
            }

            double perimetroTotal = 0;

            for (int i = 0; i < entrada.Leituras.Count; i++)
            {
                var leitura = entrada.Leituras[i];
                var verticeOrigem = poligonalBruta[i];
                var verticeVante = poligonalBruta[i + 1];

                double dh = elementos?.DistanciasHorizontais[i]
                    ?? GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = elementos?.Desniveis[i]
                    ?? GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                perimetroTotal += dh;

                string pontoRe = (i == 0)
                    ? (entrada.Metadados?.NomeRe ?? "RÉ")
                    : poligonalBruta[i - 1].Nome;

                double azimuteRe = (i == 0)
                    ? GeometriaTopograficaHelper.Normalizar360(entrada.AzimuteInicial + 180.0)
                    : GeometriaTopograficaHelper.Normalizar360(poligonalBruta[i].AzimuteChegada + 180.0);

                double azimuteVante = verticeVante.AzimuteChegada;

                lances.Add(new PassoLanceGeometricoDto
                {
                    OrdemLance = i + 1,
                    EstacaoOrigem = verticeOrigem.Nome,
                    PontoRe = pontoRe,
                    EstacaoVante = verticeVante.Nome,

                    AnguloHzGraus = leitura.AnguloHorizontal,
                    AnguloVGraus = leitura.AnguloVertical,
                    DistanciaInclinada = leitura.DistanciaInclinada,
                    AlturaInstrumento = leitura.AlturaInstrumento,
                    AlturaAlvo = leitura.AlturaPrisma,

                    DistanciaHorizontal = dh,
                    DeltaZ = dn,

                    AzimuteReGraus = azimuteRe,
                    AzimuteVanteGraus = azimuteVante,

                    DeltaEBruto = verticeVante.X - verticeOrigem.X,
                    DeltaNBruto = verticeVante.Y - verticeOrigem.Y,

                    CoordenadaEBruta = verticeVante.X,
                    CoordenadaNBruta = verticeVante.Y,
                    CoordenadaZBruta = verticeVante.Z
                });
            }

            return new MemorialLancesDto
            {
                Lances = lances,
                PerimetroTotal = perimetroTotal
            };
        }

        private static IReadOnlyList<MemoriaRateioVerticeDto> CriarRateioVertices(
            CompensacaoPoligonalInputDTO entrada,
            List<PontoCoordenada> poligonalCompensada,
            ElementosPoligonal elementos,
            double erroX,
            double erroY,
            double erroAltimetrico)
        {
            var rateios = new List<MemoriaRateioVerticeDto>();
            var poligonalBruta = entrada.PoligonalBruta;

            rateios.Add(new MemoriaRateioVerticeDto
            {
                NomeVertice = entrada.PontoPartida.Nome,
                DistanciaAcumulada = 0.0,
                CorrecaoEMilimetros = 0.0,
                CorrecaoNMilimetros = 0.0,
                CorrecaoZMilimetros = 0.0,
                DeslocamentoTotalEMilimetros = 0.0,
                DeslocamentoTotalNMilimetros = 0.0,
                DeslocamentoTotalZMilimetros = 0.0,
                CoordenadaECompensada = entrada.PontoPartida.X,
                CoordenadaNCompensada = entrada.PontoPartida.Y,
                CoordenadaZCompensada = entrada.PontoPartida.Z
            });

            double distAcumulada = 0.0;
            double perimetroTotal = elementos.PerimetroTotal;
            int totalLeituras = entrada.Leituras.Count;

            for (int i = 0; i < totalLeituras; i++)
            {
                distAcumulada += elementos.DistanciasHorizontais[i];
                var pComp = poligonalCompensada[i + 1];
                var pBruto = (poligonalBruta != null && i + 1 < poligonalBruta.Count) ? poligonalBruta[i + 1] : pComp;

                double fatorDist = perimetroTotal > 0.0001 ? distAcumulada / perimetroTotal : 0.0;
                double bowditchE_mm = -erroX * fatorDist * 1000.0;
                double bowditchN_mm = -erroY * fatorDist * 1000.0;
                double bowditchZ_mm = totalLeituras > 0 ? -erroAltimetrico * ((double)(i + 1) / totalLeituras) * 1000.0 : 0.0;

                double deltaTotalE_mm = (pComp.X - pBruto.X) * 1000.0;
                double deltaTotalN_mm = (pComp.Y - pBruto.Y) * 1000.0;
                double deltaTotalZ_mm = (pComp.Z - pBruto.Z) * 1000.0;

                rateios.Add(new MemoriaRateioVerticeDto
                {
                    NomeVertice = pComp.Nome,
                    DistanciaAcumulada = distAcumulada,
                    CorrecaoEMilimetros = bowditchE_mm,
                    CorrecaoNMilimetros = bowditchN_mm,
                    CorrecaoZMilimetros = bowditchZ_mm,
                    DeslocamentoTotalEMilimetros = deltaTotalE_mm,
                    DeslocamentoTotalNMilimetros = deltaTotalN_mm,
                    DeslocamentoTotalZMilimetros = deltaTotalZ_mm,
                    CoordenadaECompensada = pComp.X,
                    CoordenadaNCompensada = pComp.Y,
                    CoordenadaZCompensada = pComp.Z
                });
            }

            return rateios;
        }

        private static IReadOnlyList<MemoriaRateioVerticeDto> CriarRateioNulo(CompensacaoPoligonalInputDTO entrada)
        {
            var rateios = new List<MemoriaRateioVerticeDto>();
            if (entrada.PoligonalBruta == null) return rateios;

            foreach (var p in entrada.PoligonalBruta)
            {
                rateios.Add(new MemoriaRateioVerticeDto
                {
                    NomeVertice = p.Nome,
                    DistanciaAcumulada = 0,
                    CorrecaoEMilimetros = 0,
                    CorrecaoNMilimetros = 0,
                    CorrecaoZMilimetros = 0,
                    CoordenadaECompensada = p.X,
                    CoordenadaNCompensada = p.Y,
                    CoordenadaZCompensada = p.Z
                });
            }
            return rateios;
        }

        private static MemoriaAjustamentoDto CriarMemoriaAjustamento(
            TipoEstrategiaAjustamento tipo,
            double erroAngular,
            double erroLinearTotal,
            double erroAltimetrico,
            double precisaoRelativa,
            int nEstacoes,
            double perimetroTotal,
            ParametrosTolerancia tolerancias,
            IReadOnlyList<MemoriaRateioVerticeDto> rateios,
            bool aprovadoGeral)
        {
            double tolAngularSegundos = tolerancias.CalcularToleranciaAngularSegundos(nEstacoes);
            double tolLinearM = tolerancias.CalcularToleranciaLinearMetros(perimetroTotal);
            double tolAltimetricaM = tolerancias.CalcularToleranciaAltimetricaMetros(perimetroTotal);

            bool angularOk = Math.Abs(erroAngular) <= tolerancias.CalcularToleranciaAngularGraus(nEstacoes) + AngularEpsilonGraus;
            bool linearOk = tolerancias.AvaliarConformidadeLinear(erroLinearTotal, perimetroTotal).AprovadoGeral;
            bool altimetricoOk = Math.Abs(erroAltimetrico) <= tolAltimetricaM + PrecisaoRelativaEpsilon;

            return new MemoriaAjustamentoDto
            {
                Tipo = aprovadoGeral ? tipo : TipoEstrategiaAjustamento.SemAjustamento,
                ErroFechamentoAngularSegundos = Math.Abs(erroAngular) * 3600.0,
                ToleranciaAngularSegundos = tolAngularSegundos,
                AngularAprovado = angularOk,

                ErroFechamentoLinearMetros = erroLinearTotal,
                ToleranciaLinearMetros = tolLinearM,
                PrecisaoRelativaObtida = precisaoRelativa,
                LinearAprovado = linearOk,

                ErroAltimetricoMetros = erroAltimetrico,
                ToleranciaAltimetricaMetros = tolAltimetricaM,
                AltimetricoAprovado = altimetricoOk,

                RateioVertices = rateios
            };
        }

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
