using MahApps.Metro.Controls;
using TopoGente.UI.ViewModels;

namespace TopoGente.UI.Views;

public partial class ImportarConfiguracaoDialogView : MetroWindow
{
    public ImportarConfiguracaoDialogView(ImportarConfiguracaoDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.FecharJanela = result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
