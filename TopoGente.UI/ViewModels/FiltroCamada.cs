using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopoGente.UI.ViewModels
{
    public class FiltroCamada : INotifyPropertyChanged
    {
        private bool _isVisivel;
        public string Nome { get; set; } = string.Empty;

        public bool IsVisivel
        {
            get => _isVisivel;
            set
            {
                if (_isVisivel != value)
                {
                    _isVisivel = value;
                    OnPropertyChanged();
                    VisibilidadeAlterada?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Evento vital para avisar o MainViewModel que a tela precisa ser redesenhada
        public event EventHandler? VisibilidadeAlterada;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
