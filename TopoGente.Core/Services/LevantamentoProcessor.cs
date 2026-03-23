using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;
using System.Globalization;
using System.IO;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    public class LevantamentoProcessor : ILevantamentoProcessor
    {
        private readonly CalculoTopograficoService _calculoService;

        private readonly IClassificadorGrafo _classificadorGrafo;

        private const double ToleranciaFechamento = 0.05; // 5 cm por km

        public LevantamentoProcessor(IClassificadorGrafo classificadorGrafo)
        {
            _calculoService = new CalculoTopograficoService();
            _classificadorGrafo = classificadorGrafo;
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
        private List<PontoCoordenada> OrquestrarCalculoPoligonal(PontoCoordenada pontoPartida,double azimuteInicial, List<Estacao> todasEstacoes)
        {
            ITopografiaIterator iterator = new CaminhamentoPoligonalIterator(todasEstacoes,pontoPartida.Nome);

            var visitante = new CalculoPoligonalVisitor(pontoPartida,azimuteInicial,_calculoService);

            // Execução do Double-Dispatch 
            for (iterator.First();!iterator.IsDone();iterator.Next())
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
                ? _calculoService.CalcularAzimutePorCoordenadas(metadadosAtuais.PartidaX, metadadosAtuais.PartidaY, metadadosAtuais.ReX, metadadosAtuais.ReY)
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

            var todasLeituras = todasEstacoes.SelectMany(e => e.Leituras).ToList();
            var leiturasPoligonal = todasLeituras.Where(l => l.Tipo == TipoLeitura.Poligonal).ToList();
            var leiturasRe = todasLeituras.Where(l => l.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = todasLeituras.Where(l => l.Tipo == TipoLeitura.Irradiacao).ToList();


            double perimetro = 0;
            if (poligonalBruta != null && poligonalBruta.Count > 1)
            {
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += _calculoService.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }
            }
            resultado.Perimetro = perimetro;

            double anguloFechamento = 0;

            switch (metadadosAtuais.TipoCenario)
            {
                case TipoCenarioPoligonal.Fechada:
                    resultado.PoligonalFechada = true;
                    resultado.TipoCenario = TipoCenarioPoligonal.Fechada;

                    bool fechou = ProcessarFechada(resultado, poligonalBruta, PontoPartida, perimetro);

                    string nomeEstcaoInicial = leiturasPoligonal.FirstOrDefault()?.EstacaoOcupada ?? PontoPartida.Nome;
                    var reInicial = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == nomeEstcaoInicial);

                    if (reInicial == null)
                        throw new DadosInsuficientesException($"Poligonal fechada exige leitura de Ré inicial na estação '{PontoPartida.Nome}'.");

                    string nomePontoReInicial = reInicial.PontoVisado;
                    var leituraFechamento = leiturasRe.Where(r => r.EstacaoOcupada == nomeEstcaoInicial && r.PontoVisado.Equals(nomePontoReInicial, StringComparison.OrdinalIgnoreCase)).LastOrDefault();

                    anguloFechamento = leituraFechamento?.AnguloHorizontal ?? 0;

                    resultado.Poligonal = _calculoService.CompensarPoligonal(PontoPartida, PontoPartida, PontoPartida.AzimuteChegada, PontoPartida.AzimuteChegada,
                        leiturasPoligonal, poligonalBruta, metadadosAtuais.TipoCenario, anguloFechamento, out double ea, out double erroX, out double erroY, out double erroLinearT, out double precisaoRelativa, out double erroAltimetrico);

                    resultado.ErroAngular = ea; resultado.ErroLinear = erroLinearT; resultado.Precisao = precisaoRelativa;
                    resultado.ErroFechamentoX = erroX; resultado.ErroFechamentoY = erroY; resultado.ErroFechamentoZ = erroAltimetrico;
                    break;

                case TipoCenarioPoligonal.Enquadrada:
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

                    resultado.Poligonal = _calculoService.CompensarPoligonal(PontoPartida, pontoChegadaConhecido, PontoPartida.AzimuteChegada, metadadosAtuais.AzimuteChegada, leiturasPoligonal, poligonalBruta,
                        metadadosAtuais.TipoCenario, anguloFechamento, out double eaEnq, out double erroXEnq, out double erroYEnq, out double erroLinearEnq, out double precisaoRelativaEnq, out double erroAltimetricoEnq);

                    resultado.ErroAngular = eaEnq; resultado.ErroFechamentoX = erroXEnq; resultado.ErroFechamentoY = erroYEnq;
                    resultado.ErroLinear = erroLinearEnq; resultado.ErroFechamentoZ = erroAltimetricoEnq; resultado.Precisao = precisaoRelativaEnq;
                    break;

                case TipoCenarioPoligonal.AbertaOrientada:
                    resultado.TipoCenario = TipoCenarioPoligonal.AbertaOrientada;
                    resultado.PoligonalFechada = false;
                    ProcessarAberta(resultado, poligonalBruta);
                    break;
            }

            OrquestrarCalculoIrradiacoes(resultado, todasEstacoes, pontosConhecidos, azimuteInicial);

            return resultado;

        }

        private void OrquestrarCalculoIrradiacoes(ResultadoLevantamento resultado , List<Estacao> todasEstacoes,Dictionary<string,PontoCoordenada>? pontosConhecidos, double azimuteInicial)
        {
            var visitante = new CalculoIrradiacaoVisitor(resultado.Poligonal,pontosConhecidos,azimuteInicial,_calculoService);

            foreach (var estacao in todasEstacoes)
            {
                estacao.Accept(visitante);
            }

            resultado.Irradiacoes = visitante.IrradiacoesCalculadas;
        }


        public bool ProcessarFechada(ResultadoLevantamento resultado, List<PontoCoordenada> poligonalBruta, PontoCoordenada pontoPartida, double perimetro)
        {
            if (poligonalBruta.Count <= 1) return false;
            var pontoChegada = poligonalBruta.Last();

            bool fechouPorNome = pontoChegada.Nome.Equals(pontoPartida.Nome, StringComparison.OrdinalIgnoreCase);
            double dx = pontoChegada.X - pontoPartida.X;
            double dy = pontoChegada.Y - pontoPartida.Y;
            double distanciaFechamento = Math.Sqrt(dx * dx + dy * dy);

            bool fechouPorCoordenada = distanciaFechamento <= ToleranciaFechamento;

            bool fechou = fechouPorNome || fechouPorCoordenada;

            // Calcular erros brutos (antes de compensar)
            var erros = _calculoService.CalcularErroFechamento(pontoChegada, pontoPartida, perimetro);

            System.Diagnostics.Debug.WriteLine(
                $"[Fechamento Bruto] fx={erros.erroX:F4} fy={erros.erroY:F4} fz={pontoChegada.Z - pontoPartida.Z:F4} " +
                $"fLinearXY={erros.erroLinearTotal:F4} precisao={erros.precisaoRelativa:F4} " +
                $"fechou={fechou} (nome={fechouPorNome}, coord={fechouPorCoordenada}, dist={distanciaFechamento:F4})");


            return fechou;

        }

        public void ProcessarAberta(ResultadoLevantamento resultado, List<PontoCoordenada> poligonalBruta)
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



        private double ResolverAzimuteOrientacao(
            PontoCoordenada estacao,
            List<LeituraEstacaoTotal> leiturasRe,
            List<PontoCoordenada> poligonal,
            Dictionary<string, PontoCoordenada>? pontosConhecidos,
            double azimuteInicial)
        {
            var leituraRe = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == estacao.Nome);
            if (leituraRe != null)
            {
                PontoCoordenada? pontoReCoord = null;

                if (pontosConhecidos != null && pontosConhecidos.TryGetValue(leituraRe.PontoVisado, out var pk))
                {
                    pontoReCoord = pk;
                }
                else
                {
                    // ponto na poligonal
                    pontoReCoord = poligonal.FirstOrDefault(p =>
                    p.Nome.Equals(leituraRe.PontoVisado, StringComparison.OrdinalIgnoreCase));
                }
                if (pontoReCoord != null)
                {
                    return _calculoService.CalcularAzimutePorCoordenadas(
                        estacao.X, estacao.Y,
                        pontoReCoord.X, pontoReCoord.Y
                    );
                }
            }
            return estacao == poligonal.First()
                            ? azimuteInicial
                            : (estacao.AzimuteChegada < 180
                                ? estacao.AzimuteChegada + 180
                                : estacao.AzimuteChegada - 180);
        }



    }

    public class ResultadoLevantamento
    {
        /// <summary>
        /// Poligonal calculada diretamente a partir das leituras (sem ajuste).
        /// </summary>
        public List<PontoCoordenada> PoligonalBruta { get; set; } = new List<PontoCoordenada>();

        /// <summary>
        /// Poligonal utilizada no resultado (ajustada se fechou, senão bruta).
        /// </summary>
        public List<PontoCoordenada> Poligonal { get; set; } = new List<PontoCoordenada>();

        public List<PontoCoordenada> Irradiacoes { get; set; } = new List<PontoCoordenada>();

        public List<PontoCoordenada> TodosOsPontos => Poligonal.Concat(Irradiacoes).ToList();

        public bool PoligonalFechada { get; set; }

        public double Perimetro { get; set; }

        /// <summary>
        /// Erro linear planimétrico (XY) que estava sendo exposto (mantido).
        /// </summary>
        public double ErroLinear { get; set; }

        /// <summary>
        /// Precisão (1:M) que estava sendo exposta (mantida).
        /// </summary>
        public double Precisao { get; set; }

        /// <summary>
        /// Erros de fechamento BRUTOS (antes do ajuste).
        /// </summary>
        public double ErroFechamentoX { get; set; }
        public double ErroFechamentoY { get; set; }
        public double ErroFechamentoZ { get; set; }

        public double ErroFechamentoLinearXY { get; set; }

        /// <summary>
        /// Precisão calculada a partir do fechamento bruto (antes do ajuste).
        /// Normalmente igual a 'Precisao' quando fechou, mas fica disponível mesmo se não ajustar.
        /// </summary>
        public double PrecisaoBruta { get; set; }

        public double ErroAngular { get; set; }

        public TipoCenarioPoligonal TipoCenario { get; set; }
    }
}
