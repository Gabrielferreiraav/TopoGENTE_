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
    public class LeitorFbk : ILeitorArquivo
    {
        public string NomeFormato => "FBK";

        /// <summary>
        /// Avisos não-fatais gerados durante a última importação.
        /// Disponível para consulta pela Factory após a chamada de Ler().
        /// </summary>
        public IReadOnlyList<string> UltimosAvisos => _ultimosAvisos;
        private readonly List<string> _ultimosAvisos = new();

        public IReadOnlyDictionary<string, PontoCoordenada> UltimosPontosConhecidos => _ultimosPontosConhecidos;
        private readonly Dictionary<string, PontoCoordenada> _ultimosPontosConhecidos = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<PontoCoordenada> UltimosPontosImportados => Array.Empty<PontoCoordenada>();

        public List<Estacao> Ler(IEnumerable<string> linhas)
        {
            return Ler(linhas, ConfiguracaoImportacao.PadraoFbk);
        }

        public List<Estacao> Ler(IEnumerable<string> linhas, ConfiguracaoImportacao config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _ultimosAvisos.Clear();
            _ultimosPontosConhecidos.Clear();

            var estacoes = new List<Estacao>();
            Estacao? estacaoAtual = null;
            double alturaPrisma = 0.0;
            var falhas = new List<string>();
            int numeroLinha = 0;
            bool arquivoDeclarouDecDeg = false;

            foreach (var linhaRaw in linhas)
            {
                numeroLinha++;
                string linha = linhaRaw.Trim();
                if (string.IsNullOrWhiteSpace(linha) || linha.StartsWith("!"))
                    continue;

                var partes = linha.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) continue;

                string comando = partes[0].ToUpperInvariant();

                try
                {
                    // ── UNIT — Validação de unidades geodésicas ──
                    if (comando == "UNIT")
                    {
                        string unidadeDistancia = partes.Length > 1 ? partes[1].ToUpperInvariant() : "";
                        string unidadeAngular = partes.Length > 2 ? partes[2].ToUpperInvariant() : "";

                        if (unidadeDistancia != "METER" && unidadeDistancia != "METRE" && unidadeDistancia != "")
                        {
                            throw new NotSupportedException(
                                $"Unidade de distância '{unidadeDistancia}' não suportada (linha {numeroLinha}). " +
                                "Converta o arquivo para METER antes de importar.");
                        }

                        if (unidadeAngular != "DECDEG" && unidadeAngular != "DMS" && unidadeAngular != "")
                        {
                            throw new NotSupportedException(
                                $"Unidade angular '{unidadeAngular}' não suportada (linha {numeroLinha}). " +
                                "Converta o arquivo para DECDEG antes de importar.");
                        }

                        if (unidadeAngular == "DECDEG")
                        {
                            arquivoDeclarouDecDeg = true;
                        }

                        continue;
                    }

                    // ── NEZ / NE — Coordenadas de controle ──
                    if (comando == "NEZ" || comando == "NE")
                    {
                        int tamanhoMinimo = comando == "NEZ" ? 5 : 4;

                        if (EhLinhaResiduaSentinela(partes, tamanhoMinimo, config))
                        {
                            _ultimosAvisos.Add(
                                $"NEZ residual ignorado (linha {numeroLinha}): " +
                                $"todos os campos zerados e sem identificador válido. " +
                                $"Linha original: '{linha.Trim()}' ");
                            continue;
                        }

                        if (partes.Length < tamanhoMinimo)
                        {
                            throw new FormatException(
                                $"{comando} exige nome do ponto e coordenadas completas (linha {numeroLinha}).");
                        }

                        string nomePonto = partes[1].Replace("\"", "");
                        if (!NomePontoValido(nomePonto))
                        {
                            throw new FormatException(
                                $"{comando} exige nome de ponto válido (não decimal) antes das coordenadas (linha {numeroLinha}).");
                        }

                        double y = ParseNumero(partes[2], config, numeroLinha);
                        double x = ParseNumero(partes[3], config, numeroLinha);
                        double z = (comando == "NEZ" && partes.Length > 4)
                            ? ParseNumero(partes[4], config, numeroLinha)
                            : 0.0;

                        // Tolerância a coordenadas locais/truncadas (ex: linha 161 do 121377.FBK)
                        if (config.Magnitude == MagnitudeCoordenadaEnum.UtmCompleta && y < 1_000_000.0)
                        {
                            _ultimosAvisos.Add(
                                $"Aviso de Magnitude (linha {numeroLinha}): Ponto '{nomePonto}' possui coordenadas sem a casa de milhão (N={y:F3}, E={x:F3}). " +
                                "Ponto registrado como apoio local.");
                        }

                        if (!_ultimosPontosConhecidos.ContainsKey(nomePonto))
                        {
                            _ultimosPontosConhecidos.Add(nomePonto, new PontoCoordenada
                            {
                                Nome = nomePonto,
                                X = x,
                                Y = y,
                                Z = z,
                            });
                        }
                        else
                        {
                            var existente = _ultimosPontosConhecidos[nomePonto];
                            double deltaX = Math.Abs(existente.X - x);
                            double deltaY = Math.Abs(existente.Y - y);
                            double deltaZ = Math.Abs(existente.Z - z);

                            if (deltaX > 0.001 || deltaY > 0.001 || deltaZ > 0.001)
                            {
                                _ultimosAvisos.Add(
                                    $"CONFLITO NEZ (linha {numeroLinha}): Ponto '{nomePonto}' declarado com " +
                                    $"coordenadas divergentes. Original: ({existente.X:F4}, {existente.Y:F4}, " +
                                    $"{existente.Z:F4}) | Rejeitado: ({x:F4}, {y:F4}, {z:F4}). " +
                                    "A primeira declaração foi mantida.");
                            }
                            else
                            {
                                _ultimosAvisos.Add(
                                    $"NEZ DUPLICADO (linha {numeroLinha}): Ponto '{nomePonto}' repetido com coordenadas equivalentes. " +
                                    "A primeira declaração foi mantida.");
                            }
                        }
                    }

                    // ── STN — Ocupação de estação ──
                    else if (comando == "STN" && partes.Length >= 3)
                    {
                        string nome = partes[1].Replace("\"", "");
                        double hi = ParseNumero(partes[2], config, numeroLinha);

                        estacaoAtual = new Estacao
                        {
                            Nome = nome,
                            AlturaInstrumento = hi
                        };

                        if (_ultimosPontosConhecidos.TryGetValue(nome, out var coordConhecida))
                        {
                            estacaoAtual.CoordenadaConhecida = coordConhecida;
                        }

                        estacoes.Add(estacaoAtual);
                    }

                    // ── PRISM / PRISMA — Altura do sinal ──
                    else if ((comando == "PRISM" || comando == "PRISMA") && partes.Length >= 2)
                    {
                        alturaPrisma = ParseNumero(partes[1], config, numeroLinha);
                    }

                    // ── BS — Visada de Ré (Backsight) ──
                    else if (comando == "BS" && partes.Length >= 3)
                    {
                        if (estacaoAtual == null)
                        {
                            _ultimosAvisos.Add(
                                $"Linha {numeroLinha} ignorada: comando BS sem estação ativa (STN ausente).");
                            continue;
                        }

                        string alvoNome = partes[1].Replace("\"", "");
                        double anguloRaw = ParseNumero(partes[2], config, numeroLinha);
                        double anguloDecimal = arquivoDeclarouDecDeg
                            ? anguloRaw
                            : ConversorAngulos.ConverterParaDecimal(anguloRaw, config.FormatoAngular);

                        estacaoAtual.AdicionarVisada(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacaoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacaoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = anguloDecimal,
                            Tipo = TipoLeitura.Irradiacao,
                            Purpose = "re",
                            Observacao = "RE (BS)"
                        });
                    }

                    // ── AD VA — Observação polar (ângulo + distância + zenital) ──
                    else if (comando == "AD" && partes.Length >= 6 && partes[1].ToUpperInvariant() == "VA")
                    {
                        if (estacaoAtual == null)
                        {
                            _ultimosAvisos.Add(
                                $"Linha {numeroLinha} ignorada: comando AD VA sem estação ativa (STN ausente).");
                            continue;
                        }

                        string alvoNome = partes[2].Replace("\"", "");
                        double angHRaw = ParseNumero(partes[3], config, numeroLinha);
                        double dist = ParseNumero(partes[4], config, numeroLinha);
                        double angVRaw = ParseNumero(partes[5], config, numeroLinha);

                        double angHDecimal = arquivoDeclarouDecDeg
                            ? angHRaw
                            : ConversorAngulos.ConverterParaDecimal(angHRaw, config.FormatoAngular);

                        double angVDecimal = arquivoDeclarouDecDeg
                            ? angVRaw
                            : ConversorAngulos.ConverterParaDecimal(angVRaw, config.FormatoAngular);

                        string descricao = "";
                        if (partes.Length > 6)
                        {
                            descricao = partes[6].Replace("\"", "");
                        }

                        estacaoAtual.AdicionarVisada(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacaoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacaoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angHDecimal,
                            AnguloVertical = angVDecimal,
                            DistanciaInclinada = dist,
                            Observacao = descricao,
                            Tipo = TipoLeitura.Irradiacao,
                            Purpose = MapearPurposeSugerido(descricao)
                        });
                    }
                }
                catch (NotSupportedException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    falhas.Add($"Linha {numeroLinha}: '{linha}' → {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (falhas.Count > 0)
            {
                throw new FormatException(
                    $"{falhas.Count} linha(s) falharam no parsing do arquivo FBK. " +
                    $"Verifique se o separador decimal é ponto (.) e não vírgula (,).\n" +
                    string.Join("\n", falhas.Take(10)));
            }

            return estacoes;
        }

        private static bool NomePontoValido(string nomePonto)
        {
            if (string.IsNullOrWhiteSpace(nomePonto))
                return false;

            if (long.TryParse(nomePonto, out _))
                return true;

            string normalizado = nomePonto.Replace(',', '.');
            if (double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                (normalizado.Contains('.') || normalizado.Contains(',')))
            {
                return false;
            }

            return true;
        }

        private static bool EhLinhaResiduaSentinela(string[] partes, int tamanhoMinimo, ConfiguracaoImportacao config)
        {
            const double Epsilon = 1e-6;
            bool semIdentificadorValido = partes.Length < tamanhoMinimo || !NomePontoValido(partes[1].Replace("\"", ""));

            if (!semIdentificadorValido)
                return false;

            double somaModulos = 0;
            for (int i = 1; i < partes.Length; i++)
            {
                if (LeitorCsvEngine.ParseNumeroResiliente(partes[i].AsSpan(), config, out double val))
                {
                    somaModulos += Math.Abs(val);
                }
            }

            return somaModulos < Epsilon;
        }

        private static double ParseNumero(string valor, ConfiguracaoImportacao config, int numeroLinha)
        {
            if (!LeitorCsvEngine.ParseNumeroResiliente(valor.AsSpan(), config, out double resultado))
            {
                throw new FormatException(
                    $"Valor numérico '{valor}' inválido para a configuração informada (linha {numeroLinha}).");
            }
            return resultado;
        }

        private static string? MapearPurposeSugerido(string descricao)
        {
            string descLimpa = descricao.Trim().ToUpperInvariant();

            return descLimpa switch
            {
                "V" or "VT" or "VANTE" or "FORE" => "vante",
                "R" or "RE" or "RÉ" or "BACK" => "re",
                "CHECK" => "check",
                _ => null
            };
        }
    }
}
