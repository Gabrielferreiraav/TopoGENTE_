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

        public List<Estacao> Ler(string[] linhas)
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

                // Formato mínimo utilizado aqui:
                // 0 EstacaoOcupada, 1 Hi, 2 PontoVisado, 3 Observacao, 4 AngH, 5 AngV(Zenite), 6 DI, 7 Hp
                if (colunas.Length < 8) continue;

                try
                {
                    double ahCompacto = double.Parse(colunas[4], cultura);
                    double avCompacto = double.Parse(colunas[5], cultura);
                    double diLida = double.Parse(colunas[6], cultura);

                    double ahDecimal = ConversorAngulos.DeFormatoCompacto(ahCompacto);
                    double avDecimal = ConversorAngulos.DeFormatoCompacto(avCompacto);

                    string observacao = colunas[3].Trim();

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

            var estacoes = leiturasBrutas
                .GroupBy(l => l.EstacaoOcupada)
                .Select(grupo => new Estacao
                {
                    Nome = grupo.Key,
                    AlturaInstrumento = grupo.First().AlturaInstrumento,
                    Leituras = grupo.ToList()
                })
                .ToList();

            return estacoes;
        }
    }
}
