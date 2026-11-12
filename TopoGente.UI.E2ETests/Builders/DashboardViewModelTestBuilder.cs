using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Storage;
using TopoGente.UI.Services;
using TopoGente.UI.ViewModels;
using Xunit;

namespace TopoGente.UI.E2ETests.Builders;

/// <summary>
/// Test Data Builder para construção isolada e determinística de DashboardViewModel
/// em testes de UI/E2E, substituindo arquivos externos em disco por caderneta sintética
/// em memória e dublês espiões para captura explícita de erros.
/// </summary>
internal sealed class DashboardViewModelTestBuilder
{
    public static readonly string[] CadernetaQuadradoSinteticoFbk =
    {
        "UNIT METER DECDEG",
        "NEZ \"E4\" 7700000.0000 719900.0000 100.000",
        "NEZ \"E1\" 7700000.0000 720000.0000 100.000",
        "STN \"E1\" 1.5000 E",
        "BS \"E4\" 0.0000",
        "PRISM 1.5000",
        "AD VA \"E4\" 0.0000 100.0000 90.0000 \"R\"",
        "AD VA \"P1\" 180.0000 100.0000 90.0000 \"V\"",
        "AD VA 1 135.0000 20.0000 60.0000 \"POSTE\"",
        "STN \"P1\" 1.5000 E",
        "BS \"E1\" 0.0000",
        "PRISM 1.5000",
        "AD VA \"E1\" 0.0000 100.0000 90.0000 \"R\"",
        "AD VA \"P2\" 90.0000 100.0000 90.0000 \"V\"",
        "STN \"P2\" 1.5000 E",
        "BS \"P1\" 0.0000",
        "PRISM 1.5000",
        "AD VA \"P1\" 0.0000 100.0000 90.0000 \"R\"",
        "AD VA \"P3\" 90.0000 100.0000 90.0000 \"V\"",
        "STN \"P3\" 1.5000 E",
        "BS \"P2\" 0.0000",
        "PRISM 1.5000",
        "AD VA \"P2\" 0.0000 100.0000 90.0000 \"R\"",
        "AD VA \"E1\" 90.0000 100.0000 90.0000 \"V\"",
        "STN \"E1\" 1.5000 E",
        "BS \"P3\" 0.0000",
        "PRISM 1.5000",
        "AD VA \"P3\" 0.0000 100.0000 90.0000 \"R\"",
        "AD VA \"E4\" 270.0000 100.0000 90.0000 \"V\""
    };

    private string _nomeVirtualArquivo = "poligonal_sintetica.fbk";
    private string[] _linhasEmMemoria = CadernetaQuadradoSinteticoFbk;
    private string? _caminhoSalvamento;
    private double _viewportLargura = 800;
    private double _viewportAltura = 600;
    private double? _stepIntervalInicial;

    public SpyMessageService MessageSpy { get; } = new();

    public DashboardViewModelTestBuilder ComCadernetaEmMemoria(string nomeVirtual, params string[] linhas)
    {
        _nomeVirtualArquivo = nomeVirtual;
        _linhasEmMemoria = linhas;
        return this;
    }

    public DashboardViewModelTestBuilder ComArquivoSalvamento(string caminhoSalvamento)
    {
        _caminhoSalvamento = caminhoSalvamento;
        return this;
    }

    public DashboardViewModelTestBuilder ComDimensoesViewport(double largura, double altura)
    {
        _viewportLargura = largura;
        _viewportAltura = altura;
        return this;
    }

    public DashboardViewModelTestBuilder ComEquidistanciaInicial(double stepInterval)
    {
        _stepIntervalInicial = stepInterval;
        return this;
    }

    public DashboardViewModel Build()
    {
        var classificador = new ClassificadorGrafo();
        var processador = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());

        var vm = new DashboardViewModel(
            () => new RichFeatureTinfourAdapter(),
            () => new RichFeatureTinfourAdapter(),
            new LeituraArquivoFactory(),
            new OrganizarCaminhamento(),
            processador,
            new QaCheckService(),
            classificador,
            new ArquivoProjetoService(),
            new ExportarTxtService(),
            new ExportadorDxfService(),
            new StubDialogService(_nomeVirtualArquivo, _caminhoSalvamento),
            new InMemoryFileService(_nomeVirtualArquivo, _linhasEmMemoria),
            MessageSpy);

        vm.AtualizarDimensoesViewport(_viewportLargura, _viewportAltura);

        if (_stepIntervalInicial.HasValue)
        {
            vm.StepInterval = _stepIntervalInicial.Value;
        }

        return vm;
    }

    public async Task<DashboardViewModel> BuildCompensadoAsync(
        PresetToleranciaEnum preset = PresetToleranciaEnum.ClasseIVP)
    {
        var vm = Build();
        await ((AsyncRelayCommand)vm.ImportarCadernetaCommand).ExecuteAsync(null);
        MessageSpy.AssertSemErros();

        vm.PresetToleranciaSelecionado = preset;
        await ((AsyncRelayCommand)vm.ProcessarCommand).ExecuteAsync(null);
        MessageSpy.AssertSemErros();

        return vm;
    }

    internal sealed class InMemoryFileService : IFileService
    {
        private readonly string _caminhoEsperado;
        private readonly string[] _linhas;

        public InMemoryFileService(string caminhoEsperado, string[] linhas)
        {
            _caminhoEsperado = caminhoEsperado;
            _linhas = linhas;
        }

        public string[] LerLinhas(string caminho)
        {
            if (!string.Equals(caminho, _caminhoEsperado, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Caminho inesperado no stub em memória: '{caminho}'.");
            }

            return _linhas;
        }
    }

    internal sealed class StubDialogService : IDialogService
    {
        private readonly string? _arquivoParaAbrir;
        private readonly string? _arquivoParaSalvar;

        public StubDialogService(string? arquivoParaAbrir, string? arquivoParaSalvar = null)
        {
            _arquivoParaAbrir = arquivoParaAbrir;
            _arquivoParaSalvar = arquivoParaSalvar;
        }

        public string? SelecionarArquivoAbertura(string filtro, string titulo) => _arquivoParaAbrir;

        public string? SelecionarArquivoSalvamento(string filtro, string titulo, string nomePadrao, string extensaoPadrao) =>
            _arquivoParaSalvar;
    }

    internal sealed class SpyMessageService : IMessageService
    {
        public List<(string Titulo, string Mensagem)> Erros { get; } = new();
        public List<(string Titulo, string Mensagem)> Avisos { get; } = new();
        public List<(string Titulo, string Mensagem)> Sucessos { get; } = new();

        public void MostrarErro(string mensagem, string titulo) => Erros.Add((titulo, mensagem));
        public void MostrarAviso(string mensagem, string titulo) => Avisos.Add((titulo, mensagem));
        public void MostrarSucesso(string mensagem, string titulo) => Sucessos.Add((titulo, mensagem));

        public void AssertSemErros()
        {
            Assert.True(
                Erros.Count == 0,
                Erros.Count == 0 ? string.Empty : $"Erros inesperados emitidos via IMessageService: {Erros[0].Titulo} - {Erros[0].Mensagem}");
        }
    }
}
