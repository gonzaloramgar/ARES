using AresAssistant.Core;
using Newtonsoft.Json.Linq;

namespace AresAssistant.Tools;

/// <summary>Guarda un procedimiento aprendido (objetivo + pasos) para reutilizarlo en el futuro.</summary>
public class SkillSaveTool(SkillLibrary skillLibrary, OllamaClient ollamaClient) : ITool
{
    public string Name => "skill_save";
    public string Description => "Guarda un procedimiento ('skill': objetivo + lista de pasos con herramienta y argumentos) para ejecutarlo automáticamente en tareas similares en el futuro. Aprende de la experiencia.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["goal"] = new() { Type = "string", Description = "Objetivo a recordar, ej 'Poner el volumen al 50 y silenciar el micro en Discord'" },
            ["app_context"] = new() { Type = "string", Description = "App/contexto en el que aplica (opcional)" },
            ["steps"] = new() { Type = "array", Description = "Pasos: objetos con tool, args (objeto) y description" }
        },
        Required = new() { "goal", "steps" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var goal = args.TryGetValue("goal", out var g) ? g?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(goal))
            return new ToolResult(false, "Debes indicar el objetivo del procedimiento.");

        var appContext = args.TryGetValue("app_context", out var ac) ? ac?.ToString() : null;
        var steps = new List<SkillStepItem>();
        if (args.TryGetValue("steps", out var stepsJ) && stepsJ is JArray arr)
        {
            foreach (var item in arr.OfType<JObject>())
            {
                var tool = item["tool"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(tool)) continue;
                var argsObj = item["args"] as JObject ?? new JObject();
                var desc = item["description"]?.ToString();
                steps.Add(new SkillStepItem
                {
                    Tool = tool,
                    ArgsJson = argsObj.ToString(Newtonsoft.Json.Formatting.None),
                    Description = desc
                });
            }
        }

        if (steps.Count == 0)
            return new ToolResult(false, "No se pudieron interpretar pasos válidos (cada paso necesita 'tool').");

        List<float>? embedding = null;
        try { embedding = await ollamaClient.EmbedAsync(goal).ConfigureAwait(false); } catch { }

        var skill = new SkillItem
        {
            Goal = goal,
            AppContext = appContext,
            Steps = steps,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow
        };
        skillLibrary.Save(skill);

        return new ToolResult(true, $"Procedimiento guardado: '{goal}' ({steps.Count} pasos). Lo reutilizaré en tareas similares.");
    }
}

/// <summary>Recupera procedimientos aprendidos relacionados con un objetivo.</summary>
public class SkillRecallTool(SkillLibrary skillLibrary, OllamaClient ollamaClient) : ITool
{
    public string Name => "skill_recall";
    public string Description => "Recupera procedimientos aprendidos ('skills') relacionados con un objetivo, para saber cómo hacer algo que ya hicimos antes.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["goal"] = new() { Type = "string", Description = "Objetivo del que quieres recordar el procedimiento" }
        },
        Required = new() { "goal" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var goal = args.TryGetValue("goal", out var g) ? g?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(goal))
            return new ToolResult(false, "Debes indicar el objetivo a recordar.");

        List<float>? embedding = null;
        try { embedding = await ollamaClient.EmbedAsync(goal).ConfigureAwait(false); } catch { }

        var related = skillLibrary.FindRelated(goal, embedding, max: 3);
        if (related.Count == 0)
            return new ToolResult(false, "No hay ningún procedimiento guardado para ese objetivo.");

        var sb = new System.Text.StringBuilder();
        foreach (var skill in related)
        {
            sb.AppendLine($"Objetivo: {skill.Goal} (app: {skill.AppContext ?? "general"}, usos: {skill.UseCount}, confianza: {skill.Confidence:P0})");
            foreach (var step in skill.Steps)
                sb.AppendLine($"  - {step.Tool}: {step.ArgsJson}  {(string.IsNullOrWhiteSpace(step.Description) ? "" : $"// {step.Description}")}");
            sb.AppendLine();
        }
        return new ToolResult(true, sb.ToString().TrimEnd());
    }
}

/// <summary>Lista todos los procedimientos aprendidos.</summary>
public class SkillListTool(SkillLibrary skillLibrary) : ITool
{
    public string Name => "skill_list";
    public string Description => "Lista todos los procedimientos aprendidos (skills) guardados, con usos y confianza.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new(),
        Required = new()
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var all = skillLibrary.GetAll();
        if (all.Count == 0)
            return Task.FromResult(new ToolResult(false, "No hay procedimientos aprendidos todavía."));

        var lines = all
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => $"- '{s.Goal}' (app: {s.AppContext ?? "general"}, pasos: {s.Steps.Count}, usos: {s.UseCount}, confianza: {s.Confidence:P0})");
        return Task.FromResult(new ToolResult(true, string.Join("\n", lines)));
    }
}

/// <summary>Olvida un procedimiento aprendido.</summary>
public class SkillForgetTool(SkillLibrary skillLibrary) : ITool
{
    public string Name => "skill_forget";
    public string Description => "Elimina un procedimiento aprendido por su objetivo.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["goal"] = new() { Type = "string", Description = "Objetivo del procedimiento a olvidar" }
        },
        Required = new() { "goal" }
    };

    public Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var goal = args.TryGetValue("goal", out var g) ? g?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(goal))
            return Task.FromResult(new ToolResult(false, "Debes indicar el objetivo a olvidar."));

        return Task.FromResult(
            skillLibrary.Forget(goal)
                ? new ToolResult(true, $"Procedimiento '{goal}' olvidado.")
                : new ToolResult(false, $"No había un procedimiento llamado '{goal}'."));
    }
}

/// <summary>Ejecuta un procedimiento aprendido paso a paso mediante las herramientas reales.</summary>
public class RunSkillTool(ToolDispatcher dispatcher, SkillLibrary skillLibrary, OllamaClient ollamaClient) : ITool
{
    public string Name => "run_skill";
    public string Description => "Ejecuta un procedimiento aprendido ('skill') paso a paso usando las herramientas disponibles. Ideal para repetir una tarea que ya hicimos antes.";

    public ToolParameterSchema Parameters { get; } = new()
    {
        Properties = new()
        {
            ["goal"] = new() { Type = "string", Description = "Objetivo del procedimiento a ejecutar" }
        },
        Required = new() { "goal" }
    };

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, JToken> args)
    {
        var goal = args.TryGetValue("goal", out var g) ? g?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(goal))
            return new ToolResult(false, "Debes indicar el objetivo del procedimiento a ejecutar.");

        List<float>? embedding = null;
        try { embedding = await ollamaClient.EmbedAsync(goal).ConfigureAwait(false); } catch { }

        var skill = skillLibrary.FindRelated(goal, embedding, max: 1).FirstOrDefault();
        if (skill == null)
            return new ToolResult(false, $"No hay ningún procedimiento guardado para '{goal}'.");

        var results = new List<string>();
        var allOk = true;
        foreach (var step in skill.Steps)
        {
            Dictionary<string, JToken> stepArgs;
            try
            {
                stepArgs = JObject.Parse(step.ArgsJson).ToObject<Dictionary<string, JToken>>() ?? new();
            }
            catch
            {
                stepArgs = new Dictionary<string, JToken>();
            }

            var resultMsg = await dispatcher.ExecuteAsync(step.Tool, stepArgs).ConfigureAwait(false);
            var ok = !LooksLikeError(resultMsg);
            if (!ok) allOk = false;
            results.Add($"— {step.Tool}: {(ok ? "OK" : "FALLO")} — {resultMsg}");
        }

        skillLibrary.RecordUse(skill, allOk);

        var status = allOk ? "completado" : "completado con algún fallo";
        return new ToolResult(allOk,
            $"Procedimiento '{skill.Goal}' {status} ({skill.Steps.Count} pasos):\n" + string.Join("\n", results));
    }

    private static bool LooksLikeError(string msg)
    {
        var m = (msg ?? string.Empty).Trim();
        return m.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("Acción bloqueada", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("Acción cancelada", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("No se encontró", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("No se pudo", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("Debes indicar", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("No hay", StringComparison.OrdinalIgnoreCase)
            || m.StartsWith("No se pueden interpretar", StringComparison.OrdinalIgnoreCase);
    }
}
