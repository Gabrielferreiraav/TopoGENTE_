using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;
using CommunityToolkit.Mvvm.Input;
using TopoGente.Core.Domain;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Storage;
using TopoGente.UI.Services;
using TopoGente.UI.ViewModels;
using TopoGente.UI.Views;
using Xunit;

namespace TopoGente.UI.E2ETests
{
    [Collection("STA UI Tests")]
    public class CadViewportInteractionE2ETests
    {
        private static readonly object _appLock = new();

        [Fact]
        public void FluxoCompleto_CadernetaFbk_DeveRenderizarPoligonaisEEnquadrarViewportCad()
        {
            Exception? testEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (_appLock)
                    {
                        if (Application.Current == null)
                        {
                            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        }
                    }

                    var frame = new System.Windows.Threading.DispatcherFrame();

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(async () =>
                        {
                            try
                            {
                                await ExecutarCenarioE2EAsync();
                            }
                            catch (Exception innerEx)
                            {
                                testEx = innerEx;
                            }
                            finally
                            {
                                frame.Continue = false;
                            }
                        }));

                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    testEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (testEx != null)
            {
                throw new Exception($"Falha no teste E2E na thread STA: {testEx.Message}", testEx);
            }
        }

        private async Task ExecutarCenarioE2EAsync()
        {
            // 1. Localização física do arquivo de teste 113503.FBK
            string caminhoFbk = LocalizarArquivoFbk("113503.FBK");

            // 2. Montagem dos serviços reais da arquitetura hexagonal
            var classificador = new ClassificadorGrafo();
            var leitorFactory = new LeituraArquivoFactory();
            var organizador = new OrganizarCaminhamento();
            var processador = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());
            var qaCheckService = new QaCheckService();
            var projetoService = new ArquivoProjetoService();
            var exportarTxt = new ExportarTxtService();
            var exportarDxf = new ExportadorDxfService();

            var mockDialog = new MockDialogService(caminhoFbk);
            var fileService = new LocalFileService();
            var mockMessage = new MockMessageService();

            var vm = new DashboardViewModel(
                () => new RichFeatureTinfourAdapter(),
                () => new RichFeatureTinfourAdapter(),
                leitorFactory,
                organizador,
                processador,
                qaCheckService,
                classificador,
                projetoService,
                exportarTxt,
                exportarDxf,
                mockDialog,
                fileService,
                mockMessage);

            // Informa dimensões da viewport de teste (800x600)
            vm.AtualizarDimensoesViewport(800, 600);

            // 3. Execução da Importação da Caderneta FBK
            var importarCmd = (AsyncRelayCommand)vm.ImportarCadernetaCommand;
            await importarCmd.ExecuteAsync(null);

            // Verificações pós-importação
            Assert.NotEmpty(vm.Visadas);
            Assert.NotEmpty(vm.Poligonais);
            Assert.NotEmpty(vm.Poligonais[0].Estacoes);
            Assert.False(vm.StaticDraftGeometry.IsEmpty(), "Esboço bruto prévio deve ser gerado na importação.");

            // 4. Execução do Cálculo e Ajustamento de Poligonal (Bowditch)
            var processarCmd = (AsyncRelayCommand)vm.ProcessarCommand;
            await processarCmd.ExecuteAsync(null);

            // Asserções do Processamento e NBR 13.133
            Assert.Equal(EstadoMotor.Compensado, vm.EstadoAtualMotor);
            Assert.NotNull(vm.RelatorioQaAtual);
            Assert.True(vm.RelatorioQaAtual.ErroLinear < 0.05, $"Erro linear deve ser subdecimétrico. Obtido: {vm.RelatorioQaAtual.ErroLinear}");

            // 5. Asserções das Geometrias da Viewport CAD
            Assert.False(vm.StaticPoligonalCompensadaGeometry.IsEmpty(), "Poligonal Compensada Geometry não pode estar vazia.");
            Assert.False(vm.StaticDraftGeometry.IsEmpty(), "Draft Geometry bruta não pode estar vazia.");
            Assert.False(vm.StaticPointsGeometry.IsEmpty(), "Pontos Geometry não pode estar vazia.");

            // Validação de fechamento topológico das geometrias (IsClosed == true sem gap angular)
            var pgCompensada = PathGeometry.CreateFromGeometry(vm.StaticPoligonalCompensadaGeometry);
            Assert.NotEmpty(pgCompensada.Figures);
            Assert.True(pgCompensada.Figures[0].IsClosed, "Poligonal Compensada deve ser uma figura fechada (IsClosed == true).");

            var pgDraft = PathGeometry.CreateFromGeometry(vm.StaticDraftGeometry);
            Assert.NotEmpty(pgDraft.Figures);
            Assert.True(pgDraft.Figures[0].IsClosed, "Poligonal Bruta de cenário fechado deve ser uma figura fechada (IsClosed == true).");

            // 6. Asserções da Câmera (Enquadramento Isométrico e Norte Topográfico)
            var matOriginal = vm.CameraMatrix;
            Assert.True(matOriginal.HasInverse, "CameraMatrix deve ser inversível.");
            Assert.True(matOriginal.M11 > 0, "Eixo Leste/X deve ter escala positiva (para a direita).");
            Assert.True(matOriginal.M22 < 0, "Eixo Norte/Y deve ter escala negativa (para cima no WPF).");

            // Validação de que coordenadas de campo caem dentro da viewport
            // Vértice E1 em coordenadas UTM (~722020.712, ~7702635.787)
            Point telaE1 = matOriginal.Transform(new Point(722020.712 - vm.OriginX, 7702635.787 - vm.OriginY));
            Assert.InRange(telaE1.X, 0, 800);
            Assert.InRange(telaE1.Y, 0, 600);

            // Validação de ida e volta (Pixel -> UTM real)
            Point e1Recuperado = vm.GetModelCoordinates(telaE1);
            Assert.Equal(722020.712, e1Recuperado.X, 2);
            Assert.Equal(7702635.787, e1Recuperado.Y, 2);

            // Validação geométrica: Ponto mais ao Norte deve ter menor Y em tela (mais próximo do topo)
            Point ptSul = matOriginal.Transform(new Point(10, 0));
            Point ptNorte = matOriginal.Transform(new Point(10, 100));
            Assert.True(ptNorte.Y < ptSul.Y, "Vértice mais ao Norte DEVE ter Y menor em tela (Norte Topográfico para cima).");
            Assert.Equal(ptSul.X, ptNorte.X, 3);

            // Validação de recuperação de coordenadas UTM através de GetModelCoordinates
            Point telaCentro = new Point(400, 300);
            Point modeloCentro = vm.GetModelCoordinates(telaCentro);
            Assert.True(modeloCentro.X > 700000, "GetModelCoordinates deve retornar coordenadas UTM absolutas (Leste > 700.000m).");
            Assert.True(modeloCentro.Y > 7000000, "GetModelCoordinates deve retornar coordenadas UTM absolutas (Norte > 7.000.000m).");

            // 7. Simulação de Pan e Zoom
            // Teste de Pan
            vm.AplicarPan(50, -30);
            Assert.Equal(matOriginal.OffsetX + 50, vm.CameraMatrix.OffsetX, 3);
            Assert.Equal(matOriginal.OffsetY - 30, vm.CameraMatrix.OffsetY, 3);

            // Teste de Zoom
            var matAposPan = vm.CameraMatrix;
            vm.AplicarZoom(1.2, 400, 300);
            Assert.True(Math.Abs(vm.CameraMatrix.M11) > Math.Abs(matAposPan.M11), "Zoom in deve aumentar a escala absoluta.");

            // Teste de FitToScreen / ZoomExtentsCommand
            vm.ZoomExtentsCommand.Execute(null);
            Assert.Equal(matOriginal.M11, vm.CameraMatrix.M11, 3);
            Assert.Equal(matOriginal.M22, vm.CameraMatrix.M22, 3);
            Assert.Equal(matOriginal.OffsetX, vm.CameraMatrix.OffsetX, 3);
            Assert.Equal(matOriginal.OffsetY, vm.CameraMatrix.OffsetY, 3);

            // 8. Inspeção da Árvore Visual WPF (DashboardView)
            var view = new DashboardView { DataContext = vm };
            view.Measure(new Size(1280, 720));
            view.Arrange(new Rect(0, 0, 1280, 720));
            view.UpdateLayout();

            // Localiza todos os elementos Path na View
            var paths = FindVisualChildren<WpfPath>(view);
            Assert.NotEmpty(paths);

            // Confere se o Path da Poligonal Compensada existe com Stroke Azul Cobalto
            var pathCompensada = paths.FirstOrDefault(p => ReferenceEquals(p.Data, vm.StaticPoligonalCompensadaGeometry));
            Assert.NotNull(pathCompensada);
            Assert.Contains("1565C0", pathCompensada.Stroke?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

            // Confere dimensões do Path no layout (devem ser métricas locais, NUNCA milhões de pixels)
            Assert.True(pathCompensada.ActualWidth < 1000, $"ActualWidth deve ser métrico local (< 1000). Obtido: {pathCompensada.ActualWidth}");
            Assert.True(pathCompensada.ActualHeight < 1000, $"ActualHeight deve ser métrico local (< 1000). Obtido: {pathCompensada.ActualHeight}");

            // Confere se o Path da Poligonal Bruta de Referência existe na Layer 1 com Opacity 0.45 e Stroke Âmbar Escuro
            var pathBrutaRef = paths.FirstOrDefault(p => ReferenceEquals(p.Data, vm.StaticDraftGeometry) && Math.Abs(p.Opacity - 0.45) < 0.05);
            Assert.NotNull(pathBrutaRef);
            Assert.Contains("E65100", pathBrutaRef.Stroke?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

            // Validação do Controle Reativo de Visibilidade de Camadas (Aba Camadas)
            Assert.True(vm.MostrarPoligonalCompensada);
            Assert.Equal(Visibility.Visible, pathCompensada.Visibility);

            vm.MostrarPoligonalCompensada = false;
            view.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, pathCompensada.Visibility);

            vm.MostrarPoligonalCompensada = true;
            view.UpdateLayout();
            Assert.Equal(Visibility.Visible, pathCompensada.Visibility);

            // 9. Rasterização Real com RenderTargetBitmap na Viewport CAD
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 720, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(view);
            int[] pixels = new int[1280 * 720];
            rtb.CopyPixels(pixels, 1280 * 4, 0);

            // Contabiliza pixels renderizados na região do Viewport CAD (X > 405)
            int cadNonZeroPixels = 0;
            for (int y = 0; y < 720; y++)
            {
                for (int x = 405; x < 1280; x++)
                {
                    if (pixels[y * 1280 + x] != 0) cadNonZeroPixels++;
                }
            }

            Assert.True(cadNonZeroPixels > 0, $"A viewport CAD deve rasterizar primitivas gráficas visíveis. Pixels encontrados: {cadNonZeroPixels}");

            // 10. Asserções das Métricas de Superfície (TIN e Relevo)
            Assert.True(vm.TotalTriangulosMdt > 0, "Delaunay TIN deve gerar triângulos para a nuvem de pontos calculada.");
            Assert.True(vm.TotalVerticesMdt > 0, "Vértices da malha TIN devem ser maiores que zero.");
            Assert.True(vm.CotaMaximaTerreno > vm.CotaMinimaTerreno, "Cota máxima deve ser superior à cota mínima.");
            Assert.True(vm.DesnivelTerreno > 0, "Desnível do terreno deve ser positivo.");
            Assert.True(vm.RecalcularMalhaCommand.CanExecute(null), "RecalcularMalhaCommand deve estar habilitado com levantamento compensado.");
            Assert.True(vm.ExportarRelatorioQaCommand.CanExecute(null), "ExportarRelatorioQaCommand deve estar habilitado com laudo QA.");
        }

        [Fact]
        public void AlternanciaWorkspaces_DeveAplicarPresetsEAtivarPaineisContextuais_STA()
        {
            Exception? testEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (_appLock)
                    {
                        if (Application.Current == null)
                        {
                            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        }
                    }

                    var frame = new System.Windows.Threading.DispatcherFrame();

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(() =>
                        {
                            try
                            {
                                var vm = new DashboardViewModel(
                                    () => new RichFeatureTinfourAdapter(),
                                    () => new RichFeatureTinfourAdapter(),
                                    new LeituraArquivoFactory(),
                                    new OrganizarCaminhamento(),
                                    new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory()),
                                    new QaCheckService(),
                                    new ClassificadorGrafo(),
                                    new ArquivoProjetoService(),
                                    new ExportarTxtService(),
                                    new ExportadorDxfService(),
                                    new MockDialogService("dummy"),
                                    new LocalFileService(),
                                    new MockMessageService());

                                // 1. Módulo Inicial: Planimetria
                                Assert.Equal(WorkspaceModulo.Planimetria, vm.ModuloAtivo);
                                Assert.True(vm.EhModuloPlanimetria);
                                Assert.False(vm.EhModuloMdt);
                                Assert.False(vm.EhModuloAltimetria);
                                Assert.False(vm.EhModuloPlantaGeral);

                                Assert.False(vm.RecalcularMalhaCommand.CanExecute(null));
                                Assert.False(vm.ExportarRelatorioQaCommand.CanExecute(null));

                                Assert.True(vm.MostrarPoligonalCompensada);
                                Assert.True(vm.MostrarEsbocoBruto);
                                Assert.True(vm.MostrarPontos);
                                Assert.False(vm.MostrarTin);
                                Assert.False(vm.MostrarBreaklines);
                                Assert.False(vm.MostrarCurvasMestras);
                                Assert.False(vm.MostrarCurvasSecundarias);

                                // 2. Transição para Modelagem Digital (MDT)
                                vm.MudarModuloCommand.Execute(WorkspaceModulo.ModelagemMdt);
                                Assert.Equal(WorkspaceModulo.ModelagemMdt, vm.ModuloAtivo);
                                Assert.False(vm.EhModuloPlanimetria);
                                Assert.True(vm.EhModuloMdt);
                                Assert.False(vm.EhModuloAltimetria);
                                Assert.False(vm.EhModuloPlantaGeral);

                                Assert.True(vm.MostrarTin);
                                Assert.True(vm.MostrarBreaklines);
                                Assert.True(vm.MostrarPontos);
                                Assert.False(vm.MostrarPoligonalCompensada);
                                Assert.False(vm.MostrarEsbocoBruto);
                                Assert.False(vm.MostrarCurvasMestras);
                                Assert.False(vm.MostrarCurvasSecundarias);

                                // 3. Transição para Altimetria (Curvas de Nível)
                                vm.MudarModuloCommand.Execute(WorkspaceModulo.Altimetria);
                                Assert.Equal(WorkspaceModulo.Altimetria, vm.ModuloAtivo);
                                Assert.False(vm.EhModuloPlanimetria);
                                Assert.False(vm.EhModuloMdt);
                                Assert.True(vm.EhModuloAltimetria);
                                Assert.False(vm.EhModuloPlantaGeral);

                                Assert.True(vm.MostrarCurvasMestras);
                                Assert.True(vm.MostrarCurvasSecundarias);
                                Assert.True(vm.MostrarPoligonalCompensada);
                                Assert.False(vm.MostrarPontos);
                                Assert.False(vm.MostrarTin);
                                Assert.False(vm.MostrarEsbocoBruto);
                                Assert.False(vm.MostrarBreaklines);

                                // 4. Transição para Planta Geral (Entrega)
                                vm.MudarModuloCommand.Execute(WorkspaceModulo.PlantaGeral);
                                Assert.Equal(WorkspaceModulo.PlantaGeral, vm.ModuloAtivo);
                                Assert.False(vm.EhModuloPlanimetria);
                                Assert.False(vm.EhModuloMdt);
                                Assert.False(vm.EhModuloAltimetria);
                                Assert.True(vm.EhModuloPlantaGeral);

                                Assert.True(vm.MostrarPoligonalCompensada);
                                Assert.True(vm.MostrarPontos);
                                Assert.True(vm.MostrarCurvasMestras);
                                Assert.True(vm.MostrarCurvasSecundarias);
                                Assert.True(vm.MostrarBreaklines);
                                Assert.False(vm.MostrarEsbocoBruto);
                                Assert.False(vm.MostrarTin);

                                // 5. Validação da Árvore Visual WPF (Layout e Abas de Navegação)
                                var view = new DashboardView { DataContext = vm };
                                view.Measure(new Size(1280, 720));
                                view.Arrange(new Rect(0, 0, 1280, 720));
                                view.UpdateLayout();

                                var radioButtons = FindVisualChildren<RadioButton>(view);
                                var rbPlanimetria = radioButtons.First(r => r.Content?.ToString()?.Contains("Apoio Planimétrico") == true);
                                var rbMdt = radioButtons.First(r => r.Content?.ToString()?.Contains("Modelagem Digital") == true);
                                var rbAltimetria = radioButtons.First(r => r.Content?.ToString()?.Contains("Altimetria") == true);
                                var rbPlanta = radioButtons.First(r => r.Content?.ToString()?.Contains("Planta Geral") == true);

                                // Alternância interativa via comandos nos botões da barra superior
                                rbPlanimetria.Command?.Execute(rbPlanimetria.CommandParameter);
                                Assert.Equal(WorkspaceModulo.Planimetria, vm.ModuloAtivo);
                                Assert.True(vm.EhModuloPlanimetria);

                                rbMdt.Command?.Execute(rbMdt.CommandParameter);
                                Assert.Equal(WorkspaceModulo.ModelagemMdt, vm.ModuloAtivo);
                                Assert.True(vm.EhModuloMdt);
                                Assert.True(vm.MostrarTin);

                                rbAltimetria.Command?.Execute(rbAltimetria.CommandParameter);
                                Assert.Equal(WorkspaceModulo.Altimetria, vm.ModuloAtivo);
                                Assert.True(vm.EhModuloAltimetria);
                                Assert.False(vm.MostrarPontos);

                                rbPlanta.Command?.Execute(rbPlanta.CommandParameter);
                                Assert.Equal(WorkspaceModulo.PlantaGeral, vm.ModuloAtivo);
                                Assert.True(vm.EhModuloPlantaGeral);
                                Assert.True(vm.MostrarPontos);
                            }
                            catch (Exception innerEx)
                            {
                                testEx = innerEx;
                            }
                            finally
                            {
                                frame.Continue = false;
                            }
                        }));

                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    testEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (testEx != null)
            {
                throw new Exception($"Falha no teste de Workspaces na thread STA: {testEx.Message}", testEx);
            }
        }

        [Fact]
        public void WorkspacesComLevantamentoCalculado_DeveExibirMetricasEComandosContextuais_STA()
        {
            Exception? testEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    lock (_appLock)
                    {
                        if (Application.Current == null)
                        {
                            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        }
                    }

                    var frame = new System.Windows.Threading.DispatcherFrame();

                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(async () =>
                        {
                            string tempQaFile = Path.Combine(Path.GetTempPath(), $"QA_{Guid.NewGuid():N}.txt");
                            try
                            {
                                string caminhoFbk = LocalizarArquivoFbk("113503.FBK");
                                var classificador = new ClassificadorGrafo();
                                var leitorFactory = new LeituraArquivoFactory();
                                var organizador = new OrganizarCaminhamento();
                                var processador = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());
                                var qaCheckService = new QaCheckService();
                                var projetoService = new ArquivoProjetoService();
                                var exportarTxt = new ExportarTxtService();
                                var exportarDxf = new ExportadorDxfService();

                                var mockDialog = new MockDialogService(caminhoFbk, tempQaFile);
                                var fileService = new LocalFileService();
                                var mockMessage = new MockMessageService();

                                var vm = new DashboardViewModel(
                                    () => new RichFeatureTinfourAdapter(),
                                    () => new RichFeatureTinfourAdapter(),
                                    leitorFactory,
                                    organizador,
                                    processador,
                                    qaCheckService,
                                    classificador,
                                    projetoService,
                                    exportarTxt,
                                    exportarDxf,
                                    mockDialog,
                                    fileService,
                                    mockMessage);

                                vm.AtualizarDimensoesViewport(800, 600);

                                // Estado inicial pré-processamento
                                Assert.Equal(0, vm.TotalTriangulosMdt);
                                Assert.Equal(0, vm.CotaMinimaTerreno);
                                Assert.Equal(0, vm.CotaMaximaTerreno);
                                Assert.Equal(0, vm.DesnivelTerreno);
                                Assert.False(vm.RecalcularMalhaCommand.CanExecute(null));
                                Assert.False(vm.ExportarRelatorioQaCommand.CanExecute(null));

                                // Importação e Cálculo
                                await ((AsyncRelayCommand)vm.ImportarCadernetaCommand).ExecuteAsync(null);
                                await ((AsyncRelayCommand)vm.ProcessarCommand).ExecuteAsync(null);

                                Assert.Equal(EstadoMotor.Compensado, vm.EstadoAtualMotor);
                                Assert.True(vm.TotalPontos > 0);
                                Assert.True(vm.TotalTriangulosMdt > 0);
                                Assert.True(vm.CotaMaximaTerreno > vm.CotaMinimaTerreno);
                                Assert.True(vm.DesnivelTerreno > 0);
                                Assert.True(vm.RecalcularMalhaCommand.CanExecute(null));
                                Assert.True(vm.ExportarRelatorioQaCommand.CanExecute(null));

                                // Exportação de Relatório QA
                                vm.ExportarRelatorioQaCommand.Execute(null);
                                Assert.True(File.Exists(tempQaFile));
                                string relatorioConteudo = File.ReadAllText(tempQaFile);
                                Assert.Contains("LAUDO TÉCNICO DE AUDITORIA", relatorioConteudo);
                                Assert.Contains("NBR 13.133", relatorioConteudo);
                                Assert.Contains("ESTATÍSTICAS DA SUPERFÍCIE", relatorioConteudo);

                                // Recálculo de malha TIN
                                await ((AsyncRelayCommand)vm.RecalcularMalhaCommand).ExecuteAsync(null);
                                Assert.True(vm.TotalTriangulosMdt > 0);

                                // Parametrização de equidistância altimétrica
                                vm.StepInterval = 0.5;
                                Assert.Equal(0.5, vm.StepInterval);

                                // Verificação com Árvore Visual WPF
                                var view = new DashboardView { DataContext = vm };
                                view.Measure(new Size(1280, 720));
                                view.Arrange(new Rect(0, 0, 1280, 720));
                                view.UpdateLayout();

                                // Alternância para MDT
                                vm.MudarModulo(WorkspaceModulo.ModelagemMdt);
                                view.UpdateLayout();
                                Assert.True(vm.EhModuloMdt);
                                Assert.True(vm.MostrarTin);
                                Assert.False(vm.MostrarCurvasMestras);

                                // Alternância para Altimetria
                                vm.MudarModulo(WorkspaceModulo.Altimetria);
                                view.UpdateLayout();
                                Assert.True(vm.EhModuloAltimetria);
                                Assert.True(vm.MostrarCurvasMestras);
                                Assert.False(vm.MostrarTin);
                                Assert.False(vm.MostrarPontos);

                                // Alternância para Planta Geral
                                vm.MudarModulo(WorkspaceModulo.PlantaGeral);
                                view.UpdateLayout();
                                Assert.True(vm.EhModuloPlantaGeral);
                                Assert.True(vm.MostrarCurvasMestras);
                                Assert.True(vm.MostrarPoligonalCompensada);
                                Assert.True(vm.MostrarPontos);
                                Assert.False(vm.MostrarTin);
                            }
                            catch (Exception innerEx)
                            {
                                testEx = innerEx;
                            }
                            finally
                            {
                                if (File.Exists(tempQaFile))
                                {
                                    try { File.Delete(tempQaFile); } catch { }
                                }
                                frame.Continue = false;
                            }
                        }));

                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    testEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (testEx != null)
            {
                throw new Exception($"Falha no teste de Métricas/Workspaces calculados na thread STA: {testEx.Message}", testEx);
            }
        }

        private static string LocalizarArquivoFbk(string nomeArquivo)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var p1 = Path.Combine(dir.FullName, "publish", nomeArquivo);
                if (File.Exists(p1)) return p1;

                var p2 = Path.Combine(dir.FullName, "readme", nomeArquivo);
                if (File.Exists(p2)) return p2;

                dir = dir.Parent;
            }

            throw new FileNotFoundException($"Arquivo {nomeArquivo} não encontrado nos diretórios ascendentes.");
        }

        private static System.Collections.Generic.List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            var list = new System.Collections.Generic.List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    list.Add(typed);
                }
                list.AddRange(FindVisualChildren<T>(child));
            }
            return list;
        }

        private class MockDialogService : IDialogService
        {
            private readonly string _arquivoParaAbrir;
            private readonly string? _arquivoParaSalvar;
            public MockDialogService(string arquivoParaAbrir, string? arquivoParaSalvar = null)
            {
                _arquivoParaAbrir = arquivoParaAbrir;
                _arquivoParaSalvar = arquivoParaSalvar;
            }
            public string? SelecionarArquivoAbertura(string filtro, string titulo) => _arquivoParaAbrir;
            public string? SelecionarArquivoSalvamento(string filtro, string titulo, string nomePadrao, string extensaoPadrao) => _arquivoParaSalvar;
        }

        private class MockMessageService : IMessageService
        {
            public void MostrarErro(string mensagem, string titulo) { }
            public void MostrarAviso(string mensagem, string titulo) { }
            public void MostrarSucesso(string mensagem, string titulo) { }
        }
    }
}
