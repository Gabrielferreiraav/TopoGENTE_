using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Strategies;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class LevantamentoProcessor : ILevantamentoProcessor
    {
        private readonly IClassificadorGrafo _classificadorGrafo;
        private readonly CompensacaoStrategyFactory _compensacaoStrategyFactory;

        private const double ToleranciaFechamento = 0.05; // 5 cm por km

        public LevantamentoProcessor(IClassificadorGrafo classificadorGrafo, CompensacaoStrategyFactory compensacaoStrategyFactory)
        {
            _classificadorGrafo = classificadorGrafo;
            _compensacaoStrategyFactory = compensacaoStrategyFactory;
        }


        /// <summary>
        /// Valida se o ponto de partida possui coordenadas reais
        /// Coordenadas (0,0,0) sao aceitas apenas se explicitamente fornecidas
        /// </summary>
        private static void ValidarCoordenadasPartida(PontoCoordenada pontoPartida)
        {
            if (pontoPartida == null)
            {
                throw new DadosInsuficientesException(
                    "Levantamentos fechados exigem coordenadas de partida e chegada reais para calculo de fechamento.");
            }
        }


        private static IReadOnlyList<string> ValidarSequenciaEstacoes(MetadadosCenario metadadosAtuais, List<Estacao> todasEstacoes)
        {
            if (metadadosAtuais.SequenciaEstacoesSelecionadas == null)
            {
                throw new DadosInsuficientesException("Sequencia de estacoes nao foi informada.");
            }

            var sequencia = metadadosAtuais.SequenciaEstacoesSelecionadas
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .Select(nome => nome.Trim())
                .ToList();

            if (sequencia.Count == 0)
            {
                throw new DadosInsuficientesException("Sequencia de estacoes nao foi informada.");
            }

            var mapaEstacoes = new HashSet<string>(todasEstacoes.Select(e => e.Nome), StringComparer.OrdinalIgnoreCase);

            // A fi­sica geodsica exige apenas nas origens das visadas
            // O ultimo no da sequencia é o alvo final e nao exige classe Estacao instanciada
            var estacoesDeOrigem = sequencia.Take(sequencia.Count - 1);

            if (estacoesDeOrigem.Any(nome => !mapaEstacoes.Contains(nome)))
            {
                throw new DadosInsuficientesException("A sequencia informada contém estacoes de origem nao carregadas na caderneta fisica.");
            }

            var validadores = new Dictionary<TipoCenarioPoligonal, Action>
            {
                { TipoCenarioPoligonal.Fechada, () => ValidarSequenciaFechada(sequencia[0], sequencia[^1]) },
                { TipoCenarioPoligonal.Enquadrada, () => ValidarSequenciaEnquadrada(sequencia[0], sequencia[^1], metadadosAtuais) }
            };

            if (validadores.TryGetValue(metadadosAtuais.TipoCenario, out var validar))
            {
                validar();
            }

            return sequencia;
        }

        private static void ValidarSequenciaFechada(string primeira, string ultima)
        {
            if (!string.Equals(primeira, ultima, StringComparison.OrdinalIgnoreCase))
            {
                throw new DadosInsuficientesException("Poligonal Fechada exige que a estacao de partida seja a mesma de fechamento");
            }
        }

        private static void ValidarSequenciaEnquadrada(string primeira, string ultima, MetadadosCenario metadadosAtuais)
        {
            if (string.Equals(primeira, ultima, StringComparison.OrdinalIgnoreCase))
            {
                throw new DadosInsuficientesException("Poligonal Enquadrada exige que a estacao de partida seja diferente da estacao de chegada.");
            }

            if (metadadosAtuais.ChegadaX is null || metadadosAtuais.ChegadaY is null || metadadosAtuais.ChegadaZ is null)
            {
                throw new DadosInsuficientesException("Poligonal enquadrada exige coordenadas conhecidas de chegada.");
            }

            if (string.IsNullOrWhiteSpace(metadadosAtuais.NomeChegada) || !string.Equals(metadadosAtuais.NomeChegada, ultima, StringComparison.OrdinalIgnoreCase))
            {
                throw new DadosInsuficientesException("Poligonal enquadrada exige que a estacao final corresponda ao nome de chegada informado.");
            }
        }

        public ResultadoLevantamento Processar(
            MetadadosCenario metadadosAtuais, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            if (metadadosAtuais == null)
            {
                throw new DadosInsuficientesException("Dados Inciais nÃ£o foram preenchidos.");
            }

            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                pontosConhecidos ??= new Dictionary<string, PontoCoordenada>();
                if (metadadosAtuais.ChegadaX.HasValue && metadadosAtuais.ChegadaY.HasValue && metadadosAtuais.ChegadaZ.HasValue)
                {
                    pontosConhecidos["CHEGADA"] = new PontoCoordenada
                    {
                        Nome = metadadosAtuais.NomeChegada ?? "CHEGADA",
                        X = metadadosAtuais.ChegadaX.Value,
                        Y = metadadosAtuais.ChegadaY.Value,
                        Z = metadadosAtuais.ChegadaZ.Value
                    };
                }
            }

            if (todasEstacoes == null)
            {
                throw new DadosInsuficientesException("Caderneta de estaÃ§Ãµes nÃ£o foi informada.");
            }


            var sequenciaEstacoes = ValidarSequenciaEstacoes(metadadosAtuais, todasEstacoes);

            double azimuteInicial = metadadosAtuais.UsarCoordenadaRe
                ? GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(metadadosAtuais.PartidaX, metadadosAtuais.PartidaY, metadadosAtuais.ReX, metadadosAtuais.ReY)
                : metadadosAtuais.AzimutePartida;

            string nomePontoIncial = sequenciaEstacoes.Count > 0
                ? sequenciaEstacoes[0]
                : (todasEstacoes.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Nome))?.Nome ?? "Partida");

            var pontoPartida = new PontoCoordenada
            {
                Nome = nomePontoIncial,
                X = metadadosAtuais.PartidaX,
                Y = metadadosAtuais.PartidaY,
                Z = metadadosAtuais.PartidaZ,
                EhPontoPoligonal = true,
                AzimuteChegada = azimuteInicial
            };

            _classificadorGrafo.ClassificarArestasGrafo(todasEstacoes, metadadosAtuais);
            var todasLeituras = todasEstacoes.SelectMany(e => e.Leituras).ToList();
            var leiturasRe = todasLeituras.Where(l => l.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = todasLeituras.Where(l => l.Tipo == TipoLeitura.Irradiacao).ToList();

            // NÃ“ PREDICADO: ExtraÃ§Ã£o ordenada restrita Ã  topologia do engenheiro (Garantia do Iterator GoF)
            var leiturasPoligonal = new List<LeituraEstacaoTotal>();
            var estacoesOrigem = VincularEstacoesDeOrigem(todasEstacoes, sequenciaEstacoes);
            for (int i = 0; i < sequenciaEstacoes.Count - 1; i++)
            {
                string estacaoDe = sequenciaEstacoes[i];
                string estacaoPara = sequenciaEstacoes[i + 1];

                var estacaoOrigem = estacoesOrigem[i];
                var leituraAlvo = estacaoOrigem?.Leituras.FirstOrDefault(l =>
                    l.Tipo == TipoLeitura.Poligonal &&
                    string.Equals(l.PontoVisado, estacaoPara, StringComparison.OrdinalIgnoreCase));

                if (leituraAlvo != null)
                {
                    leiturasPoligonal.Add(leituraAlvo);
                }
                else
                {
                    throw new DadosInsuficientesException($"Ruptura Topológica: Não há visada registrada de '{estacaoDe}' para '{estacaoPara}'. Verifique a caderneta.");
                }
            }

            var poligonalBrutaLocal = CalcularMalhaBruta(pontoPartida, leiturasPoligonal);

            double minX = poligonalBrutaLocal.Min(p => p.X);
            double maxX = poligonalBrutaLocal.Max(p => p.X);
            double minY = poligonalBrutaLocal.Min(p => p.Y);
            double maxY = poligonalBrutaLocal.Max(p => p.Y);

            // eixos de Bounding Box
            double dimensaoX = maxX - minX;
            double dimensaoY = maxY - minY;

            //Diagonal Euclidiana max da area levantada
            double diagonalPlanoMetros = Math.Sqrt((dimensaoX * dimensaoX) + (dimensaoY * dimensaoY));
            double diagonalPlanoKm = diagonalPlanoMetros / 1000.0;

            //Trava de seguranca
            if (diagonalPlanoKm > 35.0)
            {
                throw new NotSupportedException(
                $"OPERAÃ‡ÃƒO ABORTADA: A dimensÃ£o mÃ¡xima deste levantamento ({diagonalPlanoKm:F2} km) " +
                $"excede o limite fÃ­sico de 35 km do plano topogrÃ¡fico. " +
                $"Para distÃ¢ncias maiores, a Topografia clÃ¡ssica nÃ£o garante a exatidÃ£o e " +
                $"Ã© estritamente necessÃ¡rio utilizar cÃ¡lculos e reduÃ§Ãµes geodÃ©sicas."
    );
            }

            double perimetro = 0;
            if (poligonalBrutaLocal.Count > 1)
            {
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }
            }

            // CenÃ¡rio ABERTO: sem compensaÃ§Ã£o (se quiser eliminar este `if`, crie uma Strategy para Aberta e dÃª suporte na Factory)
            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.AbertaOrientada)
            {
                var irradiacoesAberta = OrquestrarCalculoIrradiacoes(poligonalBrutaLocal, todasEstacoes, pontosConhecidos, azimuteInicial);

                return new ResultadoLevantamento
                {
                    PoligonalBruta = poligonalBrutaLocal,
                    Poligonal = poligonalBrutaLocal,
                    Irradiacoes = irradiacoesAberta,
                    TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                    PoligonalFechada = false,
                    Perimetro = perimetro,
                    ErroFechamentoX = 0,
                    ErroFechamentoY = 0,
                    ErroFechamentoZ = 0,
                    ErroFechamentoLinearXY = 0,
                    PrecisaoBruta = 0,
                    ErroLinear = 0,
                    Precisao = 0
                };
            }

            double anguloFechamento = 0;

            // CÃ¡lculo neutro: Strategy decide como interpretar.
            // Para Fechada: RÃ© inicial + Ãºltima leitura no mesmo visado (como era).
            // Para Enquadrada: pode usar a Ãºltima RÃ© (como era).
            string nomeEstacaoInicial = leiturasPoligonal.FirstOrDefault()?.EstacaoOcupada ?? pontoPartida.Nome;
            var reInicial = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == nomeEstacaoInicial);
            if (reInicial != null)
            {
                string nomePontoReInicial = reInicial.PontoVisado;
                var leituraFechamento = todasLeituras
                    .Where(r => r.EstacaoOcupada == nomeEstacaoInicial && r.PontoVisado.Equals(nomePontoReInicial, StringComparison.OrdinalIgnoreCase))
                    .LastOrDefault();

                anguloFechamento = leituraFechamento?.AnguloHorizontal
                    ?? leiturasRe.LastOrDefault()?.AnguloHorizontal
                    ?? 0;
            }
            else
            {
                anguloFechamento = leiturasRe.LastOrDefault()?.AnguloHorizontal ?? 0;
            }

            var entrada = new CompensacaoPoligonalInputDTO
            {
                Metadados = metadadosAtuais,
                PontoPartida = pontoPartida,
                PontoChegada = pontoPartida,
                AzimuteInicial = pontoPartida.AzimuteChegada,
                AzimuteChegada = metadadosAtuais.AzimuteChegada,
                AnguloFechamento = anguloFechamento,
                Leituras = leiturasPoligonal,
                PoligonalBruta = poligonalBrutaLocal
            };

            var estrategia = _compensacaoStrategyFactory.Criar(metadadosAtuais.TipoCenario);
            var compensacao = estrategia.Compensar(entrada);

            var poligonalCompensadaLocal = compensacao.PoligonalCompensada != null ? compensacao.PoligonalCompensada.ToList() : new List<PontoCoordenada>();
            var alertasLocal = new List<string>();
            if (!compensacao.AprovadoNorma) alertasLocal.Add(compensacao.AlertaReprovacao);

            var irradiacoesLocal = OrquestrarCalculoIrradiacoes(poligonalCompensadaLocal, todasEstacoes, pontosConhecidos, azimuteInicial);

            return new ResultadoLevantamento
            {
                PoligonalBruta = poligonalBrutaLocal,
                Poligonal = poligonalCompensadaLocal,
                Irradiacoes = irradiacoesLocal,
                Alertas = alertasLocal,
                AprovadoNorma = compensacao.AprovadoNorma,
                ErroAngular = compensacao.ErroAngular,
                ErroLinear = compensacao.ErroLinearTotal,
                Precisao = compensacao.PrecisaoRelativa,
                ErroFechamentoX = compensacao.ErroX,
                ErroFechamentoY = compensacao.ErroY,
                ErroFechamentoZ = compensacao.ErroAltimetrico,
                Perimetro = perimetro,
                TipoCenario = metadadosAtuais.TipoCenario
            };
        }

        private bool ProcessarFechada(ResultadoLevantamento resultado, List<PontoCoordenada> poligonalBruta, PontoCoordenada pontoPartida, double perimetro)
        {
            if (poligonalBruta.Count <= 1) return false;
            var pontoChegada = poligonalBruta.Last();

            bool fechouPorNome = pontoChegada.Nome.Equals(pontoPartida.Nome, StringComparison.OrdinalIgnoreCase);
            double dx = pontoChegada.X - pontoPartida.X;
            double dy = pontoChegada.Y - pontoPartida.Y;
            double distanciaFechamento = Math.Sqrt(dx * dx + dy * dy);

            bool fechouPorCoordenada = distanciaFechamento <= ToleranciaFechamento;

            bool fechou = fechouPorNome || fechouPorCoordenada;

            var erros = GeometriaTopograficaHelper.CalcularErroFechamento(pontoChegada, pontoPartida, perimetro);

            System.Diagnostics.Debug.WriteLine(
                $"[Fechamento Bruto] fx={erros.erroX:F4} fy={erros.erroY:F4} fz={pontoChegada.Z - pontoPartida.Z:F4} " +
                $"fLinearXY={erros.erroLinearTotal:F4} precisao={erros.precisaoRelativa:F4} " +
                $"fechou={fechou} (nome={fechouPorNome}, coord={fechouPorCoordenada}, dist={distanciaFechamento:F4})");

            return fechou;
        }

        private IReadOnlyList<PontoCoordenada> OrquestrarCalculoIrradiacoes(IReadOnlyList<PontoCoordenada> poligonal, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada>? pontosConhecidos, double azimuteInicial)
        {
            var visitante = new CalculoIrradiacaoVisitor(poligonal.ToList(), pontosConhecidos, azimuteInicial);

            foreach (var estacao in todasEstacoes)
            {
                estacao.Accept(visitante);
            }

            return visitante.IrradiacoesCalculadas;
        }

        private static List<Estacao?> VincularEstacoesDeOrigem(List<Estacao> todasEstacoes, IReadOnlyList<string> sequencia)
        {
            var usadasPorNome = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var origemPorAresta = new List<Estacao?>();

            for (int i = 0; i < sequencia.Count - 1; i++)
            {
                string nomeOrigem = sequencia[i].Trim();
                usadasPorNome.TryGetValue(nomeOrigem, out int usadas);

                var estacao = todasEstacoes
                    .Where(e => string.Equals(e.Nome, nomeOrigem, StringComparison.OrdinalIgnoreCase))
                    .Skip(usadas)
                    .FirstOrDefault();

                origemPorAresta.Add(estacao);
                usadasPorNome[nomeOrigem] = usadas + 1;
            }

            return origemPorAresta;
        }

        public ResultadoLevantamento GerarEsbocoBruto(MetadadosCenario metadadosAtuais, List<Estacao> todasEstacoes)
        {
            if (metadadosAtuais == null || todasEstacoes == null || todasEstacoes.Count == 0)
                return new ResultadoLevantamento { AprovadoNorma = false };

            if (metadadosAtuais.SequenciaEstacoesSelecionadas == null || metadadosAtuais.SequenciaEstacoesSelecionadas.Count < 2)
                return new ResultadoLevantamento { AprovadoNorma = false };

            var poligonalBrutaLocal = new List<PontoCoordenada>();

            try
            {
                var sequenciaEstacoes = metadadosAtuais.SequenciaEstacoesSelecionadas
                    .Where(nome => !string.IsNullOrWhiteSpace(nome))
                    .Select(nome => nome.Trim())
                    .ToList();

                if (sequenciaEstacoes.Count < 2) return new ResultadoLevantamento { AprovadoNorma = false };

                double azimuteInicial = metadadosAtuais.UsarCoordenadaRe
                    ? GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(metadadosAtuais.PartidaX, metadadosAtuais.PartidaY, metadadosAtuais.ReX, metadadosAtuais.ReY)
                    : metadadosAtuais.AzimutePartida;

                var pontoPartida = new PontoCoordenada
                {
                    Nome = sequenciaEstacoes[0],
                    X = metadadosAtuais.PartidaX,
                    Y = metadadosAtuais.PartidaY,
                    Z = metadadosAtuais.PartidaZ,
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimuteInicial
                };

                _classificadorGrafo.ClassificarArestasGrafo(todasEstacoes, metadadosAtuais);

                var leiturasPoligonal = new List<LeituraEstacaoTotal>();
                var estacoesOrigem = VincularEstacoesDeOrigem(todasEstacoes, sequenciaEstacoes);
                for (int i = 0; i < sequenciaEstacoes.Count - 1; i++)
                {
                    string estacaoPara = sequenciaEstacoes[i + 1];
                    var estacaoOrigem = estacoesOrigem[i];
                    var leituraAlvo = estacaoOrigem?.Leituras.FirstOrDefault(l =>
                        l.Tipo == TipoLeitura.Poligonal &&
                        string.Equals(l.PontoVisado, estacaoPara, StringComparison.OrdinalIgnoreCase));

                    if (leituraAlvo != null)
                        leiturasPoligonal.Add(leituraAlvo);
                    else
                        break; // Interrompe silenciosamente para desenhar até o ponto onde há ruptura
                }

                poligonalBrutaLocal = CalcularMalhaBruta(pontoPartida, leiturasPoligonal);
            }
            catch
            {
                // Falha silenciosa para manter a tela limpa ou parcial
            }

            return new ResultadoLevantamento
            {
                PoligonalBruta = poligonalBrutaLocal,
                AprovadoNorma = false
            };
        }

        private static List<PontoCoordenada> CalcularMalhaBruta(PontoCoordenada pontoPartida, List<LeituraEstacaoTotal> leiturasPoligonal)
        {
            var poligonalBruta = new List<PontoCoordenada>
            {
                new PontoCoordenada
                {
                    Nome = pontoPartida.Nome,
                    X = pontoPartida.X,
                    Y = pontoPartida.Y,
                    Z = pontoPartida.Z,
                    EhPontoPoligonal = true,
                    AzimuteChegada = pontoPartida.AzimuteChegada
                }
            };

            double azimuteVigente = pontoPartida.AzimuteChegada;

            foreach (var leitura in leiturasPoligonal)
            {
                double dh = GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                double dn = GeometriaTopograficaHelper.CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);

                azimuteVigente = GeometriaTopograficaHelper.Normalizar360(azimuteVigente + leitura.AnguloHorizontal);
                var (dx, dy) = GeometriaTopograficaHelper.CalcularProjecao(dh, azimuteVigente);

                var pontoAnterior = poligonalBruta[^1];
                poligonalBruta.Add(new PontoCoordenada
                {
                    Nome = leitura.PontoVisado,
                    X = pontoAnterior.X + dx,
                    Y = pontoAnterior.Y + dy,
                    Z = pontoAnterior.Z + dn,
                    EhPontoPoligonal = true,
                    AzimuteChegada = azimuteVigente
                });
                azimuteVigente = GeometriaTopograficaHelper.Normalizar360(azimuteVigente + 180);
            }
            return poligonalBruta;
        }
    }
}

