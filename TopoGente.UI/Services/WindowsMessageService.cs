using System.Windows;

namespace TopoGente.UI.Services
{
    public class WindowsMessageService : IMessageService
    {
        public void MostrarErro(string mensagem, string titulo)
        {
            MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void MostrarAviso(string mensagem, string titulo)
        {
            MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void MostrarSucesso(string mensagem, string titulo)
        {
            MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
