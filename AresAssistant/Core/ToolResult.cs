namespace AresAssistant.Core;

/// <summary>
/// Resultado de la ejecución de una herramienta.
/// <paramref name="Success"/> indica si se ejecutó correctamente;
/// <paramref name="Message"/> contiene el resultado o el mensaje de error.
/// </summary>
public record ToolResult(bool Success, string Message);
