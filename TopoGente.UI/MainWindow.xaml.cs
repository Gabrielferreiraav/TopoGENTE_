using MahApps.Metro.Controls;
using TopoGente.UI.ViewModels;

namespace TopoGente.UI
{
    public partial class MainWindow : MetroWindow
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
