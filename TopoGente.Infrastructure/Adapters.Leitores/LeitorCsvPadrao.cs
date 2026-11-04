using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Core.Utilities;

namespace TopoGente.Infrastructure.Adapters.Leitores
{
    public class LeitorCsvPadrao : ILeitorArquivo
    {
        public string NomeFormato => "Texto/CSV Padrão";

        public IReadOnlyList<string> UltimosAvisos => _ultimosAvisos;
        private readonly List<string> _ultimosAvisos = new();

        public IReadOnlyDictionary<string, PontoCoordenada> UltimosPontosConhecidos => _ultimosPontosConhecidos;
        private readonly Dictionary<string, PontoCoordenada> _ultimosPontosConhecidos = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<PontoCoordenada> UltimosPontosImportados => _ultimosPontosImportados;
        private readonly List<PontoCoordenada> _ultimosPontosImportados = new();

        public List<Estacao> Ler(IEnumerable<string> linhas)
        {
            var config = LeitorCsvEngine.DetectarConfiguracaoSugerida(linhas, FormatoArquivoEntrada.CsvPadrao);
            return Ler(linhas, config);
        }

        public List<Estacao> Ler(IEnumerable<string> linhas, ConfiguracaoImportacao config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _ultimosAvisos.Clear();
            _ultimosPontosConhecidos.Clear();
            _ultimosPontosImportados.Clear();

            var leiturasBrutas = new List<LeituraEstacaoTotal>();
            int numeroLinha = 0;
            Span<Range> tokens = stackalloc Range[16];

            // Heurística de cabeçalho para listas retangulares
            bool ordemNortePrimeiro = false;
            bool cabecalhoIdentificado = false;

            var listaLinhas = linhas.ToList();

            // 1. Varredura preliminar para inspeção de cabeçalho em arquivos cartesianos
            foreach (var l in listaLinhas.Take(5))
            {
                string raw = l.Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                string rawUpper = raw.ToUpperInvariant();
                if (rawUpper.Contains("NORTE") || rawUpper.Contains("NORTH") || rawUpper.Contains(" Y "))
                {
                    int idxNorte = rawUpper.IndexOf("NORTE", StringComparison.Ordinal);
                    if (idxNorte == -1) idxNorte = rawUpper.IndexOf("NORTH", StringComparison.Ordinal);
                    if (idxNorte == -1) idxNorte = rawUpper.IndexOf("Y", StringComparison.Ordinal);

                    int idxEste = rawUpper.IndexOf("ESTE", StringComparison.Ordinal);
                    if (idxEste == -1) idxEste = rawUpper.IndexOf("EAST", StringComparison.Ordinal);
                    if (idxEste == -1) idxEste = rawUpper.IndexOf("X", StringComparison.Ordinal);

                    if (idxNorte != -1 && idxEste != -1 && idxNorte < idxEste)
                    {
                        ordemNortePrimeiro = true;
                        cabecalhoIdentificado = true;
                        break;
                    }
                    else if (idxEste != -1 && idxNorte != -1 && idxEste < idxNorte)
                    {
                        ordemNortePrimeiro = false;
                        cabecalhoIdentificado = true;
                        break;
                    }
                }
            }

            foreach (var linhaRaw in listaLinhas)
            {
                numeroLinha++;
                string linha = linhaRaw.Trim();
                if (string.IsNullOrWhiteSpace(linha)) continue;

                // Ignora linhas de comentário ou cabeçalho textual conhecido
                if (linha.StartsWith("#") || linha.StartsWith("!") ||
                    linha.StartsWith("Estação", StringComparison.OrdinalIgnoreCase) ||
                    linha.StartsWith("Ponto", StringComparison.OrdinalIgnoreCase) ||
                    linha.StartsWith("Point", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ReadOnlySpan<char> spanLinha = linha.AsSpan();
                char delimLinha = config.Delimitador;
                if (delimLinha == ';' && !spanLinha.Contains(';') && spanLinha.Contains(','))
                    delimLinha = ',';
                else if (delimLinha == ',' && !spanLinha.Contains(',') && spanLinha.Contains(';'))
                    delimLinha = ';';

                int nTokens = LeitorCsvEngine.TokenizarLinha(spanLinha, delimLinha, tokens);
                if (nTokens == 0) continue;

                // ── Linha de Controle de Coordenadas Fixas (#COORD ou NEZ ou COORD) ──
                string primeiroToken = spanLinha[tokens[0]].Trim().Trim('"').ToString().ToUpperInvariant();
                if ((primeiroToken == "COORD" || primeiroToken == "#COORD" || primeiroToken == "NEZ") && nTokens >= 4)
                {
                    try
                    {
                        string nomePt = spanLinha[tokens[1]].Trim().Trim('"').ToString();
                        bool okCoord1 = LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[2]], config, out double c1);
                        bool okCoord2 = LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[3]], config, out double c2);
                        double c3 = 0.0;
                        if (nTokens > 4)
                        {
                            LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[4]], config, out c3);
                        }

                        if (okCoord1 && okCoord2)
                        {
                            double xCoord = c1;
                            double yCoord = c2;
                            if (primeiroToken == "NEZ")
                            {
                                yCoord = c1;
                                xCoord = c2;
                            }

                            _ultimosPontosConhecidos[nomePt] = new PontoCoordenada
                            {
                                Nome = nomePt,
                                X = xCoord,
                                Y = yCoord,
                                Z = c3,
                                EhPontoPoligonal = true
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _ultimosAvisos.Add($"Erro ao ler linha de controle COORD (linha {numeroLinha}): {ex.Message}");
                    }
                    continue;
                }

                // ── Caso A: Caderneta Polar (8 colunas) ──
                // [0] EstacaoOcupada  [1] AngH  [2] Observacao  [3] AngV(Zenite)
                // [4] DI              [5] AlturaInstrumento      [6] PontoVisado  [7] AlturaPrisma
                if (nTokens >= 8)
                {
                    try
                    {
                        ReadOnlySpan<char> ahToken = spanLinha[tokens[1]];
                        ReadOnlySpan<char> avToken = spanLinha[tokens[3]];
                        ReadOnlySpan<char> diToken = spanLinha[tokens[4]];
                        ReadOnlySpan<char> hiToken = spanLinha[tokens[5]];
                        ReadOnlySpan<char> htToken = spanLinha[tokens[7]];

                        if (!LeitorCsvEngine.ParseNumeroResiliente(ahToken, config, out double ahRaw) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(avToken, config, out double avRaw) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(diToken, config, out double diLida) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(hiToken, config, out double hiLida) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(htToken, config, out double htLida))
                        {
                            _ultimosAvisos.Add($"Erro léxico na linha {numeroLinha}: Falha ao converter números decimais. Linha ignorada.");
                            continue;
                        }

                        double ahDecimal = ConversorAngulos.ConverterParaDecimal(ahRaw, config.FormatoAngular);
                        double avDecimal = ConversorAngulos.ConverterParaDecimal(avRaw, config.FormatoAngular);

                        string obs = spanLinha[tokens[2]].Trim().Trim('"').ToString();
                        string stn = spanLinha[tokens[0]].Trim().Trim('"').ToString();
                        string vis = spanLinha[tokens[6]].Trim().Trim('"').ToString();

                        var leitura = new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = stn,
                            AlturaInstrumento = hiLida,
                            PontoVisado = vis,
                            Observacao = obs,
                            AnguloHorizontal = ahDecimal,
                            AnguloVertical = avDecimal,
                            DistanciaInclinada = diLida,
                            AlturaPrisma = htLida,
                            Tipo = TipoLeitura.Irradiacao,
                            Purpose = MapearPurposeSugerido(obs),
                            OrdemArquivo = numeroLinha
                        };

                        leiturasBrutas.Add(leitura);
                    }
                    catch (Exception ex)
                    {
                        _ultimosAvisos.Add($"Erro na linha {numeroLinha}: {ex.Message}. Leitura ignorada.");
                    }
                }
                // ── Caso B: Lista de Coordenadas Retangulares (4 ou 5 colunas) ──
                // [0] Ponto [1] Coordenada1 [2] Coordenada2 [3] Cota [4] Código (opcional)
                else if (nTokens >= 4)
                {
                    try
                    {
                        string nomePonto = spanLinha[tokens[0]].Trim().Trim('"').ToString();
                        if (string.IsNullOrWhiteSpace(nomePonto)) continue;

                        if (!LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[1]], config, out double c1) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[2]], config, out double c2) ||
                            !LeitorCsvEngine.ParseNumeroResiliente(spanLinha[tokens[3]], config, out double c3))
                        {
                            _ultimosAvisos.Add($"Erro léxico na linha {numeroLinha}: Coordenadas retangulares inválidas.");
                            continue;
                        }

                        string codigo = nTokens > 4 ? spanLinha[tokens[4]].Trim().Trim('"').ToString() : "";

                        // Cascata de resolução de eixos
                        double x, y;
                        if (cabecalhoIdentificado)
                        {
                            x = ordemNortePrimeiro ? c2 : c1;
                            y = ordemNortePrimeiro ? c1 : c2;
                        }
                        else
                        {
                            // Heurística UTM no Hemisfério Sul: Norte > 1.000.000m e Este < 1.000.000m
                            if (c1 > 1_000_000.0 && c2 < 1_000_000.0)
                            {
                                y = c1; // Norte
                                x = c2; // Este
                            }
                            else if (c2 > 1_000_000.0 && c1 < 1_000_000.0)
                            {
                                x = c1; // Este
                                y = c2; // Norte
                            }
                            else
                            {
                                // Fallback para convenção padrão cartesiana topoGRAPH: P, E, N, Z
                                x = c1;
                                y = c2;
                            }
                        }

                        var pt = new PontoCoordenada
                        {
                            Nome = nomePonto,
                            X = x,
                            Y = y,
                            Z = c3,
                            Descricao = codigo,
                            EhPontoPoligonal = false
                        };

                        _ultimosPontosImportados.Add(pt);
                        _ultimosPontosConhecidos[nomePonto] = pt;
                    }
                    catch (Exception ex)
                    {
                        _ultimosAvisos.Add($"Erro ao importar coordenada retangular na linha {numeroLinha}: {ex.Message}");
                    }
                }
                else
                {
                    _ultimosAvisos.Add($"Linha {numeroLinha} possui colunas insuficientes ({nTokens} colunas). Linha ignorada.");
                }
            }

            // Agrupamento de sessões para caderneta polar
            var estacoes = new List<Estacao>();
            Estacao? estacaoAtual = null;
            string? nomeEstacaoAtual = null;
            double? hiAtual = null;

            foreach (var leitura in leiturasBrutas.OrderBy(l => l.OrdemArquivo))
            {
                var nomeOcupada = leitura.EstacaoOcupada?.Trim() ?? string.Empty;
                var hiLeitura = leitura.AlturaInstrumento;

                var quebraSessao =
                    estacaoAtual == null ||
                    !string.Equals(nomeOcupada, nomeEstacaoAtual, StringComparison.InvariantCultureIgnoreCase) ||
                    hiAtual != hiLeitura;

                if (quebraSessao)
                {
                    estacaoAtual = new Estacao
                    {
                        Id = Guid.NewGuid().ToString(),
                        Nome = nomeOcupada,
                        AlturaInstrumento = hiLeitura
                    };

                    if (_ultimosPontosConhecidos.TryGetValue(nomeOcupada, out var pConhecido))
                    {
                        estacaoAtual.CoordenadaConhecida = pConhecido;
                    }

                    estacoes.Add(estacaoAtual);
                    nomeEstacaoAtual = nomeOcupada;
                    hiAtual = hiLeitura;
                }

                leitura.SetupId = estacaoAtual!.Id;
                estacaoAtual.AdicionarVisada(leitura);
            }

            return estacoes;
        }

        private static string? MapearPurposeSugerido(string observacao)
        {
            string obsUpper = observacao.Trim().ToUpperInvariant();

            return obsUpper switch
            {
                "RE" or "RÉ" or "R" or "BACK" => "re",
                "VANTE" or "VT" or "V" or "FORE" => "vante",
                "CHECK" => "check",
                "AUXILIAR" or "AUX" or "P_AUX" => "auxiliar",
                _ => null
            };
        }
    }
}
