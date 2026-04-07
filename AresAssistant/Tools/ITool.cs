using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Interfaz que deben implementar todas las herramientas de ARES.
/// Define nombre, descripción, parámetros y método de ejecución.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolParameterSchema Parameters { get; }
    Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args);
}
