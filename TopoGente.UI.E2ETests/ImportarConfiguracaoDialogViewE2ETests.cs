using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using TopoGente.Core.Entities;
using TopoGente.UI.E2ETests.Infrastructure;
using TopoGente.UI.ViewModels;
using TopoGente.UI.Views;
using Xunit;

namespace TopoGente.UI.E2ETests
{
    [Collection("STA UI Tests")]
    public class ImportarConfiguracaoDialogViewE2ETests
    {
        [Fact]
        public void LivePreviewDataGrid_AoRenderizarLinhasValidasEInvalidas_ExibeIconeCorrespondenteSemErrosDeBinding()
        {
            StaTestRunner.Executar(() =>
            {
                string[] linhas =
                [
                    "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
                    "STN \"E1\" 1.500",
                    "LINHA_INVALIDA_SEM_TOKENS_VALIDOS"
                ];

                var dialogVm = new ImportarConfiguracaoDialogViewModel(
                    "teste.fbk",
                    linhas,
                    FormatoArquivoEntrada.Fbk);

                using var bindingTrace = new DataBindingTraceScope(SourceLevels.Warning);
                var dialog = new ImportarConfiguracaoDialogView(dialogVm);
                try
                {
                    var rootContent = Assert.IsAssignableFrom<FrameworkElement>(dialog.Content);
                    rootContent.Measure(new Size(920, 650));
                    rootContent.Arrange(new Rect(0, 0, 920, 650));
                    rootContent.UpdateLayout();

                    var linhasGrid = VisualTreeTestHelper.FindVisualChildren<DataGridRow>(rootContent);
                    Assert.Collection(linhasGrid,
                        row => AssertIconeLinha(row, esperaValida: true, PackIconKind.CheckCircle),
                        row => AssertIconeLinha(row, esperaValida: true, PackIconKind.CheckCircle),
                        row => AssertIconeLinha(row, esperaValida: false, PackIconKind.AlertCircle));

                    Assert.Empty(bindingTrace.Messages);
                }
                finally
                {
                    dialog.Close();
                }
            });
        }

        private static void AssertIconeLinha(DataGridRow row, bool esperaValida, PackIconKind kindEsperado)
        {
            var dto = Assert.IsType<LinhaPreviewDTO>(row.Item);
            Assert.Equal(esperaValida, dto.Valida);

            var icone = Assert.Single(VisualTreeTestHelper.FindVisualChildren<PackIcon>(row));
            Assert.Equal(kindEsperado, icone.Kind);
        }
    }
}
