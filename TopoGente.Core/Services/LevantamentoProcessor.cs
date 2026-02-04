using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services
{
    public class LevantamentoProcessor
    {
        private readonly CalculoTopograficoService _calculoService;

        private const double ToleranciaFechamento = 0.05; // 5 cm por km

        public LevantamentoProcessor()
        {
            _calculoService = new CalculoTopograficoService();
        }

        /// <summary>
        /// Sobrecarga para processar quando temos Coordenada de Ré em vez de Azimute
        /// </summary>
        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, PontoCoordenada pontoRe, List<LeituraEstacaoTotal> leiturasBrutas, List<LeituraEstacaoTotal> leiturasBruas)
        {
            double azimuteInicialCalculado = _calculoService.CalcularAzimutePorCoordenadas(
                pontoPartida.X, pontoPartida.Y,
                pontoRe.X, pontoRe.Y
            );

            return Processar(pontoPartida, azimuteInicialCalculado, leiturasBrutas);
        }

        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial,List<LeituraEstacaoTotal> leiturasBrutas)
            => Processar(pontoPartida, azimuteInicial, leiturasBrutas,pontosConhecidos:null);

        /// <summary>
        /// Método principal que processa a caderneta dado um Azimute Inicial conhecido.
        /// </summary>
        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial, List<LeituraEstacaoTotal> leiturasBrutas,Dictionary<string,PontoCoordenada>? pontosConhecidos)
        {
            var resultado = new ResultadoLevantamento();
            // evitar re ser considerada irradiacao ou poligonal
            var leiturasPoligonal = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Poligonal).ToList();
            var leiturasRe = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Irradiacao).ToList();

            // Calculamos primeiro como se não houvesse erro
            var poligonalBruta = _calculoService.CalcularPoligonal(pontoPartida, azimuteInicial, leiturasPoligonal);

            bool fechou = false;
            double erroX = 0, erroY = 0;
            double perimetro = 0;

            if (poligonalBruta.Count > 1)
            {
                foreach (var leitura in leiturasPoligonal)
                {
                    perimetro += _calculoService.CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);
                }
                resultado.Perimetro = perimetro;

                var pontoChegada = poligonalBruta.Last();
                bool fechouPorNome = pontoChegada.Nome.Equals(pontoPartida.Nome, StringComparison.OrdinalIgnoreCase);

                double dx = pontoChegada.X - pontoPartida.X;
                double dy = pontoChegada.Y - pontoPartida.Y;
                double distanciaFechamento = Math.Sqrt(dx * dx + dy * dy);

                bool fechouPorCoordenada = distanciaFechamento <= ToleranciaFechamento;

                fechou = fechouPorNome || fechouPorCoordenada;

                if (fechou)
                {
                    // Calcula os erros brutos
                    var erros = _calculoService.CalcularErroFechamento(pontoChegada, pontoPartida, perimetro);

                    resultado.PoligonalFechada = true;
                    resultado.ErroLinear = erros.erroLinearTotal;
                    resultado.Precisao = erros.precisaoRelativa;

                    erroX = erros.erroX;
                    erroY = erros.erroY;

                    // Substituímos a poligonal bruta pela ajustada
                    resultado.Poligonal = _calculoService.CompensarPoligonal(poligonalBruta, erroX, erroY, perimetro);
                }
                else
                {
                    // Aberta usa a bruta
                    resultado.Poligonal = poligonalBruta;
                    resultado.PoligonalFechada = false;
                }
            }
            else
            {
                // caso base com apenas 1 ponto (início)
                resultado.Poligonal = poligonalBruta;
            }

            //  Calcular irradiações apos o ajuste da poligonal
            foreach (var estacao in resultado.Poligonal)
            {
                var irradiacoesDestaEstacao = leiturasIrradiadas
                    .Where(l => l.EstacaoOcupada == estacao.Nome)
                    .ToList();

                if (!irradiacoesDestaEstacao.Any()) continue;

                double azimuteOrientacao;
                
                // Tenta por Ré Real
                var leituraRe = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == estacao.Nome);
                if (leituraRe != null)
                {
                    PontoCoordenada? pontoReCoord = null;

                    // ponto conhecido
                    if (pontosConhecidos != null && pontosConhecidos.TryGetValue(leituraRe.PontoVisado, out var pk))
                    {
                        pontoReCoord = pk;
                    }
                    else
                    {
                        // ponto na poligonal
                        pontoReCoord = resultado.Poligonal.FirstOrDefault(p =>
                        p.Nome.Equals(leituraRe.PontoVisado, StringComparison.OrdinalIgnoreCase));
                    }
                    if (pontoReCoord != null)
                    {
                        azimuteOrientacao = _calculoService.CalcularAzimutePorCoordenadas(
                            estacao.X, estacao.Y,
                            pontoReCoord.X, pontoReCoord.Y
                        );
                    }
                    else
                    {
                        // regra anterior
                        azimuteOrientacao = estacao == resultado.Poligonal.First()
                            ? azimuteInicial
                            : (estacao.AzimuteChegada < 180
                                ? estacao.AzimuteChegada + 180
                                : estacao.AzimuteChegada - 180);
                    }
                }
                else
                {
                    // regra atual
                    azimuteOrientacao = estacao == resultado.Poligonal.First() ? azimuteInicial :
                        (estacao.AzimuteChegada < 180
                            ? estacao.AzimuteChegada + 180
                            : estacao.AzimuteChegada - 180);
                }
                foreach (var leitura in irradiacoesDestaEstacao)
                {
                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacao, leitura, azimuteOrientacao);
                    resultado.Irradiacoes.Add(pontoIrradiado);
                }
            }

            return resultado;
        }

    public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial,List<Estacao> estacoesOrganizadas,Dictionary<string,PontoCoordenada>? pontosConhecidos)
        {
            estacoesOrganizadas ??= new List<Estacao>();

            foreach (var e in estacoesOrganizadas)
            {
                e.PontosCalculados = new List<PontoCoordenada>();
            }
            var leiturasBrutas = estacoesOrganizadas
                .SelectMany(e => e.Leituras ?? new List<LeituraEstacaoTotal>()).ToList();

            var resultado = Processar(pontoPartida, azimuteInicial, leiturasBrutas, pontosConhecidos);

            // indice da poligonal 
            var poligonalPorNome = resultado.Poligonal
                .GroupBy(p => p.Nome ,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(G => G.Key, G => G.Last(), StringComparer.OrdinalIgnoreCase);

            // para cada estacao, adiciona o porpio ponto e as irradiacoes estaoOcupada == nome estacao
            foreach (var estacao in estacoesOrganizadas)
            {
                if (poligonalPorNome.TryGetValue(estacao.Nome, out var pontoDaEstacao))
                {
                    estacao.PontosCalculados.Add(pontoDaEstacao);
                }

                var irradiacoes = leiturasBrutas
                    .Where(l => l.Tipo == TipoLeitura.Irradiacao && l.EstacaoOcupada.Equals(estacao.Nome, StringComparison.OrdinalIgnoreCase))
                    .Select(l => {
                        if (!poligonalPorNome.TryGetValue(estacao.Nome, out var pEst)) return null;

                        var az = pEst == resultado.Poligonal.First() ? azimuteInicial :
                            (pEst.AzimuteChegada < 180
                                ? pEst.AzimuteChegada + 180
                                : pEst.AzimuteChegada - 180);
                        return _calculoService.CalcularPontoIrradiado(pEst, l, az);
                    }).Where(p => p != null).Select(p => p!).ToList();

                estacao.PontosCalculados.AddRange(irradiacoes);
            }
                return resultado;
            }
    }            

    public class ResultadoLevantamento
    {
        public List<PontoCoordenada> Poligonal { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> Irradiacoes { get; set; } = new List<PontoCoordenada>();
        public List<PontoCoordenada> TodosOsPontos => Poligonal.Concat(Irradiacoes).ToList();
        public bool PoligonalFechada {  get; set; }
        public double Perimetro { get; set; }
        public double ErroLinear { get; set; }
        public double Precisao { get; set; }

    }
}