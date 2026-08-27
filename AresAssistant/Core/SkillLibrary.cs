using Newtonsoft.Json;

namespace AresAssistant.Core;

/// <summary>
/// Biblioteca de procedimientos aprendidos ("skills"): guarda secuencias de acciones que
/// funcionaron para lograr un objetivo en una app, con su embedding para recuperarlas
/// semánticamente en tareas futuras. Hace que ARES mejore con el tiempo.
/// </summary>
public sealed class SkillLibrary
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<SkillItem> _items = new();

    public int Version { get; private set; }

    public SkillLibrary(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        Load();
    }

    // ═══════════════════ API pública ═══════════════════

    public void Save(SkillItem skill)
    {
        lock (_lock)
        {
            skill.UpdatedAt = DateTime.UtcNow;
            var existing = _items.FirstOrDefault(i => i.Goal.Equals(skill.Goal, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                _items.Remove(existing);
            _items.Add(skill);

            // Mantener compacto: máximo 150 skills.
            _items = _items.OrderByDescending(i => i.UseCount).Take(150).ToList();
            SaveLocked();
            Version++;
        }
    }

    public bool Forget(string goal)
    {
        lock (_lock)
        {
            var removed = _items.RemoveAll(i => i.Goal.Equals(goal, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0) return false;
            SaveLocked();
            Version++;
            return true;
        }
    }

    public List<SkillItem> GetAll() { lock (_lock) return _items.ToList(); }

    /// <summary>Recupera skills relacionadas con un objetivo. Prioriza coincidencia por texto y luego por similitud de embedding.</summary>
    public List<SkillItem> FindRelated(string goal, IReadOnlyList<float>? goalEmbedding = null, int max = 3)
    {
        lock (_lock)
        {
            if (_items.Count == 0) return new();

            var g = (goal ?? string.Empty).Trim().ToLowerInvariant();

            // 1) Coincidencia por texto (exacta/contiene): puntuación alta
            var scored = new List<(SkillItem Item, double Score)>();
            foreach (var item in _items)
            {
                var textScore = 0.0;
                var goalLower = item.Goal.ToLowerInvariant();
                var stepsText = string.Join(" ", item.Steps.Select(s => (s.Description ?? "") + " " + s.Tool)).ToLowerInvariant();

                if (goalLower == g) textScore += 3.0;
                else if (goalLower.Contains(g)) textScore += 2.0;
                else if (g.Length > 3 && goalLower.Split(' ').Any(w => g.Contains(w))) textScore += 1.0;

                if (stepsText.Contains(g)) textScore += 0.5;

                // Similitud por embedding
                var embScore = 0.0;
                if (goalEmbedding != null && item.Embedding != null && item.Embedding.Count > 0 && goalEmbedding.Count > 0)
                    embScore = CosineSimilarity(goalEmbedding, item.Embedding);

                scored.Add((item, textScore + embScore));
            }

            return scored
                .OrderByDescending(s => s.Score)
                .Take(max)
                .Select(s => s.Item)
                .ToList();
        }
    }

    /// <summary>Genera un bloque de texto con los procedimientos más relevantes para inyectar en el prompt.</summary>
    public string BuildPromptContext(string goal, IReadOnlyList<float>? goalEmbedding = null, int max = 2)
    {
        var related = FindRelated(goal, goalEmbedding, max);
        if (related.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var skill in related)
        {
            sb.AppendLine($"- Objetivo: {skill.Goal} (app: {skill.AppContext ?? "general"}, usos: {skill.UseCount}, confianza: {skill.Confidence:P0})");
            foreach (var step in skill.Steps)
                sb.AppendLine($"    · {step.Description ?? step.Tool}");
        }
        return sb.ToString().TrimEnd();
    }

    public void RecordUse(SkillItem skill, bool success)
    {
        lock (_lock)
        {
            skill.UseCount++;
            if (success) skill.SuccessCount++;
            var total = skill.UseCount;
            skill.Confidence = total > 0 ? (double)skill.SuccessCount / total : 0;
            SaveLocked();
        }
    }

    // ═══════════════════ Persistencia ═══════════════════

    private void Load()
    {
        if (!File.Exists(_path)) { SaveLocked(); return; }
        try
        {
            var raw = File.ReadAllText(_path);
            lock (_lock)
                _items = JsonConvert.DeserializeObject<List<SkillItem>>(raw) ?? new List<SkillItem>();
        }
        catch
        {
            lock (_lock) _items = new List<SkillItem>();
        }
    }

    private void SaveLocked()
    {
        File.WriteAllText(_path, JsonConvert.SerializeObject(_items, Formatting.Indented));
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        var n = Math.Min(a.Count, b.Count);
        if (n == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}

/// <summary>Procedimiento aprendido: objetivo, contexto de app y pasos ejecutables.</summary>
public sealed class SkillItem
{
    public string Goal { get; set; } = "";
    public string? AppContext { get; set; }
    public List<SkillStepItem> Steps { get; set; } = new();
    public List<float>? Embedding { get; set; }
    public int UseCount { get; set; }
    public int SuccessCount { get; set; }
    public double Confidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Un paso de un procedimiento: herramienta + argumentos JSON + descripción.</summary>
public sealed class SkillStepItem
{
    public string Tool { get; set; } = "";
    public string ArgsJson { get; set; } = "{}";
    public string? Description { get; set; }
}
