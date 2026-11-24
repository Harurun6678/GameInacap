using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Acciones del juego que se pueden reasignar desde Configuración > Controles.
// El orden define el orden de las filas en la pantalla de controles.
public enum GameAction
{
    MoveLeft, MoveRight, MoveDown, Jump, Dash, Attack,
    SwitchWeapon, Weapon1, Weapon2, Interact, Map, Dimension
}

// Teclas del teclado asignadas a cada acción: una principal y una alternativa (opcional).
// El mando y el clic izquierdo para atacar siguen fijos; Esc queda reservado para pausa / volver.
public static class GameControls
{
    public const int Primary = 0, Alternate = 1;
    private const string PrefsKey = "GyaruRogue.Controls.v1";
    private static readonly int ActionCount = Enum.GetValues(typeof(GameAction)).Length;

    private static readonly Key[,] Defaults =
    {
        { Key.A, Key.LeftArrow },        // MoveLeft
        { Key.D, Key.RightArrow },       // MoveRight
        { Key.S, Key.DownArrow },        // MoveDown
        { Key.Space, Key.None },         // Jump
        { Key.LeftShift, Key.None },     // Dash
        { Key.J, Key.None },             // Attack
        { Key.Q, Key.None },             // SwitchWeapon
        { Key.Digit1, Key.None },        // Weapon1
        { Key.Digit2, Key.None },        // Weapon2
        { Key.E, Key.None },             // Interact
        { Key.M, Key.None },             // Map
        { Key.G, Key.None },             // Dimension
    };

    private static Key[,] bindings;
    private static string[] labels, primaryLabels;
    private static readonly Dictionary<(int, string), string> hints = new Dictionary<(int, string), string>();
    private static readonly Dictionary<string, string> cached = new Dictionary<string, string>();

    // Cambia cada vez que se reasigna una tecla: los textos del HUD la usan para saber cuándo redibujarse.
    public static int Revision
    {
        get; private set;
    }

    // ---------- Lectura en el juego ----------

    public static bool IsPressed(GameAction action)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return false;
        EnsureLoaded();
        int a = (int)action;
        return IsPressed(kb, bindings[a, Primary]) || IsPressed(kb, bindings[a, Alternate]);
    }

    public static bool WasPressed(GameAction action)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return false;
        EnsureLoaded();
        int a = (int)action;
        return WasPressed(kb, bindings[a, Primary]) || WasPressed(kb, bindings[a, Alternate]);
    }

    private static bool IsPressed(Keyboard kb, Key key)
    {
        return key != Key.None && kb[key].isPressed;
    }
    private static bool WasPressed(Keyboard kb, Key key)
    {
        return key != Key.None && kb[key].wasPressedThisFrame;
    }

    // ---------- Edición ----------

    public static Key Get(GameAction action, int slot)
    {
        EnsureLoaded();
        return bindings[(int)action, slot];
    }

    public static bool CanAssign(Key key)
    {
        return key != Key.None && key != Key.Escape;
    }

    // Asigna la tecla. Si ya la usaba otra acción, esa acción recibe la tecla anterior (intercambio),
    // así nunca quedan dos acciones con la misma tecla. Devuelve null si se asignó, o el motivo si no.
    public static string Assign(GameAction action, int slot, Key key)
    {
        if (key == Key.Escape)
            return "Esc está reservada para pausa / volver.";
        if (!CanAssign(key))
            return "Esa tecla no se puede asignar.";
        EnsureLoaded();
        int a = (int)action;
        Key previous = bindings[a, slot];
        if (previous == key)
            return null;
        int owner = -1, ownerSlot = -1;
        for (int i = 0; i < ActionCount && owner < 0; i++)
            for (int s = 0; s < 2; s++)
                if (bindings[i, s] == key)
                {
                    owner = i;
                    ownerSlot = s;
                    break;
                }
        if (owner >= 0)
        {
            // Sin tecla anterior para entregar, la otra acción podría quedar sin ninguna tecla.
            bool ownerLeftEmpty = previous == Key.None && owner != a && ownerSlot == Primary && bindings[owner, Alternate] == Key.None;
            if (ownerLeftEmpty)
                return KeyName(key).ToUpperInvariant() + " es la única tecla de «" + ActionName((GameAction)owner) + "». Cámbiala primero allí.";
            bindings[owner, ownerSlot] = previous;
        }
        bindings[a, slot] = key;
        NormalizeAlternates();
        Changed();
        return null;
    }

    // Solo la tecla alternativa puede quedar vacía; la principal siempre debe existir.
    public static void ClearAlternate(GameAction action)
    {
        EnsureLoaded();
        if (bindings[(int)action, Alternate] == Key.None)
            return;
        bindings[(int)action, Alternate] = Key.None;
        Changed();
    }

    public static void ResetDefaults()
    {
        EnsureLoaded();
        Array.Copy(Defaults, bindings, Defaults.Length);
        Changed();
    }

    // Si un intercambio dejó una acción sin principal, sube la alternativa a principal.
    private static void NormalizeAlternates()
    {
        for (int i = 0; i < ActionCount; i++)
        {
            if (bindings[i, Primary] != Key.None)
                continue;
            bindings[i, Primary] = bindings[i, Alternate];
            bindings[i, Alternate] = Key.None;
        }
    }

    private static void Changed()
    {
        Revision++;
        labels = primaryLabels = null;
        hints.Clear();
        cached.Clear();
        Save();
    }

    // ---------- Textos ----------

    public static string ActionName(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveLeft:
                return "Moverse a la izquierda";
            case GameAction.MoveRight:
                return "Moverse a la derecha";
            case GameAction.MoveDown:
                return "Bajar de una pasarela";
            case GameAction.Jump:
                return "Saltar / salto doble";
            case GameAction.Dash:
                return "Dash";
            case GameAction.Attack:
                return "Atacar";
            case GameAction.SwitchWeapon:
                return "Cambiar arma";
            case GameAction.Weapon1:
                return "Arma 1";
            case GameAction.Weapon2:
                return "Arma 2";
            case GameAction.Interact:
                return "Interactuar";
            case GameAction.Map:
                return "Mapa del distrito";
            default:
                return "Cambiar dimensión";
        }
    }

    public static string GamepadName(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveLeft:
            case GameAction.MoveRight:
                return "Stick izquierdo";
            case GameAction.MoveDown:
                return "Stick abajo";
            case GameAction.Jump:
                return "A / botón inferior";
            case GameAction.Dash:
                return "RB";
            case GameAction.Attack:
                return "X  ·  clic izquierdo";
            case GameAction.SwitchWeapon:
                return "LB";
            case GameAction.Interact:
                return "Y / botón superior";
            case GameAction.Map:
                return "Desde Pausa";
            default:
                return "—";
        }
    }

    public static string KeyName(Key key)
    {
        switch (key)
        {
            case Key.None:
                return "—";
            case Key.Space:
                return "Espacio";
            case Key.Enter:
                return "Enter";
            case Key.NumpadEnter:
                return "Enter num.";
            case Key.Tab:
                return "Tab";
            case Key.Backspace:
                return "Retroceso";
            case Key.LeftShift:
                return "Shift";
            case Key.RightShift:
                return "Shift der.";
            case Key.LeftCtrl:
                return "Ctrl";
            case Key.RightCtrl:
                return "Ctrl der.";
            case Key.LeftAlt:
                return "Alt";
            case Key.RightAlt:
                return "Alt Gr";
            case Key.LeftArrow:
                return "←";
            case Key.RightArrow:
                return "→";
            case Key.UpArrow:
                return "↑";
            case Key.DownArrow:
                return "↓";
            case Key.CapsLock:
                return "Bloq Mayús";
            case Key.Delete:
                return "Supr";
            case Key.Insert:
                return "Insert";
            case Key.Home:
                return "Inicio";
            case Key.End:
                return "Fin";
            case Key.PageUp:
                return "Re Pág";
            case Key.PageDown:
                return "Av Pág";
        }
        if (key >= Key.Digit1 && key <= Key.Digit9)
            return ((int)(key - Key.Digit1) + 1).ToString();
        if (key == Key.Digit0)
            return "0";
        if (key >= Key.Numpad0 && key <= Key.Numpad9)
            return "Num " + (int)(key - Key.Numpad0);
        if (key >= Key.A && key <= Key.Z)
            return key.ToString();
        // Para teclas de símbolos usa el nombre según la distribución del teclado (por ejemplo, Ñ).
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            string display = kb[key].displayName;
            if (!string.IsNullOrEmpty(display))
                return display;
        }
        return key.ToString();
    }

    // "A / ←" en mayúsculas, para mostrar en el juego.
    public static string Label(GameAction action)
    {
        BuildLabels();
        return labels[(int)action];
    }

    // Solo la tecla principal, en mayúsculas (para espacios pequeños del HUD).
    public static string PrimaryLabel(GameAction action)
    {
        BuildLabels();
        return primaryLabels[(int)action];
    }

    // "[ E ] ABRIR COFRE". Se guarda en caché: llamarlo en cada frame no crea strings nuevos.
    public static string Hint(GameAction action, string text)
    {
        var id = ((int)action, text);
        if (!hints.TryGetValue(id, out string value))
        {
            value = "[ " + PrimaryLabel(action) + " ] " + text;
            hints.Add(id, value);
        }
        return value;
    }

    // Guarda en caché un texto que depende de las teclas. Se recalcula solo cuando cambian.
    // Usa lambdas que no capturen variables para no crear basura en cada llamada.
    public static string Cached(string id, Func<string> build)
    {
        if (!cached.TryGetValue(id, out string value))
        {
            value = build();
            cached.Add(id, value);
        }
        return value;
    }

    private static void BuildLabels()
    {
        if (labels != null)
            return;
        EnsureLoaded();
        labels = new string[ActionCount];
        primaryLabels = new string[ActionCount];
        for (int i = 0; i < ActionCount; i++)
        {
            primaryLabels[i] = KeyName(bindings[i, Primary]).ToUpperInvariant();
            Key alternate = bindings[i, Alternate];
            labels[i] = alternate == Key.None ? primaryLabels[i] : primaryLabels[i] + " / " + KeyName(alternate).ToUpperInvariant();
        }
    }

    // ---------- Captura de una tecla nueva ----------

    // Devuelve la primera tecla presionada en este frame (Key.None si no hay ninguna).
    public static Key PressedKeyThisFrame()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return Key.None;
        var keys = kb.allKeys;
        for (int i = 0; i < keys.Count; i++)
        {
            KeyControl control = keys[i];
            if (control != null && control.wasPressedThisFrame)
                return control.keyCode;
        }
        return Key.None;
    }

    // ---------- Guardado ----------

    [Serializable]
    private sealed class SaveData
    {
        public string[] actions;
        public string[] primary;
        public string[] alternate;
    }

    private static void EnsureLoaded()
    {
        if (bindings != null)
            return;
        bindings = (Key[,])Defaults.Clone();
        string json = GamePreferences.ReadString(PrefsKey);
        if (string.IsNullOrEmpty(json))
            return;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.actions == null || data.primary == null || data.alternate == null)
                return;
            Key[,] loaded = (Key[,])Defaults.Clone();
            for (int i = 0; i < data.actions.Length && i < data.primary.Length && i < data.alternate.Length; i++)
            {
                if (!Enum.TryParse(data.actions[i], out GameAction action))
                    continue;
                loaded[(int)action, Primary] = Parse(data.primary[i], Defaults[(int)action, Primary]);
                loaded[(int)action, Alternate] = Parse(data.alternate[i], Key.None);
            }
            // Si el archivo guardado trae teclas repetidas o inválidas, se vuelve a los valores por defecto.
            if (IsValid(loaded))
                bindings = loaded;
        }
        catch (ArgumentException) { }
    }

    private static Key Parse(string value, Key fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;
        return Enum.TryParse(value, out Key key) && Enum.IsDefined(typeof(Key), key) ? key : fallback;
    }

    private static bool IsValid(Key[,] map)
    {
        var used = new HashSet<Key>();
        for (int i = 0; i < ActionCount; i++)
        {
            if (!CanAssign(map[i, Primary]) || !used.Add(map[i, Primary]))
                return false;
            Key alternate = map[i, Alternate];
            if (alternate == Key.None)
                continue;
            if (!CanAssign(alternate) || !used.Add(alternate))
                return false;
        }
        return true;
    }

    private static void Save()
    {
        var data = new SaveData { actions = new string[ActionCount], primary = new string[ActionCount], alternate = new string[ActionCount] };
        for (int i = 0; i < ActionCount; i++)
        {
            data.actions[i] = ((GameAction)i).ToString();
            data.primary[i] = bindings[i, Primary].ToString();
            data.alternate[i] = bindings[i, Alternate].ToString();
        }
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
