using System;
using System.Globalization;
using System.Windows.Data;
using TopoGente.PresentationCore.Units;

namespace TopoGente.UI.Converters;

/// <summary>
/// Converte um valor numérico em graus para a representação textual na UnidadeAngularEnum especificada.
/// </summary>
public sealed class AngularUnitConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double valorGraus)
        {
            if (value is float f) valorGraus = f;
            else if (value is decimal d) valorGraus = (double)d;
            else if (value is int i) valorGraus = i;
            else return "0.00\"";
        }

        UnidadeAngularEnum unidade = UnidadeAngularEnum.Segundos;
        if (parameter is UnidadeAngularEnum u)
        {
            unidade = u;
        }

        return FormatadorUnidades.FormatarAngular(valorGraus, unidade);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Conversão reversa não suportada.");
    }
}
