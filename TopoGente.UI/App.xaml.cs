using System.Windows;
using TopoGente.Core.Interfaces; // Portas
using TopoGente.Core.Services;   // Regras de negócio (dentro)

// Referências exclusivas do Main para a Infraestrutura (fora)
using TopoGente.Infrastructure.Adapters.Leitores;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGente.Infrastructure.Adapters.Storage;

namespace TopoGente.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Adaptadores concretos (fora)
            ILeituraArquivoFactory leitorFactory = new LeituraArquivoFactory();
            IArquivoProjetoService projetoService = new ArquivoProjetoService();
            IExportadorDxfService dxfService = new ExportadorDxfService();
            IExportarTxtService exportarTxtService = new ExportarTxtService();

            // 2. Serviços do domínio (dentro)
            IClassificadorGrafo classificador = new ClassificadorGrafo();
            ILevantamentoProcessor processador = new LevantamentoProcessor(classificador);
            IOrganizarCaminhamento organizar = new OrganizarCaminhamento();
            IQaCheckService qaCheck = new QaCheckService();

            // 3. Injeção de dependência (costura)
            MainWindow janelaPrincipal = new MainWindow(
                leitorFactory,
                processador,
                projetoService,
                organizar,
                dxfService,
                exportarTxtService,
                qaCheck,
                classificador
            );

            janelaPrincipal.Show();
        }
    }
}
