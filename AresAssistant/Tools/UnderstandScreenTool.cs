using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AresAssistant.Config;
using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>
/// Captura la pantalla (o una ventana) y pide al modelo multimodal local un JSON con los
/// elementos visibles y sus coordenadas REALES de pantalla. Es el "ojo" del bucle computer-use:
/// el agente obtiene dónde están los botones/campos y puede hacer clic en esas coordenadas
/// sin que el usuario tenga que dárselas.
/// </summary>
public class UnderstandScreenTool(OllamaClient client, ConfigManager configManager) : ITool
{
    private const int TimeoutSeconds = 16;
    private const int MaxSide = 2000; // lado máximo de la imagen enviada al modelo

    public string Name => "understand_screen";
    public string Description => "Captura la pantalla (o una ventana) y devuelve un JSON con los elementos visibles y sus coordenadas REALES de pantalla (botones, campos, enlaces). Ideal para saber dónde hacer clic sin que el usuario dé posiciones.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["question"] = new() { Type = "string", Description = "Qué quieres localizar (opcional). Ej: 'el botón Guardar'" },
            ["window"] = new() { Type = "string", Description = "Título de la ventana a capturar (opcional; por defecto toda la pantalla)" }
        },
        Required = new()
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var question = args.TryGetValue("question", out var q) && !string.IsNullOrWhiteSpace(q?.ToString())
            ? q!.ToString()
            : "Lista los elementos interactivos visibles (botones, campos, enlaces, áreas clicables).";
        var window = args.TryGetValue("window", out var wj) ? wj?.ToString() ?? "" : "";
        var cfg = configManager.Config;

        // (bitmap, anchoReal, altoReal, escalaUsada)
        var capture = !string.IsNullOrWhiteSpace(window) ? CaptureWindow(window) : CaptureFullScreen();
        if (capture == null)
            return new ToolResult(false, "No se pudo capturar la pantalla/ventana.");

        var (captured, origW, origH, scale) = capture.Value;

        var imagesBase64 = new List<string> { EncodeJpegBase64(captured, MaxSide, 88L, scale) };
        // Recortes para mejorar OCR; sus coordenadas NO se usan para el mapeo final.
        AddCropImages(imagesBase64, captured, scale);
        captured.Dispose();

        var installed = await client.GetInstalledModelsAsync().ConfigureAwait(false);
        var candidates = BuildVisionCandidates(cfg, installed);
        if (candidates.Count == 0)
            return new ToolResult(false, "No hay modelo multimodal instalado. Instala uno (ej. qwen2.5-vl:7b o llava:7b) para usar understand_screen.");

        var prompt = BuildPrompt(question, origW, origH, scale);

        foreach (var model in candidates)
        {
            try
            {
                var messages = new List<OllamaMessage>
                {
                    new("system",
                        "Eres un analista visual local. Respondes SOLO en español de España. Devuelves EXCLUSIVAMENTE JSON válido (sin texto adicional) con este esquema: {\"elements\":[{\"label\":\"...\",\"type\":\"button|field|link|menu|other\",\"text\":\"...\",\"x\":0,\"y\":0,\"w\":0,\"h\":0}],\"summary\":\"...\"}."),
                    new("user", prompt) { Images = imagesBase64 }
                };

                var resp = await ChatWithTimeoutAsync(messages, model, TimeoutSeconds).ConfigureAwait(false);
                if (resp == null || !string.IsNullOrWhiteSpace(resp.Error)) continue;

                var text = Sanitize(resp.Message?.Content);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var (jsonValid, jsonStr, parsedElements) = ExtractJson(text);
                if (jsonValid)
                {
                    // Mapear de la imagen escalada a la pantalla real.
                    var real = RescaleElements(parsedElements, scale, origW, origH);
                    var readable = BuildReadable(real, question, model);
                    var compact = CompactJson(BuildJson(real));
                    return new ToolResult(true, $"[modelo: {model}]\n{readable}\n---\nJSON: {compact}");
                }

                return new ToolResult(true, $"[modelo: {model} — sin JSON]\n{text}");
            }
            catch { /* prueba siguiente modelo */ }
        }

        return new ToolResult(false, "No se pudo analizar la imagen con los modelos locales disponibles.");
    }

    // ═══════════════════ Captura ═══════════════════

    private static (Bitmap Bmp, int OrigW, int OrigH, double Scale)? CaptureFullScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return (bmp, bounds.Width, bounds.Height, ComputeScale(bounds.Width, bounds.Height));
    }

    private static (Bitmap Bmp, int OrigW, int OrigH, double Scale)? CaptureWindow(string title)
    {
        var (ok, _) = WindowManager.CaptureWindow(title, out var path);
        if (!ok || string.IsNullOrEmpty(path)) return null;

        Bitmap bmp;
        using (var loaded = Image.FromFile(path))
        {
            bmp = new Bitmap(loaded);
        }
        try { System.IO.File.Delete(path); } catch { }

        var w = bmp.Width;
        var h = bmp.Height;
        return (bmp, w, h, ComputeScale(w, h));
    }

    private static double ComputeScale(int w, int h)
        => Math.Min(1.0, MaxSide / (double)Math.Max(1, Math.Max(w, h)));

    // ═══════════════════ Imágenes ═══════════════════

    private static void AddCropImages(List<string> images, Bitmap bmp, double scale)
    {
        var w = bmp.Width;
        var h = bmp.Height;
        var crop1 = new Rectangle(0, 0, Math.Max(1, (int)(w * 0.65)), Math.Max(1, (int)(h * 0.65)));
        var crop2 = new Rectangle(Math.Max(0, (int)(w * 0.35)), 0, Math.Max(1, (int)(w * 0.65)), Math.Max(1, (int)(h * 0.65)));

        using (var b1 = bmp.Clone(crop1, bmp.PixelFormat)) images.Add(EncodeJpegBase64(b1, 1600, 86L, scale));
        using (var b2 = bmp.Clone(crop2, bmp.PixelFormat)) images.Add(EncodeJpegBase64(b2, 1600, 86L, scale));
    }

    private static string EncodeJpegBase64(Bitmap bmp, int maxSide, long quality, double scale)
    {
        var targetW = Math.Max(1, (int)Math.Round(bmp.Width * scale));
        var targetH = Math.Max(1, (int)Math.Round(bmp.Height * scale));

        using var resized = new Bitmap(targetW, targetH);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(bmp, 0, 0, targetW, targetH);
        }

        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);
        if (encoder == null)
        {
            resized.Save(ms, ImageFormat.Jpeg);
        }
        else
        {
            using var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            resized.Save(ms, encoder, encParams);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    // ═══════════════════ Modelos ═══════════════════

    private static List<string> BuildVisionCandidates(AppConfig cfg, List<string> installed)
    {
        var candidates = new List<string>();

        void Add(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return;
            if (!candidates.Contains(model, StringComparer.OrdinalIgnoreCase)) candidates.Add(model.Trim());
        }

        Add(cfg.MultiModelVisionModel);
        foreach (var m in (cfg.MultiModelFallbacks ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Add(m);
        Add(cfg.OllamaModel);

        if (installed.Count > 0)
        {
            candidates = candidates.Where(c => installed.Any(i => i.Equals(c, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var model in installed.Where(ModelRouter.IsLikelyVisionModel)) Add(model);
            candidates = candidates.Where(c => installed.Any(i => i.Equals(c, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        return candidates.Where(ModelRouter.IsLikelyVisionModel).Take(2).ToList();
    }

    private static string BuildPrompt(string question, int origW, int origH, double scale)
    {
        return
            $"Pregunta del usuario: {question}\n\n" +
            $"La imagen es de {origW}x{origH} píxeles (puede estar reescalada). Las coordenadas x,y que devuelvas deben ser RELATIVAS a la imagen ENVIADA.\n" +
            "Identifica los elementos interactivos visibles y su rectángulo: label (nombre corto), type (button/field/link/menu/other), text (texto visible), " +
            "y x,y (esquina superior izquierda) con w,h (ancho/alto).\n" +
            "Limita a 15 elementos. Devuelve SOLO el JSON, nada más.";
    }

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var t = text.Replace("\r\n", "\n");
        t = Regex.Replace(t, @"[^\u0009\u000A\u000D\u0020-\u007E\u00A1-\u00FF]", "");
        return t.Trim();
    }

    // ═══════════════════ Extracción y mapeo ═══════════════════

    private static (bool Valid, string Json, List<JObject> Elements) ExtractJson(string text)
    {
        var elements = new List<JObject>();
        var candidate = ExtractJsonObject(text);
        if (candidate == null) return (false, "", elements);

        try
        {
            var obj = JObject.Parse(candidate);
            if (obj["elements"] is JArray arr)
            {
                foreach (var item in arr.OfType<JObject>())
                    elements.Add(item);
            }
            return (true, obj.ToString(Newtonsoft.Json.Formatting.None), elements);
        }
        catch
        {
            var arrayMatch = Regex.Match(text, @"\[\s*\{.*\}\s*\]", RegexOptions.Singleline);
            if (arrayMatch.Success)
            {
                try
                {
                    var arr = JArray.Parse(arrayMatch.Value);
                    foreach (var item in arr.OfType<JObject>()) elements.Add(item);
                    return (true, arrayMatch.Value, elements);
                }
                catch { }
            }
            return (false, "", elements);
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var braces = 0;
        var brackets = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) { escape = false; }
                else if (ch == '\\') { escape = true; }
                else if (ch == '"') { inString = false; }
                continue;
            }

            if (ch == '"') { inString = true; }
            else if (ch == '{') { braces++; }
            else if (ch == '}') { braces--; }
            else if (ch == '[') { brackets++; }
            else if (ch == ']') { brackets--; }

            if (braces == 0 && brackets == 0 && i > start)
                return text[start..(i + 1)];
        }

        return braces == 0 && brackets == 0 ? text[start..] : null;
    }

    /// <summary>Reescala coordenadas de la imagen enviada a la pantalla real, acotando al área útil.</summary>
    private static List<JObject> RescaleElements(List<JObject> elements, double scale, int origW, int origH)
    {
        if (scale <= 0 || scale >= 1.0) return elements;
        var inv = 1.0 / scale;

        foreach (var el in elements)
        {
            if (el["x"] is JToken xj && xj.Type != JTokenType.Null)
                el["x"] = Math.Clamp((int)Math.Round(xj.Value<int>() * inv), 0, origW);
            if (el["y"] is JToken yj && yj.Type != JTokenType.Null)
                el["y"] = Math.Clamp((int)Math.Round(yj.Value<int>() * inv), 0, origH);
            if (el["w"] is JToken wj && wj.Type != JTokenType.Null)
                el["w"] = Math.Clamp((int)Math.Round(wj.Value<int>() * inv), 0, origW);
            if (el["h"] is JToken hj && hj.Type != JTokenType.Null)
                el["h"] = Math.Clamp((int)Math.Round(hj.Value<int>() * inv), 0, origH);
        }

        return elements;
    }

    private static string BuildJson(List<JObject> elements)
    {
        var obj = new JObject
        {
            ["elements"] = new JArray(elements)
        };
        return obj.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string CompactJson(string json)
    {
        var s = json.Length > 2500 ? json[..2500] + "…" : json;
        return s;
    }

    private static string BuildReadable(List<JObject> elements, string question, string model)
    {
        if (elements.Count == 0)
            return "No se identificaron elementos claramente.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Elementos ({elements.Count}) — pregunta: '{question}':");
        for (var i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            var label = el["label"]?.ToString() ?? "";
            var type = el["type"]?.ToString() ?? "?";
            var text = el["text"]?.ToString() ?? "";
            var x = el["x"]?.Value<int>() ?? 0;
            var y = el["y"]?.Value<int>() ?? 0;
            var w = el["w"]?.Value<int>() ?? 0;
            var h = el["h"]?.Value<int>() ?? 0;
            sb.AppendLine($"  {i + 1}. [{type}] '{label}' ({text}) @ ({x},{y}) {w}x{h}");
        }
        return sb.ToString().TrimEnd();
    }

    private async Task<OllamaResponse?> ChatWithTimeoutAsync(List<OllamaMessage> messages, string model, int timeoutSeconds)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return await client.ChatAsync(messages, new List<ToolDefinition>(), model, keepAlive: "5m", cancellationToken: cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
