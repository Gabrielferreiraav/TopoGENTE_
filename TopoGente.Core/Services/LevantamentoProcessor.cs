using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Entities;
using System.Globalization;
using System.IO;

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
        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, PontoCoordenada pontoRe, List<LeituraEstacaoTotal> leiturasBrutas)
        {
            double azimuteInicialCalculado = _calculoService.CalcularAzimutePorCoordenadas(
                pontoPartida.X, pontoPartida.Y,
                pontoRe.X, pontoRe.Y
            );

            return Processar(pontoPartida, azimuteInicialCalculado, leiturasBrutas);
        }

        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial, List<LeituraEstacaoTotal> leiturasBrutas)
            => Processar(pontoPartida, azimuteInicial, leiturasBrutas, pontosConhecidos: null);

        private static void SalvarSaidaTxt(ResultadoLevantamento resultado)
        {

            var pontos = resultado.TodosOsPontos ?? new List<PontoCoordenada>();
            
            if (pontos.Count == 0)
            {
                return;
            }

            var PastaSaidaTeste = Path.Combine(
                @"C:\Users\gabri\source\repos\TopoGENTE_\TopoGente.Core",
                "Saida_teste");
            Directory.CreateDirectory(PastaSaidaTeste);

            uint state = (uint)Environment.TickCount;
            state ^= state << 13; state ^= state >> 11; state ^= state << 5;

            var arquivo = Path.Combine(PastaSaidaTeste, $"coordenadas_{state}.txt");

            using var writer = new StreamWriter(arquivo, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var ic = CultureInfo.InvariantCulture;

            writer.WriteLine("#TopoGente - Resultado do Levantamento");
            
            // Erros brutos (antes de qualquer ajuste)
            writer.WriteLine("# Fechamento bruto (antes do ajuste):");
            writer.WriteLine($"# fx={resultado.ErroFechamentoX.ToString("F4", ic)}; fy={resultado.ErroFechamentoY.ToString("F4", ic)}; fz={resultado.ErroFechamentoZ.ToString("F4", ic)}");
            writer.WriteLine($"# f_linear_xy={resultado.ErroFechamentoLinearXY.ToString("F4", ic)}; perimetro={resultado.Perimetro.ToString("F3", ic)}; precisao_1_M={(resultado.PrecisaoBruta > 0 ? resultado.PrecisaoBruta.ToString("F2", ic) : "0")}");

            writer.WriteLine("# Nome;X;Y;Z;AzimuteChegada; Tipo Descrição ");
            foreach (var p in pontos)
            {
                writer.WriteLine($"{p.Nome};{p.X.ToString("F3", ic)};{p.Y.ToString("F3", ic)};" +
                                 $"{p.Z.ToString("F3", ic)};{p.AzimuteChegada};{p.TipoDescricao}");
            }
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

            // Impedir coordenadas default arbitrárias (1000, 1000, 100)
            // que mascaram uma poligonal aberta como se fosse fechada
            if (pontoPartida.X == 1000.0 && pontoPartida.Y == 1000.0 && pontoPartida.Z == 100.0)
            {
                throw new DadosInsuficientesException(
                    "Coordenadas de partida (1000, 1000, 100) são valores default e não representam apoio geodésico real. " +
                    "Forneça coordenadas conhecidas.");
            }
        }

        public ResultadoLevantamento Processar(
            PontoCoordenada pontoPartida,
            double azimuteInicial,
            List<LeituraEstacaoTotal> leiturasBrutas,
            Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            ValidarCoordenadasPartida(pontoPartida);

            var resultado = new ResultadoLevantamento();

            var leiturasPoligonal = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Poligonal).ToList();
            var leiturasRe = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Irradiacao).ToList();

            //  Calcula poligonal bruta 
            var poligonalBruta = _calculoService.CalcularPoligonal(pontoPartida, azimuteInicial, leiturasPoligonal);
            resultado.PoligonalBruta = poligonalBruta;

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

                // Calcula e armazena erros brutos (antes de compensar)
                var erros = _calculoService.CalcularErroFechamento(pontoChegada, pontoPartida, perimetro);
                resultado.ErroFechamentoX = erros.erroX;
                resultado.ErroFechamentoY = erros.erroY;
                resultado.ErroFechamentoLinearXY = erros.erroLinearTotal;
                resultado.PrecisaoBruta = erros.precisaoRelativa;

                // Fechamento altimétrico bruto 
                resultado.ErroFechamentoZ = pontoChegada.Z - pontoPartida.Z;

                if (fechou)
                {
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
                resultado.Poligonal = poligonalBruta;

                // Sem fechamento possível, ainda assim preenche erros como zero
                resultado.ErroFechamentoX = 0;
                resultado.ErroFechamentoY = 0;
                resultado.ErroFechamentoZ = 0;
                resultado.ErroFechamentoLinearXY = 0;
                resultado.PrecisaoBruta = 0;
            }

            //  Impedir irradiação duplicada no ponto de fechamento:
            // Se poligonal fechada e último nome == primeiro nome, ignora o último para fins de irradiação.
            IEnumerable<PontoCoordenada> estacoesParaIrradiar = resultado.Poligonal;

            if (resultado.PoligonalFechada && resultado.Poligonal.Count >= 2)
            {
                var primeira = resultado.Poligonal.First();
                var ultima = resultado.Poligonal.Last();

                if (ultima.Nome.Equals(primeira.Nome, StringComparison.OrdinalIgnoreCase))
                {
                    estacoesParaIrradiar = resultado.Poligonal.Take(resultado.Poligonal.Count - 1);
                }
            }

            foreach (var estacao in estacoesParaIrradiar)
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
                        azimuteOrientacao = estacao == resultado.Poligonal.First()
                            ? azimuteInicial
                            : (estacao.AzimuteChegada < 180
                                ? estacao.AzimuteChegada + 180
                                : estacao.AzimuteChegada - 180);
                    }
                }
                else
                {
                    azimuteOrientacao = estacao == resultado.Poligonal.First()
                        ? azimuteInicial
                        : (estacao.AzimuteChegada < 180
                            ? estacao.AzimuteChegada + 180
                            : estacao.AzimuteChegada - 180);
                }

                foreach (var leitura in irradiacoesDestaEstacao)
                {
                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacao, leitura, azimuteOrientacao);
                    resultado.Irradiacoes.Add(pontoIrradiado);
                }
            }

            SalvarSaidaTxt(resultado);
            return resultado;
        }

        public ResultadoLevantamento Processar(PontoCoordenada pontoPartida, double azimuteInicial, List<Estacao> estacoesOrganizadas, Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            estacoesOrganizadas ??= new List<Estacao>();

            foreach (var e in estacoesOrganizadas)
            {
                e.PontosCalculados = new List<PontoCoordenada>();
            }

            var leiturasBrutas = estacoesOrganizadas
                .SelectMany(e => e.Leituras ?? new List<LeituraEstacaoTotal>())
                .ToList();

            var resultado = Processar(pontoPartida, azimuteInicial, leiturasBrutas, pontosConhecidos);

            var poligonalPorNome = resultado.Poligonal
                .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            var irradiacoesPorVisado = resultado.Irradiacoes
                .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new Queue<PontoCoordenada>(g), StringComparer.OrdinalIgnoreCase);

            var irradiacoesPorEstacao = new Dictionary<string, List<PontoCoordenada>>(StringComparer.OrdinalIgnoreCase);

            foreach (var leitura in leiturasBrutas.Where(l => l.Tipo == TipoLeitura.Irradiacao))
            {
                if (!irradiacoesPorVisado.TryGetValue(leitura.PontoVisado, out var fila) || fila.Count == 0)
                    continue;

                var pontoCalculado = fila.Dequeue();

                if (!irradiacoesPorEstacao.TryGetValue(leitura.EstacaoOcupada, out var lista))
                {
                    lista = new List<PontoCoordenada>();
                    irradiacoesPorEstacao[leitura.EstacaoOcupada] = lista;
                }

                lista.Add(pontoCalculado);
            }

            foreach (var estacao in estacoesOrganizadas)
            {
                if (poligonalPorNome.TryGetValue(estacao.Nome, out var pontoDaEstacao))
                {
                    estacao.PontosCalculados.Add(pontoDaEstacao);
                }

                if (irradiacoesPorEstacao.TryGetValue(estacao.Nome, out var irradiacoes))
                {
                    estacao.PontosCalculados.AddRange(irradiacoes);
                }
            }

            SalvarSaidaTxt(resultado);
            return resultado;
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
    }
}