using System.Drawing.Drawing2D;

namespace YureiCrosshair;

static class Theme
{
    public static readonly Color Bg = Color.FromArgb(10, 10, 12);
    public static readonly Color PanelBg = Color.FromArgb(17, 17, 21);
    public static readonly Color Panel2 = Color.FromArgb(28, 28, 34);
    public static readonly Color Border = Color.FromArgb(42, 42, 50);
    public static readonly Color Accent = Color.FromArgb(235, 235, 240);
    public static readonly Color AccentDim = Color.FromArgb(48, 48, 54);
    public static readonly Color Text = Color.FromArgb(236, 236, 240);
    public static readonly Color TextDim = Color.FromArgb(128, 128, 140);

    public const int Radius = 8;          // corner roundness for buttons / boxes
    public const int WindowRadius = 14;   // corner roundness for the main window

    public static readonly Font Title = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font Header = new("Segoe UI", 8f, FontStyle.Bold);
    public static readonly Font Normal = new("Segoe UI Semibold", 9f);
    public static readonly Font Tiny = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9f);

    public static readonly Image? Logo = LoadLogo();
    static Image? LoadLogo()
    {
        try
        {
            using var st = typeof(Theme).Assembly.GetManifestResourceStream("logo.png");
            return st == null ? null : new Bitmap(Image.FromStream(st));
        }
        catch { return null; }
    }

    public static GraphicsPath Round(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2);
        int d = rad * 2;
        if (rad <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Rectangle r, int rad, Color fill, Color? border = null, float bw = 1)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Round(r, rad);
        using (var b = new SolidBrush(fill)) g.FillPath(b, p);
        if (border is Color bc) using (var pen = new Pen(bc, bw)) g.DrawPath(pen, p);
    }
}

class SinControl : Control
{
    protected bool Hover;
    public SinControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { Hover = false; Invalidate(); base.OnMouseLeave(e); }
}

class SinSlider : SinControl
{
    public string Label = "";
    public string Suffix = "";
    public int Min, Max = 100;
    int _v;
    bool _drag;
    public event EventHandler? ValueChanged;

    public SinSlider() { Height = 44; }

    public int Value
    {
        get => _v;
        set
        {
            int v = Util.Clamp(value, Min, Max);
            if (v == _v) return;
            _v = v; Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public void SetSilently(int v) { _v = Util.Clamp(v, Min, Max); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        TextRenderer.DrawText(g, Label, Theme.Header, new Point(0, 4), Theme.TextDim);
        TextRenderer.DrawText(g, _v + Suffix, Theme.Header, new Rectangle(0, 4, Width, 16), Theme.Text,
            TextFormatFlags.Right);

        float frac = Max == Min ? 0 : (float)(_v - Min) / (Max - Min);
        int fx = 8 + (int)(frac * (Width - 16));

        Theme.FillRound(g, new Rectangle(0, 26, Width - 1, 8), 4, Theme.Panel2);
        Theme.FillRound(g, new Rectangle(0, 26, Math.Max(8, fx), 8), 4, Theme.Accent);

        int kr = (_drag || Hover) ? 8 : 7;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Theme.Bg)) g.FillEllipse(b, fx - kr, 30 - kr, kr * 2, kr * 2);
        using (var pen = new Pen(Theme.Accent, 2)) g.DrawEllipse(pen, fx - kr, 30 - kr, kr * 2, kr * 2);
    }

    void SetFromX(int x)
    {
        float f = (x - 8f) / Math.Max(1, Width - 16f);
        Value = Min + (int)Math.Round(Util.Clamp(f, 0f, 1f) * (Max - Min));
    }
    protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromX(e.X); Focus(); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _drag = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { Value += Math.Sign(e.Delta); base.OnMouseWheel(e); }
}

class SinToggle : SinControl
{
    bool _c;
    public event EventHandler? CheckedChanged;
    public SinToggle() { Height = 30; }

    public bool Checked
    {
        get => _c;
        set { if (_c == value) return; _c = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }
    public void SetSilently(bool b) { _c = b; Invalidate(); }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        TextRenderer.DrawText(g, Text, Theme.Header, new Rectangle(0, 0, Width - 46, Height),
            Hover ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var pill = new Rectangle(Width - 42, (Height - 20) / 2, 40, 20);
        Theme.FillRound(g, pill, 10, _c ? Theme.Accent : Theme.Panel2, _c ? Theme.Accent : Theme.Border);
        int kx = _c ? pill.Right - 17 : pill.X + 3;
        using (var b = new SolidBrush(_c ? Theme.Bg : Theme.Text)) g.FillEllipse(b, kx, pill.Y + 3, 14, 14);
    }
}

class SinButton : SinControl
{
    public bool Selected;
    public bool Tab;      // sidebar tab look
    public SinButton(string text) { Text = text; Height = 32; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (Tab)
        {
            var r = new Rectangle(10, 2, Width - 20, Height - 4);
            if (Selected || Hover)
                Theme.FillRound(g, r, Theme.Radius, Selected ? Theme.Panel2 : Color.FromArgb(22, 22, 27));
            if (Selected)
                Theme.FillRound(g, new Rectangle(r.X + 8, r.Y + r.Height / 2 - 3, 6, 6), 3, Theme.Accent);
            TextRenderer.DrawText(g, Text, Theme.Header, new Rectangle(r.X + 22, 0, Width - r.X - 22, Height),
                Selected ? Theme.Text : Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            return;
        }

        Color fill = Selected ? Theme.AccentDim : Hover ? Theme.Panel2 : Theme.PanelBg;
        Theme.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), Theme.Radius, fill,
            Selected || Hover ? Theme.Accent : Theme.Border);
        TextRenderer.DrawText(g, Text, Theme.Header, ClientRectangle, Selected ? Theme.Text : Hover ? Theme.Text : Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

class Swatch : SinControl
{
    public Color Swatchcolor;
    public bool Selected;
    public Swatch(Color c) { Swatchcolor = c; Size = new Size(30, 30); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Selected)
        {
            using (var pen = new Pen(Theme.Text, 2)) g.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
            using (var b = new SolidBrush(Swatchcolor)) g.FillEllipse(b, 5, 5, Width - 11, Height - 11);
        }
        else
        {
            using (var b = new SolidBrush(Swatchcolor)) g.FillEllipse(b, 2, 2, Width - 5, Height - 5);
            using (var pen = new Pen(Hover ? Theme.Accent : Theme.Border)) g.DrawEllipse(pen, 2, 2, Width - 5, Height - 5);
        }
    }
}

/// Click it, press a key (combos with Ctrl/Shift/Alt work). ESC cancels, BACKSPACE unbinds.
class KeybindButton : SinControl
{
    Keys _key;
    public bool Listening { get; private set; }
    public bool Failed;
    public event EventHandler? KeyPicked;
    public event EventHandler? ListeningChanged;

    public KeybindButton() { Height = 30; SetStyle(ControlStyles.Selectable, true); TabStop = true; }

    public Keys Key { get => _key; set { _key = value; Invalidate(); } }

    protected override void OnClick(EventArgs e) { if (!Listening) Start(); base.OnClick(e); }

    void Start() { Listening = true; Focus(); Invalidate(); ListeningChanged?.Invoke(this, EventArgs.Empty); }
    void Stop()
    {
        if (!Listening) return;
        Listening = false; Invalidate();
        ListeningChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool IsInputKey(Keys keyData) => Listening || base.IsInputKey(keyData);
    protected override void OnLostFocus(EventArgs e) { Stop(); base.OnLostFocus(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Listening) { base.OnKeyDown(e); return; }
        e.Handled = true;
        e.SuppressKeyPress = true;
        var code = e.KeyCode;
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return; // wait for real key
        if (code == Keys.Escape) { Stop(); return; }
        _key = code == Keys.Back ? Keys.None : code | e.Modifiers;
        KeyPicked?.Invoke(this, EventArgs.Empty);
        Stop();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Color fill = Listening ? Theme.AccentDim : Hover ? Theme.Panel2 : Theme.PanelBg;
        Color border = Listening || Hover ? Theme.Accent : Failed ? Color.FromArgb(150, 150, 158) : Theme.Border;
        Theme.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), Theme.Radius, fill, border);
        string text = Listening ? "PRESS A KEY..." : Util.KeyName(_key) + (Failed ? "  (IN USE)" : "");
        TextRenderer.DrawText(g, text, Theme.Header, ClientRectangle,
            Listening || _key != Keys.None ? Theme.Text : Theme.TextDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// Rounded dark text box.
class SinTextBox : SinControl
{
    public readonly TextBox Box;
    readonly string _cue;

    public SinTextBox(string cue = "")
    {
        _cue = cue;
        Height = 34;
        Cursor = Cursors.IBeam;
        Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Panel2, ForeColor = Theme.Text, Font = Theme.Mono };
        Box.GotFocus += (_, _) => Invalidate();
        Box.LostFocus += (_, _) => Invalidate();
        Box.HandleCreated += (_, _) => Native.SendMessageStr(Box.Handle, Native.EM_SETCUEBANNER, (IntPtr)1, _cue);
        Controls.Add(Box);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Box.SetBounds(12, (Height - Box.Height) / 2, Width - 24, Box.Height);
    }

    protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Theme.FillRound(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Theme.Radius, Theme.Panel2,
            Box.Focused ? Theme.Accent : Theme.Border);
    }
}

class PreviewPanel : SinControl
{
    readonly Func<CrosshairSettings> _get;
    const int Zoom = 3;
    public PreviewPanel(Func<CrosshairSettings> get) { _get = get; Cursor = Cursors.Default; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        int sw = Width / Zoom, sh = Height / Zoom;
        using var bmp = new Bitmap(sw, sh);
        using (var bg = Graphics.FromImage(bmp))
        {
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, sw, sh),
                       Color.FromArgb(120, 124, 130), Color.FromArgb(50, 52, 56), 90f))
                bg.FillRectangle(lg, 0, 0, sw, sh);
            CrosshairRenderer.Draw(bg, _get(), sw / 2, sh / 2);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var clip = Theme.Round(rect, 14))
        {
            g.SetClip(clip);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bmp, new Rectangle(0, 0, sw * Zoom, sh * Zoom));
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.ResetClip();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Border, 1.5f);
            g.DrawPath(pen, clip);
        }
        TextRenderer.DrawText(g, $"{Zoom}x ZOOM", Theme.Tiny, new Point(10, 8), Color.FromArgb(230, 230, 235));
    }
}
