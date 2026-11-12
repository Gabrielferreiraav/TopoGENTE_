using MahApps.Metro.Controls;

namespace TopoGente.UI.Views
{
    public partial class DashboardWindow : MetroWindow
    {
        public DashboardWindow()
        {
            InitializeComponent();
        }

        private void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void ExportarMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (BtnExportar != null)
            {
                BtnExportar.IsChecked = false;
            }
        }
    }
}
