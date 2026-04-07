using System.IO;
using System.Net.Http;
using System.Windows;
using AresAssistant.Core;
using AresAssistant.Helpers;

namespace AresAssistant.Views;

/// <summary>
/// Ventana modal para descargar e instalar Ollama automáticamente.
/// Gestiona 3 fases: descarga del instalador (0-60%), ejecución silenciosa (60-80%)
/// y espera a que el servicio responda (80-100%).
/// </summary>
public partial class OllamaInstallWindow : Window
{
    private static readonly string InstallerUrl = "https://ollama.com/download/OllamaSetup.exe";
    private CancellationTokenSource? _cts;

    public OllamaInstallWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, _) => DragMove();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            // ── Step 1: Download installer ──
            TxtStep.Text = "Descargando instalador de Ollama...";
            TxtStatus.Text = "Conectando con ollama.com...";

            var installerPath = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                using var resp = await http.GetAsync(InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                var totalBytes = resp.Content.Headers.ContentLength ?? 0;

                using var dlStream = await resp.Content.ReadAsStreamAsync(ct);
                using var fs = File.Create(installerPath);
                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await dlStream.ReadAsync(buffer, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;

                    if (totalBytes > 0)
                    {
                        var pct = (double)downloaded / totalBytes;
                        SetProgress(pct * 0.60); // Download = 0–60%
                        TxtPercentage.Text = $"{pct * 60:F0} %";
                        TxtSize.Text = $"{FormatHelper.FormatBytes(downloaded)} / {FormatHelper.FormatBytes(totalBytes)}";
                    }

                    TxtStatus.Text = "Descargando instalador...";
                }
            }

            // ── Step 2: Run installer ──
            TxtStep.Text = "Instalando Ollama...";
            TxtStatus.Text = "Ejecutando instalador (se pedirá permiso de administrador)...";
            SetProgress(0.62);
            TxtPercentage.Text = "62 %";
            TxtSize.Text = "";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/VERYSILENT /NORESTART",
                UseShellExecute = true,
                Verb = "runas"
            };
            var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
                await proc.WaitForExitAsync(ct);

            try { File.Delete(installerPath); } catch { }

            SetProgress(0.80);
            TxtPercentage.Text = "80 %";

            // ── Step 3: Wait for Ollama API ──
            TxtStep.Text = "Esperando a que Ollama inicie...";
            TxtStatus.Text = "Comprobando servicio...";

            var client = new OllamaClient();
            bool ready = false;
            for (int i = 0; i < 30; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(2000, ct);
                if (await client.IsAvailableAsync())
                {
                    ready = true;
                    break;
                }
                SetProgress(0.80 + 0.18 * ((i + 1) / 30.0));
                TxtPercentage.Text = $"{80 + 18.0 * ((i + 1) / 30.0):F0} %";
                TxtStatus.Text = $"Esperando servicio... ({i + 1}/30)";
            }

            if (!ready)
            {
                TxtStep.Text = "Ollama no responde";
                TxtStatus.Text = "Intenta ejecutar 'ollama serve' manualmente.";
                TxtPercentage.Text = "Error";
                BtnCancel.Content = "Cerrar";
                return;
            }

            // ── Success ──
            SetProgress(1.0);
            TxtPercentage.Text = "100 %";
            TxtStep.Text = "Ollama instalado correctamente";
            TxtStatus.Text = "Servicio activo y listo.";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            DialogResult = false;
        }
        catch (Exception ex)
        {
            TxtStep.Text = "Error durante la instalación";
            TxtStatus.Text = ex.Message;
            TxtPercentage.Text = "Error";
            BtnCancel.Content = "Cerrar";
        }
    }

    private void SetProgress(double pct)
    {
        pct = Math.Clamp(pct, 0, 1);
        var barWidth = ProgressBarGrid.ActualWidth;
        if (barWidth > 0)
            ProgressFill.Width = barWidth * pct;
    }

    /// <summary>Cancela la operación en curso o cierra la ventana si ya terminó.</summary>
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
            _cts.Cancel();
        else
            DialogResult = false;
    }
}
