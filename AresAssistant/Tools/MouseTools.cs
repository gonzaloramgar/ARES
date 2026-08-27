using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>Mueve el cursor a coordenadas absolutas o relativas.</summary>
public class MouseMoveTool : ITool
{
    public string Name => "mouse_move";
    public string Description => "Mueve el cursor del ratón. Usa x,y (absolutas) o dx,dy (relativas a la posición actual).";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["x"] = new() { Type = "integer", Description = "Coordenada X absoluta de pantalla (con dx,dy es relativo)" },
            ["y"] = new() { Type = "integer", Description = "Coordenada Y absoluta de pantalla (con dx,dy es relativo)" },
            ["dx"] = new() { Type = "integer", Description = "Desplazamiento horizontal relativo" },
            ["dy"] = new() { Type = "integer", Description = "Desplazamiento vertical relativo" }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var hasDx = args.TryGetValue("dx", out var dxJ);
            var hasDy = args.TryGetValue("dy", out var dyJ);
            if (hasDx || hasDy)
            {
                var dx = hasDx ? dxJ?.Value<int>() ?? 0 : 0;
                var dy = hasDy ? dyJ?.Value<int>() ?? 0 : 0;
                InputController.MoveMouseBy(dx, dy);
                return Task.FromResult(new ToolResult(true, $"Cursor movido +{dx}, +{dy}."));
            }

            var x = args.TryGetValue("x", out var xJ) ? xJ?.Value<int>() ?? 0 : 0;
            var y = args.TryGetValue("y", out var yJ) ? yJ?.Value<int>() ?? 0 : 0;
            InputController.MoveMouseTo(x, y);
            return Task.FromResult(new ToolResult(true, $"Cursor movido a ({x}, {y})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al mover el ratón: {ex.Message}"));
        }
    }
}

/// <summary>Hace clic (izquierdo/derecho/central), opcionalmente mover antes.</summary>
public class MouseClickTool : ITool
{
    public string Name => "mouse_click";
    public string Description => "Hace clic con el ratón. button: left|right|middle. Si pasas x,y, primero mueve el cursor ahí.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["button"] = new() { Type = "string", Description = "Botón: left, right, middle (por defecto left)", Default = "left" },
            ["x"] = new() { Type = "integer", Description = "Coordenada X opcional (mueve antes de clicar)" },
            ["y"] = new() { Type = "integer", Description = "Coordenada Y opcional (mueve antes de clicar)" }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var button = args.TryGetValue("button", out var b) ? b?.ToString() ?? "left" : "left";
            if (args.TryGetValue("x", out var xJ) && args.TryGetValue("y", out var yJ))
            {
                var x = xJ?.Value<int>() ?? 0;
                var y = yJ?.Value<int>() ?? 0;
                InputController.MoveMouseTo(x, y);
                InputController.HumanPause();
            }
            InputController.ClickMouseButton(button);
            return Task.FromResult(new ToolResult(true, $"Clic de botón '{button}' realizado."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al hacer clic: {ex.Message}"));
        }
    }
}

/// <summary>Doble clic en la posición actual o en unas coordenadas.</summary>
public class MouseDoubleClickTool : ITool
{
    public string Name => "mouse_double_click";
    public string Description => "Doble clic con el botón indicado (por defecto izquierdo). Opcional x,y para mover antes.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["button"] = new() { Type = "string", Description = "Botón: left, right, middle (por defecto left)", Default = "left" },
            ["x"] = new() { Type = "integer", Description = "Coordenada X opcional" },
            ["y"] = new() { Type = "integer", Description = "Coordenada Y opcional" }
        },
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var button = args.TryGetValue("button", out var b) ? b?.ToString() ?? "left" : "left";
            if (args.TryGetValue("x", out var xJ) && args.TryGetValue("y", out var yJ))
            {
                InputController.MoveMouseTo(xJ?.Value<int>() ?? 0, yJ?.Value<int>() ?? 0);
                InputController.HumanPause();
            }
            InputController.DoubleClickMouseButton(button);
            return Task.FromResult(new ToolResult(true, $"Doble clic de '{button}' realizado."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al hacer doble clic: {ex.Message}"));
        }
    }
}

/// <summary>Arrastra el ratón desde un punto a otro manteniendo el botón pulsado.</summary>
public class MouseDragTool : ITool
{
    public string Name => "mouse_drag";
    public string Description => "Arrastra desde (from_x,from_y) hasta (to_x,to_y) manteniendo el botón pulsado (por defecto left).";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["from_x"] = new() { Type = "integer", Description = "X de inicio" },
            ["from_y"] = new() { Type = "integer", Description = "Y de inicio" },
            ["to_x"] = new() { Type = "integer", Description = "X de destino" },
            ["to_y"] = new() { Type = "integer", Description = "Y de destino" },
            ["button"] = new() { Type = "string", Description = "Botón: left, right, middle (por defecto left)", Default = "left" }
        },
        Required = new() { "from_x", "from_y", "to_x", "to_y" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var fromX = args["from_x"].Value<int>();
            var fromY = args["from_y"].Value<int>();
            var toX = args["to_x"].Value<int>();
            var toY = args["to_y"].Value<int>();
            var button = args.TryGetValue("button", out var b) ? b?.ToString() ?? "left" : "left";

            InputController.Drag(fromX, fromY, toX, toY, button);
            return Task.FromResult(new ToolResult(true, $"Arrastre realizado de ({fromX},{fromY}) a ({toX},{toY})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al arrastrar: {ex.Message}"));
        }
    }
}

/// <summary>Gira la rueda del ratón (scroll). delta positivo = arriba, negativo = abajo.</summary>
public class MouseScrollTool : ITool
{
    public string Name => "mouse_scroll";
    public string Description => "Gira la rueda del ratón. delta positivo sube, negativo baja (ej. -120 = bajar un paso).";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["delta"] = new() { Type = "integer", Description = "Cantidad de scroll (positivo arriba, negativo abajo)" }
        },
        Required = new() { "delta" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            var delta = args["delta"].Value<int>();
            InputController.ScrollWheel(delta);
            return Task.FromResult(new ToolResult(true, $"Scroll {delta} unidades."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"Error al hacer scroll: {ex.Message}"));
        }
    }
}
