using System;
using System.Globalization;

namespace TopoGente.PresentationCore.Units;

/// <summary>
/// Serviço puro de apresentação para conversão e formatação de grandezas em diferentes unidades de medida.
/// Não depende de WPF e é 100% testável em suíte unitária.
/// </summary>
public static class FormatadorUnidades
{
    private static readonly CultureInfo CulturaPadrao = CultureInfo.InvariantCulture;

    public static string FormatarLinear(double valorMetros, UnidadeLinearEnum unidade, int casasDecimais = -1)
    {
        return unidade switch
        {
            UnidadeLinearEnum.Milimetros =>
                $"{((casasDecimais >= 0) ? (valorMetros * 1000.0).ToString($"F{casasDecimais}", CulturaPadrao) : (valorMetros * 1000.0).ToString("0.##", CulturaPadrao))} mm",

            UnidadeLinearEnum.Centimetros =>
                $"{((casasDecimais >= 0) ? (valorMetros * 100.0).ToString($"F{casasDecimais}", CulturaPadrao) : (valorMetros * 100.0).ToString("0.###", CulturaPadrao))} cm",

            _ =>
                $"{((casasDecimais >= 0) ? valorMetros.ToString($"F{casasDecimais}", CulturaPadrao) : valorMetros.ToString("0.####", CulturaPadrao))} m"
        };
    }

    public static string FormatarAngular(double valorGraus, UnidadeAngularEnum unidade, int casasDecimais = -1)
    {
        return unidade switch
        {
            UnidadeAngularEnum.Segundos =>
                $"{((casasDecimais >= 0) ? (valorGraus * 3600.0).ToString($"F{casasDecimais}", CulturaPadrao) : (valorGraus * 3600.0).ToString("0.##", CulturaPadrao))}\"",

            UnidadeAngularEnum.GrausDecimais =>
                $"{((casasDecimais >= 0) ? valorGraus.ToString($"F{casasDecimais}", CulturaPadrao) : valorGraus.ToString("0.######", CulturaPadrao))}°",

            _ => FormatarGms(valorGraus)
        };
    }

    public static string FormatarGms(double valorGraus)
    {
        bool negativo = valorGraus < 0;
        double valorAbs = Math.Abs(valorGraus);

        int graus = (int)Math.Floor(valorAbs);
        double restoMinutos = (valorAbs - graus) * 60.0;
        int minutos = (int)Math.Floor(restoMinutos);
        double segundos = (restoMinutos - minutos) * 60.0;

        // Tratar arredondamento para 60 segundos
        if (Math.Round(segundos, 2) >= 60.0)
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
        return $"{sinal}{graus:D2}°{minutos:D2}'{segundos:00.00}\"";
    }

    public static string GerarTooltipLinearCompleto(double valorMetros)
    {
        double mm = valorMetros * 1000.0;
        double cm = valorMetros * 100.0;
        return $"Equivalências Lineares:\n• {valorMetros.ToString("0.0000", CulturaPadrao)} m\n• {cm.ToString("0.00", CulturaPadrao)} cm\n• {mm.ToString("0.00", CulturaPadrao)} mm";
    }

    public static string GerarTooltipAngularCompleto(double valorGraus)
    {
        double seg = valorGraus * 3600.0;
        string gms = FormatarGms(valorGraus);
        return $"Equivalências Angulares:\n• {seg.ToString("0.00", CulturaPadrao)}\"\n• {gms}\n• {valorGraus.ToString("0.000000", CulturaPadrao)}°";
    }
}
