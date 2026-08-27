using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>Activa (pone en primer plano) una ventana existente por título parcial.</summary>
public class FocusWindowTool : ITool
{
    public string Name => "focus_window";
    public string Description => "Activa y trae al frente una ventana por título parcial. Útil antes de escribir o hacer clic en una app concreta.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var win = WindowManager.Find(title);
        if (win == null)
            return Task.FromResult(new ToolResult(false, $"No se encontró ventana con título '{title}'."));

        var ok = WindowManager.FocusWindow(win.Hwnd);
        return Task.FromResult(new ToolResult(ok,
            ok ? $"Ventana '{win.Title}' activada." : $"No se pudo activar la ventana '{win.Title}'."));
    }
}

/// <summary>Devuelve información de la ventana actualmente en primer plano.</summary>
public class GetForegroundWindowTool : ITool
{
    public string Name => "get_foreground_window";
    public string Description => "Devuelve el título, proceso y posición de la ventana que tiene el foco ahora mismo.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var (title, process, _) = WindowManager.GetForegroundWindowInfo();
        if (string.IsNullOrEmpty(title))
            return Task.FromResult(new ToolResult(false, "No hay una ventana en primer plano (o es el escritorio)."));

        return Task.FromResult(new ToolResult(true, $"Ventana activa: '{title}' (proceso: {process})."));
    }
}

/// <summary>Mueve y redimensiona una ventana por título.</summary>
public class SetWindowPositionTool : ITool
{
    public string Name => "set_window_position";
    public string Description => "Mueve y/o redimensiona una ventana por título. Requiere title; x,y,width,height opcionales (los que no pases se mantienen).";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" },
            ["x"] = new() { Type = "integer", Description = "Posición X (esquina superior izquierda)" },
            ["y"] = new() { Type = "integer", Description = "Posición Y" },
            ["width"] = new() { Type = "integer", Description = "Ancho" },
            ["height"] = new() { Type = "integer", Description = "Alto" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var win = WindowManager.Find(title);
        if (win == null)
            return Task.FromResult(new ToolResult(false, $"No se encontró ventana con título '{title}'."));

        var x = args.TryGetValue("x", out var xJ) ? xJ?.Value<int>() ?? win.Bounds.X : win.Bounds.X;
        var y = args.TryGetValue("y", out var yJ) ? yJ?.Value<int>() ?? win.Bounds.Y : win.Bounds.Y;
        var w = args.TryGetValue("width", out var wJ) ? wJ?.Value<int>() ?? win.Bounds.Width : win.Bounds.Width;
        var h = args.TryGetValue("height", out var hJ) ? hJ?.Value<int>() ?? win.Bounds.Height : win.Bounds.Height;

        var ok = WindowManager.SetWindowPositionByTitle(title, x, y, w, h);
        return Task.FromResult(new ToolResult(ok,
            ok ? $"Ventana '{title}' reubicada a ({x},{y}) con {w}x{h}." : $"No se pudo mover '{title}'."));
    }
}

/// <summary>Mueve una ventana manteniendo su tamaño.</summary>
public class MoveWindowTool : ITool
{
    public string Name => "move_window";
    public string Description => "Mueve una ventana a una nueva posición sin cambiar su tamaño.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" },
            ["x"] = new() { Type = "integer", Description = "Posición X" },
            ["y"] = new() { Type = "integer", Description = "Posición Y" }
        },
        Required = new() { "title", "x", "y" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var x = args.TryGetValue("x", out var xJ) ? xJ?.Value<int>() ?? 0 : 0;
        var y = args.TryGetValue("y", out var yJ) ? yJ?.Value<int>() ?? 0 : 0;

        var ok = WindowManager.MoveWindowByTitle(title, x, y);
        return Task.FromResult(new ToolResult(ok,
            ok ? $"Ventana '{title}' movida a ({x},{y})." : $"No se pudo mover '{title}'."));
    }
}

/// <summary>Redimensiona una ventana manteniendo su posición.</summary>
public class ResizeWindowTool : ITool
{
    public string Name => "resize_window";
    public string Description => "Redimensiona una ventana sin cambiar su posición.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" },
            ["width"] = new() { Type = "integer", Description = "Nuevo ancho" },
            ["height"] = new() { Type = "integer", Description = "Nuevo alto" }
        },
        Required = new() { "title", "width", "height" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var w = args.TryGetValue("width", out var wJ) ? wJ?.Value<int>() ?? 0 : 0;
        var h = args.TryGetValue("height", out var hJ) ? hJ?.Value<int>() ?? 0 : 0;

        var ok = WindowManager.ResizeWindowByTitle(title, w, h);
        return Task.FromResult(new ToolResult(ok,
            ok ? $"Ventana '{title}' redimensionada a {w}x{h}." : $"No se pudo redimensionar '{title}'."));
    }
}

/// <summary>Cierra una ventana por título (WM_CLOSE). Protege procesos críticos.</summary>
public class CloseWindowTool : ITool
{
    public string Name => "close_window";
    public string Description => "Cierra una ventana de forma elegante buscándola por título. No cierra procesos críticos del sistema.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var (ok, msg) = WindowManager.CloseByTitle(title);
        return Task.FromResult(new ToolResult(ok, msg));
    }
}

/// <summary>Captura una ventana concreta a un PNG temporal.</summary>
public class ScreenshotWindowTool : ITool
{
    public string Name => "screenshot_window";
    public string Description => "Captura una ventana concreta (por título) y guarda el PNG en la carpeta temporal. Devuelve la ruta.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["title"] = new() { Type = "string", Description = "Título parcial o completo de la ventana" }
        },
        Required = new() { "title" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var title = args.TryGetValue("title", out var t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, "Debes indicar el título de la ventana."));

        var (ok, msg) = WindowManager.CaptureWindow(title, out var path);
        return Task.FromResult(new ToolResult(ok, msg));
    }
}

/// <summary>Lista todas las ventanas visibles con título, proceso y coordenadas.</summary>
public class ListWindowsDetailedTool : ITool
{
    public string Name => "list_windows";
    public string Description => "Lista las ventanas abiertas con su título, proceso y posición/tamaño. Útil para conocer coordenadas antes de hacer clic.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var windows = WindowManager.EnumerateWindows();
        if (windows.Count == 0)
            return Task.FromResult(new ToolResult(false, "No se encontraron ventanas abiertas."));

        var lines = windows
            .OrderBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(w => $"{w.ProcessName}: \"{w.Title}\" [{w.Bounds}]")
            .ToList();

        return Task.FromResult(new ToolResult(true, string.Join("\n", lines)));
    }
}

/// <summary>Información del escritorio virtual y escala DPI.</summary>
public class GetScreenInfoTool : ITool
{
    public string Name => "get_screen_info";
    public string Description => "Devuelve los límites del escritorio virtual (todos los monitores) y la escala DPI. Útil para calcular coordenadas de clic.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var vs = InputController.GetVirtualScreen();
        var dpi = GetDpiScale();
        var cursor = InputController.GetCursorPosition();

        var msg = $"Escritorio virtual: x={vs.X}, y={vs.Y}, ancho={vs.Width}, alto={vs.Height}.\n" +
                  $"DPI: {dpi:P0}.\n" +
                  $"Cursor actual: ({cursor.X}, {cursor.Y}).";
        return Task.FromResult(new ToolResult(true, msg));
    }

    private static double GetDpiScale()
    {
        try
        {
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX > 0 ? g.DpiX / 96.0 : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }
}
