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

        private static void SalvarSaidaTxt(ResultadoLevantamento resultado)
        {

            var pontos = resultado.TodosOsPontos ?? new List<PontoCoordenada>();

            if (pontos.Count == 0)
            {
                return;
            }

            var PastaSaidaTeste = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
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
        }

        private void CalcularIrradiacoesSequencial(ResultadoLevantamento resultado, List<LeituraEstacaoTotal> leiturasBrutas,
            Dictionary<string, PontoCoordenada>? pontosConhecidos, MetadadosCenario metadadosAtuais, double azimuteInicial)
        {

            if (leiturasBrutas == null || leiturasBrutas.Count == 0)
            {
                return;
            }

            var poligonalPorNome = resultado.Poligonal.GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var conhecidos = pontosConhecidos != null ? new Dictionary<string, PontoCoordenada>(pontosConhecidos, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);

            if (metadadosAtuais.UsarCoordenadaRe)
            {
                var primeiraRe = leiturasBrutas.Where(l => l.Tipo == TipoLeitura.Re).OrderBy(l => l.OrdemArquivo > 0 ? l.OrdemArquivo : int.MaxValue).FirstOrDefault();

                if (primeiraRe != null && !conhecidos.ContainsKey(primeiraRe.PontoVisado))
                {
                    conhecidos[primeiraRe.PontoVisado] = new PontoCoordenada
                    {
                        Nome = primeiraRe.PontoVisado,
                        X = metadadosAtuais.ReX,
                        Y = metadadosAtuais.ReY,
                        Z = metadadosAtuais.ReZ,
                        EhPontoPoligonal = false,
                    };
                }
            }

            // Ordenar cronologicamente 
            var ordenadas = leiturasBrutas.OrderBy(l => l.OrdemArquivo > 0 ? l.OrdemArquivo : int.MaxValue).ToList();

            resultado.Irradiacoes.Clear();

            PontoCoordenada? estacaoAtual = null;
            double? azimuteReAtual = null;
            string? reAtualNome = null;

            foreach (var leitura in ordenadas)
            {
                // Descobrir qual estcao estamos
                poligonalPorNome.TryGetValue(leitura.EstacaoOcupada, out estacaoAtual);

                if (leitura.Tipo == TipoLeitura.Re)
                {
                    if (estacaoAtual == null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                        $"[IRR-ORIENT][WARN] Linha={leitura.OrdemArquivo} Estação '{leitura.EstacaoOcupada}' não encontrada na poligonal. Ré ignorada.");
                        continue;
                    }

                    PontoCoordenada? pontoRe = null;

                    if (conhecidos.TryGetValue(leitura.PontoVisado, out var pConhecido))
                    {
                        pontoRe = pConhecido;
                    }
                    else if (poligonalPorNome.TryGetValue(leitura.PontoVisado, out var pPoligonal))
                    {
                        pontoRe = pPoligonal;
                    }

                    if (pontoRe != null)
                    {
                        // Tem coordenada: Calcula a direção das projeções
                        azimuteReAtual = _calculoService.CalcularAzimutePorCoordenadas(
                            estacaoAtual.X, estacaoAtual.Y,
                            pontoRe.X, pontoRe.Y
                        );
                    }
                    else if (leitura.EstacaoOcupada.Equals(ordenadas.First().EstacaoOcupada, StringComparison.OrdinalIgnoreCase))
                    {
                        // não tem coordenada, mas é a Estação de Partida. 
                        // O Azimute Inicial é a referência de Ré
                        azimuteReAtual = azimuteInicial;
                        System.Diagnostics.Debug.WriteLine($"[IRR-ORIENT] Linha={leitura.OrdemArquivo} Usando Azimute Inicial {azimuteInicial:F4}° para a Ré {leitura.PontoVisado}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                        $"[IRR-ORIENT][WARN] Linha={leitura.OrdemArquivo} Est={leitura.EstacaoOcupada} Ré='{leitura.PontoVisado}' sem coordenadas. Mantendo orientação anterior.");
                        continue; // Aborta esta atualização de Ré e continua com o azimute antigo (se existir)
                    }

                    reAtualNome = leitura.PontoVisado;

                    // Log seguro da operação
                    if (azimuteReAtual.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine(
                        $"[IRR-ORIENT] Linha={leitura.OrdemArquivo} Est={leitura.EstacaoOcupada} Ré={reAtualNome} AzRe={azimuteReAtual.Value:F4}°");
                    }



                }
                else if (leitura.Tipo == TipoLeitura.Irradiacao)
                {

                    if (estacaoAtual == null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                        $"[IRR][WARN] Linha={leitura.OrdemArquivo} Irr '{leitura.PontoVisado}' com estação '{leitura.EstacaoOcupada}' não encontrada. Ignorando.");
                        continue;
                    }

                    double azReUsado;
                    if (azimuteReAtual.HasValue)
                    {
                        azReUsado = azimuteReAtual.Value;
                    }
                    else
                    {
                        azReUsado = estacaoAtual.Nome.Equals(resultado.Poligonal.First().Nome, StringComparison.OrdinalIgnoreCase)
                        ? azimuteInicial
                        : (estacaoAtual.AzimuteChegada < 180 ? estacaoAtual.AzimuteChegada + 180 : estacaoAtual.AzimuteChegada - 180);

                        System.Diagnostics.Debug.WriteLine(
                        $"[IRR][WARN] Linha={leitura.OrdemArquivo} Irr '{leitura.PontoVisado}' sem Ré vigente. Usando fallback AzRe={azReUsado:F4}°");

                    }

                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacaoAtual, leitura, azReUsado);
                    resultado.Irradiacoes.Add(pontoIrradiado);

                    // ✅ LOG: coordenadas finais geradas para o ponto irradiado
                    System.Diagnostics.Debug.WriteLine(
                        $"[IRR][OUT] Linha={leitura.OrdemArquivo} Est={leitura.EstacaoOcupada} " +
                        $"ReVigente={(reAtualNome ?? "-")} AzRe={azReUsado:F4}° AngH={leitura.AnguloHorizontal:F4}° " +
                        $"→ {pontoIrradiado.Nome} X={pontoIrradiado.X:F3} Y={pontoIrradiado.Y:F3} Z={pontoIrradiado.Z:F3}");

                }
            }
        }

        public ResultadoLevantamento Processar(
            MetadadosCenario metadadosAtuais,
            List<LeituraEstacaoTotal> leiturasBrutas, Dictionary<string, PontoCoordenada>? pontosConhecidos)
        {
            if (metadadosAtuais == null)
            {
                throw new DadosInsuficientesException("Dados Inciais não foram prrenchidos.");
            }

            var resultado = new ResultadoLevantamento();


            double azimuteInicial = 0;
            if (metadadosAtuais.UsarCoordenadaRe)
            {
                azimuteInicial = _calculoService.CalcularAzimutePorCoordenadas(metadadosAtuais.PartidaX, metadadosAtuais.PartidaY, metadadosAtuais.ReX, metadadosAtuais.ReY);
            }
            else
            {
                azimuteInicial = metadadosAtuais.AzimutePartida;
            }

            string nomePontoIncial = leiturasBrutas.FirstOrDefault()?.EstacaoOcupada ?? "Partida";

            var PontoPartida = new PontoCoordenada
            {
                Nome = nomePontoIncial,
                X = metadadosAtuais.PartidaX,
                Y = metadadosAtuais.PartidaY,
                Z = metadadosAtuais.PartidaZ,
                EhPontoPoligonal = true,
                AzimuteChegada = metadadosAtuais.UsarCoordenadaRe ? azimuteInicial : metadadosAtuais.AzimutePartida
            };


            if (metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Fechada || metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                ValidarCoordenadasPartida(PontoPartida);
            }


            var leiturasPoligonal = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Poligonal).ToList();
            var leiturasRe = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Re).ToList();
            var leiturasIrradiadas = leiturasBrutas.Where(x => x.Tipo == TipoLeitura.Irradiacao).ToList();

            System.Diagnostics.Debug.WriteLine($"=== DEBUG: Classificação de Leituras ===");
            System.Diagnostics.Debug.WriteLine($"Total Bruto: {leiturasBrutas.Count}");
            System.Diagnostics.Debug.WriteLine($"Poligonal: {leiturasPoligonal.Count}");
            System.Diagnostics.Debug.WriteLine($"Ré: {leiturasRe.Count}");
            System.Diagnostics.Debug.WriteLine($"Irradiação: {leiturasIrradiadas.Count}");
            foreach (var l in leiturasBrutas)
            {
                System.Diagnostics.Debug.WriteLine($"{l.EstacaoOcupada} → {l.PontoVisado} | {l.Observacao} | TIPO={l.Tipo}");
            }

            // Calclulo da poligonal bruta
            // referencia angular (ea) = azFinal - azInicial - > k = -ea/n (Fecahda) , para aberta ea = 0 -> k = 0
            double? referenciaAngular = metadadosAtuais.TipoCenario switch
            {
                TipoCenarioPoligonal.Fechada => azimuteInicial,
                TipoCenarioPoligonal.AbertaOrientada => null,
                _ => azimuteInicial
            };


            var poligonalBruta = _calculoService.CalcularPoligonal(PontoPartida, azimuteInicial, leiturasPoligonal);

            resultado.PoligonalBruta = poligonalBruta;

            System.Diagnostics.Debug.WriteLine($"\n=== DEBUG: POLIGONAL BRUTA (Antes da Compensação) ===");
            foreach (var ponto in poligonalBruta)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"{ponto.Nome} | X={ponto.X:F3} | Y={ponto.Y:F3} | Z={ponto.Z:F3} | Az={ponto.AzimuteChegada:F4}°");
            }

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
                    if (!fechou)
                    {
                        System.Diagnostics.Debug.WriteLine(
                        $"[WARN] Poligonal não fechou no critério bruto. Mesmo assim, será tentada compensação para avaliar erros pós-ajuste.");


                    }
                    string nomeEstcaoInicial = leiturasPoligonal.FirstOrDefault()?.EstacaoOcupada ?? PontoPartida.Nome;

                    var reInicial = leiturasRe.FirstOrDefault(r => r.EstacaoOcupada == nomeEstcaoInicial);

                    if (reInicial == null)
                    {
                        throw new DadosInsuficientesException(
                            $"Poligonal fechada exige leitura de Ré inicial na estação '{PontoPartida.Nome}'. " +
                            "Verifique se o CSV contém uma linha de Ré antes da primeira Vante.");
                    }

                    string nomePontoReInicial = reInicial.PontoVisado;

                    var leituraFechamento = leiturasRe.Where(r => r.EstacaoOcupada == nomeEstcaoInicial && r.PontoVisado.Equals(nomePontoReInicial, StringComparison.OrdinalIgnoreCase))
                        .LastOrDefault();

                    anguloFechamento = leituraFechamento?.AnguloHorizontal ?? 0;

                    System.Diagnostics.Debug.WriteLine($"\n=== DEBUG: DADOS DE FECHAMENTO ===");
                    System.Diagnostics.Debug.WriteLine($"Estação Inicial: {nomeEstcaoInicial}");
                    System.Diagnostics.Debug.WriteLine($"Ponto Ré Inicial: {nomePontoReInicial}");
                    System.Diagnostics.Debug.WriteLine($"Ângulo de Fechamento: {anguloFechamento:F4}°");


                    resultado.Poligonal = _calculoService.CompensarPoligonal(PontoPartida, PontoPartida, PontoPartida.AzimuteChegada,PontoPartida.AzimuteChegada,
                    leiturasPoligonal, poligonalBruta, metadadosAtuais.TipoCenario, anguloFechamento, out double ea, out double erroX, out double erroY, out double erroLinearT, out double precisaoRelativa, out double erroAltimetrico);

                    System.Diagnostics.Debug.WriteLine($"\n=== DEBUG: POLIGONAL COMPENSADA (Após Compensação) ===");
                    foreach (var ponto in resultado.Poligonal)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"{ponto.Nome} | X={ponto.X:F3} | Y={ponto.Y:F3} | Z={ponto.Z:F3} | Az={ponto.AzimuteChegada:F4}°");
                    }

                    resultado.ErroAngular = ea; resultado.ErroLinear = erroLinearT; resultado.Precisao = precisaoRelativa;
                    resultado.ErroFechamentoX = erroX;
                    resultado.ErroFechamentoY = erroY;
                    resultado.ErroFechamentoZ = erroAltimetrico;

                    System.Diagnostics.Debug.WriteLine($"\n=== DEBUG: ERROS DE FECHAMENTO ===");
                    System.Diagnostics.Debug.WriteLine($"Erro Angular: {ea:F4}° ({ea * 60:F2}')");
                    System.Diagnostics.Debug.WriteLine($"Erro X: {erroX:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Y: {erroY:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Linear XY: {erroLinearT:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Altimétrico: {erroAltimetrico:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Precisão: 1:{(precisaoRelativa > 0 ? (1 / precisaoRelativa).ToString("F4") : "∞")}");
                    System.Diagnostics.Debug.WriteLine($"Perímetro: {perimetro:F3} m");


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

                    if (ultimaLeituraReferencia != null)
                    {
                        anguloFechamento = ultimaLeituraReferencia.AnguloHorizontal;
                        System.Diagnostics.Debug.WriteLine($"[ENQUADRADA] Ângulo lido para a Ref Final: {anguloFechamento:F4}°");

                    }

                    var azimuteChegada = metadadosAtuais.AzimuteChegada;

                    resultado.Poligonal = _calculoService.CompensarPoligonal(PontoPartida, pontoChegadaConhecido, PontoPartida.AzimuteChegada, azimuteChegada , leiturasPoligonal,poligonalBruta,
                        metadadosAtuais.TipoCenario, anguloFechamento,out double eaEnq, out double erroXEnq, out double erroYEnq, out double erroLinearEnq, out double precisaoRelativaEnq, out double erroAltimetricoEnq);

                    System.Diagnostics.Debug.WriteLine($"\n DEBUG: POLIGONAL COMPENSADA ");
                    foreach (var ponto in resultado.Poligonal)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"{ponto.Nome} | X={ponto.X:F3} | Y={ponto.Y:F3} | Z={ponto.Z:F3} | Az={ponto.AzimuteChegada:F4}°");
                    }

                    resultado.ErroAngular = eaEnq;
                    resultado.ErroFechamentoX = erroXEnq;
                    resultado.ErroFechamentoY = erroYEnq;
                    resultado.ErroLinear = erroLinearEnq;
                    resultado.ErroFechamentoZ = erroAltimetricoEnq;
                    resultado.Precisao = precisaoRelativaEnq;

                    System.Diagnostics.Debug.WriteLine($"\nDEBUG: ERROS DE FECHAMENTO");
                    System.Diagnostics.Debug.WriteLine($"Erro Angular: {resultado.ErroAngular:F4}°");
                    System.Diagnostics.Debug.WriteLine($"Erro X: {resultado.ErroFechamentoX:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Y: {resultado.ErroFechamentoY:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Linear XY: {resultado.ErroLinear:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Erro Altimétrico: {resultado.ErroFechamentoZ:F4} m");
                    System.Diagnostics.Debug.WriteLine($"Precisão: 1:{(resultado.Precisao > 0 ? (1 / resultado.Precisao).ToString("F0") : "∞")}");


                    break;
                case TipoCenarioPoligonal.AbertaOrientada:
                    resultado.TipoCenario = TipoCenarioPoligonal.AbertaOrientada;
                    resultado.PoligonalFechada = false;
                    ProcessarAberta(resultado, poligonalBruta);
                    System.Diagnostics.Debug.WriteLine($"\n[WARN] Este levantamento é do tipo ABERTO. As coordenadas finais não foram auditadas contra erros de fechamento.");

                    break;
                default:
                    break;

            }



            CalcularIrradiacoesSequencial(resultado, leiturasBrutas, pontosConhecidos, metadadosAtuais, azimuteInicial);


            return resultado;
        }

        public bool ProcessarFechada(ResultadoLevantamento resultado, List<PontoCoordenada> poligonalBruta, PontoCoordenada pontoPartida, double perimetro)
        {
            if (poligonalBruta.Count <= 1)
            {
                return false;
            }

            var pontoChegada = poligonalBruta.Last();

            // Verificar fechamento 
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

        private void CalcularIrradiacoes(ResultadoLevantamento resultado, List<LeituraEstacaoTotal> leiturasIrradiadas, List<LeituraEstacaoTotal> leituraRe,
            Dictionary<string, PontoCoordenada>? pontosConhecidos, double azimuteInicial)
        {
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

                double azimuteOrientacao = ResolverAzimuteOrientacao(estacao, leituraRe, resultado.Poligonal, pontosConhecidos, azimuteInicial);

                foreach (var leitura in irradiacoesDestaEstacao)
                {
                    var pontoIrradiado = _calculoService.CalcularPontoIrradiado(estacao, leitura, azimuteOrientacao);
                    resultado.Irradiacoes.Add(pontoIrradiado);
                }
            }
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