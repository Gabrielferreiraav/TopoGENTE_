using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TopoGente.Core.Entities.Pericia;
using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Services
{
    /// <summary>
    /// Serviço de domínio puro para formatação do memorial pericial de cálculo e rastreabilidade em formato monospaçado ASCII / Laudo Pericial.
    /// Estruturado em 3 capítulos conforme as diretrizes periciais e da NBR 13.133.
    /// </summary>
    public sealed class FormatadorMemorialPericialService : IFormatadorMemorialPericialService
    {
        private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

        public string FormatarMemorialCompleto(MemorialRastreabilidadePericial memorial)
        {
            if (memorial == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(FormatarCapitulo1TrilhaTopologica(memorial.TrilhaTopologica));
            sb.AppendLine();
            sb.AppendLine(FormatarCapitulo2MemorialLances(memorial.MemorialLances));
            sb.AppendLine();
            sb.AppendLine(FormatarCapitulo3Ajustamento(memorial.MemoriaAjustamento));
            return sb.ToString();
        }

        public string FormatarCapitulo1TrilhaTopologica(TrilhaTopologicaDto trilha)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine("CAPÍTULO 1: HISTÓRICO OPERACIONAL E OCUPAÇÃO DE ESTAÇÕES");
            sb.AppendLine("========================================================================================================================");

            if (trilha == null)
            {
                sb.AppendLine("  (Histórico operacional não disponível)");
                return sb.ToString();
            }

            if (!string.IsNullOrWhiteSpace(trilha.ResumoClassificacaoGrafo))
            {
                string resumoDidatico = trilha.ResumoClassificacaoGrafo
                    .Replace("Grafo topológico composto por", "Levantamento composto por")
                    .Replace("sessão(ões) física(s) de estação", "ocupação(ões) de estação")
                    .Replace("ponto(s) homólogo(s) auditado(s)", "ponto(s) de controle com dupla visada")
                    .Replace("sentinela(s) descartada(s)", "registro(s) nulo(s) descartado(s)");
                sb.AppendLine($"Estrutura Operacional: {resumoDidatico}");
                sb.AppendLine();
            }

            // 1.1 Ocupações Físicas de Estação
            sb.AppendLine("1.1 Histórico de Ocupações de Estação e Funções no Levantamento:");
            if (trilha.Estacoes == null || trilha.Estacoes.Count == 0)
            {
                sb.AppendLine("  (Nenhuma estação registrada)");
            }
            else
            {
                sb.AppendLine("+---------+--------+---------+-------------------------+-----------+---------+------------------+-------------------------------------------------------------+");
                sb.AppendLine("| Estação | Sessão | HI (m)  | Função no Levantamento  | Ré Ref.   | Irrad.  | Linhas Caderneta | Finalidade da Ocupação / Decisão Técnica                    |");
                sb.AppendLine("+---------+--------+---------+-------------------------+-----------+---------+------------------+-------------------------------------------------------------+");

                foreach (var est in trilha.Estacoes)
                {
                    string linhasStr = FormatarLinhasOrigem(est.LinhasArquivoOrigem);
                    var linhasJust = QuebrarTexto(est.Justificativa, 59);
                    string papelStr = FormatarPapelDidatico(est.Papel);
                    string reExibicao = string.IsNullOrWhiteSpace(est.PontoReReferencia) ? "---" : est.PontoReReferencia;

                    sb.AppendLine($"| {est.NomeEstacao,-7} | {est.NumeroSessao,6} | {est.AlturaInstrumento,7:F4} | {papelStr,-23} | {reExibicao,-9} | {est.TotalIrradiacoesVinculadas,7} | {linhasStr,-16} | {linhasJust[0],-59} |");
                    for (int k = 1; k < linhasJust.Count; k++)
                    {
                        sb.AppendLine($"| {"",-7} | {"",6} | {"",7} | {"",-23} | {"",-9} | {"",7} | {"",-16} | {linhasJust[k],-59} |");
                    }
                }

                sb.AppendLine("+---------+--------+---------+-------------------------+-----------+---------+------------------+-------------------------------------------------------------+");
            }
            sb.AppendLine();

            // 1.2 Pontos Homólogos de Detalhe
            sb.AppendLine("1.2 Conferência de Pontos com Dupla Visada (Checkpoints de Controle):");
            if (trilha.Homologos == null || trilha.Homologos.Count == 0)
            {
                sb.AppendLine("  (Nenhum ponto com dupla visada registrado no levantamento)");
            }
            else
            {
                sb.AppendLine("+-------+-------------------------+-------------------------+-------------+-------------------------------------------------------------+");
                sb.AppendLine("| Ponto | 1ª Visada (Est / Linha) | 2ª Visada (Est / Linha) | MDT Adotado | Justificativa Técnica                                       |");
                sb.AppendLine("+-------+-------------------------+-------------------------+-------------+-------------------------------------------------------------+");

                foreach (var h in trilha.Homologos)
                {
                    string vis1 = $"{h.EstacaoPrimeiraVisada} (Linha {h.OrdemPrimeiraVisada})";
                    string vis2 = $"{h.EstacaoSegundaVisada} (Linha {h.OrdemSegundaVisada})";
                    string mdt = h.AdotadoParaMdt ? "SIM (1ª)" : "NÃO";
                    var linhasJust = QuebrarTexto(h.Justificativa, 59);

                    sb.AppendLine($"| {h.NomePonto,-5} | {vis1,-23} | {vis2,-23} | {mdt,-11} | {linhasJust[0],-59} |");
                    for (int k = 1; k < linhasJust.Count; k++)
                    {
                        sb.AppendLine($"| {"",-5} | {"",-23} | {"",-23} | {"",-11} | {linhasJust[k],-59} |");
                    }
                }

                sb.AppendLine("+-------+-------------------------+-------------------------+-------------+-------------------------------------------------------------+");
            }
            sb.AppendLine();

            // 1.3 Sentinelas Descartadas
            sb.AppendLine("1.3 Linhas em Branco ou Registros Nulos Descartados:");
            if (trilha.Sentinelas == null || trilha.Sentinelas.Count == 0)
            {
                sb.AppendLine("  (Nenhuma linha residual ou registro nulo descartado)");
            }
            else
            {
                sb.AppendLine("+-------+-------------------------------+-------------------------------------------------------------+");
                sb.AppendLine("| Linha | Conteúdo Bruto                | Motivo do Descarte                                          |");
                sb.AppendLine("+-------+-------------------------------+-------------------------------------------------------------+");

                foreach (var s in trilha.Sentinelas)
                {
                    var linhasMotivo = QuebrarTexto(s.MotivoDescarte, 59);
                    sb.AppendLine($"| {s.LinhaArquivo,5} | {Truncar(s.ConteudoBruto, 29),-29} | {linhasMotivo[0],-59} |");
                    for (int k = 1; k < linhasMotivo.Count; k++)
                    {
                        sb.AppendLine($"| {"",5} | {"",-29} | {linhasMotivo[k],-59} |");
                    }
                }

                sb.AppendLine("+-------+-------------------------------+-------------------------------------------------------------+");
            }

            return sb.ToString().TrimEnd();
        }

        public string FormatarCapitulo2MemorialLances(MemorialLancesDto lances)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine("CAPÍTULO 2: MEMORIAL DE REDUÇÃO GEOMÉTRICA E TRANSPORTE ANALÍTICO (LANCE A LANCE)");
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine("Modelagem de Redução Geométrica e Orientação:");
            sb.AppendLine("  Distância Horizontal (DH):  DH = DI · sin(Z)");
            sb.AppendLine("  Desnível Trigonométrico:    ΔZ = DI · cos(Z) + HI - AP");
            sb.AppendLine("  Projeção Este:              ΔE = DH · sin(Az)");
            sb.AppendLine("  Projeção Norte:             ΔN = DH · cos(Az)");
            sb.AppendLine();

            if (lances == null || lances.Lances == null || lances.Lances.Count == 0)
            {
                sb.AppendLine("  (Nenhum lance de caminhamento registrado)");
                return sb.ToString().TrimEnd();
            }

            // 2.1 Observações Instrumentais e Reduções
            sb.AppendLine("2.1 Observações Instrumentais Brutas e Reduções Geométricas:");
            sb.AppendLine("+----+--------------+-------+--------------+--------------+----------+--------+--------+----------+----------+--------------+");
            sb.AppendLine("| N° | De -> Para   | Ré    | Âng. Hz (AH) | Âng. Zen (Z) | DI (m)   | HI (m) | AP (m) | DH (m)   | ΔZ (m)   | Azimute Vante|");
            sb.AppendLine("+----+--------------+-------+--------------+--------------+----------+--------+--------+----------+----------+--------------+");

            foreach (var l in lances.Lances)
            {
                string dePara = $"{l.EstacaoOrigem} -> {l.EstacaoVante}";
                string ahGms = FormatarGms(l.AnguloHzGraus);
                string avGms = FormatarGms(l.AnguloVGraus);
                string azGms = FormatarGms(l.AzimuteVanteGraus);

                sb.AppendLine($"| {l.OrdemLance,2} | {dePara,-12} | {l.PontoRe,-5} | {ahGms,12} | {avGms,12} | {l.DistanciaInclinada,8:F4} | {l.AlturaInstrumento,6:F4} | {l.AlturaAlvo,6:F4} | {l.DistanciaHorizontal,8:F4} | {l.DeltaZ,+8:F4} | {azGms,12} |");
            }

            sb.AppendLine("+----+--------------+-------+--------------+--------------+----------+--------+--------+----------+----------+--------------+");
            sb.AppendLine("Convenções: AH = Ângulo Horizontal | Z = Ângulo Zenital | DI = Distância Inclinada | DH = Distância Horizontal | HI = Altura Instrumento | AP = Altura Prisma");
            sb.AppendLine($"Perímetro Total Desenvolvido: {lances.PerimetroTotal:F4} m");
            sb.AppendLine();

            // 2.2 Transporte de Coordenadas Brutas
            sb.AppendLine("2.2 Transporte de Coordenadas Cartesianas Brutas:");
            sb.AppendLine("+----+---------+---------------+---------------+---------------+---------------+---------------+");
            sb.AppendLine("| N° | Vértice | ΔEste Bruto(m)| ΔNorte Brut(m)| Este Bruta(m) | Norte Bruta(m)| Cota Bruta(m) |");
            sb.AppendLine("+----+---------+---------------+---------------+---------------+---------------+---------------+");

            var primeiro = lances.Lances[0];
            double eOrigem = primeiro.CoordenadaEBruta - primeiro.DeltaEBruto;
            double nOrigem = primeiro.CoordenadaNBruta - primeiro.DeltaNBruto;
            double zOrigem = primeiro.CoordenadaZBruta - primeiro.DeltaZ;

            sb.AppendLine($"|  0 | {primeiro.EstacaoOrigem,-7} |           --- |           --- | {eOrigem,13:F4} | {nOrigem,13:F4} | {zOrigem,13:F4} |");

            foreach (var l in lances.Lances)
            {
                sb.AppendLine($"| {l.OrdemLance,2} | {l.EstacaoVante,-7} | {l.DeltaEBruto,+13:F4} | {l.DeltaNBruto,+13:F4} | {l.CoordenadaEBruta,13:F4} | {l.CoordenadaNBruta,13:F4} | {l.CoordenadaZBruta,13:F4} |");
            }

            sb.AppendLine("+----+---------+---------------+---------------+---------------+---------------+---------------+");

            return sb.ToString().TrimEnd();
        }

        public string FormatarCapitulo3Ajustamento(MemoriaAjustamentoDto ajustamento)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine("CAPÍTULO 3: MEMÓRIA ANALÍTICA DE AJUSTAMENTO E RATEIO DE ERROS");
            sb.AppendLine("========================================================================================================================");

            if (ajustamento == null)
            {
                sb.AppendLine("  (Memória de ajustamento não disponível)");
                return sb.ToString().TrimEnd();
            }

            string nomeEstrategia = ajustamento.Tipo switch
            {
                TipoEstrategiaAjustamento.Bowditch => "COMPENSAÇÃO PROPORCIONAL DE BOWDITCH (NBR 13.133)",
                TipoEstrategiaAjustamento.PoligonalEnquadrada => "COMPENSAÇÃO DE POLIGONAL ENQUADRADA (NBR 13.133)",
                TipoEstrategiaAjustamento.VerificacaoLinhaBase => "VERIFICAÇÃO DE LINHA DE BASE (NBR 13.133 — CONFRONTO GEODÉSICO)",
                _ => "SEM COMPENSAÇÃO (POLIGONAL ABERTA / OPERAÇÃO ABORTADA)"
            };

            bool geralAprovado = ajustamento.AngularAprovado && ajustamento.LinearAprovado && ajustamento.AltimetricoAprovado;
            string statusNorma = ajustamento.Tipo == TipoEstrategiaAjustamento.SemAjustamento
                ? "NÃO COMPENSADO"
                : (geralAprovado ? "CONFORME (Aprovado nas tolerâncias normativas)" : "REPROVADO");

            sb.AppendLine($"Estratégia Aplicada: {nomeEstrategia}");
            sb.AppendLine($"Status Normativo:    {statusNorma}");
            sb.AppendLine();

            if (ajustamento.Tipo == TipoEstrategiaAjustamento.VerificacaoLinhaBase && ajustamento.LaudoLinhaBase != null)
            {
                var l = ajustamento.LaudoLinhaBase;
                sb.AppendLine("3.1 Confronto Geodésico com Vértice de Controle:");
                sb.AppendLine($"  * Estação de Partida:   {l.NomePontoPartida}");
                sb.AppendLine($"  * Marco de Chegada:     {l.NomePontoChegada}");
                sb.AppendLine($"  * Distância Teórica:    {l.DistanciaTeórica:F4} m");
                sb.AppendLine($"  * Azimute Teórico:      {FormatarGms(l.AzimuteTeóricoGraus)} | Azimute Medido: {FormatarGms(l.AzimuteMedidoGraus)}");
                sb.AppendLine($"  * Discrepância Angular: {l.ErroAngularSegundos:F1}\" (Tolerância Ta: {l.ToleranciaAngularSegundos:F1}\") -> {(ajustamento.AngularAprovado ? "CONFORME" : "REPROVADO")}");
                sb.AppendLine($"  * Discrepâncias:        ΔE = {l.DeltaEsteMetros:+0.0000;-0.0000;0.0000} m | ΔN = {l.DeltaNorteMetros:+0.0000;-0.0000;0.0000} m | ΔZ = {l.DeltaZMetros:+0.0000;-0.0000;0.0000} m");
                sb.AppendLine($"  * Discrepância Linear:  {l.ErroLinearMm:F1} mm (Tolerância Tl: {l.ToleranciaLinearMm:F1} mm) -> {(ajustamento.LinearAprovado ? "CONFORME" : "REPROVADO")}");
                double denomLinha = l.PrecisaoRelativa > 0 ? Math.Round(1.0 / l.PrecisaoRelativa) : 0;
                sb.AppendLine($"  * Precisão Planar:      1:{denomLinha}");
                sb.AppendLine("  (Transporte analítico puro mantido sem rateio proporcional de coordenadas)");
            }
            else if (ajustamento.Tipo == TipoEstrategiaAjustamento.Bowditch || ajustamento.Tipo == TipoEstrategiaAjustamento.PoligonalEnquadrada)
            {
                sb.AppendLine("3.1 Balanço de Fechamento e Tolerâncias Normativas:");
                string angGms = FormatarGms(ajustamento.ErroFechamentoAngularSegundos / 3600.0);
                sb.AppendLine($"  * Fechamento Angular:     fa = {angGms} (Admissível Ta = {ajustamento.ToleranciaAngularSegundos:F2}\") -> {(ajustamento.AngularAprovado ? "CONFORME" : "REPROVADO")}");
                sb.AppendLine($"  * Fechamento Linear:      fL = {ajustamento.ErroFechamentoLinearMetros:F4} m (Admissível TL = {ajustamento.ToleranciaLinearMetros:F4} m) -> {(ajustamento.LinearAprovado ? "CONFORME" : "REPROVADO")}");
                double denomLinear = ajustamento.PrecisaoRelativaObtida > 0 ? Math.Round(1.0 / ajustamento.PrecisaoRelativaObtida) : 0;
                sb.AppendLine($"  * Precisão Linear:        1:{denomLinear} -> {(ajustamento.LinearAprovado ? "CONFORME" : "REPROVADO")}");
                sb.AppendLine($"  * Fechamento Altimétrico: fz = {ajustamento.ErroAltimetricoMetros:F4} m (Admissível Tz = {ajustamento.ToleranciaAltimetricaMetros:F4} m) -> {(ajustamento.AltimetricoAprovado ? "CONFORME" : "REPROVADO")}");
                sb.AppendLine();

                sb.AppendLine("3.2 Memória Analítica de Compensação e Rateio de Vértices (NBR 13.133):");
                if (ajustamento.RateioVertices == null || ajustamento.RateioVertices.Count == 0)
                {
                    sb.AppendLine("  (Nenhum vértice compensado registrado)");
                }
                else
                {
                    sb.AppendLine("+---------+----------------+---------------------------------+---------------------------------+-----------------------------------------------+");
                    sb.AppendLine("| Vértice | Dist. Acum (m) | COMPENSAÇÃO LINEAR PARCIAL (mm) | CORREÇÃO VETORIAL ACUMULADA(mm) | COORDENADAS FINAIS COMPENSADAS (m)            |");
                    sb.AppendLine("|         |                |   δEste   |  δNorte   |   δCota   |  ΔE Total |  ΔN Total |  ΔZ Total | Este (E)      | Norte (N)     | Cota (Z)      |");
                    sb.AppendLine("+---------+----------------+-----------+-----------+-----------+-----------+-----------+-----------+---------------+---------------+---------------+");

                    foreach (var r in ajustamento.RateioVertices)
                    {
                        sb.AppendLine($"| {r.NomeVertice,-7} | {r.DistanciaAcumulada,14:F4} | {r.CorrecaoEMilimetros,+9:F1} | {r.CorrecaoNMilimetros,+9:F1} | {r.CorrecaoZMilimetros,+9:F1} | {r.DeslocamentoTotalEMilimetros,+9:F1} | {r.DeslocamentoTotalNMilimetros,+9:F1} | {r.DeslocamentoTotalZMilimetros,+9:F1} | {r.CoordenadaECompensada,13:F4} | {r.CoordenadaNCompensada,13:F4} | {r.CoordenadaZCompensada,13:F4} |");
                    }

                    sb.AppendLine("+---------+----------------+-----------+-----------+-----------+-----------+-----------+-----------+---------------+---------------+---------------+");
                    var ultimo = ajustamento.RateioVertices.Last();
                    sb.AppendLine("Vetor Final no Vértice de Chegada:");
                    sb.AppendLine($"  * Compensação Linear Parcial (Bowditch): δEste = {ultimo.CorrecaoEMilimetros:+0.0;-0.0;0.0} mm | δNorte = {ultimo.CorrecaoNMilimetros:+0.0;-0.0;0.0} mm | δCota = {ultimo.CorrecaoZMilimetros:+0.0;-0.0;0.0} mm");
                    sb.AppendLine($"  * Correção Vetorial Total Acumulada:     ΔEste = {ultimo.DeslocamentoTotalEMilimetros:+0.0;-0.0;0.0} mm | ΔNorte = {ultimo.DeslocamentoTotalNMilimetros:+0.0;-0.0;0.0} mm | ΔCota = {ultimo.DeslocamentoTotalZMilimetros:+0.0;-0.0;0.0} mm");
                    sb.AppendLine();
                    sb.AppendLine("Nota Técnica:");
                    sb.AppendLine("1. 'COMPENSAÇÃO LINEAR PARCIAL' representa a distribuição proporcional estrita de Bowditch para o erro linear residual (fL) ao longo dos lances.");
                    sb.AppendLine("2. 'CORREÇÃO VETORIAL ACUMULADA' representa a variação vetorial líquida completa sofrida por cada marco em relação às coordenadas brutas instrumentais.");
                }
            }
            else
            {
                sb.AppendLine("3.1 Fechamento Linear e Angular:");
                sb.AppendLine("  (Poligonal em modo aberto ou sem fechamento. Coordenadas calculadas puramente brutas.)");
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatarPapelDidatico(PapelTopologicoEnum papel) => papel switch
        {
            PapelTopologicoEnum.EstacaoPartida => "Partida da Poligonal",
            PapelTopologicoEnum.FechamentoAnel => "Reocupação / Fechamento",
            PapelTopologicoEnum.VerticeIntermediario => "Vértice de Apoio",
            PapelTopologicoEnum.ReReferencia => "Ré de Amarração",
            PapelTopologicoEnum.PontoHomologoCheck => "Ponto de Controle",
            PapelTopologicoEnum.IrradiacaoCadastral => "Ponto de Detalhe",
            PapelTopologicoEnum.SentinelaDescartada => "Registro Nulo",
            _ => papel.ToString()
        };

        private static string FormatarLinhasOrigem(IReadOnlyList<int>? linhas)
        {
            if (linhas == null || linhas.Count == 0) return "---";
            int min = linhas.Min();
            int max = linhas.Max();
            return min == max ? min.ToString() : $"{min}-{max}";
        }

        private static List<string> QuebrarTexto(string? texto, int larguraMaxima)
        {
            var linhas = new List<string>();
            if (string.IsNullOrWhiteSpace(texto))
            {
                linhas.Add(string.Empty);
                return linhas;
            }

            if (larguraMaxima <= 0)
            {
                linhas.Add(texto);
                return linhas;
            }

            var palavras = texto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var linhaAtual = new StringBuilder();

            foreach (var palavra in palavras)
            {
                if (linhaAtual.Length == 0)
                {
                    if (palavra.Length > larguraMaxima)
                    {
                        linhas.Add(palavra);
                    }
                    else
                    {
                        linhaAtual.Append(palavra);
                    }
                }
                else if (linhaAtual.Length + 1 + palavra.Length <= larguraMaxima)
                {
                    linhaAtual.Append(' ').Append(palavra);
                }
                else
                {
                    linhas.Add(linhaAtual.ToString());
                    linhaAtual.Clear();
                    linhaAtual.Append(palavra);
                }
            }

            if (linhaAtual.Length > 0)
            {
                linhas.Add(linhaAtual.ToString());
            }

            return linhas;
        }

        private static string Truncar(string? texto, int limite)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            return texto.Length <= limite ? texto : texto.Substring(0, limite - 3) + "...";
        }

        private static string FormatarGms(double valorGraus)
        {
            bool negativo = valorGraus < 0;
            double valorAbs = Math.Abs(valorGraus);

            int graus = (int)Math.Floor(valorAbs);
            double restoMinutos = (valorAbs - graus) * 60.0;
            int minutos = (int)Math.Floor(restoMinutos);
            double segundos = (restoMinutos - minutos) * 60.0;

            if (Math.Round(segundos, 1) >= 60.0)
            {
                segundos = 0.0;
                minutos++;
                if (minutos >= 60)
                {
                    minutos = 0;
                    graus++;
                }
            }

            string sinal = negativo ? "-" : "";
            return $"{sinal}{graus:D2}°{minutos:D2}'{segundos:00.0}\"";
        }
    }
}
