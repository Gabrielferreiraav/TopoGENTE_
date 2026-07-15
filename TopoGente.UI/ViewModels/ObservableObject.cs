using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopoGente.UI.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value)) return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
