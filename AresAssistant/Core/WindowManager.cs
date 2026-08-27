using System.Runtime.InteropServices;
using System.Text;

namespace AresAssistant.Core;

/// <summary>
/// Gestor de ventanas Win32: enumeración de ventanas visibles, foco, posición,
/// tamaño, cierre elegante (WM_CLOSE) y captura de una ventana concreta.
/// </summary>
public static class WindowManager
{
    // ═══════════════════ Tipos públicos ═══════════════════

    public sealed record WindowInfo(IntPtr Hwnd, string Title, string ProcessName, Rect Bounds, bool Visible);

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Rect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public Rect(int left, int top, int right, int bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public int X => Left;
        public int Y => Top;

        public override string ToString() => $"x={Left},y={Top},w={Width},h={Height}";
    }

    // ═══════════════════ Enumeración ═══════════════════

    /// <summary>
    /// Lista ventanas de nivel superior visibles con título no vacío.
    /// Si se indica un fragmento, filtra por coincidencia parcial de título.
    /// </summary>
    public static List<WindowInfo> EnumerateWindows(string? titleFragment = null)
    {
        var result = new List<WindowInfo>();
        var callback = new EnumWindowsProc((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindowTextLength(hwnd) == 0) return true;

            var title = GetWindowTitle(hwnd);
            if (titleFragment != null
                && !title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
                return true;

            GetWindowRect(hwnd, out var rect);
            var processName = GetProcessName(hwnd);
            result.Add(new WindowInfo(hwnd, title, processName, rect, true));
            return true;
        });

        // El delegado permanece enraizado durante toda la enumeración.
        EnumWindows(callback, IntPtr.Zero);
        return result;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    // ═══════════════════ Foco ═══════════════════

    /// <summary>Obtiene el título de la ventana que tiene el foco.</summary>
    public static (string Title, string ProcessName, IntPtr Hwnd) GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (string.Empty, string.Empty, IntPtr.Zero);

        GetWindowThreadProcessId(hwnd, out var pid);
        var processName = string.Empty;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            processName = p.ProcessName;
        }
        catch { /* process may have exited */ }

        return (GetWindowTitle(hwnd), processName, hwnd);
    }

    /// <summary>
    /// Activa y trae al frente una ventana de forma fiable.
    /// Restaura si está minimizada y usa el "truco" AttachThreadInput
    /// para que SetForegroundWindow funcione desde un proceso en segundo plano.
    /// </summary>
    public static bool FocusWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        var foreground = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foreground != IntPtr.Zero
            ? GetWindowThreadProcessId(foreground, out _)
            : 0u;

        // El foco real exige permiso de la ventana en primer plano en Windows.
        var attached = false;
        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            attached = AttachThreadInput(foregroundThread, currentThread, true);
        }

        try
        {
            BringWindowToTop(hwnd);
            ShowWindow(hwnd, SW_SHOW);
            return SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
                AttachThreadInput(foregroundThread, currentThread, false);
        }
    }

    /// <summary>Busca la primera ventana cuyo título contenga el fragmento (o el proceso coincida).</summary>
    public static WindowInfo? Find(string titleFragment, string? processName = null)
    {
        // Si no hay criterio, no devolvemos una ventana arbitraria.
        if (string.IsNullOrWhiteSpace(titleFragment) && string.IsNullOrWhiteSpace(processName))
            return null;

        var windows = EnumerateWindows();
        return windows.FirstOrDefault(w =>
            (!string.IsNullOrWhiteSpace(titleFragment)
             && w.Title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(processName)
                && w.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)));
    }

    // ═══════════════════ Posición / tamaño ═══════════════════

    /// <summary>Mueve una ventana a una posición absoluta de pantalla virtual.</summary>
    public static bool MoveWindowByTitle(string title, int x, int y, int? width = null, int? height = null)
    {
        var win = Find(title);
        if (win == null) return false;

        var w = width ?? win.Bounds.Width;
        var h = height ?? win.Bounds.Height;
        return MoveWindow(win.Hwnd, x, y, w, h, true);
    }

    /// <summary>Redimensiona una ventana manteniendo su posición actual.</summary>
    public static bool ResizeWindowByTitle(string title, int width, int height)
    {
        var win = Find(title);
        if (win == null) return false;
        return MoveWindow(win.Hwnd, win.Bounds.X, win.Bounds.Y, width, height, true);
    }

    /// <summary>Mueve y redimensiona en una sola llamada.</summary>
    public static bool SetWindowPositionByTitle(string title, int x, int y, int width, int height)
    {
        var win = Find(title);
        if (win == null) return false;
        return MoveWindow(win.Hwnd, x, y, width, height, true);
    }

    /// <summary>Obtiene las coordenadas de una ventana por título.</summary>
    public static Rect? GetRectByTitle(string title)
    {
        var win = Find(title);
        return win?.Bounds;
    }

    // ═══════════════════ Cerrar ═══════════════════

    /// <summary>
    /// Cierra una ventana de forma elegante (WM_CLOSE), permitiendo guardado si la app lo pide.
    /// Los procesos críticos del sistema están excluidos.
    /// </summary>
    public static (bool Success, string Message) CloseByTitle(string title)
    {
        var win = Find(title);
        if (win == null)
            return (false, $"No se encontró una ventana con título '{title}'.");

        var processLower = win.ProcessName.ToLowerInvariant();
        var critical = KnownCriticalProcesses.Contains(processLower);
        if (critical)
            return (false, $"Rechazado: '{win.ProcessName}' es un proceso crítico del sistema.");

        SendMessage(win.Hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        return (true, $"Solicitud de cierre enviada a '{win.Title}' ({win.ProcessName}).");
    }

    private static readonly HashSet<string> KnownCriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "csrss", "winlogon", "services", "lsass", "smss",
        "dwm", "taskhostw", "sihost", "wininit", "system"
    };

    // ═══════════════════ Captura de ventana ═══════════════════

    /// <summary>
    /// Captura una ventana concreta a un PNG en la carpeta temporal.
    /// Usa PrintWindow(PW_RENDERFULLCONTENT) para incluir contenido acelerado por GPU.
    /// Devuelve la ruta del archivo.
    /// </summary>
    public static (bool Success, string Message) CaptureWindow(string title, out string path)
    {
        path = string.Empty;
        var win = Find(title);
        if (win == null)
            return (false, $"No se encontró una ventana con título '{title}'.");

        var rect = win.Bounds;
        if (rect.Width <= 0 || rect.Height <= 0)
            return (false, "La ventana tiene dimensiones inválidas.");

        try
        {
            using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                var hdc = g.GetHdc();
                try
                {
                    // 2 = PW_RENDERFULLCONTENT (incluye apps aceleradas por hardware)
                    PrintWindow(win.Hwnd, hdc, 2);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }

            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"ares_win_{DateTime.Now:yyyyMMddHHmmss}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return (true, $"Captura de '{win.Title}' guardada en: {path}");
        }
        catch (Exception ex)
        {
            return (false, $"Error al capturar la ventana: {ex.Message}");
        }
    }

    // ═══════════════════ P/Invoke Win32 ═══════════════════

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const uint WM_CLOSE = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
}
