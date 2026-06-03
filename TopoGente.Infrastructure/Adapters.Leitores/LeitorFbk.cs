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

        public List<Estacao> Ler(IEnumerable<string> linhas)
        {
            _ultimosAvisos.Clear();

            var estacoes = new List<Estacao>();
            Estacao estacaoAtual = null;
            double alturaPrisma = 0.0;
            var coordenadasConhecidas = new Dictionary<string, PontoCoordenada>();
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
                    if ((comando == "NEZ" && partes.Length >= 4) || (comando == "NE" && partes.Length >= 3))
                    {
                        string nomePonto = partes[1].Replace("\"", "");
                        double y = double.Parse(partes[2], cultura);
                        double x = double.Parse(partes[3], cultura);
                        double z = (comando == "NEZ" && partes.Length > 4)
                            ? double.Parse(partes[4], cultura)
                            : 0.0;

                        if (!coordenadasConhecidas.ContainsKey(nomePonto))
                        {
                            coordenadasConhecidas.Add(nomePonto, new PontoCoordenada
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
                            var existente = coordenadasConhecidas[nomePonto];
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
                        }
                    }

                    // ── STN — Ocupação de estação ──
                    else if (comando == "STN" && partes.Length >= 3)
                    {
                        string nome = partes[1].Replace("\"", "");
                        double hi = double.Parse(partes[2], cultura);

                        estacaoAtual = new Estacao
                        {
                            Nome = nome,
                            AlturaInstrumento = hi
                        };

                        if (coordenadasConhecidas.ContainsKey(nome))
                        {
                            estacaoAtual.CoordenadaConhecida = coordenadasConhecidas[nome];
                        }

                        estacoes.Add(estacaoAtual);
                    }

                    // ── PRISM / PRISMA — Altura do sinal ──
                    else if ((comando == "PRISM" || comando == "PRISMA") && partes.Length >= 2)
                    {
                        alturaPrisma = double.Parse(partes[1], cultura);
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
                        double angulo = double.Parse(partes[2], cultura);

                        estacaoAtual.Leituras.Add(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacaoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacaoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angulo,
                            Tipo = TipoLeitura.Re,
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
                        double angH = double.Parse(partes[3], cultura);
                        double dist = double.Parse(partes[4], cultura);
                        double angV = double.Parse(partes[5], cultura);

                        string descricao = "";
                        if (partes.Length > 6)
                        {
                            descricao = partes[6].Replace("\"", "");
                        }

                        // Pré-classificação por igualdade estrita.
                        // A classificação topológica definitiva é feita pelo ClassificadorGrafo (Core).
                        string descLimpa = descricao.Trim().ToUpperInvariant();
                        var tipoLeitura = descLimpa switch
                        {
                            "V" or "VANTE" => TipoLeitura.Poligonal,
                            "R" or "RE" or "RÉ" => TipoLeitura.Re,
                            _ => TipoLeitura.Irradiacao
                        };

                        estacaoAtual.Leituras.Add(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacaoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacaoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angH,
                            AnguloVertical = angV,
                            DistanciaInclinada = dist,
                            Observacao = descricao,
                            Tipo = tipoLeitura
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

            // Diagnóstico pós-parsing: se nenhuma estação foi extraída e houve falhas, algo está errado
            if (estacoes.Count == 0 && falhas.Count > 0)
            {
                throw new FormatException(
                    $"Nenhuma estação foi extraída do arquivo FBK. " +
                    $"{falhas.Count} linha(s) falharam no parsing. " +
                    $"Verifique se o separador decimal é ponto (.) e não vírgula (,).\n" +
                    $"Primeiras falhas:\n" +
                    string.Join("\n", falhas.Take(5)));
            }

            // Registrar falhas parciais como avisos (dados foram extraídos, mas com perdas)
            if (falhas.Count > 0)
            {
                _ultimosAvisos.Add(
                    $"{falhas.Count} linha(s) ignorada(s) durante a importação FBK:\n" +
                    string.Join("\n", falhas.Take(10)));
            }

            return estacoes;
        }
    }
}
