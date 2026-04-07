using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AresAssistant.ViewModels;

/// <summary>
/// Clase base MVVM que implementa INotifyPropertyChanged.
/// Proporciona <see cref="OnPropertyChanged"/> y <see cref="SetField{T}"/>
/// para simplificar la notificación de cambios en propiedades.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
