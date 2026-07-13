using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Infrastructure.Adapters.Leitores
{
    public class LeitorCsvPadrao : ILeitorArquivo
    {
        public string NomeFormato => "Texto/CSV Padrão";

        public IReadOnlyDictionary<string, PontoCoordenada> UltimosPontosConhecidos { get; } = new Dictionary<string, PontoCoordenada>();

        public List<Estacao> Ler(IEnumerable<string> linhas)
        {
            var leiturasBrutas = new List<LeituraEstacaoTotal>();
            var avisoImportacao = new List<string>();
            int numeroLinha = 0;
            var cultura = CultureInfo.InvariantCulture;

            foreach (var linha in linhas)
            {
                numeroLinha++;
                if (string.IsNullOrWhiteSpace(linha)) continue;
                if (linha.StartsWith("#") || linha.StartsWith("Estação")) continue;

                char separador = linha.Contains(";") ? ';' : ',';
                var colunas = linha.Split(separador);

                // Formato mínimo: 8 colunas obrigatórias. A coluna 8 (Tipo) é OPCIONAL.
                // [0] EstacaoOcupada  [1] Hi  [2] PontoVisado  [3] Observacao
                // [4] AngH           [5] AngV(Zenite)          [6] DI   [7] Hp   [8?] TipoExplicito
                if (colunas.Length < 8) continue;

                try
                {
                    double ahCompacto = double.Parse(colunas[4], cultura);
                    double avCompacto = double.Parse(colunas[5], cultura);
                    double diLida = double.Parse(colunas[6], cultura);

                    double ahDecimal = ConversorAngulos.DeFormatoCompacto(ahCompacto);
                    double avDecimal = ConversorAngulos.DeFormatoCompacto(avCompacto);

                    string observacao = colunas[3].Trim();

                    string? purposeSugerido = MapearPurposeSugerido(observacao);

                    // Coluna 8 opcional: preserva a intenção textual legada sem autoridade topológica.
                    if (colunas.Length >= 9 && int.TryParse(colunas[8].Trim(), out int tipoExplicito))
                    {
                        purposeSugerido = tipoExplicito switch
                        {
                            1 => "re",
                            2 => "vante",
                            3 => "irradiacao",
                            _ => purposeSugerido
                        };
                    }

                    var leitura = new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = colunas[0].Trim(),
                        AlturaInstrumento = double.Parse(colunas[1], cultura),
                        PontoVisado = colunas[2].Trim(),
                        Observacao = observacao,
                        AnguloHorizontal = ahDecimal,
                        AnguloVertical = avDecimal,
                        DistanciaInclinada = diLida,
                        AlturaPrisma = double.Parse(colunas[7], cultura),
                        Tipo = TipoLeitura.Irradiacao,
                        Purpose = purposeSugerido,
                        OrdemArquivo = numeroLinha
                    };

                    leiturasBrutas.Add(leitura);
                }
                catch (FormatException)
                {
                    avisoImportacao.Add($"Erro léxico na linha {numeroLinha}: Falha ao converter os dados numéricos. Leitura ignorada.");
                }
                catch (Exception ex)
                {
                    avisoImportacao.Add($"Erro inesperado na linha {numeroLinha}: {ex.Message}. Leitura ignorada.");
                }
            }

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
                        AlturaInstrumento = hiLeitura,
                        Leituras = new List<LeituraEstacaoTotal>()
                    };

                    estacoes.Add(estacaoAtual);
                    nomeEstacaoAtual = nomeOcupada;
                    hiAtual = hiLeitura;
                }

                leitura.SetupId = estacaoAtual!.Id;
                estacaoAtual.Leituras.Add(leitura);
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
                _ => null
            };
        }
    }
}
