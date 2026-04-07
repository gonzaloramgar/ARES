using System.Windows;
using System.Windows.Threading;
using AresAssistant.Config;
using AresAssistant.Core;
using AresAssistant.ViewModels;

namespace AresAssistant.Views;

public partial class SplashWindow : Window
{
    private readonly bool _isFirstLaunch;
    private readonly SplashViewModel _vm = new();
    private static readonly TimeSpan ScanRefreshInterval = TimeSpan.FromHours(12);

    public SplashWindow(bool isFirstLaunch)
    {
        InitializeComponent();
        DataContext = _vm;
        _isFirstLaunch = isFirstLaunch;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ShouldRunBlockingScan())
            {
                await RunFirstLaunchScanAsync();
            }
            else
            {
                UpdateStatus("Cargando catálogo guardado...", 88);
                _ = RunDeferredRefreshScanAsync();
                await Task.Delay(60);
            }

            await EnsureRequiredModelsAsync(interactive: _isFirstLaunch);

            // Keep progress below 100 while MainWindow is being constructed (heavy init).
            // This avoids the perception of a frozen "100% listo" splash.
            UpdateStatus("Abriendo interfaz principal...", 99);
            await Task.Delay(90);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            UpdateStatus("ARES iniciado.", 100);
            await Task.Delay(120);

            OpenMainWindow();
        }
        catch (Exception ex)
        {
            App.WriteCrash("SplashWindow.OnLoaded", ex);
            UpdateStatus($"Error: {ex.Message}", 0);
            await Task.Delay(3000);
            try { OpenMainWindow(); }
            catch (Exception ex2)
            {
                App.WriteCrash("OpenMainWindow", ex2);
                AresMessageBox.Show(
                    $"Error crítico al iniciar ARES:\n\n{ex2.Message}\n\nRevisa los archivos crash_*.log en:\n{AppPaths.DataDirectory}",
                    "ARES — Error");
                Application.Current.Shutdown(1);
            }
        }
    }

    private async Task RunFirstLaunchScanAsync()
    {
        var scanner = new SystemScanner();
        int step = 0;
        // Keep splash below 100% until model checks are also complete.
        var steps = new[] { 10, 28, 44, 58, 70, 82, 92 };

        scanner.StatusChanged += msg =>
        {
            Dispatcher.Invoke(() =>
            {
                _vm.StatusText = msg;
                if (step < steps.Length)
                    _vm.Progress = steps[step++];
            });
        };

        var tools = await scanner.ScanAsync();
        SystemScanner.SaveToJson(tools, AppPaths.ToolsFile);

        UpdateStatus("Escaneo completado. Verificando modelos locales...", 94);
        await Task.Delay(180);
    }

    private async Task RunDeferredRefreshScanAsync()
    {
        try
        {
            var scanner = new SystemScanner();
            var tools = await scanner.ScanAsync();
            SystemScanner.SaveToJson(tools, AppPaths.ToolsFile);

            // Best effort: hot-reload scanned tools if the main registry is already initialized.
            await Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow.ToolRegistry != null)
                    MainWindow.ToolRegistry.LoadFromJson(AppPaths.ToolsFile);
            });
        }
        catch (Exception ex)
        {
            App.WriteCrash("SplashWindow.RunDeferredRefreshScanAsync", ex);
        }
    }

    private bool ShouldRunBlockingScan()
    {
        if (_isFirstLaunch)
            return true;

        if (!File.Exists(AppPaths.ToolsFile))
            return true;

        try
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(AppPaths.ToolsFile);
            return age > ScanRefreshInterval;
        }
        catch
        {
            return true;
        }
    }

    private void UpdateStatus(string text, int progress)
    {
        Dispatcher.Invoke(() =>
        {
            _vm.StatusText = text;
            _vm.Progress = progress;
        });
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
        Close();
    }

    private async Task EnsureRequiredModelsAsync(bool interactive)
    {
        var cfg = App.ConfigManager.Config;

        // If multi-model is off, no strict model bundle enforcement is needed.
        if (!cfg.MultiModelEnabled)
            return;

        UpdateStatus("Verificando modelos locales...", 96);

        var client = new OllamaClient();
        var ready = await client.IsAvailableAsync();
        if (!ready)
            ready = await client.TryStartAsync(12);

        if (!ready)
        {
            if (interactive)
            {
                AresMessageBox.Show(
                    "Ollama no está disponible al iniciar, no pude verificar modelos.\n\nAbre Ajustes > IA para instalar modelos requeridos.",
                    "ARES — Verificación de modelos");
            }
            return;
        }

        var installed = await client.GetInstalledModelsAsync();
        var missing = ModelRouter.GetMissingPreferredModels(cfg, installed);
        if (missing.Count == 0)
        {
            UpdateStatus("Modelos locales verificados.", 99);
            return;
        }

        if (!interactive)
        {
            UpdateStatus("Modelos de IA pendientes (revisar en Ajustes > IA)", 99);
            return;
        }

        var installNow = AresMessageBox.Show(
            "Faltan modelos requeridos para multimodelo:\n\n" +
            string.Join("\n", missing.Select(m => $"• {m}")) +
            "\n\n¿Quieres instalarlos ahora?",
            "ARES — Modelos faltantes",
            MessageBoxButton.YesNo);

        if (installNow != MessageBoxResult.Yes)
            return;

        foreach (var model in missing)
        {
            UpdateStatus($"Instalando {model}...", 98);
            var dl = new ModelDownloadWindow(client, model) { Owner = this };
            var ok = dl.ShowDialog() == true;
            if (!ok) break;
        }

        var after = await client.GetInstalledModelsAsync();
        var stillMissing = ModelRouter.GetMissingPreferredModels(cfg, after);
        if (stillMissing.Count > 0)
        {
            AresMessageBox.Show(
                "No se pudieron instalar todos los modelos:\n\n" +
                string.Join("\n", stillMissing.Select(m => $"• {m}")),
                "ARES — Modelos pendientes");
        }

        UpdateStatus(stillMissing.Count == 0
            ? "Modelos locales verificados."
            : "Verificación completada con modelos pendientes.", 99);
    }
}
