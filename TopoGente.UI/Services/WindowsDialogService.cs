using Microsoft.Win32;

namespace TopoGente.UI.Services
{
    public class WindowsDialogService : IDialogService
    {
        public string? SelecionarArquivoAbertura(string filtro, string titulo)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = filtro,
                Title = titulo
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            return null;
        }

        public string? SelecionarArquivoSalvamento(string filtro, string titulo, string nomePadrao, string extensaoPadrao)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = titulo,
                Filter = filtro,
                FileName = nomePadrao,
                DefaultExt = extensaoPadrao
            };

            if (saveDialog.ShowDialog() == true)
            {
                return saveDialog.FileName;
            }
            return null;
        }
    }
}
