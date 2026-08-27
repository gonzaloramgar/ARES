using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>Lista los controles reales (UI Automation) de la ventana en primer plano o de una concreta.</summary>
public class ListUiaElementsTool : ITool
{
    public string Name => "list_uia_elements";
    public string Description => "Lista los controles accesibles (botones, campos de texto, menús) de la ventana activa o de una ventana concreta, con su nombre, tipo y coordenadas. Más preciso que la visión por píxeles.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["window"] = new() { Type = "string", Description = "Título de la ventana a inspeccionar (opcional; por defecto la activa)" }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var window = args.TryGetValue("window", out var w) ? w?.ToString() ?? "" : "";
        var root = !string.IsNullOrWhiteSpace(window)
            ? UiAutomationProvider.GetWindowRoot(window)
            : UiAutomationProvider.GetForegroundRoot();

        if (root == null)
            return Task.FromResult(new ToolResult(false, "No se pudo acceder al árbol de la ventana."));

        var elements = UiAutomationProvider.Enumerate(root);
        if (elements.Count == 0)
            return Task.FromResult(new ToolResult(false, "No se encontraron controles accesibles en esa ventana."));

        var lines = elements
            .OrderBy(e => e.Y).ThenBy(e => e.X)
            .Select(e => $"[{e.ControlType}] '{e.Name}' (id: '{e.AutomationId}', enabled: {e.IsEnabled}) @ ({e.X},{e.Y}) {e.Width}x{e.Height}")
            .ToList();

        return Task.FromResult(new ToolResult(true, string.Join("\n", lines)));
    }
}

/// <summary>Activa un control por nombre (botón, checkbox, menú...) en la ventana activa o una concreta.</summary>
public class ClickUiaElementTool : ITool
{
    public string Name => "click_uia_element";
    public string Description => "Activa un control por su nombre visible (ej: 'Guardar', 'Aceptar', 'Enviar') en la ventana activa o una concreta. Usa UI Automation: invoca o hace clic en el centro del control.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["name"] = new() { Type = "string", Description = "Nombre visible del control (puede ser parcial)" },
            ["window"] = new() { Type = "string", Description = "Título de la ventana (opcional; por defecto la activa)" }
        },
        Required = new() { "name" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var name = args.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(new ToolResult(false, "Debes indicar el nombre del control."));

        var window = args.TryGetValue("window", out var w) ? w?.ToString() ?? "" : "";

        // Si indican ventana, la enfocamos primero para que el clic aterrice ahí.
        if (!string.IsNullOrWhiteSpace(window))
        {
            var win = WindowManager.Find(window);
            if (win == null)
                return Task.FromResult(new ToolResult(false, $"No se encontró la ventana '{window}'."));
            WindowManager.FocusWindow(win.Hwnd);
            InputController.HumanPause(120);
        }

        var root = !string.IsNullOrWhiteSpace(window)
            ? UiAutomationProvider.GetWindowRoot(window)
            : UiAutomationProvider.GetForegroundRoot();

        if (root == null)
            return Task.FromResult(new ToolResult(false, "No se pudo acceder al árbol de la ventana."));

        var el = UiAutomationProvider.FindByName(root, name);
        if (el == null)
            return Task.FromResult(new ToolResult(false, $"No se encontró un control llamado '{name}'."));

        var (ok, msg) = UiAutomationProvider.Invoke(el);
        return Task.FromResult(new ToolResult(ok, msg));
    }
}
