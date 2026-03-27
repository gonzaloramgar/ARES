using System.Windows;
using System.Windows.Input;
using AresAssistant.Core;
using AresAssistant.Helpers;

namespace AresAssistant.Views;

/// <summary>
/// Ventana modal de progreso para la descarga de modelos de Ollama.
/// Muestra porcentaje, tamaño descargado y estado en tiempo real.
/// </summary>
public partial class ModelDownloadWindow : Window
{
    private readonly OllamaClient _client;
    private readonly string _model;
    private CancellationTokenSource? _cts;

    public ModelDownloadWindow(OllamaClient client, string model)
    {
        InitializeComponent();
        _client = client;
        _model = model;
        TxtModelName.Text = model;
        MouseLeftButtonDown += (_, _) => DragMove();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        try
        {
            await Task.Run(() => _client.PullModelAsync(_model, onProgress: OnProgress, ct: _cts.Token));
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            DialogResult = false;
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
            TxtPercentage.Text = "Error";
            BtnCancel.Content = "Cerrar";
        }
    }

    private void OnProgress(double pct, string status, long completed, long total)
    {
        Dispatcher.BeginInvoke(() =>
        {
            // Update progress bar
            var barWidth = ProgressFill.Parent is FrameworkElement parent ? parent.ActualWidth : ActualWidth - 56;
            if (barWidth > 0)
                ProgressFill.Width = barWidth * Math.Clamp(pct, 0, 1);

            // Percentage
            TxtPercentage.Text = $"{pct:P0}";

            // Size info
            if (total > 0)
                TxtSize.Text = $"{FormatHelper.FormatBytes(completed)} / {FormatHelper.FormatBytes(total)}";

            // Status
            if (status.Contains("pulling"))
                TxtStatus.Text = "Descargando capas del modelo...";
            else if (status.Contains("verifying"))
                TxtStatus.Text = "Verificando integridad...";
            else if (status == "success")
                TxtStatus.Text = "✓ Descarga completada";
            else if (!string.IsNullOrEmpty(status))
                TxtStatus.Text = status;
        });
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        else
        {
            DialogResult = false;
        }
    }

}
