using System.Windows.Automation;

namespace AresAssistant.Core;

/// <summary>
/// Lector del árbol de accesibilidad (UI Automation) de la ventana en primer plano.
/// Permite listar controles reales (botones, campos de texto, menús...) con su
/// nombre, tipo y rectángulo, y activarlos mediante patrones (Invoke/Toggle/Select)
/// o con un clic en el centro geométrico. Es mucho más fiable que la visión por píxeles.
/// </summary>
public static class UiAutomationProvider
{
    public sealed record UiElement(
        string Name,
        string ControlType,
        string AutomationId,
        bool IsEnabled,
        int X,
        int Y,
        int Width,
        int Height);

    private const int MaxElements = 250;

    /// <summary>Devuelve el elemento raíz de la ventana en primer plano (o del escritorio).</summary>
    public static AutomationElement? GetForegroundRoot()
    {
        var (_, _, hwnd) = WindowManager.GetForegroundWindowInfo();
        if (hwnd == IntPtr.Zero)
            return AutomationElement.RootElement;
        return AutomationElement.FromHandle(hwnd);
    }

    /// <summary>Devuelve el elemento raíz de una ventana concreta por título.</summary>
    public static AutomationElement? GetWindowRoot(string title)
    {
        var win = WindowManager.Find(title);
        return win == null ? null : AutomationElement.FromHandle(win.Hwnd);
    }

    /// <summary>Enumera los controles descendientes del elemento raíz dado.</summary>
    public static List<UiElement> Enumerate(AutomationElement root)
    {
        var result = new List<UiElement>();
        if (root == null) return result;

        try
        {
            var all = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
            if (all == null) return result;

            var count = Math.Min(all.Count, MaxElements);
            for (var i = 0; i < count; i++)
            {
                try
                {
                    var el = all[i];
                    var name = el.Current.Name;
                    var type = el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                    var id = el.Current.AutomationId;
                    var enabled = el.Current.IsEnabled;
                    var rect = el.Current.BoundingRectangle;

                    if (rect.Width <= 0 || rect.Height <= 0)
                        continue;
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id))
                        continue;

                    result.Add(new UiElement(
                        name, type, id, enabled,
                        (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));
                }
                catch { /* elemento individual puede fallar; seguimos */ }
            }
        }
        catch { /* FindAll puede lanzar si la ventana cierra a mitad */ }

        return result;
    }

    /// <summary>
    /// Busca un control por nombre (parcial o exacto) dentro del elemento raíz.
    /// </summary>
    public static AutomationElement? FindByName(AutomationElement root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            var cond = new PropertyCondition(AutomationElement.NameProperty, name);
            var exact = root.FindFirst(TreeScope.Descendants, cond);
            if (exact != null) return exact;

            // Coincidencia parcial: barremos y comparamos con criterio de tolerancia.
            var all = root.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
            if (all == null) return null;

            var n = name.Trim();
            for (var i = 0; i < all.Count; i++)
            {
                var el = all[i];
                var nm = el.Current.Name;
                if (nm != null && nm.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return el;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    /// Activa un elemento mediante el patrón adecuado o haciendo clic en su centro.
    /// Devuelve un mensaje descriptivo.
    /// </summary>
    public static (bool Success, string Message) Invoke(AutomationElement el)
    {
        if (el == null) return (false, "Elemento no encontrado.");

        try
        {
            // 1) Patrones de acción directa
            if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var invokeObj))
            {
                ((InvokePattern)invokeObj).Invoke();
                return (true, $"Invocado '{el.Current.Name}' (Invoke).");
            }

            if (el.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj))
            {
                ((TogglePattern)toggleObj).Toggle();
                return (true, $"Alternado '{el.Current.Name}' (Toggle).");
            }

            if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selObj))
            {
                ((SelectionItemPattern)selObj).Select();
                return (true, $"Seleccionado '{el.Current.Name}' (Select).");
            }

            if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expObj))
            {
                ((ExpandCollapsePattern)expObj).Expand();
                return (true, $"Expandido '{el.Current.Name}' (Expand).");
            }

            // 2) Clic en el centro geométrico vía SendInput
            var rect = el.Current.BoundingRectangle;
            var cx = (int)(rect.X + rect.Width / 2);
            var cy = (int)(rect.Y + rect.Height / 2);

            var wasForeground = WindowManager.GetForegroundWindowInfo().Hwnd;
            var hwndRoot = el.Current.NativeWindowHandle != 0
                ? new IntPtr(el.Current.NativeWindowHandle)
                : wasForeground;
            if (hwndRoot != IntPtr.Zero)
                WindowManager.FocusWindow(hwndRoot);

            InputController.HumanPause(80);
            InputController.MoveMouseTo(cx, cy);
            InputController.HumanPause(80);
            InputController.ClickMouseButton("left");
            return (true, $"Clic en '{el.Current.Name}' en ({cx},{cy}).");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo activar el elemento: {ex.Message}");
        }
    }
}
