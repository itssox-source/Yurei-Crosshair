using System.Globalization;
using System.Reflection;
using System.Text;

namespace YureiCrosshair;

public enum CrossStyle { Cross, TShape, Dot, Circle, CircleDot, XShape }

public class CrosshairSettings
{
    public bool Enabled { get; set; } = true;
    public CrossStyle Style { get; set; } = CrossStyle.Cross;

    public int ColorArgb { get; set; } = unchecked((int)0xFFFFFFFF);   // white
    public int Opacity { get; set; } = 100;                             // %

    public int Length { get; set; } = 7;
    public int Thickness { get; set; } = 2;
    public int Gap { get; set; } = 3;
    public int CircleRadius { get; set; } = 8;
    public int DotSize { get; set; } = 2;
    public bool CenterDot { get; set; } = false;

    public bool Outline { get; set; } = true;
    public int OutlineThickness { get; set; } = 1;
    public int OutlineArgb { get; set; } = unchecked((int)0xFF000000);

    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public CrosshairSettings Clone() => (CrosshairSettings)MemberwiseClone();

    // ================= share codes =================
    // Format: YUREI-<base64url of "1,style,color,opacity,...">
    const string Prefix = "YUREI-";

    public string ToShareCode()
    {
        string raw = string.Join(",",
            1, (int)Style, ColorArgb.ToString("X8"), Opacity, Length, Thickness, Gap, CircleRadius, DotSize,
            CenterDot ? 1 : 0, Outline ? 1 : 0, OutlineThickness, OutlineArgb.ToString("X8"), OffsetX, OffsetY);
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static CrosshairSettings? FromShareCode(string? code)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code!.Trim();
            if (!code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string b64 = code.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            var p = Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Split(',');
            if (p.Length != 15 || p[0] != "1") return null;

            int I(int i) => int.Parse(p[i], CultureInfo.InvariantCulture);
            int Hex(int i) => unchecked((int)uint.Parse(p[i], NumberStyles.HexNumber));
            return new CrosshairSettings
            {
                Style = (CrossStyle)Util.Clamp(I(1), 0, 5),
                ColorArgb = Hex(2),
                Opacity = Util.Clamp(I(3), 10, 100),
                Length = Util.Clamp(I(4), 0, 60),
                Thickness = Util.Clamp(I(5), 1, 12),
                Gap = Util.Clamp(I(6), 0, 40),
                CircleRadius = Util.Clamp(I(7), 2, 60),
                DotSize = Util.Clamp(I(8), 1, 12),
                CenterDot = I(9) == 1,
                Outline = I(10) == 1,
                OutlineThickness = Util.Clamp(I(11), 1, 4),
                OutlineArgb = Hex(12),
                OffsetX = Util.Clamp(I(13), -300, 300),
                OffsetY = Util.Clamp(I(14), -300, 300),
            };
        }
        catch { return null; }
    }
}

/// App-wide options (keybinds, window behaviour).
public class AppOptions
{
    public int KeyMenu { get; set; } = (int)Keys.Insert;
    public int KeyToggle { get; set; } = (int)Keys.Home;
    public int KeyTaskbar { get; set; } = (int)Keys.PageDown;
    public bool HideFromTaskbar { get; set; } = false;
}

public class AppConfig
{
    public const int SlotCount = 4;

    public AppOptions Options { get; set; } = new();
    public CrosshairSettings Current { get; set; } = new();
    public CrosshairSettings?[] Slots { get; set; } = new CrosshairSettings?[SlotCount];

    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YureiCrosshair");
    static string FilePath => Path.Combine(Dir, "config.ini");

    public static AppConfig Load()
    {
        var cfg = new AppConfig();
        try
        {
            if (!File.Exists(FilePath)) return cfg;
            object? target = null;
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var sec = line.Substring(1, line.Length - 2);
                    if (sec == "App") target = cfg.Options;
                    else if (sec == "Current") target = cfg.Current;
                    else if (sec.StartsWith("Slot") && int.TryParse(sec.Substring(4), out int n) && n >= 1 && n <= SlotCount)
                        target = cfg.Slots[n - 1] = new CrosshairSettings();
                    else target = null;
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq > 0 && target != null) Ini.Set(target, line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
        }
        catch { return new AppConfig(); }
        return cfg;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var sb = new StringBuilder("; Yurei Crosshair config\r\n\r\n");
            Ini.Write(Options, sb, "App");
            Ini.Write(Current, sb, "Current");
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null) Ini.Write(Slots[i]!, sb, $"Slot{i + 1}");
            File.WriteAllText(FilePath, sb.ToString());
        }
        catch { }
    }
}

/// Tiny reflection-based key=value writer/reader.
static class Ini
{
    public static void Write(object o, StringBuilder sb, string section)
    {
        sb.AppendLine($"[{section}]");
        foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanWrite) continue;
            object v = p.GetValue(o)!;
            string s = p.PropertyType == typeof(int) && p.Name.EndsWith("Argb")
                ? "#" + ((int)v).ToString("X8")
                : Convert.ToString(v, CultureInfo.InvariantCulture)!;
            sb.AppendLine($"{p.Name}={s}");
        }
        sb.AppendLine();
    }

    public static void Set(object o, string key, string value)
    {
        var p = o.GetType().GetProperty(key);
        if (p == null || !p.CanWrite) return;
        try
        {
            if (p.PropertyType == typeof(bool)) p.SetValue(o, bool.Parse(value));
            else if (p.PropertyType.IsEnum) p.SetValue(o, Enum.Parse(p.PropertyType, value));
            else if (value.StartsWith("#")) p.SetValue(o, unchecked((int)uint.Parse(value.Substring(1), NumberStyles.HexNumber)));
            else p.SetValue(o, int.Parse(value, CultureInfo.InvariantCulture));
        }
        catch { /* bad value -> keep default */ }
    }
}
