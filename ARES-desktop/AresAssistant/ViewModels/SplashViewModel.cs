namespace AresAssistant.ViewModels;

/// <summary>
/// ViewModel para la pantalla de splash/carga inicial.
/// Expone el texto de estado, progreso y flag de completado.
/// </summary>
public class SplashViewModel : ViewModelBase
{
    private string _statusText = "Inicializando ARES...";
    private int _progress;
    private bool _isComplete;

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public int Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public bool IsComplete
    {
        get => _isComplete;
        set => SetField(ref _isComplete, value);
    }
}
