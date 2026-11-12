using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;
using CommunityToolkit.Mvvm.Input;
using TopoGente.Core.Domain;
using TopoGente.UI.E2ETests.Builders;
using TopoGente.UI.E2ETests.Infrastructure;
using TopoGente.UI.ViewModels;
using TopoGente.UI.Views;
using Xunit;

namespace TopoGente.UI.E2ETests
{
    [Collection("STA UI Tests")]
    public class CadViewportInteractionE2ETests
    {
        [Fact]
        public void EnquadramentoCamera_AoProcessarPoligonalSintetica_PreservaNorteParaCimaEInversaoPixelParaUtm()
        {
            StaTestRunner.Executar(async () =>
            {
                var builder = new DashboardViewModelTestBuilder();
                var vm = await builder.BuildCompensadoAsync();

                var matOriginal = vm.CameraMatrix;
                Assert.True(matOriginal.HasInverse, "CameraMatrix deve ser inversível.");
                Assert.True(matOriginal.M11 > 0, "Eixo Leste/X deve ter escala positiva.");
                Assert.True(matOriginal.M22 < 0, "Eixo Norte/Y deve ter escala negativa (Norte para cima no WPF).");

                // Vértice E1 da poligonal sintética em (720000.000, 7700000.000)
                Point telaE1 = matOriginal.Transform(new Point(720000.000 - vm.OriginX, 7700000.000 - vm.OriginY));
                Assert.InRange(telaE1.X, 0, 800);
                Assert.InRange(telaE1.Y, 0, 600);

                // Round-trip Pixel -> UTM real
                Point e1Recuperado = vm.GetModelCoordinates(telaE1);
                Assert.Equal(720000.000, e1Recuperado.X, 2);
                Assert.Equal(7700000.000, e1Recuperado.Y, 2);

                // Ponto mais ao Norte deve possuir menor coordenada Y em tela
                Point ptSul = matOriginal.Transform(new Point(10, 0));
                Point ptNorte = matOriginal.Transform(new Point(10, 100));
                Assert.True(ptNorte.Y < ptSul.Y, "Norte topográfico deve apontar para o topo da tela.");
                Assert.Equal(ptSul.X, ptNorte.X, 3);

                // Pan
                vm.AplicarPan(50, -30);
                Assert.Equal(matOriginal.OffsetX + 50, vm.CameraMatrix.OffsetX, 3);
                Assert.Equal(matOriginal.OffsetY - 30, vm.CameraMatrix.OffsetY, 3);

                // Zoom
                var matAposPan = vm.CameraMatrix;
                vm.AplicarZoom(1.2, 400, 300);
                Assert.True(Math.Abs(vm.CameraMatrix.M11) > Math.Abs(matAposPan.M11), "Zoom in deve ampliar a escala absoluta.");

                // FitToScreen (ZoomExtents)
                vm.ZoomExtentsCommand.Execute(null);
                Assert.Equal(matOriginal.M11, vm.CameraMatrix.M11, 3);
                Assert.Equal(matOriginal.M22, vm.CameraMatrix.M22, 3);
                Assert.Equal(matOriginal.OffsetX, vm.CameraMatrix.OffsetX, 3);
                Assert.Equal(matOriginal.OffsetY, vm.CameraMatrix.OffsetY, 3);

                builder.MessageSpy.AssertSemErros();
            });
        }

        [Fact]
        public void ViewportCad_QuandoPoligonalFechadaCompensada_GeraGeometriaFechadaEAlternaVisibilidadeDeCamada()
        {
            StaTestRunner.Executar(async () =>
            {
                var builder = new DashboardViewModelTestBuilder();
                var vm = await builder.BuildCompensadoAsync();

                Assert.Equal(EstadoMotor.Compensado, vm.EstadoAtualMotor);
                Assert.NotNull(vm.RelatorioQaAtual);
                Assert.Equal(0.0, vm.RelatorioQaAtual.ErroLinear, 3);

                Assert.False(vm.StaticPoligonalCompensadaGeometry.IsEmpty());
                Assert.False(vm.StaticDraftGeometry.IsEmpty());
                Assert.False(vm.StaticPointsGeometry.IsEmpty());

                var pgCompensada = PathGeometry.CreateFromGeometry(vm.StaticPoligonalCompensadaGeometry);
                Assert.NotEmpty(pgCompensada.Figures);
                Assert.True(pgCompensada.Figures[0].IsClosed, "Poligonal compensada deve fechar topologicamente.");

                var pgDraft = PathGeometry.CreateFromGeometry(vm.StaticDraftGeometry);
                Assert.NotEmpty(pgDraft.Figures);
                Assert.True(pgDraft.Figures[0].IsClosed, "Esboço bruto da poligonal fechada deve fechar topologicamente.");

                // Métricas determinísticas da superfície TIN (cota base 100.0 m e irradiação 110.0 m)
                Assert.True(vm.TotalTriangulosMdt > 0);
                Assert.True(vm.TotalVerticesMdt >= 5);
                Assert.Equal(100.0, vm.CotaMinimaTerreno, 3);
                Assert.Equal(110.0, vm.CotaMaximaTerreno, 3);
                Assert.Equal(10.0, vm.DesnivelTerreno, 3);

                // Binding visual na DashboardView e alternância reativa de camada
                var view = new DashboardView { DataContext = vm };
                view.Measure(new Size(1280, 720));
                view.Arrange(new Rect(0, 0, 1280, 720));
                view.UpdateLayout();

                var paths = VisualTreeTestHelper.FindVisualChildren<WpfPath>(view);
                var pathCompensada = paths.FirstOrDefault(p => ReferenceEquals(p.Data, vm.StaticPoligonalCompensadaGeometry));
                Assert.NotNull(pathCompensada);
                Assert.InRange(pathCompensada.ActualWidth, 1.0, 1280.0);
                Assert.InRange(pathCompensada.ActualHeight, 1.0, 720.0);

                Assert.True(vm.MostrarPoligonalCompensada);
                Assert.Equal(Visibility.Visible, pathCompensada.Visibility);

                vm.MostrarPoligonalCompensada = false;
                view.UpdateLayout();
                Assert.Equal(Visibility.Collapsed, pathCompensada.Visibility);

                vm.MostrarPoligonalCompensada = true;
                view.UpdateLayout();
                Assert.Equal(Visibility.Visible, pathCompensada.Visibility);

                builder.MessageSpy.AssertSemErros();
            });
        }

        [Fact]
        public void InspecaoCadastral_AoInspecionarPoligonalIrradiacaoOuBreakline_FormataTextoComPrecisaoMilimetrica()
        {
            StaTestRunner.Executar(async () =>
            {
                var builder = new DashboardViewModelTestBuilder();
                var vm = await builder.BuildCompensadoAsync();

                // 1. Vértice da poligonal (P1 no índice 1)
                var p1 = vm.ObterPontoPorIndice(1);
                Assert.NotNull(p1);
                Assert.Equal("P1", p1.Nome);

                vm.NotificarElementoInspecionado(1, null, null, null, null);
                Assert.True(vm.TemInspecaoAtiva);
                Assert.Equal("Ponto: P1 | X: 720100.000 | Y: 7700000.000 | Z: 100.000 m | Poligonal", vm.TextoInspecaoAtual);

                // 2. Vértice de irradiação (ponto "1", descrição "POSTE", índice 5 após os 5 vértices da poligonal fechada E1-P1-P2-P3-E1)
                var irradiacao = vm.ObterPontoPorIndice(5);
                Assert.NotNull(irradiacao);
                Assert.Equal("1", irradiacao.Nome);

                vm.NotificarElementoInspecionado(5, null, null, null, null);
                Assert.True(vm.TemInspecaoAtiva);
                Assert.Equal("Ponto: 1 | X: 720012.247 | Y: 7700012.247 | Z: 110.000 m | Desc: POSTE", vm.TextoInspecaoAtual);

                Assert.Equal(110.000, vm.GetElevation(5)!.Value, 3);

                // 3. Breakline identificada por texto
                vm.NotificarElementoInspecionado(null, null, null, null, "Breakline de E1 para P1");
                Assert.True(vm.TemInspecaoAtiva);
                Assert.Equal("Breakline de E1 para P1", vm.TextoInspecaoAtual);

                builder.MessageSpy.AssertSemErros();
            });
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(999999)]
        public void InspecaoCadastral_QuandoIndiceForaDosLimites_RetornaNuloSemExcecao(int indiceInvalido)
        {
            StaTestRunner.Executar(async () =>
            {
                var vm = await new DashboardViewModelTestBuilder().BuildCompensadoAsync();

                Assert.Null(vm.GetElevation(indiceInvalido));
                Assert.Null(vm.ObterPontoPorIndice(indiceInvalido));
                Assert.Null(vm.ObterIdentificadorVertice(indiceInvalido));
            });
        }

        [Theory]
        [InlineData(WorkspaceModulo.Planimetria, true, true, true, false, false, false, false)]
        [InlineData(WorkspaceModulo.ModelagemMdt, false, false, true, true, true, false, false)]
        [InlineData(WorkspaceModulo.Altimetria, true, false, false, false, false, true, true)]
        [InlineData(WorkspaceModulo.PlantaGeral, true, false, true, false, true, true, true)]
        public void AlternarWorkspace_DeveAplicarPresetsDeCamadasDoModulo(
            WorkspaceModulo moduloAlvo,
            bool esperadoPoligonalCompensada,
            bool esperadoEsbocoBruto,
            bool esperadoPontos,
            bool esperadoTin,
            bool esperadoBreaklines,
            bool esperadoCurvasMestras,
            bool esperadoCurvasSecundarias)
        {
            StaTestRunner.Executar(() =>
            {
                var vm = new DashboardViewModelTestBuilder().Build();

                vm.MudarModuloCommand.Execute(moduloAlvo);

                Assert.Equal(moduloAlvo, vm.ModuloAtivo);
                Assert.Equal(moduloAlvo == WorkspaceModulo.Planimetria, vm.EhModuloPlanimetria);
                Assert.Equal(moduloAlvo == WorkspaceModulo.ModelagemMdt, vm.EhModuloMdt);
                Assert.Equal(moduloAlvo == WorkspaceModulo.Altimetria, vm.EhModuloAltimetria);
                Assert.Equal(moduloAlvo == WorkspaceModulo.PlantaGeral, vm.EhModuloPlantaGeral);

                Assert.Equal(esperadoPoligonalCompensada, vm.MostrarPoligonalCompensada);
                Assert.Equal(esperadoEsbocoBruto, vm.MostrarEsbocoBruto);
                Assert.Equal(esperadoPontos, vm.MostrarPontos);
                Assert.Equal(esperadoTin, vm.MostrarTin);
                Assert.Equal(esperadoBreaklines, vm.MostrarBreaklines);
                Assert.Equal(esperadoCurvasMestras, vm.MostrarCurvasMestras);
                Assert.Equal(esperadoCurvasSecundarias, vm.MostrarCurvasSecundarias);
            });
        }

        [Theory]
        [InlineData(WorkspaceModulo.Planimetria)]
        [InlineData(WorkspaceModulo.ModelagemMdt)]
        [InlineData(WorkspaceModulo.Altimetria)]
        [InlineData(WorkspaceModulo.PlantaGeral)]
        public void BarraNavegacaoWorkspaces_QuandoAcionadaNaView_AtualizaModuloAtivoViaCommandParameter(WorkspaceModulo moduloAlvo)
        {
            StaTestRunner.Executar(() =>
            {
                var vm = new DashboardViewModelTestBuilder().Build();
                var view = new DashboardView { DataContext = vm };
                view.Measure(new Size(1280, 720));
                view.Arrange(new Rect(0, 0, 1280, 720));
                view.UpdateLayout();

                var botoesModulo = VisualTreeTestHelper.FindVisualChildren<RadioButton>(view)
                    .Where(rb => rb.CommandParameter is WorkspaceModulo)
                    .ToDictionary(rb => (WorkspaceModulo)rb.CommandParameter!);

                Assert.Equal(4, botoesModulo.Count);
                Assert.True(botoesModulo.TryGetValue(moduloAlvo, out var botao));
                Assert.True(botao.Command?.CanExecute(botao.CommandParameter) == true);

                botao.Command!.Execute(botao.CommandParameter);

                Assert.Equal(moduloAlvo, vm.ModuloAtivo);
            });
        }

        [Fact]
        public void ExportacaoLaudoQa_QuandoLevantamentoCompensado_GeraLaudoSemErrosENotificaSucesso()
        {
            StaTestRunner.Executar(async () =>
            {
                string tempQaFile = Path.Combine(Path.GetTempPath(), $"QA_{Guid.NewGuid():N}.txt");
                try
                {
                    var builder = new DashboardViewModelTestBuilder()
                        .ComArquivoSalvamento(tempQaFile);

                    var vm = builder.Build();
                    Assert.False(vm.ExportarRelatorioQaCommand.CanExecute(null));
                    Assert.False(vm.RecalcularMalhaCommand.CanExecute(null));
                    Assert.False(vm.ExportarTxtCommand.CanExecute(null));
                    Assert.False(vm.ExportarDxfCommand.CanExecute(null));

                    await ((AsyncRelayCommand)vm.ImportarCadernetaCommand).ExecuteAsync(null);
                    vm.PresetToleranciaSelecionado = TopoGente.Core.Entities.PresetToleranciaEnum.ClasseIVP;
                    await ((AsyncRelayCommand)vm.ProcessarCommand).ExecuteAsync(null);

                    Assert.True(vm.ExportarRelatorioQaCommand.CanExecute(null));
                    Assert.True(vm.RecalcularMalhaCommand.CanExecute(null));
                    Assert.True(vm.ExportarTxtCommand.CanExecute(null));
                    Assert.True(vm.ExportarDxfCommand.CanExecute(null));

                    vm.ExportarRelatorioQaCommand.Execute(null);

                    builder.MessageSpy.AssertSemErros();
                    Assert.Contains(builder.MessageSpy.Sucessos, s => s.Titulo == "Laudo Técnico Exportado");
                    Assert.True(File.Exists(tempQaFile));

                    string relatorioConteudo = File.ReadAllText(tempQaFile);
                    Assert.Contains("LAUDO TÉCNICO DE AUDITORIA", relatorioConteudo);
                    Assert.Contains("NBR 13.133", relatorioConteudo);
                    Assert.Contains("ESTATÍSTICAS DA SUPERFÍCIE", relatorioConteudo);
                }
                finally
                {
                    if (File.Exists(tempQaFile))
                    {
                        File.Delete(tempQaFile);
                    }
                }
            });
        }

        [Fact]
        public void EquidistanciaAltimetrica_QuandoReduzida_AumentaQuantidadeDeCurvasDeNivelGeradas()
        {
            StaTestRunner.Executar(async () =>
            {
                var vmEsparsa = await new DashboardViewModelTestBuilder()
                    .ComEquidistanciaInicial(5.0)
                    .BuildCompensadoAsync();

                var vmDensa = await new DashboardViewModelTestBuilder()
                    .ComEquidistanciaInicial(1.0)
                    .BuildCompensadoAsync();

                int curvasEsparsas = ContarFiguras(vmEsparsa.StaticMajorContoursGeometry)
                                   + ContarFiguras(vmEsparsa.StaticMinorContoursGeometry);

                int curvasDensas = ContarFiguras(vmDensa.StaticMajorContoursGeometry)
                                 + ContarFiguras(vmDensa.StaticMinorContoursGeometry);

                Assert.True(curvasEsparsas > 0, "Equidistância de 5m em desnível de 10m deve gerar ao menos uma curva.");
                Assert.True(curvasDensas > curvasEsparsas, $"Equidistância de 1m ({curvasDensas} curvas) deve gerar mais curvas que 5m ({curvasEsparsas} curvas).");
            });
        }

        private static int ContarFiguras(Geometry geometry) =>
            geometry.IsEmpty() ? 0 : PathGeometry.CreateFromGeometry(geometry).Figures.Count;
    }
}
