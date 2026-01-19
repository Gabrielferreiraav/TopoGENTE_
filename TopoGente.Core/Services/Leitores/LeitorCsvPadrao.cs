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

                if (colunas.Length < 9) continue;

                try
                {
                    double ahCompacto = double.Parse(colunas[4], cultura);
                    double avCompacto = double.Parse(colunas[5], cultura);

                    double ahDecimal = ConversorAngulos.DeFormatoCompacto(ahCompacto);
                    double avDecimal = ConversorAngulos.DeFormatoCompacto(avCompacto);

                    TipoLeitura tipo = TipoLeitura.Irradiacao;
                    string descUpper = int.Parse(colunas[8]).ToString().ToUpper();

                    if (descUpper.Contains("VANTE") || descUpper.Contains("-V") || descUpper.StartsWith("M"))
                    {
                        tipo = TipoLeitura.Poligonal;
                    }
                    if (descUpper.Contains("RE") || descUpper.Contains("RÉ") || descUpper.Contains("BS"))
                    {
                        tipo = TipoLeitura.Re;
                    }

                    if (int.TryParse(colunas[8], out int tipoInt))
                        tipo = (TipoLeitura)tipoInt;

                    var leitura = new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = colunas[0].Trim(),
                        AlturaInstrumento = double.Parse(colunas[1], cultura),
                        PontoVisado = colunas[2].Trim(),
                        Observacao = colunas[3].Trim(),
                        AnguloHorizontal = ahDecimal,
                        AnguloVertical = avDecimal,
                        DistanciaInclinada = double.Parse(colunas[6], cultura),
                        AlturaPrisma = double.Parse(colunas[7], cultura),
                        Tipo = tipo
                    };

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

            if (estacoes.Count > 0)
            {
                var primeiraEstacao = estacoes.First();

                // Verifica se já não foi setada
                if (primeiraEstacao.CoordenadaConhecida == null)
                {
                    primeiraEstacao.CoordenadaConhecida = new PontoCoordenada
                    {
                        Nome = primeiraEstacao.Nome,
                        X = 1000.0,
                        Y = 1000.0,
                        Z = 100.0
                    };
                }
            }

            return estacoes;

        }
    }

}
