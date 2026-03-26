using AresAssistant.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace AresAssistant.Tools;

public class LocationTool : ITool
{
    public string Name => "get_location";
    public string Description => "Obtiene la ubicación aproximada del usuario mediante su IP pública (ciudad, región, país, coordenadas). No requiere GPS.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        try
        {
            // ip-api.com — free, no key needed, returns JSON with location data
            var json = await Http.GetStringAsync("http://ip-api.com/json/?fields=status,message,country,regionName,city,lat,lon,timezone,query&lang=es");
            var data = JObject.Parse(json);

            if (data["status"]?.ToString() != "success")
                return new ToolResult(false, $"No se pudo obtener la ubicación: {data["message"]}");

            var result = new
            {
                ciudad = data["city"]?.ToString(),
                region = data["regionName"]?.ToString(),
                pais = data["country"]?.ToString(),
                latitud = data["lat"]?.ToObject<double>(),
                longitud = data["lon"]?.ToObject<double>(),
                zona_horaria = data["timezone"]?.ToString(),
                ip_publica = data["query"]?.ToString()
            };

            return new ToolResult(true, JsonConvert.SerializeObject(result, Formatting.Indented));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Error al obtener ubicación: {ex.Message}");
        }
    }
}
