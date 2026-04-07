using Newtonsoft.Json;

namespace AresAssistant.Core;

/// <summary>
/// Definición de una herramienta para el modelo Ollama.
/// Sigue el esquema "function calling" del protocolo de Ollama.
/// </summary>
public class ToolDefinition
{
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    [JsonProperty("function")]
    public ToolFunction Function { get; set; } = new();
}

/// <summary>
/// Describe la función de la herramienta: nombre, descripción y parámetros esperados.
/// </summary>
public class ToolFunction
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("parameters")]
    public ToolParameterSchema Parameters { get; set; } = new();
}

/// <summary>
/// Esquema de los parámetros que acepta una herramienta (tipo JSON Schema).
/// </summary>
public class ToolParameterSchema
{
    [JsonProperty("type")]
    public string Type { get; set; } = "object";

    [JsonProperty("properties")]
    public Dictionary<string, ToolParameterProperty> Properties { get; set; } = new();

    [JsonProperty("required")]
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// Propiedad individual dentro del esquema de parámetros de una herramienta.
/// </summary>
public class ToolParameterProperty
{
    [JsonProperty("type")]
    public string Type { get; set; } = "string";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("default", NullValueHandling = NullValueHandling.Ignore)]
    public object? Default { get; set; }
}
