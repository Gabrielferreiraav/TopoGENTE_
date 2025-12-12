using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services.Leitores
{
    public class LeitorFbk : ILeitorArquivo
    {
        public string NomeFormato => "FBK";
        public bool IdentificarFormato(string cabecalhoArquivo)
        {
            return cabecalhoArquivo.Contains("UNIT") || cabecalhoArquivo.Contains("STN") || cabecalhoArquivo.Contains("AD VA");
        }

        public List<Estacao> Ler(string[] linhas)
        {
            var estacoes = new List<Estacao>();
            Estacao estacoAtual = null;
            double alturaPrisma = 0.0;
            var cultura = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var linhaRaw in linhas)
            {
                string linha = linhaRaw.Trim();
                if (string.IsNullOrWhiteSpace(linha) || linha.StartsWith("!")) // Ignorar linhas vazias e comentários
                    continue;
                var partes = linha.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) continue;

                string comando = partes[0].ToUpper();

                try
                {
                    if (comando == "STN")
                    {
                        string nome = partes[1].Replace("\"", "");
                        double hi = double.Parse(partes[2], cultura);
                        estacoAtual = new Estacao
                        {
                            Nome = nome,
                            AlturaInstrumento = hi
                        };
                        estacoes.Add(estacoAtual);
                    }
                    else if (comando == "PRISMA")
                    {
                        alturaPrisma = double.Parse(partes[1], cultura);
                    }
                    else if (comando == "BS")
                    {
                        if (estacoAtual == null) continue;
                        string alvoNome = partes[1].Replace("\"", "");
                        double angulo = double.Parse(partes[2], cultura);
                        estacoAtual.Leituras.Add(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angulo,
                            Tipo = TipoLeitura.Re,
                            Observacao = "RE (BS)"
                        });
                    }
                    else if (comando == "AD" && partes.Length > 2 && partes[1] == "VA")
                    {
                        if (estacoAtual == null) continue;
                        string alvoNome = partes[2].Replace("\"", "");
                        double angH = double.Parse(partes[3], cultura);
                        double dist = double.Parse(partes[4], cultura);
                        double angV = double.Parse(partes[5], cultura);

                        string descricao = "";
                        if (partes.Length > 6)
                        {
                            descricao = partes[6].Replace("\"", "");
                        }

                        var tipoLeitura = TipoLeitura.Irradiacao;
                        if (descricao.ToUpper().Contains("V") || descricao.ToUpper().Contains("VANTE") || descricao.StartsWith("E"))
                        {
                            tipoLeitura = TipoLeitura.Poligonal;
                        }
                        if (descricao.ToUpper().Contains("RE") || descricao.ToUpper().Contains("R"))
                        {
                            tipoLeitura = TipoLeitura.Re;
                        }
                        estacoAtual.Leituras.Add(new LeituraEstacaoTotal
                        {
                            EstacaoOcupada = estacoAtual.Nome,
                            PontoVisado = alvoNome,
                            AlturaInstrumento = estacoAtual.AlturaInstrumento,
                            AlturaPrisma = alturaPrisma,
                            AnguloHorizontal = angH,
                            AnguloVertical = angV,
                            DistanciaInclinada = dist,
                            Observacao = descricao,
                            Tipo = tipoLeitura
                        });
                    }
                }
                catch
                {

                }
            }
            return estacoes;
        }
    }
}
