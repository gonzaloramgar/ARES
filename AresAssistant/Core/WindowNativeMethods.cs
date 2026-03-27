using System.Runtime.InteropServices;

namespace AresAssistant.Core;

/// <summary>
/// Métodos nativos de Win32 para el control de ventanas del sistema.
/// Utilizado por las herramientas de minimizar/maximizar/restaurar ventanas.
/// </summary>
public static class WindowNativeMethods
{
    /// <summary>Minimizar ventana.</summary>
    public const int SW_MINIMIZE = 6;
    /// <summary>Maximizar ventana.</summary>
    public const int SW_MAXIMIZE = 3;
    /// <summary>Restaurar ventana a su tamaño normal.</summary>
    public const int SW_RESTORE = 9;
    /// <summary>Mostrar ventana.</summary>
    public const int SW_SHOW = 5;
    /// <summary>Ocultar ventana.</summary>
    public const int SW_HIDE = 0;

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
