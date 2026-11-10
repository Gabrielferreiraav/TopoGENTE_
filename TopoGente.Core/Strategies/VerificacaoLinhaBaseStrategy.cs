using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Strategies
{
    /// <summary>
    /// Estratégia de Verificação de Linha de Base (NBR 13.133 — Seção de Verificação).
    /// <para>
    /// Realiza o transporte analítico direto das coordenadas da estação de partida até
    /// a estação de chegada, sem nenhum rateio de Bowditch. Ao atingir o ponto de
    /// fechamento, confronta as coordenadas calculadas com as coordenadas teóricas
    /// (conhecidas) do ponto de chegada e produz um <see cref="LaudoDiscrepanciaLinhaBase"/>.
    /// </para>
    /// <para>
    /// Implementa <see cref="ICompensacaoPoligonalStrategy"/> retornando
    /// um <see cref="ResultadoCompensacaoDTO"/> sem compensação planar, com o laudo
    /// de discrepância embutido em <see cref="ResultadoCompensacaoDTO.LaudoLinhaBase"/>.
    /// </para>
    /// </summary>
    public sealed class VerificacaoLinhaBaseStrategy : ICompensacaoPoligonalStrategy
    {
        private const double EpsilonLinear  = 1e-9;
        private const double EpsilonAngular = 1e-10;

        public ResultadoCompensacaoDTO Compensar(CompensacaoPoligonalInputDTO entrada)
        {
            // ── Validações de pré-condição ─────────────────────────────────────────
            if (entrada.Leituras == null || entrada.Leituras.Count == 0)
            {
                return Reprovar(
                    "Nenhuma leitura fornecida para verificação de linha de base.",
                    entrada.PontoPartida);
            }

            var pontoChegadaConhecido = ResolverPontoChegada(entrada);
            if (pontoChegadaConhecido == null)
            {
                return Reprovar(
                    "Verificação de linha de base exige coordenadas conhecidas do ponto de chegada (X, Y, Z).",
                    entrada.PontoPartida);
            }

            var tolerancias = entrada.Tolerancias ?? ParametrosTolerancia.Padrao;

            // ── 1. Transporte analítico direto (sem compensação) ──────────────────
            var (poligonalTransportada, perimetroTotal) = TransportarAnalitico(entrada);

            var pontoCalculado = poligonalTransportada[^1];

            // ── 2. Discrepâncias em metros ────────────────────────────────────────
            double deltaE = pontoCalculado.X - pontoChegadaConhecido.X;   // Leste
            double deltaN = pontoCalculado.Y - pontoChegadaConhecido.Y;   // Norte
            double deltaZ = pontoCalculado.Z - pontoChegadaConhecido.Z;   // Cota

            // ── 3. Erros escalares ────────────────────────────────────────────────
            double erroLinearPlanarM = Math.Sqrt((deltaE * deltaE) + (deltaN * deltaN));
            double erroLinearTotalM  = Math.Sqrt((deltaE * deltaE) + (deltaN * deltaN) + (deltaZ * deltaZ));
            double erroLinearMm      = erroLinearTotalM * 1000.0;

            // ── 4. Erro angular (azimute teórico vs. medido), em segundos de arco ─
            double azimuteTeórico = GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(
                entrada.PontoPartida.X, entrada.PontoPartida.Y,
                pontoChegadaConhecido.X, pontoChegadaConhecido.Y);

            double azimuteMedido = pontoCalculado.AzimuteChegada;

            double diferencaAngularGraus = GeometriaTopograficaHelper.NormalizarErroAngular(
                azimuteMedido - azimuteTeórico);
            double erroAngularSegundos = Math.Abs(diferencaAngularGraus) * 3600.0;

            // ── 5. Precisão relativa planar (1 : P) ───────────────────────────────
            double precisaoRelativa = (perimetroTotal > 0.0001 && erroLinearPlanarM > EpsilonLinear)
                ? erroLinearPlanarM / perimetroTotal
                : 0.0;

            // ── 6. Tolerâncias admissíveis ────────────────────────────────────────
            int nEstacoes = entrada.Leituras.Count;
            double toleranciaAngularSegundos = tolerancias.CalcularToleranciaAngularSegundos(nEstacoes);
            double toleranciaLinearMm        = tolerancias.CalcularToleranciaLinearMetros(perimetroTotal) * 1000.0;

            // ── 7. Distância teórica da base ──────────────────────────────────────
            double dxBase = pontoChegadaConhecido.X - entrada.PontoPartida.X;
            double dyBase = pontoChegadaConhecido.Y - entrada.PontoPartida.Y;
            double distanciaTeórica = Math.Sqrt((dxBase * dxBase) + (dyBase * dyBase));

            // ── 8. Avaliação de aprovação ─────────────────────────────────────────
            bool aprovadoLinear  = erroLinearMm        <= toleranciaLinearMm        + EpsilonAngular;
            bool aprovadoAngular = erroAngularSegundos <= toleranciaAngularSegundos + EpsilonAngular;
            bool aprovado        = aprovadoLinear && aprovadoAngular;

            string motivoReprovacao = string.Empty;
            if (!aprovado)
            {
                var partes = new List<string>();
                if (!aprovadoLinear)
                    partes.Add($"Erro linear ({erroLinearMm:F1} mm) > Tl ({toleranciaLinearMm:F1} mm)");
                if (!aprovadoAngular)
                    partes.Add($"Erro angular ({erroAngularSegundos:F1}\") > Ta ({toleranciaAngularSegundos:F1}\")");
                motivoReprovacao = string.Join(" | ", partes);
            }

            // ── 9. Montagem do laudo ──────────────────────────────────────────────
            var laudo = new LaudoDiscrepanciaLinhaBase
            {
                NomePontoPartida          = entrada.PontoPartida.Nome,
                NomePontoChegada          = pontoChegadaConhecido.Nome,
                DistanciaTeórica          = distanciaTeórica,
                AzimuteTeóricoGraus       = azimuteTeórico,
                AzimuteMedidoGraus        = azimuteMedido,
                DeltaEsteMetros           = deltaE,
                DeltaNorteMetros          = deltaN,
                DeltaZMetros              = deltaZ,
                ErroLinearMm              = erroLinearMm,
                ErroAngularSegundos       = erroAngularSegundos,
                PrecisaoRelativa          = precisaoRelativa,
                ToleranciaLinearMm        = toleranciaLinearMm,
                ToleranciaAngularSegundos = toleranciaAngularSegundos,
                Aprovado                  = aprovado,
                MotivoReprovacao          = motivoReprovacao
            };

            // ── 10. Instrumentação Pericial In-Flight ─────────────────────────────
            var memorialLances = CriarMemorialLances(entrada, poligonalTransportada, perimetroTotal);
            var rateioVertices = CriarRateioLinhaBase(poligonalTransportada, entrada.Leituras);
            var memoriaAjustamento = new MemoriaAjustamentoDto
            {
                Tipo = TipoEstrategiaAjustamento.VerificacaoLinhaBase,
                ErroFechamentoAngularSegundos = erroAngularSegundos,
                ToleranciaAngularSegundos = toleranciaAngularSegundos,
                AngularAprovado = aprovadoAngular,

                ErroFechamentoLinearMetros = erroLinearTotalM,
                ToleranciaLinearMetros = toleranciaLinearMm / 1000.0,
                PrecisaoRelativaObtida = precisaoRelativa,
                LinearAprovado = aprovadoLinear,

                ErroAltimetricoMetros = deltaZ,
                ToleranciaAltimetricaMetros = tolerancias.CalcularToleranciaAltimetricaMetros(perimetroTotal),
                AltimetricoAprovado = Math.Abs(deltaZ) <= tolerancias.CalcularToleranciaAltimetricaMetros(perimetroTotal) + EpsilonLinear,

                RateioVertices = rateioVertices,
                LaudoLinhaBase = laudo
            };

            // ── 11. DTO de resultado (poligonal = transporte bruto, sem rateio) ───
            return new ResultadoCompensacaoDTO
            {
                ErroAngular         = diferencaAngularGraus,
                ErroX               = deltaE,
                ErroY               = deltaN,
                ErroLinearTotal     = erroLinearTotalM,
                PrecisaoRelativa    = precisaoRelativa,
                ErroAltimetrico     = deltaZ,
                AprovadoNorma       = aprovado,
                AlertaReprovacao    = motivoReprovacao,
                PoligonalCompensada = poligonalTransportada,
                LaudoLinhaBase      = laudo,
                Memorial            = new MemorialRastreabilidadePericial
                {
                    Lances = memorialLances,
                    Ajustamento = memoriaAjustamento
                }
            };
        }

        // ── Helpers privados ───────────────────────────────────────────────────────

        /// <summary>
        /// Transporta analiticamente as coordenadas desde o ponto de partida,
        /// aplicando cada ângulo horizontal de visada de poligonal sem nenhuma correção.
        /// Retorna a lista de pontos calculados e o perímetro total percorrido.
        /// </summary>
        private static (List<PontoCoordenada> pontos, double perimetroTotal) TransportarAnalitico(
            CompensacaoPoligonalInputDTO entrada)
        {
            var pontos = new List<PontoCoordenada>
            {
                new PontoCoordenada
                {
                    Nome             = entrada.PontoPartida.Nome,
                    X                = entrada.PontoPartida.X,
                    Y                = entrada.PontoPartida.Y,
                    Z                = entrada.PontoPartida.Z,
                    EhPontoPoligonal = true,
                    AzimuteChegada   = entrada.AzimuteInicial
                }
            };

            double azimuteVigente = entrada.AzimuteInicial;
            double perimetroTotal = 0.0;

            foreach (var leitura in entrada.Leituras)
            {
                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(
                    leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(
                    leitura.DistanciaInclinada, leitura.AnguloVertical,
                    leitura.AlturaInstrumento, leitura.AlturaPrisma);

                // Propaga azimute pela deflexão horizontal observada
                azimuteVigente = GeometriaTopograficaHelper.Normalizar360(
                    azimuteVigente + leitura.AnguloHorizontal);

                var (dx, dy) = GeometriaTopograficaHelper.CalcularProjecao(dh, azimuteVigente);

                var anterior = pontos[^1];
                pontos.Add(new PontoCoordenada
                {
                    Nome             = leitura.PontoVisado,
                    X                = anterior.X + dx,
                    Y                = anterior.Y + dy,
                    Z                = anterior.Z + dn,
                    EhPontoPoligonal = true,
                    AzimuteChegada   = azimuteVigente
                });

                perimetroTotal += dh;

                // Preparar azimute de retorno para a próxima estação (ângulo interno)
                azimuteVigente = GeometriaTopograficaHelper.Normalizar360(azimuteVigente + 180.0);
            }

            return (pontos, perimetroTotal);
        }

        private static MemorialLancesDto CriarMemorialLances(
            CompensacaoPoligonalInputDTO entrada,
            List<PontoCoordenada> pontosTransportados,
            double perimetroTotal)
        {
            var lances = new List<PassoLanceGeometricoDto>();
            if (entrada.Leituras == null || pontosTransportados == null || pontosTransportados.Count < 2)
            {
                return new MemorialLancesDto();
            }

            for (int i = 0; i < entrada.Leituras.Count; i++)
            {
                var leitura = entrada.Leituras[i];
                var origem = pontosTransportados[i];
                var vante = pontosTransportados[i + 1];

                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                string pontoRe = (i == 0)
                    ? (entrada.Metadados?.NomeRe ?? "RÉ")
                    : pontosTransportados[i - 1].Nome;

                double azimuteRe = (i == 0)
                    ? GeometriaTopograficaHelper.Normalizar360(entrada.AzimuteInicial + 180.0)
                    : GeometriaTopograficaHelper.Normalizar360(origem.AzimuteChegada + 180.0);

                lances.Add(new PassoLanceGeometricoDto
                {
                    OrdemLance = i + 1,
                    EstacaoOrigem = origem.Nome,
                    PontoRe = pontoRe,
                    EstacaoVante = vante.Nome,

                    AnguloHzGraus = leitura.AnguloHorizontal,
                    AnguloVGraus = leitura.AnguloVertical,
                    DistanciaInclinada = leitura.DistanciaInclinada,
                    AlturaInstrumento = leitura.AlturaInstrumento,
                    AlturaAlvo = leitura.AlturaPrisma,

                    DistanciaHorizontal = dh,
                    DeltaZ = dn,

                    AzimuteReGraus = azimuteRe,
                    AzimuteVanteGraus = vante.AzimuteChegada,

                    DeltaEBruto = vante.X - origem.X,
                    DeltaNBruto = vante.Y - origem.Y,

                    CoordenadaEBruta = vante.X,
                    CoordenadaNBruta = vante.Y,
                    CoordenadaZBruta = vante.Z
                });
            }

            return new MemorialLancesDto
            {
                Lances = lances,
                PerimetroTotal = perimetroTotal
            };
        }

        private static IReadOnlyList<MemoriaRateioVerticeDto> CriarRateioLinhaBase(
            List<PontoCoordenada> pontosTransportados,
            List<LeituraEstacaoTotal> leituras)
        {
            var rateios = new List<MemoriaRateioVerticeDto>();
            if (pontosTransportados == null || pontosTransportados.Count == 0)
            {
                return rateios;
            }

            double distAcumulada = 0.0;
            rateios.Add(new MemoriaRateioVerticeDto
            {
                NomeVertice = pontosTransportados[0].Nome,
                DistanciaAcumulada = 0.0,
                CorrecaoEMilimetros = 0.0,
                CorrecaoNMilimetros = 0.0,
                CorrecaoZMilimetros = 0.0,
                CoordenadaECompensada = pontosTransportados[0].X,
                CoordenadaNCompensada = pontosTransportados[0].Y,
                CoordenadaZCompensada = pontosTransportados[0].Z
            });

            for (int i = 0; i < leituras.Count && (i + 1) < pontosTransportados.Count; i++)
            {
                var leitura = leituras[i];
                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                distAcumulada += dh;
                var pt = pontosTransportados[i + 1];

                rateios.Add(new MemoriaRateioVerticeDto
                {
                    NomeVertice = pt.Nome,
                    DistanciaAcumulada = distAcumulada,
                    CorrecaoEMilimetros = 0.0,
                    CorrecaoNMilimetros = 0.0,
                    CorrecaoZMilimetros = 0.0,
                    CoordenadaECompensada = pt.X,
                    CoordenadaNCompensada = pt.Y,
                    CoordenadaZCompensada = pt.Z
                });
            }

            return rateios;
        }

        /// <summary>Resolve o ponto de chegada teórico a partir dos metadados do input.</summary>
        private static PontoCoordenada? ResolverPontoChegada(CompensacaoPoligonalInputDTO entrada)
        {
            // Preferência 1: coordenadas explícitas nos metadados do cenário
            if (entrada.Metadados.ChegadaX is not null &&
                entrada.Metadados.ChegadaY is not null &&
                entrada.Metadados.ChegadaZ is not null)
            {
                return new PontoCoordenada
                {
                    Nome             = entrada.Metadados.NomeChegada ?? entrada.PontoChegada.Nome,
                    X                = entrada.Metadados.ChegadaX.Value,
                    Y                = entrada.Metadados.ChegadaY.Value,
                    Z                = entrada.Metadados.ChegadaZ.Value,
                    EhPontoPoligonal = true
                };
            }

            // Preferência 2: PontoChegada explicitamente fornecido com coordenadas não-nulas
            if (entrada.PontoChegada != null &&
                (Math.Abs(entrada.PontoChegada.X) > EpsilonLinear ||
                 Math.Abs(entrada.PontoChegada.Y) > EpsilonLinear))
            {
                return entrada.PontoChegada;
            }

            return null;
        }

        /// <summary>Produz um DTO de resultado reprovado com mensagem de erro.</summary>
        private static ResultadoCompensacaoDTO Reprovar(string mensagem, PontoCoordenada pontoPartida)
            => new ResultadoCompensacaoDTO
            {
                AprovadoNorma       = false,
                AlertaReprovacao    = mensagem,
                PoligonalCompensada = new List<PontoCoordenada> { pontoPartida },
                Memorial            = new MemorialRastreabilidadePericial
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
}
