using System;
using System.Collections.Generic;
using System.Globalization;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services
{
    public class LeituraArquivoService
    {
        /// <summary>
        /// Processa linhas de texto no formato CSV/TXT definido no manual.
        /// Formato esperado: Estação, HI, PontoVisado, Descrição, AH, AV, DI, HS, Tipo
        /// Separador assumido: Vírgula (,) ou Ponto-e-vírgula (;)
        /// </summary>
        public List<LeituraEstacaoTotal> LerArquivo(string[] linhas)
        {
            var leituras = new List<LeituraEstacaoTotal>();
            int numeroLinha = 0;

            foreach (var linha in linhas)
            {
                numeroLinha++;
                if (string.IsNullOrWhiteSpace(linha)) continue;
                if (linha.StartsWith("#") || linha.StartsWith("Estação")) continue; // Ignora cabeçalho

                // Detectar separador
                char separador = linha.Contains(";") ? ';' : ',';
                var colunas = linha.Split(separador);

                if (colunas.Length < 9)
                {
                    throw new Exception($"Erro na linha {numeroLinha}: Número insuficiente de colunas. Esperado 9, encontrado {colunas.Length}.");
                }

                try
                {
                    // 0: Estação (String)
                    // 1: HI (Double)
                    // 2: Ponto Visado (String)
                    // 3: Descrição (String)
                    // 4: Ang. Horiz (GGG.MMSS)
                    // 5: Ang. Vert (GGG.MMSS)
                    // 6: Dist. Inclinada (Metros)
                    // 7: HS (Altura Sinal/Prisma)
                    // 8: Tipo (0=Ré, 1=Irrad, 2=Vante)

                    var cultura = CultureInfo.InvariantCulture; // Assumindo ponto como separador decimal (Padrão internacional)

                    string estacao = colunas[0].Trim();
                    double hi = double.Parse(colunas[1], cultura);
                    string pid = colunas[2].Trim();
                    string desc = colunas[3].Trim();

                    double ahCompacto = double.Parse(colunas[4], cultura);
                    double avCompacto = double.Parse(colunas[5], cultura);
                    double di = double.Parse(colunas[6], cultura);
                    double hs = double.Parse(colunas[7], cultura);

                    int tipo = int.Parse(colunas[8]);

                    double ahDecimal = ConversorAngulos.DeFormatoCompacto(ahCompacto);
                    double avDecimal = ConversorAngulos.DeFormatoCompacto(avCompacto);

                    // Regra do TIPO
                    bool ehPoligonal = (tipo == 2);

                    var tipoEnum = (TipoLeitura)tipo;

                    var leitura = new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = estacao,
                        AlturaInstrumento = hi,
                        PontoVisado = pid,
                        Observacao = desc,
                        AnguloHorizontal = ahDecimal,
                        AnguloVertical = avDecimal,
                        DistanciaInclinada = di,
                        AlturaPrisma = hs,
                        Tipo = tipoEnum
                    };

                    leituras.Add(leitura);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Erro ao converter dados na linha {numeroLinha}: {ex.Message}");
                }
            }

            return leituras;
        }
    }
}