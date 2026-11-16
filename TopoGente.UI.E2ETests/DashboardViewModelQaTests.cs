using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using TopoGente.Core.Entities;
using TopoGente.PresentationCore.Units;
using TopoGente.UI.E2ETests.Builders;
using TopoGente.UI.ViewModels;
using Xunit;

namespace TopoGente.UI.E2ETests;

public sealed class DashboardViewModelQaTests
{
    [Fact]
    public async Task ProcessarCommand_PrimeiraExecucaoComErrosNaoNulos_DeveAtualizarRelatorioEPropriedadesFormatadasComContratoConsistente()
    {
        // Arrange: poligonal fechada sintética com erro angular real (+18") e linear real (~34,64 mm)
        var builder = new DashboardViewModelTestBuilder()
            .ComCadernetaComErrosFechamento();

        DashboardViewModel vm = builder.Build();
        vm.UnidadeLinearSelecionada = UnidadeLinearEnum.Milimetros;
        vm.UnidadeAngularSelecionada = UnidadeAngularEnum.Segundos;

        Assert.Null(vm.RelatorioQaAtual);

        // Act
        await ((AsyncRelayCommand)vm.ImportarCadernetaCommand).ExecuteAsync(null);
        await ((AsyncRelayCommand)vm.ProcessarCommand).ExecuteAsync(null);
        builder.MessageSpy.AssertSemErros();

        // Assert: verificação coesa do contrato da primeira renderização com oráculos literais independentes
        Assert.NotNull(vm.RelatorioQaAtual);

        Assert.Equal("18\"", vm.ErroAngularFormatado);
        Assert.Equal("30\"", vm.ToleranciaAngularFormatada);
        Assert.Equal("34.64 mm", vm.ErroLinearAbsolutoFormatado);
        Assert.Equal("192.74 mm", vm.ToleranciaLinearAbsolutaFormatada);

        Assert.Equal(18.0, vm.RelatorioQaAtual!.ErroAngular * 3600.0, precision: 2);
        Assert.Equal(30.0, vm.RelatorioQaAtual.ToleranciaAngularSegundos, precision: 2);
        Assert.Equal(34.64, vm.RelatorioQaAtual.ErroLinear * 1000.0, precision: 2);
        Assert.Equal(192.74, vm.RelatorioQaAtual.ToleranciaLinearAbsolutaMetros * 1000.0, precision: 2);
    }

    [Fact]
    public async Task TrocaDePresetTolerancia_DeClasseIVPParaClasseIP_NaoDeveGerarCascataNemRegredirParaPersonalizado()
    {
        // Arrange: estabiliza o processamento na Classe IV-P antes de anexar o espião de eventos
        var builder = new DashboardViewModelTestBuilder()
            .ComCadernetaComErrosFechamento();

        DashboardViewModel vm = await builder.BuildCompensadoAsync(PresetToleranciaEnum.ClasseIVP);
        vm.UnidadeAngularSelecionada = UnidadeAngularEnum.Segundos;

        Assert.Equal(PresetToleranciaEnum.ClasseIVP, vm.PresetToleranciaSelecionado);
        Assert.NotNull(vm.RelatorioQaAtual);
        Assert.Equal(120.0, vm.RelatorioQaAtual!.ToleranciaAngularSegundos, precision: 2);
        Assert.True(vm.RelatorioQaAtual.AngularConforme);

        var contagemPorPropriedade = new Dictionary<string, int>
        {
            [nameof(DashboardViewModel.RelatorioQaAtual)] = 0,
            [nameof(DashboardViewModel.ErroAngularFormatado)] = 0,
            [nameof(DashboardViewModel.PresetToleranciaSelecionado)] = 0
        };

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null && contagemPorPropriedade.ContainsKey(e.PropertyName))
            {
                contagemPorPropriedade[e.PropertyName]++;
            }
        };

        // Act
        vm.PresetToleranciaSelecionado = PresetToleranciaEnum.ClasseIP;

        // Assert: contrato observável sem tempestade de notificações e enquadramento efetivo na Classe I-P
        Assert.Equal(1, contagemPorPropriedade[nameof(DashboardViewModel.RelatorioQaAtual)]);
        Assert.Equal(1, contagemPorPropriedade[nameof(DashboardViewModel.ErroAngularFormatado)]);
        Assert.Equal(1, contagemPorPropriedade[nameof(DashboardViewModel.PresetToleranciaSelecionado)]);

        Assert.Equal(PresetToleranciaEnum.ClasseIP, vm.PresetToleranciaSelecionado);
        Assert.Equal(12.0, vm.RelatorioQaAtual!.ToleranciaAngularSegundos, precision: 2);
        Assert.Equal("12\"", vm.ToleranciaAngularFormatada);
        Assert.False(vm.RelatorioQaAtual.AngularConforme);
    }
}
