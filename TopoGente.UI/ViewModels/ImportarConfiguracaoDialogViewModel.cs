using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;

namespace TopoGente.UI.ViewModels;

public sealed class ImportarConfiguracaoDialogViewModel : ObservableObject
{
    private readonly string[] _linhasCompletas;
    private readonly FormatoArquivoEntrada _formato;

    private SeparadorDecimalEnum _separadorDecimal;
    private SeparadorMilharEnum _separadorMilhar;
    private MagnitudeCoordenadaEnum _magnitude;
    private FormatoAngularEnum _formatoAngular;
    private char _delimitador;
    private string _resumoDeteccao = string.Empty;

    public string NomeArquivo { get; }
    public string FormatoDescricao => _formato switch
    {
        FormatoArquivoEntrada.Fbk => "Autodesk Field Book (*.fbk)",
        FormatoArquivoEntrada.LandXml => "LandXML (*.xml)",
        _ => "Texto / CSV Padrão (*.txt, *.csv)"
    };

    public bool ExibeDelimitador => _formato != FormatoArquivoEntrada.Fbk && _formato != FormatoArquivoEntrada.LandXml;

    public SeparadorDecimalEnum SeparadorDecimalSelecionado
    {
        get => _separadorDecimal;
        set
        {
            if (SetProperty(ref _separadorDecimal, value))
                AtualizarPreview();
        }
    }

    public SeparadorMilharEnum SeparadorMilharSelecionado
    {
        get => _separadorMilhar;
        set
        {
            if (SetProperty(ref _separadorMilhar, value))
                AtualizarPreview();
        }
    }

    public MagnitudeCoordenadaEnum MagnitudeSelecionada
    {
        get => _magnitude;
        set
        {
            if (SetProperty(ref _magnitude, value))
                AtualizarPreview();
        }
    }

    public FormatoAngularEnum FormatoAngularSelecionado
    {
        get => _formatoAngular;
        set
        {
            if (SetProperty(ref _formatoAngular, value))
                AtualizarPreview();
        }
    }

    public char DelimitadorSelecionado
    {
        get => _delimitador;
        set
        {
            if (SetProperty(ref _delimitador, value))
                AtualizarPreview();
        }
    }

    public string ResumoDeteccao
    {
        get => _resumoDeteccao;
        private set => SetProperty(ref _resumoDeteccao, value);
    }

    public ObservableCollection<LinhaPreviewDTO> LinhasPreview { get; } = new();

    public IReadOnlyList<SeparadorDecimalEnum> OpcoesSeparadorDecimal { get; } =
        Enum.GetValues<SeparadorDecimalEnum>();

    public IReadOnlyList<SeparadorMilharEnum> OpcoesSeparadorMilhar { get; } =
        Enum.GetValues<SeparadorMilharEnum>();

    public IReadOnlyList<MagnitudeCoordenadaEnum> OpcoesMagnitude { get; } =
        Enum.GetValues<MagnitudeCoordenadaEnum>();

    public IReadOnlyList<FormatoAngularEnum> OpcoesFormatoAngular { get; } =
        Enum.GetValues<FormatoAngularEnum>();

    public IReadOnlyDictionary<string, char> OpcoesDelimitador { get; } = new Dictionary<string, char>
    {
        { "Ponto e Vírgula (;)", ';' },
        { "Vírgula (,)", ',' },
        { "Tabulação (\\t)", '\t' },
        { "Espaço ( )", ' ' }
    };

    public string DelimitadorNome
    {
        get
        {
            var par = OpcoesDelimitador.FirstOrDefault(kvp => kvp.Value == _delimitador);
            return string.IsNullOrEmpty(par.Key) ? "Ponto e Vírgula (;)" : par.Key;
        }
        set
        {
            if (OpcoesDelimitador.TryGetValue(value, out char ch))
            {
                DelimitadorSelecionado = ch;
            }
        }
    }

    public Action<bool?>? FecharJanela { get; set; }

    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public ConfiguracaoImportacao ObterConfiguracaoFinal()
    {
        return new ConfiguracaoImportacao
        {
            SeparadorDecimal = _separadorDecimal,
            SeparadorMilhar = _separadorMilhar,
            Magnitude = _magnitude,
            FormatoAngular = _formatoAngular,
            Delimitador = _formato == FormatoArquivoEntrada.Fbk ? ' ' : _delimitador
        };
    }

    public ImportarConfiguracaoDialogViewModel(string caminhoArquivo, string[] linhas, FormatoArquivoEntrada formato)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        NomeArquivo = Path.GetFileName(caminhoArquivo);
        _linhasCompletas = linhas;
        _formato = formato;

        var sugerida = LeitorCsvEngine.DetectarConfiguracaoSugerida(linhas, formato);
        _separadorDecimal = sugerida.SeparadorDecimal;
        _separadorMilhar = sugerida.SeparadorMilhar;
        _magnitude = sugerida.Magnitude;
        _formatoAngular = sugerida.FormatoAngular;
        _delimitador = sugerida.Delimitador;

        ResumoDeteccao = $"Sugestão Automática: Separador Decimal = {_separadorDecimal}, Magnitude = {_magnitude}, Delimitador = '{(_delimitador == '\t' ? "\\t" : _delimitador.ToString())}'";

        ConfirmarCommand = new RelayCommand(_ => FecharJanela?.Invoke(true));
        CancelarCommand = new RelayCommand(_ => FecharJanela?.Invoke(false));

        AtualizarPreview();
    }

    private void AtualizarPreview()
    {
        var config = ObterConfiguracaoFinal();
        var amostra = _linhasCompletas.Take(15).ToList();
        var resultadoPreview = LeitorCsvEngine.GerarPreviewReativo(amostra, config, _formato);

        LinhasPreview.Clear();
        foreach (var linha in resultadoPreview)
        {
            LinhasPreview.Add(linha);
        }
    }
}
