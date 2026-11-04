using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Utilities;

namespace TopoGente.Core.Services;

/// <summary>
/// Motor de domínio de alta performance para parsing, tokenização sem alocação (zero-allocation)
/// e detecção heurística de configurações regionais e formatos de caderneta.
/// </summary>
public static class LeitorCsvEngine
{
    /// <summary>
    /// Converte um token textual em double respeitando as regras de separador decimal e de milhar.
    /// Utiliza stackalloc char para purificação na pilha da CPU sem alocar strings na Heap.
    /// </summary>
    public static bool ParseNumeroResiliente(
        ReadOnlySpan<char> token,
        ConfiguracaoImportacao config,
        out double valor)
    {
        valor = 0.0;
        token = token.Trim();
        if (token.IsEmpty) return false;

        Span<char> bufferLimpo = stackalloc char[token.Length];
        int count = 0;

        char charMilhar = config.SeparadorMilhar switch
        {
            SeparadorMilharEnum.Ponto => '.',
            SeparadorMilharEnum.Virgula => ',',
            SeparadorMilharEnum.Espaco => ' ',
            _ => '\0'
        };

        char charDecimalEsperado = config.SeparadorDecimal == SeparadorDecimalEnum.Virgula ? ',' : '.';

        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (c == charMilhar)
                continue;

            if (c == charDecimalEsperado)
            {
                bufferLimpo[count++] = '.';
            }
            else
            {
                bufferLimpo[count++] = c;
            }
        }

        if (count == 0) return false;

        return double.TryParse(
            bufferLimpo[..count],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out valor);
    }

    /// <summary>
    /// Tokenizador compatível com RFC 4180 que respeita aspas duplas e extrai os intervalos (Ranges)
    /// dos tokens diretamente sobre o ReadOnlySpan da linha, sem alocar arrays de string intermediários.
    /// </summary>
    public static int TokenizarLinha(
        ReadOnlySpan<char> linha,
        char delimitador,
        Span<Range> tokensDestino)
    {
        if (tokensDestino.IsEmpty) return 0;

        int tokenCount = 0;
        int inicioToken = 0;
        bool dentroAspas = false;

        for (int i = 0; i < linha.Length; i++)
        {
            char c = linha[i];
            if (c == '"')
            {
                dentroAspas = !dentroAspas;
            }
            else if (c == delimitador && !dentroAspas)
            {
                if (tokenCount < tokensDestino.Length)
                {
                    tokensDestino[tokenCount++] = new Range(inicioToken, i);
                }
                inicioToken = i + 1;
            }
        }

        if (tokenCount < tokensDestino.Length)
        {
            tokensDestino[tokenCount++] = new Range(inicioToken, linha.Length);
        }

        return tokenCount;
    }

    /// <summary>
    /// Inspeciona as primeiras linhas do arquivo e sugere deterministicamente a configuração
    /// de separador decimal, delimitador, formato angular e magnitude de coordenadas.
    /// </summary>
    public static ConfiguracaoImportacao DetectarConfiguracaoSugerida(
        IEnumerable<string> linhasAmostra,
        FormatoArquivoEntrada formato)
    {
        if (formato == FormatoArquivoEntrada.Fbk)
        {
            return ConfiguracaoImportacao.PadraoFbk;
        }

        int countPontoVirgula = 0;
        int countVirgula = 0;
        int countTab = 0;
        int numerosComVirgula = 0;
        int numerosComPonto = 0;
        bool temCoordenadaMaiorQueMilhao = false;

        var linhas = linhasAmostra.Take(50).ToList();

        foreach (var l in linhas)
        {
            if (string.IsNullOrWhiteSpace(l) || l.StartsWith("#") || l.StartsWith("!"))
                continue;

            bool dentroAspas = false;
            foreach (char c in l)
            {
                if (c == '"') dentroAspas = !dentroAspas;
                else if (!dentroAspas)
                {
                    if (c == ';') countPontoVirgula++;
                    else if (c == ',') countVirgula++;
                    else if (c == '\t') countTab++;
                }
            }
        }

        char delimitador = ';';
        if (countTab > countPontoVirgula && countTab > countVirgula) delimitador = '\t';
        else if (countVirgula > countPontoVirgula) delimitador = ',';
        else delimitador = ';';

        // Análise numérica de tokens
        Span<Range> ranges = stackalloc Range[16];
        Span<char> buf = stackalloc char[64];
        foreach (var l in linhas)
        {
            if (string.IsNullOrWhiteSpace(l) || l.StartsWith("#") || l.StartsWith("!"))
                continue;

            ReadOnlySpan<char> spanLinha = l.AsSpan();
            int nTokens = TokenizarLinha(spanLinha, delimitador, ranges);

            for (int i = 0; i < nTokens; i++)
            {
                ReadOnlySpan<char> t = spanLinha[ranges[i]].Trim().Trim('"');
                if (t.Contains(',')) numerosComVirgula++;
                if (t.Contains('.')) numerosComPonto++;

                // Verificar magnitude
                if (t.Length <= buf.Length)
                {
                    int bIdx = 0;
                    foreach (char c in t) if (c != '.' && c != ',') buf[bIdx++] = c;
                    if (double.TryParse(buf[..bIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    {
                        if (val > 1_000_000.0) temCoordenadaMaiorQueMilhao = true;
                    }
                }
            }
        }

        var separadorDecimal = (delimitador == ';' && numerosComVirgula > numerosComPonto)
            ? SeparadorDecimalEnum.Virgula
            : SeparadorDecimalEnum.Ponto;

        var magnitude = temCoordenadaMaiorQueMilhao
            ? MagnitudeCoordenadaEnum.UtmCompleta
            : MagnitudeCoordenadaEnum.LocalTruncada;

        return new ConfiguracaoImportacao
        {
            Delimitador = delimitador,
            SeparadorDecimal = separadorDecimal,
            SeparadorMilhar = SeparadorMilharEnum.Nenhum,
            FormatoAngular = FormatoAngularEnum.SexagesimalDddMmSs,
            Magnitude = magnitude
        };
    }

    /// <summary>
    /// Gera a prévia interpretada em tempo real (WYSIWYG) das primeiras linhas da caderneta
    /// para renderização na grade dinâmica da janela modal de importação.
    /// </summary>
    public static IReadOnlyList<LinhaPreviewDTO> GerarPreviewReativo(
        IEnumerable<string> linhasAmostra,
        ConfiguracaoImportacao config,
        FormatoArquivoEntrada formato)
    {
        var lista = new List<LinhaPreviewDTO>();
        int numLinha = 0;
        Span<Range> tokens = stackalloc Range[16];

        foreach (var rawLinha in linhasAmostra.Take(8))
        {
            numLinha++;
            string linha = rawLinha.Trim();
            if (string.IsNullOrWhiteSpace(linha)) continue;

            if (formato == FormatoArquivoEntrada.Fbk)
            {
                lista.Add(InterpretarLinhaFbk(linha, numLinha, config));
            }
            else
            {
                lista.Add(InterpretarLinhaCsv(linha, numLinha, config, tokens));
            }
        }

        return lista;
    }

    private static LinhaPreviewDTO InterpretarLinhaFbk(string linha, int numLinha, ConfiguracaoImportacao config)
    {
        var partes = linha.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
        {
            return new LinhaPreviewDTO(numLinha, "-", "-", linha, "-", "-", "-", "-", "Vazia");
        }

        string cmd = partes[0].ToUpperInvariant();

        if (cmd == "NEZ" && partes.Length >= 5)
        {
            string nome = partes[1].Replace("\"", "");
            ParseNumeroResiliente(partes[2], config, out double y);
            ParseNumeroResiliente(partes[3], config, out double x);
            ParseNumeroResiliente(partes[4], config, out double z);

            return new LinhaPreviewDTO(
                numLinha,
                nome,
                "-",
                "Coordenada Conhecida (NEZ)",
                "-",
                "-",
                "-",
                $"Z={z:F3} (N={y:F3}, E={x:F3})",
                "OK"
            );
        }

        if (cmd == "STN" && partes.Length >= 3)
        {
            string stn = partes[1].Replace("\"", "");
            ParseNumeroResiliente(partes[2], config, out double hi);
            return new LinhaPreviewDTO(numLinha, stn, "-", $"Ocupação STN (Hi={hi:F3}m)", "-", "-", "-", "-", "OK");
        }

        if (cmd == "BS" && partes.Length >= 3)
        {
            string alvo = partes[1].Replace("\"", "");
            ParseNumeroResiliente(partes[2], config, out double ang);
            double dec = ConversorAngulos.ConverterParaDecimal(ang, config.FormatoAngular);
            return new LinhaPreviewDTO(numLinha, "-", alvo, "Visada de Ré (BS)", ConversorAngulos.ParaTexto(dec), "-", "-", "-", "OK");
        }

        if (cmd == "AD" && partes.Length >= 6 && partes[1] == "VA")
        {
            string alvo = partes[2].Replace("\"", "");
            ParseNumeroResiliente(partes[3], config, out double ah);
            ParseNumeroResiliente(partes[4], config, out double di);
            ParseNumeroResiliente(partes[5], config, out double av);

            string obs = partes.Length > 6 ? string.Join(" ", partes.Skip(6)).Replace("\"", "") : "";

            double ahDec = ConversorAngulos.ConverterParaDecimal(ah, config.FormatoAngular);
            double avDec = ConversorAngulos.ConverterParaDecimal(av, config.FormatoAngular);

            return new LinhaPreviewDTO(
                numLinha,
                "-",
                alvo,
                obs,
                ConversorAngulos.ParaTexto(ahDec),
                ConversorAngulos.ParaTexto(avDec),
                $"{di:F3} m",
                "-",
                "OK"
            );
        }

        return new LinhaPreviewDTO(numLinha, "-", "-", linha, "-", "-", "-", "-", "Comando Especial");
    }

    private static LinhaPreviewDTO InterpretarLinhaCsv(
        string linha,
        int numLinha,
        ConfiguracaoImportacao config,
        Span<Range> tokens)
    {
        if (linha.StartsWith("#") || linha.StartsWith("!"))
        {
            return new LinhaPreviewDTO(numLinha, "-", "-", linha, "-", "-", "-", "-", "Comentário");
        }

        ReadOnlySpan<char> span = linha.AsSpan();
        int n = TokenizarLinha(span, config.Delimitador, tokens);

        if (n >= 8)
        {
            // Formato Polar Clássico (8 colunas)
            string stn = span[tokens[0]].Trim().Trim('"').ToString();
            ReadOnlySpan<char> ahToken = span[tokens[1]].Trim().Trim('"');
            string obs = span[tokens[2]].Trim().Trim('"').ToString();
            ReadOnlySpan<char> avToken = span[tokens[3]].Trim().Trim('"');
            ReadOnlySpan<char> diToken = span[tokens[4]].Trim().Trim('"');
            ReadOnlySpan<char> hiToken = span[tokens[5]].Trim().Trim('"');
            string vis = span[tokens[6]].Trim().Trim('"').ToString();
            ReadOnlySpan<char> htToken = span[tokens[7]].Trim().Trim('"');

            bool okAh = ParseNumeroResiliente(ahToken, config, out double ah);
            bool okAv = ParseNumeroResiliente(avToken, config, out double av);
            bool okDi = ParseNumeroResiliente(diToken, config, out double di);

            double ahDec = okAh ? ConversorAngulos.ConverterParaDecimal(ah, config.FormatoAngular) : 0;
            double avDec = okAv ? ConversorAngulos.ConverterParaDecimal(av, config.FormatoAngular) : 0;

            string txtAh = okAh ? ConversorAngulos.ParaTexto(ahDec) : "Erro Léxico";
            string txtAv = okAv ? ConversorAngulos.ParaTexto(avDec) : "Erro Léxico";
            string txtDi = okDi ? $"{di:F3} m" : "Erro Léxico";

            string status = (okAh && okAv && okDi) ? "OK (Polar)" : "Erro Formato";

            return new LinhaPreviewDTO(numLinha, stn, vis, obs, txtAh, txtAv, txtDi, "-", status);
        }
        else if (n >= 4)
        {
            // Formato Coordenadas Retangulares (P, E, N, Z ou P, N, E, Z)
            string ponto = span[tokens[0]].Trim().Trim('"').ToString();
            ReadOnlySpan<char> c1 = span[tokens[1]].Trim().Trim('"');
            ReadOnlySpan<char> c2 = span[tokens[2]].Trim().Trim('"');
            ReadOnlySpan<char> c3 = span[tokens[3]].Trim().Trim('"');
            string codigo = n > 4 ? span[tokens[4]].Trim().Trim('"').ToString() : "";

            bool ok1 = ParseNumeroResiliente(c1, config, out double v1);
            bool ok2 = ParseNumeroResiliente(c2, config, out double v2);
            bool ok3 = ParseNumeroResiliente(c3, config, out double v3);

            string status = (ok1 && ok2 && ok3) ? "OK (Cartesiano)" : "Erro Formato";

            return new LinhaPreviewDTO(
                numLinha,
                ponto,
                "-",
                codigo,
                $"{v1:F3}",
                $"{v2:F3}",
                "-",
                $"{v3:F3}",
                status
            );
        }

        return new LinhaPreviewDTO(numLinha, "-", "-", linha, "-", "-", "-", "-", "Colunas Insuficientes");
    }
}
