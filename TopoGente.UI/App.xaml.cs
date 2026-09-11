using System;
using System.Windows;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGENTE.Domain.Ports;
using TopoGente.UI.Services;

namespace TopoGente.UI
{
    public partial class App : Application
    {
        public static readonly Func<ITerrainTriangulator> TerrainTriangulatorFactory =
            () => new RichFeatureTinfourAdapter();
        public static readonly Func<ITopographicAnalytics> TopographicAnalyticsFactory =
            () => new RichFeatureTinfourAdapter();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var classificador    = new ClassificadorGrafo();
            var leitorFactory    = new LeituraArquivoFactory();
            var organizador      = new OrganizarCaminhamento();
            var processador      = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());
            var qaCheckService   = new QaCheckService();
            
            var projetoService   = new TopoGente.Infrastructure.Adapters.Storage.ArquivoProjetoService();
            var exportarTxt      = new TopoGente.Infrastructure.Adapters.Exportadores.ExportarTxtService();
            var exportarDxf      = new TopoGente.Infrastructure.Adapters.Exportadores.ExportadorDxfService();

            var dialogService    = new WindowsDialogService();
            var fileService      = new LocalFileService();
            var messageService   = new WindowsMessageService();

            var dashboardViewModel = new TopoGente.UI.ViewModels.DashboardViewModel(
                TerrainTriangulatorFactory,
                TopographicAnalyticsFactory,
                leitorFactory, 
                organizador, 
                processador, 
                qaCheckService, 
                classificador, 
                projetoService, 
                exportarTxt, 
                exportarDxf,
                dialogService, 
                fileService, 
                messageService);

            var dashboardWindow = new TopoGente.UI.Views.DashboardWindow
            {
                DataContext = dashboardViewModel
            };

            this.MainWindow = dashboardWindow;
            dashboardWindow.Show();
        }
    }
}