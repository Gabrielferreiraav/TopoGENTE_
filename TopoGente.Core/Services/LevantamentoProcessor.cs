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
        /// Coordenadas (0,0,0) são aceitas apenas se explicitamente fornecidas
        /// </summary>
        private static void ValidarCoordenadasPartida(PontoCoordenada pontoPartida)
        {
            if (pontoPartida == null)
            {
                throw new DadosInsuficientesException(
                    "Levantamentos fechados exigem coordenadas de partida e chegada reais para cálculo de fechamento.");
            }
        }


        // CLIENTE ORQUESTRADOR: Isola o controle do fluxo (Iterator + Visitor)
        private List<PontoCoordenada> OrquestrarCalculoPoligonal(PontoCoordenada pontoPartida, double azimuteInicial, List<Estacao> todasEstacoes)
        {
            ITopografiaIterator iterator = new CaminhamentoPoligonalIterator(todasEstacoes, pontoPartida.Nome);

            var visitante = new CalculoPoligonalVisitor(pontoPartida, azimuteInicial);

            for (iterator.First(); !iterator.IsDone(); iterator.Next())
            {
                Estacao noAtual = iterator.CurrentItem();
                noAtual.Accept(visitante);
            }

            return visitante.PontosCalculados;
        }

        public ResultadoLevantamento Processar(
            MetadadosCenario metadadosAtuais, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            if (metadadosAtuais == null)
            {
                throw new DadosInsuficientesException("Dados Inciais não foram prrenchidos.");
            }

            var resultado = new ResultadoLevantamento();

            double azimuteInicial = metadadosAtuais.UsarCoordenadaRe
                ? GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(metadadosAtuais.PartidaX, metadadosAtuais.PartidaY, metadadosAtuais.ReX, metadadosAtuais.ReY)
                : metadadosAtuais.AzimutePartida;

            string nomePontoIncial = todasEstacoes.FirstOrDefault()?.Nome ?? "Partida";

            var PontoPartida = new PontoCoordenada
            {
                Nome = nomePontoIncial,
                X = metadadosAtuais.PartidaX,
                Y = metadadosAtuais.PartidaY,
                Z = metadadosAtuais.PartidaZ,
                EhPontoPoligonal = true,
                AzimuteChegada = azimuteInicial
            };

            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Fechada || metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                ValidarCoordenadasPartida(PontoPartida);
            }

            _classificadorGrafo.ClassificarArestasGrafo(todasEstacoes, metadadosAtuais);

            var poligonalBruta = OrquestrarCalculoPoligonal(PontoPartida, azimuteInicial, todasEstacoes);
            resultado.PoligonalBruta = poligonalBruta;

            //Limite do Plano Topográfico (RNF02)

            // Extracao de limites espacias extremos da malha
            double minX = poligonalBruta.Min(p => p.X);
            double maxX = poligonalBruta.Max(p => p.X);
            double minY = poligonalBruta.Min(p => p.Y);
            double maxY = poligonalBruta.Max(p => p.Y);

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
                $"OPERAÇÃO ABORTADA: A dimensão máxima deste levantamento ({diagonalPlanoKm:F2} km) " +
                $"excede o limite físico de 35 km do plano topográfico. " +
                $"Para distâncias maiores, a Topografia clássica não garante a exatidão e " +
                $"é estritamente necessário utilizar cálculos e reduções geodésicas."
    );
            }


            var todasLeituras = todasEstacoes.SelectMany(e => e.Leituras).ToList();
            var leiturasPoligonal = todasLeituras.Where(l => l.Tipo == TipoLeitura.Poligonal).ToList();
            var leiturasRe = todasLeituras.Where(l => l.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = todasLeituras.Where(l => l.Tipo == TipoLeitura.Irradiacao).ToList();

            double perimetro = 0;
            if (poligonalBruta != null && poligonalBruta.Count > 1)
            {
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += GeometriaTopograficaHelper.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }
            }
            resultado.Perimetro = perimetro;

            double anguloFechamento = 0;

            var strategyFactory = _compensacaoStrategyFactory;

            var roteiros = new Dictionary<TipoCenarioPoligonal, Action>
            {
                [TipoCenarioPoligonal.Fechada] = () =>
                {
                    resultado.PoligonalFechada = true;
                    resultado.TipoCenario = TipoCenarioPoligonal.Fechada;

                    bool fechou = ProcessarFechada(resultado, poligonalBruta, PontoPartida, perimetro);

                    string nomeEstcaoInicial = leiturasPoligonal.FirstOrDefault()?.EstacaoOcupada ?? PontoPartida.Nome;
                    var reInicial = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == nomeEstcaoInicial);

                    if (reInicial == null)
                    {
                        throw new DadosInsuficientesException($"Poligonal fechada exige leitura de Ré inicial na estação '{PontoPartida.Nome}'.");
                    }

                    string nomePontoReInicial = reInicial.PontoVisado;
                    var leituraFechamento = leiturasRe
                        .Where(r => r.EstacaoOcupada == nomeEstcaoInicial && r.PontoVisado.Equals(nomePontoReInicial, StringComparison.OrdinalIgnoreCase))
                        .LastOrDefault();

                    anguloFechamento = leituraFechamento?.AnguloHorizontal ?? 0;

                    var entrada = new CompensacaoPoligonalInputDTO
                    {
                        PontoPartida = PontoPartida,
                        PontoChegada = PontoPartida,
                        AzimuteInicial = PontoPartida.AzimuteChegada,
                        AzimuteChegada = PontoPartida.AzimuteChegada,
                        AnguloFechamento = anguloFechamento,
                        Leituras = leiturasPoligonal,
                        PoligonalBruta = poligonalBruta
                    };

                    var estrategia = strategyFactory.Criar(TipoCenarioPoligonal.Fechada);
                    var compensacao = estrategia.Compensar(entrada);

                    resultado.Poligonal = compensacao.PoligonalCompensada;
                    resultado.AprovadoNorma = compensacao.AprovadoNorma;
                    if (!compensacao.AprovadoNorma) resultado.Alertas.Add(compensacao.AlertaReprovacao);

                    resultado.ErroAngular = compensacao.ErroAngular;
                    resultado.ErroLinear = compensacao.ErroLinearTotal;
                    resultado.Precisao = compensacao.PrecisaoRelativa;
                    resultado.ErroFechamentoX = compensacao.ErroX;
                    resultado.ErroFechamentoY = compensacao.ErroY;
                    resultado.ErroFechamentoZ = compensacao.ErroAltimetrico;
                },
                [TipoCenarioPoligonal.Enquadrada] = () =>
                {
                    resultado.TipoCenario = TipoCenarioPoligonal.Enquadrada;
                    resultado.PoligonalFechada = true;

                    var pontoChegadaConhecido = new PontoCoordenada
                    {
                        Nome = "Chegada",
                        X = metadadosAtuais.ChegadaX.Value,
                        Y = metadadosAtuais.ChegadaY.Value,
                        Z = metadadosAtuais.ChegadaZ.Value,
                    };

                    var ultimaLeituraReferencia = leiturasRe.LastOrDefault();
                    if (ultimaLeituraReferencia != null) anguloFechamento = ultimaLeituraReferencia.AnguloHorizontal;

                    var entrada = new CompensacaoPoligonalInputDTO
                    {
                        PontoPartida = PontoPartida,
                        PontoChegada = pontoChegadaConhecido,
                        AzimuteInicial = PontoPartida.AzimuteChegada,
                        AzimuteChegada = metadadosAtuais.AzimuteChegada,
                        AnguloFechamento = anguloFechamento,
                        Leituras = leiturasPoligonal,
                        PoligonalBruta = poligonalBruta
                    };

                    var estrategia = strategyFactory.Criar(TipoCenarioPoligonal.Enquadrada);
                    var compensacao = estrategia.Compensar(entrada);

                    resultado.Poligonal = compensacao.PoligonalCompensada;
                    resultado.ErroAngular = compensacao.ErroAngular;
                    resultado.ErroFechamentoX = compensacao.ErroX;
                    resultado.ErroFechamentoY = compensacao.ErroY;
                    resultado.ErroLinear = compensacao.ErroLinearTotal;
                    resultado.ErroFechamentoZ = compensacao.ErroAltimetrico;
                    resultado.Precisao = compensacao.PrecisaoRelativa;

                    resultado.AprovadoNorma = compensacao.AprovadoNorma;
                    if (!compensacao.AprovadoNorma) resultado.Alertas.Add(compensacao.AlertaReprovacao);
                },
                [TipoCenarioPoligonal.AbertaOrientada] = () =>
                {
                    resultado.TipoCenario = TipoCenarioPoligonal.AbertaOrientada;
                    resultado.PoligonalFechada = false;
                    ProcessarAberta(resultado, poligonalBruta);
                }
            };

            roteiros[metadadosAtuais.TipoCenario]();

            OrquestrarCalculoIrradiacoes(resultado, todasEstacoes, pontosConhecidos, azimuteInicial);

            return resultado;
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

        private void ProcessarAberta(ResultadoLevantamento resultado, List<PontoCoordenada> poligonalBruta)
        {
            resultado.Poligonal = poligonalBruta;
            resultado.ErroFechamentoX = 0;
            resultado.ErroFechamentoY = 0;
            resultado.ErroFechamentoZ = 0;
            resultado.ErroFechamentoLinearXY = 0;
            resultado.PrecisaoBruta = 0;
            resultado.ErroLinear = 0;
            resultado.Precisao = 0;
        }

        private void OrquestrarCalculoIrradiacoes(ResultadoLevantamento resultado, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada>? pontosConhecidos, double azimuteInicial)
        {
            var visitante = new CalculoIrradiacaoVisitor(resultado.Poligonal, pontosConhecidos, azimuteInicial);

            foreach (var estacao in todasEstacoes)
            {
                estacao.Accept(visitante);
            }

            resultado.Irradiacoes = visitante.IrradiacoesCalculadas;
}
    }
}
