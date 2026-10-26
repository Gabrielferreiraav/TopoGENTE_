using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

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

        public List<Estacao> Ler(IEnumerable<string> linhas)
        {
            _ultimosAvisos.Clear();
            _ultimosPontosConhecidos.Clear();

            var estacoes = new List<Estacao>();
            Estacao? estacaoAtual = null;
            double alturaPrisma = 0.0;
            var cultura = System.Globalization.CultureInfo.InvariantCulture;
            var falhas = new List<string>();
            int numeroLinha = 0;

            foreach (var linhaRaw in linhas)
            {
                numeroLinha++;
                string linha = linhaRaw.Trim();
                if (string.IsNullOrWhiteSpace(linha) || linha.StartsWith("!"))
                    continue;

                var partes = linha.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) continue;

                string comando = partes[0].ToUpper();

                try
                {
                    // ── UNIT — Validação de unidades geodésicas (Fail-Fast) ──
                    if (comando == "UNIT")
                    {
                        string unidadeDistancia = partes.Length > 1 ? partes[1].ToUpper() : "";
                        string unidadeAngular = partes.Length > 2 ? partes[2].ToUpper() : "";

                        if (unidadeDistancia != "METER" && unidadeDistancia != "METRE"
                            && unidadeDistancia != "")
                        {
                            throw new NotSupportedException(
                                $"Unidade de distância '{unidadeDistancia}' não suportada (linha {numeroLinha}). " +
                                "Converta o arquivo para METER antes de importar.");
                        }

                        if (unidadeAngular != "DECDEG" && unidadeAngular != "")
                        {
                            throw new NotSupportedException(
                                $"Unidade angular '{unidadeAngular}' não suportada (linha {numeroLinha}). " +
                                "Converta o arquivo para DECDEG antes de importar.");
                        }

                        continue;
                    }

                    // ── NEZ / NE — Coordenadas de controle ──
                    if (comando == "NEZ" || comando == "NE")
                    {
                        int tamanhoMinimo = comando == "NEZ" ? 5 : 4;

                        // ── Detecção de linha sentinela/residual (padding de coletor de campo) ──
                        // Critério estrutural + semântico com tolerância ε = 1e-6:
                        // 1. Tokens insuficientes para conter nome + coordenadas, OU
                        //    o token de nome é um número decimal (sem identificador alfanumérico válido).
                        // 2. A soma dos módulos de todos os valores numéricos presentes < ε.
                        // Isso garante que NEZ 0 100.0 200.0 300.0 (ponto real de nome "0") NÃO seja descartado.
                        if (EhLinhaResiduaSentinela(partes, tamanhoMinimo, cultura))
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
                        if (!NomePontoValido(nomePonto, cultura))
                        {
                            throw new FormatException(
                                $"{comando} exige nome de ponto válido (não decimal) antes das coordenadas (linha {numeroLinha}).");
                        }

                        double y = ParseNumeroDecimal(partes[2], cultura, numeroLinha);
                        double x = ParseNumeroDecimal(partes[3], cultura, numeroLinha);
                        double z = (comando == "NEZ" && partes.Length > 4)
                            ? ParseNumeroDecimal(partes[4], cultura, numeroLinha)
                            : 0.0;

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
                            // Detecção de conflito geodésico (tolerância 1mm)
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
                        double hi = ParseNumeroDecimal(partes[2], cultura, numeroLinha);

                        estacaoAtual = new Estacao
                        {
                            Nome = nome,
                            AlturaInstrumento = hi
                        };

                        if (_ultimosPontosConhecidos.ContainsKey(nome))
                        {
                            estacaoAtual.CoordenadaConhecida = _ultimosPontosConhecidos[nome];
                        }

                        estacoes.Add(estacaoAtual);
                    }

                    // ── PRISM / PRISMA — Altura do sinal ──
                    else if ((comando == "PRISM" || comando == "PRISMA") && partes.Length >= 2)
                    {
                        alturaPrisma = ParseNumeroDecimal(partes[1], cultura, numeroLinha);
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
                        double angulo = ParseNumeroDecimal(partes[2], cultura, numeroLinha);

                        estacaoAtual.AdicionarVisada(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacaoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacaoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angulo,
                            Tipo = TipoLeitura.Irradiacao,
                            Purpose = "re",
                            Observacao = "RE (BS)"
                        });
                    }

                    // ── AD VA — Observação polar (ângulo + distância + zenital) ──
                    else if (comando == "AD" && partes.Length >= 6 && partes[1] == "VA")
                    {
                        if (estacaoAtual == null)
                        {
                            _ultimosAvisos.Add(
                                $"Linha {numeroLinha} ignorada: comando AD VA sem estação ativa (STN ausente).");
                            continue;
                        }

                        string alvoNome = partes[2].Replace("\"", "");
                        double angH = ParseNumeroDecimal(partes[3], cultura, numeroLinha);
                        double dist = ParseNumeroDecimal(partes[4], cultura, numeroLinha);
                        double angV = ParseNumeroDecimal(partes[5], cultura, numeroLinha);

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
                            AnguloHorizontal = angH,
                            AnguloVertical = angV,
                            DistanciaInclinada = dist,
                            Observacao = descricao,
                            Tipo = TipoLeitura.Irradiacao,
                            Purpose = MapearPurposeSugerido(descricao)
                        });
                    }
                }
                catch (NotSupportedException)
                {
                    // Exceções de validação de unidades devem propagar imediatamente (Fail-Fast)
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
                    $"Primeiras falhas:\n" +
                    string.Join("\n", falhas.Take(5)));
            }

            return estacoes;
        }

        /// <summary>
        /// Valida se o identificador do ponto é aceitável como nome de ponto topográfico.
        /// Regras:
        ///   - Não pode ser nulo ou vazio.
        ///   - Inteiros puros são permitidos (ex: "1", "100") — consistência com AD VA numérico.
        ///   - Decimais (ex: "0.0000", "1.5") são rejeitados — são coordenadas, não identificadores.
        /// </summary>
        private static bool NomePontoValido(string nomePonto, System.Globalization.CultureInfo cultura)
        {
            if (string.IsNullOrWhiteSpace(nomePonto))
                return false;

            // Inteiros puros são identificadores válidos (ex: AD VA 1, AD VA 125)
            if (long.TryParse(nomePonto, out _))
                return true;

            // Decimais (ponto ou vírgula) são coordenadas, não identificadores
            string normalizado = nomePonto.Replace(',', '.');
            return !double.TryParse(normalizado, System.Globalization.NumberStyles.Float, cultura, out _);
        }

        /// <summary>
        /// Detecta linhas sentinela/residuais geradas por coletores de campo.
        /// Uma linha é sentinela quando:
        ///   1. Não possui tokens suficientes para um NEZ/NE com identificador E coordenadas, OU
        ///      o token na posição do nome é um decimal (ausência de identificador real).
        ///   2. A soma dos módulos de todos os valores numéricos na linha é menor que ε = 1e-6.
        /// Isso garante que `NEZ 0 100.0 200.0 300.0` (ponto real com nome "0") NÃO seja descartado.
        /// </summary>
        private static bool EhLinhaResiduaSentinela(string[] partes, int tamanhoMinimo, System.Globalization.CultureInfo cultura)
        {
            const double Epsilon = 1e-6;

            bool semIdentificadorValido;

            if (partes.Length < tamanhoMinimo)
            {
                // Tokens insuficientes: não há espaço para um nome + coordenadas completas
                semIdentificadorValido = true;
            }
            else
            {
                // Verifica se o token de nome é um decimal (coordenada, não identificador)
                string tokenNome = partes[1].Replace("\"", "");
                string tokenNormalizado = tokenNome.Replace(',', '.');
                bool nomeEhDecimal = double.TryParse(tokenNormalizado, System.Globalization.NumberStyles.Float, cultura, out _)
                                     && !long.TryParse(tokenNome, out _);
                semIdentificadorValido = nomeEhDecimal;
            }

            if (!semIdentificadorValido)
                return false;

            // Soma os módulos de todos os tokens numéricos presentes (exceto o comando)
            double somaModulos = 0.0;
            for (int i = 1; i < partes.Length; i++)
            {
                string t = partes[i].Replace(',', '.').Replace("\"", "");
                if (double.TryParse(t, System.Globalization.NumberStyles.Float, cultura, out double val))
                    somaModulos += Math.Abs(val);
            }

            return somaModulos < Epsilon;
        }
        private static double ParseNumeroDecimal(string valor, System.Globalization.CultureInfo cultura, int numeroLinha)
        {
            if (valor.Contains(','))
            {
                throw new FormatException(
                    $"Valor numérico '{valor}' usa vírgula decimal ou separador de milhar não suportado (linha {numeroLinha}).");
            }

            return double.Parse(valor, System.Globalization.NumberStyles.Float, cultura);
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
