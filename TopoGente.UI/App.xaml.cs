using System.Windows;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Storage;
using TopoGente.UI.Eventing;

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
            ILevantamentoProcessor processador = new LevantamentoProcessor(classificador);
            IOrganizarCaminhamento organizador = new OrganizarCaminhamento();
            IQaCheckService qaCheck = new QaCheckService();

            IUiEventHub uiEventHub = new UiEventHub();

            MainWindow janelaPrincipal = new MainWindow(
                leitorFactory,
                processador,
                projetoService,
                organizador,
                dxfService,
                exportarTxtService,
                qaCheck,
                classificador,
                uiEventHub);

            CadernetaWindow cadernetaWindow = new CadernetaWindow(uiEventHub);
            VisualizacaoWindow visualizacaoWindow = new VisualizacaoWindow(uiEventHub);

            MainWindow = janelaPrincipal;
            janelaPrincipal.Show();

            cadernetaWindow.Owner = janelaPrincipal;
            visualizacaoWindow.Owner = janelaPrincipal;
        }
    }
}
