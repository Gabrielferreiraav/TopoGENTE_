using System;
using System.Globalization;
using System.Windows.Data;
using TopoGente.Core.Entities;
using TopoGente.PresentationCore.Tolerances;

namespace TopoGente.UI.Converters;

/// <summary>
/// Converte um PresetToleranciaEnum no objeto de informações técnicas e normativas completo (ToleranciaPresetInfo).
/// </summary>
public sealed class PresetToleranciaInfoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PresetToleranciaEnum preset)
        {
            return ToleranciaInfoHelper.ObterInfo(preset);
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converte um ModoAvaliacaoLinearEnum no objeto de informações técnicas e normativas completo (ModoAvaliacaoLinearInfo).
/// </summary>
public sealed class ModoAvaliacaoLinearInfoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ModoAvaliacaoLinearEnum modo)
        {
            return ToleranciaInfoHelper.ObterInfoModo(modo);
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converte PresetToleranciaEnum para um nome legível na interface gráfica.
/// </summary>
public sealed class PresetToleranciaDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PresetToleranciaEnum preset)
        {
            return ToleranciaInfoHelper.ObterInfo(preset).NomeExibicao;
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converte ModoAvaliacaoLinearEnum para um nome legível na interface gráfica.
/// </summary>
public sealed class ModoAvaliacaoLinearDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ModoAvaliacaoLinearEnum modo)
        {
            return ToleranciaInfoHelper.ObterInfoModo(modo).NomeExibicao;
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
