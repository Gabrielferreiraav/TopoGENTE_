using System;
using System.Windows;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Storage;
using TopoGENTE.Domain.Ports;
using TopoGente.UI.Eventing;
using TopoGente.UI.Services;

namespace TopoGente.UI
{
    public partial class App : Application
    {
        // REGISTRO DE CICLO DE VIDA — MDT:
        // RichFeatureTinfourAdapter é TRANSIENT: cada invocação da factory produz
        // uma instância isolada. O adaptador mantém estado interno de malha (IncrementalTin
        // selado via Lock()), portanto NUNCA deve ser compartilhado entre cenários paralelos.
        // Consuma a factory onde precisar de triangulação ou análise topográfica.
        public static readonly Func<ITerrainTriangulator> TerrainTriangulatorFactory =
            () => new RichFeatureTinfourAdapter();

        public static readonly Func<ITopographicAnalytics> TopographicAnalyticsFactory =
            () => new RichFeatureTinfourAdapter();

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

