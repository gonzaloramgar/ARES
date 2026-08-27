using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Pulsa una tecla con modificadores opcionales (Ctrl+S, Alt+Tab, Ctrl+Shift+Esc...).
/// </summary>
public class PressKeyTool : ITool
{
    public string Name => "press_key";
    public string Description => "Pulsa una tecla, con modificadores opcionales. Ejemplos: key='s', modifiers=['ctrl'] → Ctrl+S; key='tab', modifiers=['alt','shift'] → Alt+Shift+Tab. Puedes repetir (times).";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["key"] = new() { Type = "string", Description = "Tecla: letra, número, enter, tab, esc, backspace, delete, arrows, f1-f12, space, etc." },
            ["modifiers"] = new() { Type = "array", Description = "Modificadores: ctrl, alt, shift, win (lista)" },
            ["times"] = new() { Type = "integer", Description = "Repeticiones (por defecto 1)", Default = 1 }
        },
        Required = new() { "key" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var key = args.TryGetValue("key", out var k) ? k?.ToString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(key))
                return Task.FromResult(new ToolResult(false, "Debes indicar la tecla a pulsar."));

            var modifiers = new List<string>();
            if (args.TryGetValue("modifiers", out var mods) && mods is JArray arr)
            {
                foreach (var m in arr)
                {
                    if (m?.ToString() is { Length: > 0 } s)
                        modifiers.Add(s);
                }
            }

            var times = args.TryGetValue("times", out var t) ? t?.Value<int>() ?? 1 : 1;

            InputController.PressKey(key, modifiers, times);
            return Task.FromResult(new ToolResult(true,
                modifiers.Count > 0
                    ? $"Pulsada combinación: {string.Join("+", modifiers)}+{key}."
                    : $"Pulsada tecla: {key}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, ex.Message));
        }
    }
}
