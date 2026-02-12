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

                    double ahDecimal = ConversorAngulos.DeFormatoCompacto(ahCompacto);
                    double avDecimal = ConversorAngulos.DeFormatoCompacto(avCompacto);

                    string observacao = colunas[3].Trim();
                    string descUpper = observacao.ToUpperInvariant();

                    TipoLeitura tipo = TipoLeitura.Irradiacao;

                    // Fechamento/check deve ser Poligonal (vante de fechamento)
                    if (descUpper.Contains("FECH") || descUpper.Contains("FEC") ||
                        descUpper.Contains("CHECK") || descUpper.Contains("CHK"))
                    {
                        tipo = TipoLeitura.Poligonal;
                    }

                    // Vante/Poligonal
                    if (descUpper.Contains("VANTE") || descUpper.Contains("-V") || descUpper.StartsWith("M"))
                    {
                        tipo = TipoLeitura.Poligonal;
                    }

                    // Ré (prioridade semântica: se marcar Ré, é Ré)
                    if (descUpper.Contains("RE") || descUpper.Contains("RÉ") || descUpper.Contains("BS") || descUpper.Contains("BACKSIGHT"))
                    {
                        tipo = TipoLeitura.Re;
                    }

                    var leitura = new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = colunas[0].Trim(),
                        AlturaInstrumento = double.Parse(colunas[1], cultura),
                        PontoVisado = colunas[2].Trim(),
                        Observacao = observacao,
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
