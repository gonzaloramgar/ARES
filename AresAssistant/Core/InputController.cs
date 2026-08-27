using System.Runtime.InteropServices;

namespace AresAssistant.Core;

/// <summary>
/// Controlador de entrada de bajo nivel basado en la API Win32 SendInput.
/// Simula ratón (movimiento absoluto en pantalla virtual, clics, arrastre, rueda)
/// y teclado (teclas virtuales con modificadores) como lo haría un humano.
/// </summary>
public static class InputController
{
    // ═══════════════════ Estructuras SendInput ═══════════════════

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf(typeof(INPUT));
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short VkKeyScanW(char ch);

    // ═══════════════════ Ratón ═══════════════════

    /// <summary>
    /// Mueve el cursor a coordenadas físicas absolutas del escritorio virtual.
    /// Origen (0,0) = esquina superior izquierda del monitor primario;
    /// puede ser negativo hacia monitores a la izquierda/arriba.
    /// </summary>
    public static void MoveMouseTo(int x, int y)
    {
        var vs = GetVirtualScreen();
        var nx = (int)Math.Round((x - vs.X) * 65535.0 / Math.Max(1, vs.Width));
        var ny = (int)Math.Round((y - vs.Y) * 65535.0 / Math.Max(1, vs.Height));
        nx = Math.Clamp(nx, 0, 65535);
        ny = Math.Clamp(ny, 0, 65535);

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dx = nx, dy = ny, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE }
            }
        };
        SendInput(1, new[] { input }, INPUT.Size);
    }

    /// <summary>Mueve el cursor de forma relativa a su posición actual.</summary>
    public static void MoveMouseBy(int dx, int dy)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MOUSEEVENTF_MOVE }
            }
        };
        SendInput(1, new[] { input }, INPUT.Size);
    }

    /// <summary>Pulsación completa de un botón del ratón en la posición actual.</summary>
    public static void ClickMouseButton(string button)
    {
        var (down, up) = button.ToLowerInvariant() switch
        {
            "right"  => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            "middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _        => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };

        SendInput(1, new[] { MakeMouseInput(down) }, INPUT.Size);
        Thread.Sleep(30); // pausa humano entre pulsación y soltado
        SendInput(1, new[] { MakeMouseInput(up) }, INPUT.Size);
    }

    /// <summary>Doble clic del botón indicado en la posición actual.</summary>
    public static void DoubleClickMouseButton(string button)
    {
        ClickMouseButton(button);
        Thread.Sleep(60);
        ClickMouseButton(button);
    }

    /// <summary>
    /// Arrastre: mantiene el botón pulsado mientras mueve el cursor hasta el destino
    /// en pequeños pasos interpolados (más fiable en apps que exigen movimiento continuo).
    /// </summary>
    public static void Drag(int fromX, int fromY, int toX, int toY, string button = "left")
    {
        var (down, up) = button.ToLowerInvariant() switch
        {
            "right"  => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            "middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _        => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
        };

        MoveMouseTo(fromX, fromY);
        Thread.Sleep(80);

        SendInput(1, new[] { MakeMouseInput(down) }, INPUT.Size);
        Thread.Sleep(60);

        const int steps = 25;
        for (var i = 1; i <= steps; i++)
        {
            var x = fromX + (toX - fromX) * i / steps;
            var y = fromY + (toY - fromY) * i / steps;
            MoveMouseTo(x, y);
            Thread.Sleep(12);
        }

        Thread.Sleep(60);
        SendInput(1, new[] { MakeMouseInput(up) }, INPUT.Size);
    }

    /// <summary>Gira la rueda del ratón. delta positivo = arriba, negativo = abajo.</summary>
    public static void ScrollWheel(int delta, bool horizontal = false)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    mouseData = unchecked((uint)delta),
                    dwFlags = horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL
                }
            }
        };
        SendInput(1, new[] { input }, INPUT.Size);
    }

    private static INPUT MakeMouseInput(uint flags) => new()
    {
        type = INPUT_MOUSE,
        U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flags } }
    };

    // ═══════════════════ Teclado ═══════════════════

    /// <summary>
    /// Pulsa una tecla (con modificadores opcionales) y la suelta.
    /// Ejemplos: key="s", modifiers=["ctrl"] → Ctrl+S; key="tab", modifiers=["alt","shift"] → Alt+Shift+Tab.
    /// </summary>
    public static void PressKey(string key, IEnumerable<string>? modifiers = null, int times = 1)
    {
        if (!TryGetKeyCode(key, out var vk))
            throw new ArgumentException($"Tecla no reconocida: '{key}'");

        var mods = (modifiers ?? Enumerable.Empty<string>())
            .Select(NormalizeModifier)
            .Where(m => m != null)
            .Select(m => m!.Value)
            .ToList();

        // Salvaguardas: combinaciones destructivas/del sistema siempre bloqueadas.
        var isCtrl = mods.Contains(VK_CONTROL);
        var isAlt = mods.Contains(VK_MENU);
        var isWin = mods.Contains(VK_LWIN);
        var keyUpper = key.Trim().ToUpperInvariant();

        if (isCtrl && isAlt && (keyUpper is "DEL" or "DELETE" or "SUPR"))
            throw new InvalidOperationException("Combinación bloqueada por seguridad: Ctrl+Alt+Supr");
        if (isWin && keyUpper == "L")
            throw new InvalidOperationException("Combinación bloqueada por seguridad: Win+L");

        times = Math.Clamp(times, 1, 20);

        foreach (var m in mods) KeyDown(m);
        for (var i = 0; i < times; i++)
        {
            KeyDown(vk);
            Thread.Sleep(20);
            KeyUp(vk);
            Thread.Sleep(40);
        }
        foreach (var m in mods.AsEnumerable().Reverse()) KeyUp(m);
    }

    /// <summary>Escribe texto Unicode arbitrario carácter a carácter vía SendInput.</summary>
    public static void TypeUnicodeText(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\n' || ch == '\r')
            {
                PressEnter();
                continue;
            }

            var inputs = new List<INPUT>();
            inputs.Add(MakeCharInput(ch, down: true));
            inputs.Add(MakeCharInput(ch, down: false));
            SendInput((uint)inputs.Count, inputs.ToArray(), INPUT.Size);
            Thread.Sleep(8);
        }
    }

    /// <summary>Mantiene pulsada una tecla (para acciones tipo mantener Shift).</summary>
    public static void KeyDown(ushort vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
        };
        SendInput(1, new[] { input }, INPUT.Size);
    }

    /// <summary>Suelta una tecla mantenida.</summary>
    public static void KeyUp(ushort vk)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
        };
        SendInput(1, new[] { input }, INPUT.Size);
    }

    private static void PressEnter()
    {
        KeyDown(VK_RETURN);
        Thread.Sleep(20);
        KeyUp(VK_RETURN);
    }

    private static INPUT MakeCharInput(char ch, bool down) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (down ? 0 : KEYEVENTF_KEYUP)
            }
        }
    };

    // ═══════════════════ Códigos de teclas ═══════════════════

    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12;      // Alt
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_RETURN = 0x0D;

    private static readonly Dictionary<string, ushort> SpecialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["tab"] = 0x09,
        ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["space"] = 0x20, ["espacio"] = 0x20,
        ["backspace"] = 0x08, ["retroceso"] = 0x08,
        ["delete"] = 0x2E, ["del"] = 0x2E, ["supr"] = 0x2E,
        ["insert"] = 0x2D, ["ins"] = 0x2D,
        ["home"] = 0x24, ["inicio"] = 0x24,
        ["end"] = 0x23, ["fin"] = 0x23,
        ["pageup"] = 0x21, ["avpag"] = 0x21, ["re pag"] = 0x21,
        ["pagedown"] = 0x22, ["repag"] = 0x22, ["av pag"] = 0x22,
        ["left"] = 0x25, ["izquierda"] = 0x25,
        ["up"] = 0x26, ["arriba"] = 0x26,
        ["right"] = 0x27, ["derecha"] = 0x27,
        ["down"] = 0x28, ["abajo"] = 0x28,
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73,
        ["f5"] = 0x74, ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77,
        ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
        ["printscreen"] = 0x2C, ["imppant"] = 0x2C,
        ["capslock"] = 0x14, ["bloqmayus"] = 0x14,
        ["numlock"] = 0x90, ["bloqnum"] = 0x90,
    };

    /// <summary>Resuelve una tecla por nombre especial o carácter individual a su código virtual.</summary>
    public static bool TryGetKeyCode(string key, out ushort vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var k = key.Trim();

        // Modificadores también aceptados como tecla destino
        switch (NormalizeModifier(k))
        {
            case VK_CONTROL: vk = VK_CONTROL; return true;
            case VK_MENU: vk = VK_MENU; return true;
            case VK_SHIFT: vk = VK_SHIFT; return true;
            case VK_LWIN: vk = VK_LWIN; return true;
        }

        if (SpecialKeys.TryGetValue(k, out var special))
        {
            vk = special;
            return true;
        }

        // Tecla de un solo carácter (letra, dígito o símbolo)
        if (k.Length == 1)
        {
            var ch = char.ToUpperInvariant(k[0]);
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                vk = ch;
                return true;
            }

            var scan = VkKeyScanW(k[0]);
            if (scan != -1)
            {
                vk = unchecked((byte)(scan & 0xFF));
                return true;
            }
        }

        return false;
    }

    private static ushort? NormalizeModifier(string? modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier)) return null;
        return modifier.Trim().ToLowerInvariant() switch
        {
            "ctrl" or "control" or "controlador" => VK_CONTROL,
            "alt" => VK_MENU,
            "shift" or "mayus" or "mayús" => VK_SHIFT,
            "win" or "windows" or "meta" => VK_LWIN,
            _ => null
        };
    }

    private static string VkName(ushort vk) => vk switch
    {
        VK_CONTROL => "Ctrl",
        VK_MENU => "Alt",
        VK_SHIFT => "Shift",
        VK_LWIN => "Win",
        _ => $"VK_{vk:X2}"
    };

    // ═══════════════════ Utilidades ═══════════════════

    public record VirtualScreen(int X, int Y, int Width, int Height);

    /// <summary>Límites físicos del escritorio virtual completo (todos los monitores).</summary>
    public static VirtualScreen GetVirtualScreen()
    {
        const int SM_XVIRTUALSCREEN = 76;
        const int SM_YVIRTUALSCREEN = 77;
        const int SM_CXVIRTUALSCREEN = 78;
        const int SM_CYVIRTUALSCREEN = 79;

        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new VirtualScreen(x, y, w, h);
    }

    /// <summary>Posición física actual del cursor.</summary>
    public static Point GetCursorPosition()
    {
        GetCursorPos(out var pt);
        return pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <summary>Espera breve anti-inundación entre acciones encadenadas.</summary>
    public static void HumanPause(int ms = 120) => Thread.Sleep(ms);
}
