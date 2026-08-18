using System;
using System.Linq;
using System.Windows;
using MahApps.Metro.Controls;
using TopoGente.Core.Entities;

namespace TopoGente.UI
{
    public partial class DiagnosticoErrosWindow : MetroWindow
    {
        public DiagnosticoErrosWindow(ResultadoLevantamento resultado)
        {
            InitializeComponent();
            CarregarDados(resultado);
        }

        private void CarregarDados(ResultadoLevantamento resultado)
        {
            if (resultado == null)
            {
                txtStatus.Text = "Nenhum resultado processado para diagnosticar.";
                return;
            }

            var viewModel = new TopoGente.UI.ViewModels.DiagnosticoErrosViewModel(resultado);
            DataContext = viewModel;

            txtStatus.Text = viewModel.ResumoFormatado;
            dgDiagnostico.ItemsSource = viewModel.ItensDiagnostico;
        }
    }
}
