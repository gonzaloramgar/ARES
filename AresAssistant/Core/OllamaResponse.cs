using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Core;

/// <summary>
/// Respuesta del servidor Ollama a una petición de chat.
/// Contiene el mensaje del asistente, indicador de finalización y posibles errores.
/// </summary>
public class OllamaResponse
{
    [JsonProperty("message")]
    public OllamaMessage Message { get; set; } = new("assistant", "");

    [JsonProperty("done")]
    public bool Done { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Representa una llamada a herramienta solicitada por el modelo.
/// Incluye un ID opcional y la función con nombre y argumentos.
/// </summary>
public class OllamaToolCall
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("function")]
    public OllamaToolCallFunction Function { get; set; } = new();
}

/// <summary>
/// Detalle de la función a ejecutar dentro de un tool call:
/// nombre de la herramienta y sus argumentos como diccionario de JToken.
/// </summary>
public class OllamaToolCallFunction
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("arguments")]
    [JsonConverter(typeof(ToolArgumentsConverter))]
    public Dictionary<string, JToken> Arguments { get; set; } = new();
}

/// <summary>
/// Ollama sometimes returns arguments as a JSON string instead of a JSON object.
/// This converter handles both cases.
/// </summary>
public class ToolArgumentsConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
        => objectType == typeof(Dictionary<string, JToken>);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        if (token.Type == JTokenType.Null)
            return new Dictionary<string, JToken>();

        // Arguments returned as a JSON string — parse it
        if (token.Type == JTokenType.String)
        {
            var str = token.Value<string>() ?? "{}";
            try
            {
                var parsed = JObject.Parse(str);
                return parsed.ToObject<Dictionary<string, JToken>>() ?? new();
            }
            catch
            {
                return new Dictionary<string, JToken>();
            }
        }

        // Arguments returned as a JSON object (normal path)
        if (token.Type == JTokenType.Object)
            return ((JObject)token).ToObject<Dictionary<string, JToken>>() ?? new();

        return new Dictionary<string, JToken>();
    }

    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => serializer.Serialize(writer, value);
}
