using System.Configuration;
using System.Data;
using System.Windows;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;

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

            ILeituraArquivoFactory leitor = new LeituraArquivoFactory();
            ILevantamentoProcessor processador = new LevantamentoProcessor();
            IArquivoProjetoService projeto = new ArquivoProjetoService();
            IQaCheckService qaCheck = new QaCheckService();
            IOrganizarCaminhamento organizar = new OrganizarCaminhamento();
            IExportadorDxfService dxfService = new ExportadorDxfService();

            MainWindow janelaPrincipal = new MainWindow(leitor, processador, projeto, organizar,dxfService, qaCheck);

            janelaPrincipal.Show();
        }

    }
}
