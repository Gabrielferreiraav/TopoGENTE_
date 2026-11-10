using System;
using System.Globalization;
using System.Windows.Data;
using TopoGente.PresentationCore.Units;

namespace TopoGente.UI.Converters;

/// <summary>
/// Converte um valor numérico em metros para a representação textual na UnidadeLinearEnum especificada.
/// </summary>
public sealed class LinearUnitConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double valorMetros)
        {
            if (value is float f) valorMetros = f;
            else if (value is decimal d) valorMetros = (double)d;
            else if (value is int i) valorMetros = i;
            else return "0.00 m";
        }

        UnidadeLinearEnum unidade = UnidadeLinearEnum.Milimetros;
        if (parameter is UnidadeLinearEnum u)
        {
            unidade = u;
        }

        return FormatadorUnidades.FormatarLinear(valorMetros, unidade);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Conversão reversa não suportada.");
    }
}
