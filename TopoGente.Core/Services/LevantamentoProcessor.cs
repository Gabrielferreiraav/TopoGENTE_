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

        public ResultadoLevantamento Processar(List<SequenciaPoligonal> sequencias, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            if (sequencias == null || !sequencias.Any())
            {
                throw new DadosInsuficientesException("Nenhuma sequência poligonal foi informada.");
            }

            var principal = sequencias.FirstOrDefault(s => s.EhPrincipal) ?? sequencias.First();

            pontosConhecidos ??= new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);

            var resultadoPrincipal = ProcessarUnico(principal, todasEstacoes, pontosConhecidos);

            var poligonaisCompensadas = new List<PontoCoordenada>(resultadoPrincipal.Poligonal);
            var poligonaisBrutas = new List<PontoCoordenada>(resultadoPrincipal.PoligonalBruta);

            foreach (var p in resultadoPrincipal.Poligonal)
            {
                pontosConhecidos[p.Nome] = p;
            }

            var secundarias = sequencias.Where(s => s != principal).ToList();
            foreach (var sec in secundarias)
            {
                if (string.IsNullOrWhiteSpace(sec.EstacaoAncoragemNome) || !pontosConhecidos.ContainsKey(sec.EstacaoAncoragemNome))
                {
                    throw new DadosInsuficientesException($"Ruptura Topológica: A estação de ancoragem '{sec.EstacaoAncoragemNome}' para a poligonal secundária '{sec.Nome}' não existe ou não foi compensada na poligonal principal.");
                }

                var ancoragem = pontosConhecidos[sec.EstacaoAncoragemNome];
                sec.Metadados.PartidaX = ancoragem.X;
                sec.Metadados.PartidaY = ancoragem.Y;
                sec.Metadados.PartidaZ = ancoragem.Z;

                var resSec = ProcessarUnico(sec, todasEstacoes, pontosConhecidos);
                poligonaisCompensadas.AddRange(resSec.Poligonal);
                poligonaisBrutas.AddRange(resSec.PoligonalBruta);

                foreach (var p in resSec.Poligonal)
                {
                    pontosConhecidos[p.Nome] = p;
                }
            }

            var irradiacoes = OrquestrarCalculoIrradiacoes(poligonaisCompensadas, todasEstacoes, pontosConhecidos, principal.Metadados.AzimutePartida);

            return new ResultadoLevantamento
            {
                PoligonalBruta = poligonaisBrutas,
                Poligonal = poligonaisCompensadas,
                Irradiacoes = irradiacoes,
                Alertas = resultadoPrincipal.Alertas,
                AprovadoNorma = resultadoPrincipal.AprovadoNorma,
                ErroAngular = resultadoPrincipal.ErroAngular,
                ErroLinear = resultadoPrincipal.ErroLinear,
                Precisao = resultadoPrincipal.Precisao,
                ErroFechamentoX = resultadoPrincipal.ErroFechamentoX,
                ErroFechamentoY = resultadoPrincipal.ErroFechamentoY,
                ErroFechamentoZ = resultadoPrincipal.ErroFechamentoZ,
                Perimetro = resultadoPrincipal.Perimetro,
                TipoCenario = resultadoPrincipal.TipoCenario
            };
        }

        private ResultadoLevantamento ProcessarUnico(SequenciaPoligonal sequencia, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada> pontosConhecidos)
        {
            var metadadosAtuais = sequencia.Metadados;
            
            if (metadadosAtuais == null)
            {
                throw new DadosInsuficientesException("Dados Inciais não foram preenchidos.");
            }

            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
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
                throw new DadosInsuficientesException("Caderneta de estações não foi informada.");
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

            double dimensaoX = maxX - minX;
            double dimensaoY = maxY - minY;

            double diagonalPlanoMetros = Math.Sqrt((dimensaoX * dimensaoX) + (dimensaoY * dimensaoY));
            double diagonalPlanoKm = diagonalPlanoMetros / 1000.0;

            if (diagonalPlanoKm > 35.0)
            {
                throw new NotSupportedException(
                $"OPERAÇÃO ABORTADA: A dimensão máxima deste levantamento ({diagonalPlanoKm:F2} km) " +
                $"excede o limite físico de 35 km do plano topográfico. " +
                $"Para distâncias maiores, a Topografia clássica não garante a exatidão e " +
                $"é estritamente necessário utilizar cálculos e reduções geodésicas."
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

            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.AbertaOrientada)
            {
                return new ResultadoLevantamento
                {
                    PoligonalBruta = poligonalBrutaLocal,
                    Poligonal = poligonalBrutaLocal,
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

            return new ResultadoLevantamento
            {
                PoligonalBruta = poligonalBrutaLocal,
                Poligonal = poligonalCompensadaLocal,
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

        public ResultadoLevantamento GerarEsbocoBruto(List<SequenciaPoligonal> sequencias, List<Estacao> todasEstacoes)
        {
            if (sequencias == null || !sequencias.Any() || todasEstacoes == null || todasEstacoes.Count == 0)
                return new ResultadoLevantamento { AprovadoNorma = false };

            var principal = sequencias.FirstOrDefault(s => s.EhPrincipal);
            if (principal == null || principal.Metadados == null)
                return new ResultadoLevantamento { AprovadoNorma = false };

            var dicionarioBruto = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);

            var poligonaisBrutasTotais = new List<PontoCoordenada>();

            try
            {
                _classificadorGrafo.ClassificarArestasGrafo(todasEstacoes, principal.Metadados);

                // 2. Resolve a Geometria Bruta da Poligonal Principal (Fase 1)
                var seqEstacoesPrincipal = principal.Metadados.SequenciaEstacoesSelecionadas
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();

                var nomePartida = seqEstacoesPrincipal.FirstOrDefault() ?? "E1";

                // 1. Inicializa o ponto de partida da Poligonal Principal
                var pontoPartida = new PontoCoordenada
                {
                    Nome = nomePartida,
                    X = principal.Metadados.PartidaX,
                    Y = principal.Metadados.PartidaY,
                    Z = principal.Metadados.PartidaZ,
                    EhPontoPoligonal = true,
                    AzimuteChegada = principal.Metadados.AzimutePartida,
                    XBruto = principal.Metadados.PartidaX,
                    YBruto = principal.Metadados.PartidaY,
                    ZBruto = principal.Metadados.PartidaZ
                };
                dicionarioBruto[pontoPartida.Nome] = pontoPartida;

                var estacoesOrigemPrin = VincularEstacoesDeOrigem(todasEstacoes, seqEstacoesPrincipal);
                var leiturasPoligonalPrin = new List<LeituraEstacaoTotal>();

                for (int i = 0; i < seqEstacoesPrincipal.Count - 1; i++)
                {
                    string estacaoPara = seqEstacoesPrincipal[i + 1];
                    var estacaoOrigem = estacoesOrigemPrin[i];
                    var leituraAlvo = estacaoOrigem?.Leituras.FirstOrDefault(l =>
                        l.Tipo == TipoLeitura.Poligonal &&
                        string.Equals(l.PontoVisado, estacaoPara, StringComparison.OrdinalIgnoreCase));

                    if (leituraAlvo != null)
                        leiturasPoligonalPrin.Add(leituraAlvo);
                    else
                        break;
                }

                var malhaBrutaPrincipal = CalcularMalhaBruta(pontoPartida, leiturasPoligonalPrin);
                poligonaisBrutasTotais.AddRange(malhaBrutaPrincipal);

                foreach (var p in malhaBrutaPrincipal)
                {
                    dicionarioBruto[p.Nome] = p;
                }

                // 3. Resolve a Geometria Bruta das Poligonais Secundárias/Auxiliares (Fase 2 - DAG)
                var secundarias = sequencias.Where(s => !s.EhPrincipal).ToList();
                foreach (var sec in secundarias)
                {
                    if (string.IsNullOrWhiteSpace(sec.EstacaoAncoragemNome) || !dicionarioBruto.ContainsKey(sec.EstacaoAncoragemNome))
                        continue;

                    var ancoragemBruta = dicionarioBruto[sec.EstacaoAncoragemNome];
                    
                    var pontoPartidaSec = new PontoCoordenada
                    {
                        Nome = ancoragemBruta.Nome,
                        X = ancoragemBruta.X,
                        Y = ancoragemBruta.Y,
                        Z = ancoragemBruta.Z,
                        EhPontoPoligonal = true,
                        AzimuteChegada = sec.Metadados.AzimutePartida,
                        XBruto = ancoragemBruta.X,
                        YBruto = ancoragemBruta.Y,
                        ZBruto = ancoragemBruta.Z
                    };

                    var seqEstacoesSec = sec.Metadados.SequenciaEstacoesSelecionadas
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();

                    var estacoesOrigemSec = VincularEstacoesDeOrigem(todasEstacoes, seqEstacoesSec);
                    var leiturasPoligonalSec = new List<LeituraEstacaoTotal>();

                    for (int i = 0; i < seqEstacoesSec.Count - 1; i++)
                    {
                        string estacaoPara = seqEstacoesSec[i + 1];
                        var estacaoOrigem = estacoesOrigemSec[i];
                        var leituraAlvo = estacaoOrigem?.Leituras.FirstOrDefault(l =>
                            l.Tipo == TipoLeitura.Poligonal &&
                            string.Equals(l.PontoVisado, estacaoPara, StringComparison.OrdinalIgnoreCase));

                        if (leituraAlvo != null)
                            leiturasPoligonalSec.Add(leituraAlvo);
                        else
                            break;
                    }

                    var malhaBrutaSec = CalcularMalhaBruta(pontoPartidaSec, leiturasPoligonalSec);
                    poligonaisBrutasTotais.AddRange(malhaBrutaSec);

                    foreach (var p in malhaBrutaSec)
                    {
                        dicionarioBruto[p.Nome] = p;
                    }
                }

                // 4. Calcula os Erros de Fechamento Brutos da Poligonal Principal para amostragem prévia
                double perimetro = 0;
                foreach (var leitura in leiturasPoligonalPrin)
                {
                    perimetro += GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }

                double erroFechamentoX = 0;
                double erroFechamentoY = 0;
                double erroFechamentoZ = 0;
                double erroLinearTotal = 0;
                double precisaoRelativa = 0;

                if (malhaBrutaPrincipal.Count > 1)
                {
                    var pontoFinalBruto = malhaBrutaPrincipal.Last();
                    if (principal.Metadados.TipoCenario == TipoCenarioPoligonal.Fechada)
                    {
                        erroFechamentoX = pontoFinalBruto.X - pontoPartida.X;
                        erroFechamentoY = pontoFinalBruto.Y - pontoPartida.Y;
                        erroFechamentoZ = pontoFinalBruto.Z - pontoPartida.Z;
                        erroLinearTotal = Math.Sqrt((erroFechamentoX * erroFechamentoX) + (erroFechamentoY * erroFechamentoY));
                        precisaoRelativa = perimetro > 0.0001 ? erroLinearTotal / perimetro : 0;
                    }
                    else if (principal.Metadados.TipoCenario == TipoCenarioPoligonal.Enquadrada && principal.Metadados.ChegadaX.HasValue)
                    {
                        erroFechamentoX = pontoFinalBruto.X - principal.Metadados.ChegadaX.GetValueOrDefault();
                        erroFechamentoY = pontoFinalBruto.Y - principal.Metadados.ChegadaY.GetValueOrDefault();
                        erroFechamentoZ = pontoFinalBruto.Z - principal.Metadados.ChegadaZ.GetValueOrDefault();
                        erroLinearTotal = Math.Sqrt((erroFechamentoX * erroFechamentoX) + (erroFechamentoY * erroFechamentoY));
                        precisaoRelativa = perimetro > 0.0001 ? erroLinearTotal / perimetro : 0;
                    }
                }

                // 5. Calcula as Irradiações Brutas sobre as estacas não compensadas (Fase 3)
                // Passamos o dicionarioBruto para servir como a referência de origem espacial das visadas!
                var irradiacoesBrutas = OrquestrarCalculoIrradiacoes(poligonaisBrutasTotais, todasEstacoes, dicionarioBruto, principal.Metadados.AzimutePartida);

                // Preenche os atributos brutos de cada ponto irradiado para auditoria visual
                foreach (var irr in irradiacoesBrutas)
                {
                    irr.XBruto = irr.X;
                    irr.YBruto = irr.Y;
                    irr.ZBruto = irr.Z;
                }

                return new ResultadoLevantamento
                {
                    PoligonalBruta = poligonaisBrutasTotais,
                    Poligonal = poligonaisBrutasTotais, // No esboço, a malha de desenho assume a malha bruta
                    Irradiacoes = irradiacoesBrutas,
                    ErroFechamentoX = erroFechamentoX,
                    ErroFechamentoY = erroFechamentoY,
                    ErroFechamentoZ = erroFechamentoZ,
                    ErroLinear = erroLinearTotal,
                    Precisao = precisaoRelativa,
                    Perimetro = perimetro,
                    TipoCenario = principal.Metadados.TipoCenario,
                    AprovadoNorma = false, // Indica estado pré-ajustamento
                    EhEsboco = true
                };
            }
            catch
            {
                // Falha graciosa mantendo o retorno vazio seguro
                return new ResultadoLevantamento { AprovadoNorma = false };
            }
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

