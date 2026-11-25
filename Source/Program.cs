namespace YureiCrosshair;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "YureiCrosshair_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Yurei Crosshair is already running (check your system tray).",
                "Yurei Crosshair", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MenuForm());
    }
}

static class Util
{
    public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

    public static string KeyName(Keys k)
    {
        if ((k & Keys.KeyCode) == Keys.None) return "NONE";
        var parts = new List<string>();
        if ((k & Keys.Control) != 0) parts.Add("CTRL");
        if ((k & Keys.Shift) != 0) parts.Add("SHIFT");
        if ((k & Keys.Alt) != 0) parts.Add("ALT");
        var code = k & Keys.KeyCode;
        string n = code switch
        {
            >= Keys.D0 and <= Keys.D9 => ((int)code - (int)Keys.D0).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => "NUM " + ((int)code - (int)Keys.NumPad0),
            Keys.Next => "PAGE DOWN",
            Keys.Prior => "PAGE UP",
            Keys.Return => "ENTER",
            Keys.Capital => "CAPS LOCK",
            Keys.Oemtilde => "~",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.XButton1 => "MOUSE 4",
            Keys.XButton2 => "MOUSE 5",
            _ => code.ToString().ToUpperInvariant()
        };
        parts.Add(n);
        return string.Join(" + ", parts);
    }

    /// RegisterHotKey modifier flags + virtual key for a Keys value.
    public static (uint mods, uint vk) ToHotkey(Keys k)
    {
        uint m = Native.MOD_NOREPEAT;
        if ((k & Keys.Control) != 0) m |= Native.MOD_CONTROL;
        if ((k & Keys.Shift) != 0) m |= Native.MOD_SHIFT;
        if ((k & Keys.Alt) != 0) m |= Native.MOD_ALT;
        return (m, (uint)(k & Keys.KeyCode));
    }
}
