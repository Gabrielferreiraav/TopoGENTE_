using System.Windows;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Storage;
using TopoGente.UI.Eventing;
using TopoGente.UI.Services;

namespace TopoGente.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ILeituraArquivoFactory leitorFactory = new LeituraArquivoFactory();
            IArquivoProjetoService projetoService = new ArquivoProjetoService();
            IExportadorDxfService dxfService = new ExportadorDxfService();
            IExportarTxtService exportarTxtService = new ExportarTxtService();

            IClassificadorGrafo classificador = new ClassificadorGrafo();
            var factory = new CompensacaoStrategyFactory();
            ILevantamentoProcessor processador = new LevantamentoProcessor(classificador, factory);
            IOrganizarCaminhamento organizador = new OrganizarCaminhamento();
            IQaCheckService qaCheck = new QaCheckService();

            IUiEventHub uiEventHub = new UiEventHub();

            var mainViewModel = new TopoGente.UI.ViewModels.MainViewModel(
                leitorFactory,
                processador,
                projetoService,
                organizador,
                dxfService,
                exportarTxtService,
                qaCheck,
                classificador,
                uiEventHub,
                new WindowsDialogService(),
                new WindowsMessageService(),
                new LocalFileService());

            MainWindow janelaPrincipal = new MainWindow(mainViewModel);

            CadernetaWindow cadernetaWindow = new CadernetaWindow(uiEventHub);
            VisualizacaoWindow visualizacaoWindow = new VisualizacaoWindow(uiEventHub);

            MainWindow = janelaPrincipal;
            janelaPrincipal.Show();

            cadernetaWindow.Owner = janelaPrincipal;
            visualizacaoWindow.Owner = janelaPrincipal;
        }
    }
}
