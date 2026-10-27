using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Services;

/// <summary>
/// Serviço de domínio responsável por higienizar descrições de campo
/// em nomes de camada compatíveis com a especificação AutoCAD DXF.
/// </summary>
public static partial class SanitizadorCamadaDxf
{
    [GeneratedRegex(@"[^A-Z0-9]+")]
    private static partial Regex NaoAlfanumericoRegex();

    [GeneratedRegex(@"_+")]
    private static partial Regex SublinhadosRepetidosRegex();

    /// <summary>
    /// Normaliza uma descrição de campo (ex: 'Cerca de Arame', 'Árvore 10m') para um identificador DXF seguro.
    /// Remove acentuação, converte para maiúsculas, substitui caracteres especiais por sublinhado e elimina duplicidades.
    /// Retorna null caso o texto resultante seja vazio.
    /// </summary>
    public static string? SanitizarNomeCamada(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return null;

        // 1. Decomposição canônica para isolar diacríticos
        string normalizado = descricao.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);

        foreach (char c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        // 2. Converte para maiúsculas invariantes
        string semAcentos = sb.ToString().ToUpperInvariant();

        // 3. Substitui qualquer caractere não alfanumérico por sublinhado
        string substituto = NaoAlfanumericoRegex().Replace(semAcentos, "_");

        // 4. Compacta múltiplos sublinhados em um único
        string compactado = SublinhadosRepetidosRegex().Replace(substituto, "_");

        // 5. Remove sublinhados no início e no fim
        string resultado = compactado.Trim('_');

        return string.IsNullOrEmpty(resultado) ? null : resultado;
    }

    /// <summary>
    /// Determina a camada de destino para um ponto topográfico baseado nas regras de precedência:
    /// 1. Vértices da Poligonal -> TOPO_POLIGONAL
    /// 2. Irradiações com descrição higienizável -> TOPO_FEICAO_{DESCRICAO}
    /// 3. Irradiações genéricas sem descrição -> TOPO_IRRADIACOES
    /// </summary>
    public static string ObterNomeCamadaPonto(PontoCoordenada ponto)
    {
        ArgumentNullException.ThrowIfNull(ponto);

        if (ponto.EhPontoPoligonal)
            return "TOPO_POLIGONAL";

        if (!string.IsNullOrWhiteSpace(ponto.Descricao))
        {
            string? sanitizado = SanitizarNomeCamada(ponto.Descricao);
            if (!string.IsNullOrEmpty(sanitizado))
            {
                return $"TOPO_FEICAO_{sanitizado}";
            }
        }

        return "TOPO_IRRADIACOES";
    }
}
