using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services.Leitores
{
    public class LeitorCsvPadrao : ILeitorArquivo
    {
        public string NomeFormato => "Texto/CSV Padrão";

        public List<Estacao> Ler(string[] linhas)
        {
            var leiturasBrutas = new List<LeituraEstacaoTotal>();
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
                    string descUpper = observacao.ToUpperInvariant();

                    string descNormalizado = descUpper
                        .Replace("Á", "A").Replace("À", "A").Replace("Ã", "A")
                        .Replace("É", "E").Replace("Ê", "E")
                        .Replace("Í", "I").Replace("Ó", "O").Replace("Õ", "O")
                        .Replace("Ú", "U").Replace("Ç", "C");

                    TipoLeitura tipo = TipoLeitura.Irradiacao;

                    // Vante/Poligonal (só classifica se não for Ré)
                    if (descNormalizado.Contains("ZERAG") ||
                        descNormalizado.Contains("BACKSIGHT") ||
                        descNormalizado.Contains(" BS ") ||
                        descNormalizado.Contains("RE ") ||
                        descNormalizado.Contains("RE(") ||
                        observacao.Contains("Ré (") || observacao.Contains("Ré(") ||
                        observacao.StartsWith("Ré "))
                    {
                        tipo = TipoLeitura.Re;
                    }
                    
                    // Ré (prioridade semântica: se marcar Ré, é Ré)
                    else if (descUpper.Contains("FECH") || descUpper.Contains("FEC") ||
                             descUpper.Contains("CHECK") || descUpper.Contains("CHK"))
                    {
                        tipo = TipoLeitura.Re;
                    }
                    else if (descUpper.Contains("VANTE") || descUpper.Contains("-V"))
                    {
                        
                        tipo =  TipoLeitura.Poligonal;
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
                        Tipo = tipo,
                        OrdemArquivo = numeroLinha
                    };

                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Linha {numeroLinha}: {leitura.EstacaoOcupada} → {leitura.PontoVisado} | Obs: '{observacao}' | Tipo: {tipo} | DI: {leitura.DistanciaInclinada}");


                    leiturasBrutas.Add(leitura);
                }
                catch
                {
                    continue;
                }
            }

            var estacoes = leiturasBrutas.GroupBy(l => l.EstacaoOcupada).Select(grupo => new Estacao
            {
                Nome = grupo.Key,
                AlturaInstrumento = grupo.First().AlturaInstrumento,
                Leituras = grupo.ToList()
            }).ToList();

            return estacoes;
        }
    }
}
