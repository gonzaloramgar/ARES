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
/// Herramientas de "ratón que entiende la pantalla": localizan un elemento por su TEXTO
/// (primero UI Automation, si no por visión) y actúan, sin que el usuario dé coordenadas.
/// Es lo que permite navegar (p. ej. en Brave) solo con ratón.
/// </summary>
public class ClickScreenElementTool(OllamaClient client, ConfigManager configManager) : ITool
{
    public string Name => "click_screen_element";
    public string Description => "Encuentra en pantalla un elemento por su texto o etiqueta (ej: 'Guardar', 'Aceptar', la barra de dirección) y hace clic en él. Primero usa UI Automation y, si no, lo localiza por visión. No necesitas dar coordenadas.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["label"] = new() { Type = "string", Description = "Texto/etiqueta del elemento a clicar (parcial vale)" },
            ["window"] = new() { Type = "string", Description = "Título de la ventana (opcional; por defecto la activa)" },
            ["button"] = new() { Type = "string", Description = "Botón del ratón: left|right|middle (por defecto left)", Default = "left" }
        },
        Required = new() { "label" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var label = args.TryGetValue("label", out var l) ? l?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(label))
            return new ToolResult(false, "Debes indicar el texto del elemento a clicar.");

        var window = args.TryGetValue("window", out var w) ? w?.ToString() ?? "" : "";
        var button = args.TryGetValue("button", out var b) ? b?.ToString() ?? "left" : "left";

        // 1) Enfocar la ventana si la indican.
        if (!string.IsNullOrWhiteSpace(window))
        {
            var win = WindowManager.Find(window);
            if (win == null)
                return new ToolResult(false, $"No se encontró la ventana '{window}'.");
            WindowManager.FocusWindow(win.Hwnd);
            InputController.HumanPause(150);
        }

        // 2) UI Automation (fiable, por nombre real del control).
        var uiaRoot = !string.IsNullOrWhiteSpace(window)
            ? UiAutomationProvider.GetWindowRoot(window)
            : UiAutomationProvider.GetForegroundRoot();
        if (uiaRoot != null)
        {
            var uiaEl = UiAutomationProvider.FindByName(uiaRoot, label);
            if (uiaEl != null)
            {
                var (ok, msg) = UiAutomationProvider.Invoke(uiaEl);
                return new ToolResult(ok, $"UI Automation: {msg}");
            }
        }

        // 3) Visión: localizar por texto y hacer clic.
        var located = await ScreenVisionLocator.LocateAsync(client, configManager, label, window).ConfigureAwait(false);
        if (located == null)
            return new ToolResult(false, $"No pude encontrar en pantalla un elemento con '{label}' (ni por UI Automation ni por visión).");

        var (x, y) = located.Value;
        InputController.MoveMouseTo(x, y);
        InputController.HumanPause(120);
        InputController.ClickMouseButton(button);
        return new ToolResult(true, $"Clic en '{label}' en ({x},{y}) (localizado por visión).");
    }
}

/// <summary>Devuelve las coordenadas reales de un elemento encontrado por su texto (UI Automation o visión).</summary>
public class FindElementCoordsTool(OllamaClient client, ConfigManager configManager) : ITool
{
    public string Name => "find_element_coords";
    public string Description => "Devuelve las coordenadas (x,y) reales de pantalla de un elemento que coincida con un texto. Primero usa UI Automation y, si no, lo localiza por visión. Útil antes de hacer clic o verificar.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["label"] = new() { Type = "string", Description = "Texto/etiqueta del elemento a localizar" },
            ["window"] = new() { Type = "string", Description = "Título de la ventana (opcional; por defecto la activa)" }
        },
        Required = new() { "label" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var label = args.TryGetValue("label", out var l) ? l?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(label))
            return new ToolResult(false, "Debes indicar el texto del elemento a localizar.");

        var window = args.TryGetValue("window", out var w) ? w?.ToString() ?? "" : "";

        var root = !string.IsNullOrWhiteSpace(window)
            ? UiAutomationProvider.GetWindowRoot(window)
            : UiAutomationProvider.GetForegroundRoot();
        if (root != null)
        {
            var el = UiAutomationProvider.FindByName(root, label);
            if (el != null)
            {
                var rect = el.Current.BoundingRectangle;
                var cx = (int)(rect.X + rect.Width / 2);
                var cy = (int)(rect.Y + rect.Height / 2);
                return new ToolResult(true, $"'{label}' en ({cx},{cy}) (UI Automation).");
            }
        }

        var located = await ScreenVisionLocator.LocateAsync(client, configManager, label, window).ConfigureAwait(false);
        if (located == null)
            return new ToolResult(false, $"No se pudo localizar '{label}' en pantalla.");

        var (x, y) = located.Value;
        return new ToolResult(true, $"'{label}' en ({x},{y}) (visión).");
    }
}

/// <summary>
/// Localizador por visión: captura la pantalla/ventana, pregunta al modelo multimodal local por el
/// rectángulo del elemento cuyo texto coincide, y devuelve el centro en coordenadas REALES de pantalla.
/// </summary>
internal static class ScreenVisionLocator
{
    private const int TimeoutSeconds = 14;
    private const int MaxSide = 2000;

    public static async Task<(int X, int Y)?> LocateAsync(OllamaClient client, ConfigManager configManager, string label, string window)
    {
        var capture = !string.IsNullOrWhiteSpace(window) ? CaptureWindow(window) : CaptureFullScreen();
        if (capture == null) return null;

        var (bmp, origW, origH, scale) = capture.Value;
        var images = new List<string> { EncodeJpegBase64(bmp, scale) };
        bmp.Dispose();

        var cfg = configManager.Config;
        var installed = await client.GetInstalledModelsAsync().ConfigureAwait(false);
        var candidates = BuildVisionCandidates(cfg, installed);
        if (candidates.Count == 0) return null;

        var prompt =
            $"Busca el elemento cuyo texto o etiqueta se parece a: \"{label}\".\n" +
            "Devuelve SOLO JSON válido: {\"x\":0,\"y\":0,\"w\":0,\"h\":0} con el rectángulo del elemento (coordenadas relativas a la imagen enviada).\n" +
            "Si no está visible, devuelve {\"not_found\":true}.";

        foreach (var model in candidates)
        {
            try
            {
                var messages = new List<OllamaMessage>
                {
                    new("system", "Eres un localizador visual local. Respondes SOLO con JSON de coordenadas. Español de España."),
                    new("user", prompt) { Images = images }
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var resp = await client.ChatAsync(messages, new List<ToolDefinition>(), model, keepAlive: "5m", cancellationToken: cts.Token).ConfigureAwait(false);
                if (resp == null || !string.IsNullOrWhiteSpace(resp.Error)) continue;

                var content = Sanitize(resp.Message?.Content);
                var obj = ExtractJsonObject(content);
                if (obj == null || obj["not_found"]?.Value<bool>() == true) continue;

                var x = obj["x"]?.Value<int>() ?? -1;
                var y = obj["y"]?.Value<int>() ?? -1;
                if (x < 0 || y < 0) continue;

                var w = obj["w"]?.Value<int>() ?? 0;
                var h = obj["h"]?.Value<int>() ?? 0;

                // Reescala a la pantalla real.
                var inv = scale > 0 && scale < 1.0 ? 1.0 / scale : 1.0;
                var centerX = (int)((x + w / 2.0) * inv);
                var centerY = (int)((y + h / 2.0) * inv);

                centerX = Math.Clamp(centerX, 0, origW);
                centerY = Math.Clamp(centerY, 0, origH);
                return (centerX, centerY);
            }
            catch { /* siguiente modelo */ }
        }

        return null;
    }

    // ═══════════════════ Captura ═══════════════════

    private static (Bitmap Bmp, int W, int H, double Scale)? CaptureFullScreen()
    {
        var bounds = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return (bmp, bounds.Width, bounds.Height, ComputeScale(bounds.Width, bounds.Height));
    }

    private static (Bitmap Bmp, int W, int H, double Scale)? CaptureWindow(string title)
    {
        var (ok, _) = WindowManager.CaptureWindow(title, out var path);
        if (!ok || string.IsNullOrEmpty(path)) return null;

        Bitmap bmp;
        using (var loaded = Image.FromFile(path))
            bmp = new Bitmap(loaded);
        try { System.IO.File.Delete(path); } catch { }

        var w = bmp.Width;
        var h = bmp.Height;
        return (bmp, w, h, ComputeScale(w, h));
    }

    private static double ComputeScale(int w, int h)
        => Math.Min(1.0, MaxSide / (double)Math.Max(1, Math.Max(w, h)));

    private static string EncodeJpegBase64(Bitmap bmp, double scale)
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
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, 88L);
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

    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var t = text.Replace("\r\n", "\n");
        t = Regex.Replace(t, @"[^\u0009\u000A\u000D\u0020-\u007E\u00A1-\u00FF]", "");
        return t.Trim();
    }

    private static JObject? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var braces = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escape) escape = false;
                else if (ch == '\\') escape = true;
                else if (ch == '"') inString = false;
                continue;
            }

            if (ch == '"') inString = true;
            else if (ch == '{') braces++;
            else if (ch == '}') { braces--; if (braces == 0) { try { return JObject.Parse(text[start..(i + 1)]); } catch { return null; } } }
        }

        return null;
    }
}
