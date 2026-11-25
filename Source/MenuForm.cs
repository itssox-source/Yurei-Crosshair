using System.Drawing.Drawing2D;

namespace YureiCrosshair;

class MenuForm : Form
{
    const int W = 780, H = 500, TitleH = 44, SideW = 170, ContentW = 350;
    const int HK_MENU = 1, HK_TOGGLE = 2, HK_TASKBAR = 3;

    readonly AppConfig _cfg;
    CrosshairSettings S => _cfg.Current;
    AppOptions Opt => _cfg.Options;

    readonly OverlayForm _overlay;
    readonly System.Windows.Forms.Timer _saveTimer;
    readonly System.Windows.Forms.Timer _statusTimer;
    readonly List<Action> _refreshers = new();
    readonly List<Panel> _pages = new();
    readonly List<SinButton> _tabs = new();
    readonly List<Bind> _binds = new();

    NotifyIcon _tray = null!;
    ToolStripMenuItem _trayOpen = null!, _trayToggle = null!, _trayTaskbar = null!;
    SinToggle _master = null!;
    PreviewPanel _preview = null!;
    Label _info = null!, _bindStatus = null!, _shareStatus = null!;
    SinTextBox _shareBox = null!;
    Panel _content = null!, _side = null!;
    bool _exiting;
    int _y;

    class Bind
    {
        public int Id;
        public string Name = "";
        public Func<Keys> Get = () => Keys.None;
        public Action<Keys> Set = _ => { };
        public KeybindButton Btn = null!;
    }

    public MenuForm()
    {
        _cfg = AppConfig.Load();

        Text = "Yurei Crosshair";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(W, H);
        BackColor = Theme.PanelBg;
        DoubleBuffered = true;
        ShowInTaskbar = !Opt.HideFromTaskbar;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        _overlay = new OverlayForm(() => S);
        _overlay.Show();

        _saveTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _cfg.Save(); };
        _statusTimer = new System.Windows.Forms.Timer { Interval = 3500 };
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); _shareStatus.Text = ""; };

        BuildTitleBar();
        BuildSidebar();
        BuildPages();
        BuildPreview();
        BuildTray();
        SelectTab(0);
        RefreshAll();
    }

    // ================= window / rounding =================
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Windows 11: real rounded corners. Windows 10: clip the window to a rounded shape.
        bool rounded = false;
        if (!Native.IsMono) try
        {
            int pref = Native.DWMWCP_ROUND;
            rounded = Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)) == 0;
        }
        catch { }
        if (!rounded)
            using (var path = Theme.Round(new Rectangle(0, 0, W, H), Theme.WindowRadius))
                Region = new Region(path);

        RegisterHotkeys();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotkeys();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Border);
        using var path = Theme.Round(new Rectangle(0, 0, W - 1, H - 1), Theme.WindowRadius);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    void ExitApp()
    {
        _exiting = true;
        _cfg.Save();
        _tray.Visible = false;
        _tray.Dispose();
        _overlay.Close();
        Application.Exit();
    }

    // ================= hotkeys =================
    void RegisterHotkeys()
    {
        if (!IsHandleCreated || Native.IsMono) return;
        UnregisterHotkeys();
        var failed = new List<string>();
        foreach (var b in _binds)
        {
            var k = b.Get();
            b.Btn.Failed = false;
            if ((k & Keys.KeyCode) != Keys.None)
            {
                var (mods, vk) = Util.ToHotkey(k);
                if (!Native.RegisterHotKey(Handle, b.Id, mods, vk))
                {
                    b.Btn.Failed = true;
                    failed.Add(Util.KeyName(k));
                }
            }
            b.Btn.Invalidate();
        }
        if (_bindStatus != null)
            _bindStatus.Text = failed.Count == 0 ? "" :
                "Another app is already using: " + string.Join(", ", failed) + "\nPick a different key.";
    }

    void UnregisterHotkeys()
    {
        if (!IsHandleCreated || Native.IsMono) return;
        Native.UnregisterHotKey(Handle, HK_MENU);
        Native.UnregisterHotKey(Handle, HK_TOGGLE);
        Native.UnregisterHotKey(Handle, HK_TASKBAR);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            BeginInvoke(new Action(() => OnHotkey(id)));   // run after this message, safe to recreate handle
        }
        base.WndProc(ref m);
    }

    void OnHotkey(int id)
    {
        switch (id)
        {
            case HK_MENU: ToggleMenu(); break;
            case HK_TOGGLE: S.Enabled = !S.Enabled; Changed(); RefreshAll(); break;
            case HK_TASKBAR: SetHideFromTaskbar(!Opt.HideFromTaskbar); break;
        }
    }

    void ToggleMenu()
    {
        if (Visible && WindowState != FormWindowState.Minimized) Hide();
        else ShowMenu();
    }

    void ShowMenu()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void SetHideFromTaskbar(bool hide)
    {
        Opt.HideFromTaskbar = hide;
        if (hide && WindowState == FormWindowState.Minimized) { WindowState = FormWindowState.Normal; Hide(); }
        if (ShowInTaskbar == hide) ShowInTaskbar = !hide;   // recreates the window handle; hotkeys re-register
        QueueSave();
        RefreshAll();
    }

    // ================= state =================
    void QueueSave() { _saveTimer.Stop(); _saveTimer.Start(); }

    void Changed()
    {
        _overlay.Redraw();
        _preview.Invalidate();
        UpdateInfo();
        QueueSave();
    }

    void RefreshAll()
    {
        foreach (var r in _refreshers) r();
        _master.SetSilently(S.Enabled);
        _master.Text = S.Enabled ? "CROSSHAIR ON" : "CROSSHAIR OFF";
        _trayToggle.Checked = S.Enabled;
        _trayTaskbar.Checked = Opt.HideFromTaskbar;
        _trayOpen.Text = $"Open menu  ({Util.KeyName((Keys)Opt.KeyMenu)})";
        _trayToggle.Text = $"Crosshair on  ({Util.KeyName((Keys)Opt.KeyToggle)})";
        _trayTaskbar.Text = $"Hide from taskbar  ({Util.KeyName((Keys)Opt.KeyTaskbar)})";
        _side.Invalidate();
        _preview.Invalidate();
        UpdateInfo();
    }

    void UpdateInfo()
    {
        var c = Color.FromArgb(S.ColorArgb);
        _info.Text = $"STYLE   {StyleName(S.Style)}\nCOLOR   #{c.R:X2}{c.G:X2}{c.B:X2}\nOFFSET  {S.OffsetX}, {S.OffsetY}";
    }

    void Status(string text)
    {
        _shareStatus.Text = text;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    static string StyleName(CrossStyle s) => s switch
    {
        CrossStyle.Cross => "CROSS",
        CrossStyle.TShape => "T-SHAPE",
        CrossStyle.Dot => "DOT",
        CrossStyle.Circle => "CIRCLE",
        CrossStyle.CircleDot => "CIRCLE + DOT",
        CrossStyle.XShape => "X",
        _ => "?"
    };

    static bool TryCopy(string text)
    {
        try { Clipboard.SetText(text); return true; } catch { return false; }
    }

    // ================= layout =================
    void BuildTitleBar()
    {
        var bar = new Panel { Bounds = new Rectangle(0, 0, W, TitleH), BackColor = Theme.PanelBg };
        bar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            int x = 14;
            if (Theme.Logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var lr = new Rectangle(x, 5, 34, 34);
                using (var clip = Theme.Round(lr, 9))
                {
                    g.SetClip(clip);
                    g.DrawImage(Theme.Logo, lr);
                    g.ResetClip();
                    using var pen = new Pen(Theme.Border);
                    g.DrawPath(pen, clip);
                }
                x += 44;
            }
            var sz = TextRenderer.MeasureText(g, "YUREI", Theme.Title, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "YUREI", Theme.Title, new Point(x, 11), Theme.Accent, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "CROSSHAIR", Theme.Title, new Point(x + sz.Width + 6, 11), Theme.TextDim, TextFormatFlags.NoPadding);
            var sz2 = TextRenderer.MeasureText(g, "CROSSHAIR", Theme.Title, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "v1.1", Theme.Tiny, new Point(x + sz.Width + sz2.Width + 14, 18), Theme.TextDim);

            using var lg = new LinearGradientBrush(new Rectangle(0, TitleH - 2, W, 2),
                Theme.Accent, Color.FromArgb(0, Theme.Accent), 0f);
            g.FillRectangle(lg, 0, TitleH - 2, W, 2);
        };
        bar.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (Native.IsMono) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        };

        var close = new SinButton("✕") { Bounds = new Rectangle(W - 44, 8, 32, 28), BackColor = Theme.PanelBg };
        close.Click += (_, _) => Hide();
        var min = new SinButton("—") { Bounds = new Rectangle(W - 82, 8, 32, 28), BackColor = Theme.PanelBg };
        min.Click += (_, _) =>
        {
            if (Opt.HideFromTaskbar) Hide();             // nowhere to minimize to
            else WindowState = FormWindowState.Minimized;
        };
        bar.Controls.Add(close);
        bar.Controls.Add(min);
        Controls.Add(bar);
    }

    void BuildSidebar()
    {
        _side = new Panel { Bounds = new Rectangle(0, TitleH, SideW, H - TitleH), BackColor = Theme.PanelBg };
        string[] names = { "CROSSHAIR", "COLOR", "POSITION", "PROFILES", "SETTINGS" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var t = new SinButton(names[i]) { Tab = true, Bounds = new Rectangle(0, 14 + i * 40, SideW, 38), BackColor = Theme.PanelBg };
            t.Click += (_, _) => SelectTab(idx);
            _tabs.Add(t);
            _side.Controls.Add(t);
        }

        _master = new SinToggle { Bounds = new Rectangle(18, _side.Height - 136, SideW - 34, 30), BackColor = Theme.PanelBg };
        _master.CheckedChanged += (_, _) => { S.Enabled = _master.Checked; Changed(); RefreshAll(); };
        _side.Controls.Add(_master);

        _side.Paint += (_, e) =>
        {
            var g = e.Graphics;
            int by = _side.Height - 94;
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 18, by - 8, SideW - 18, by - 8);
            DrawKey(g, Util.KeyName((Keys)Opt.KeyMenu), "menu", by);
            DrawKey(g, Util.KeyName((Keys)Opt.KeyToggle), "on / off", by + 24);
            DrawKey(g, Util.KeyName((Keys)Opt.KeyTaskbar), "taskbar", by + 48);
        };
        Controls.Add(_side);
    }

    static void DrawKey(Graphics g, string key, string what, int y)
    {
        var sz = TextRenderer.MeasureText(key, Theme.Tiny);
        var r = new Rectangle(18, y, Math.Max(22, sz.Width + 8), 18);
        Theme.FillRound(g, r, 6, Theme.Panel2);
        TextRenderer.DrawText(g, key, Theme.Tiny, r, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, what, Theme.Tiny, new Point(r.Right + 8, y + 3), Theme.TextDim);
    }

    void SelectTab(int i)
    {
        for (int k = 0; k < _pages.Count; k++) _pages[k].Visible = k == i;
        for (int k = 0; k < _tabs.Count; k++) { _tabs[k].Selected = k == i; _tabs[k].Invalidate(); }
    }

    // ================= page helpers =================
    Panel NewPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Visible = false };
        _content.Controls.Add(p);
        _pages.Add(p);
        _y = 22;
        return p;
    }

    void Header(Panel p, string text)
    {
        p.Controls.Add(new Label
        {
            Text = text, Font = Theme.Header, ForeColor = Theme.Accent, BackColor = Theme.Bg,
            AutoSize = true, Location = new Point(24, _y)
        });
        _y += 24;
    }

    Label SmallLabel(Panel p, string text, int height, Color? color = null)
    {
        var l = new Label
        {
            Text = text, Font = Theme.Tiny, ForeColor = color ?? Theme.TextDim, BackColor = Theme.Bg,
            Location = new Point(24, _y), Size = new Size(300, height)
        };
        p.Controls.Add(l);
        _y += height;
        return l;
    }

    void Slider(Panel p, string label, int min, int max, Func<int> get, Action<int> set, string suffix = "")
    {
        var s = new SinSlider { Label = label, Min = min, Max = max, Suffix = suffix, Bounds = new Rectangle(24, _y, 300, 44) };
        s.SetSilently(get());
        s.ValueChanged += (_, _) => { set(s.Value); Changed(); };
        _refreshers.Add(() => s.SetSilently(get()));
        p.Controls.Add(s);
        _y += 48;
    }

    void Toggle(Panel p, string label, Func<bool> get, Action<bool> set)
    {
        var t = new SinToggle { Text = label, Bounds = new Rectangle(24, _y, 300, 30) };
        t.SetSilently(get());
        t.CheckedChanged += (_, _) => set(t.Checked);
        _refreshers.Add(() => t.SetSilently(get()));
        p.Controls.Add(t);
        _y += 36;
    }

    SinButton Button(Panel p, string text, Action onClick, int x = 24, int w = 300, bool advance = true)
    {
        var b = new SinButton(text) { Bounds = new Rectangle(x, _y, w, 30) };
        b.Click += (_, _) => onClick();
        p.Controls.Add(b);
        if (advance) _y += 38;
        return b;
    }

    void Swatches(Panel p, Color[] colors, Func<int> get, Action<int> set)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            var c = colors[i];
            var sw = new Swatch(c) { Location = new Point(24 + i * 38, _y) };
            sw.Click += (_, _) => { set(c.ToArgb()); Changed(); RefreshAll(); };
            _refreshers.Add(() => { sw.Selected = get() == c.ToArgb(); sw.Invalidate(); });
            p.Controls.Add(sw);
        }
        _y += 40;
    }

    void Keybind(Panel p, int id, string label, Func<Keys> get, Action<Keys> set)
    {
        p.Controls.Add(new Label
        {
            Text = label, Font = Theme.Header, ForeColor = Theme.Text, BackColor = Theme.Bg,
            AutoSize = true, Location = new Point(24, _y + 8)
        });
        var btn = new KeybindButton { Bounds = new Rectangle(176, _y, 148, 30), Key = get() };
        var bind = new Bind { Id = id, Name = label, Get = get, Set = set, Btn = btn };

        // while waiting for a key, release all hotkeys so the key reaches us
        btn.ListeningChanged += (_, _) =>
        {
            if (_binds.Any(b => b.Btn.Listening)) UnregisterHotkeys();
            else RegisterHotkeys();
        };
        btn.KeyPicked += (_, _) =>
        {
            var k = btn.Key;
            if ((k & Keys.KeyCode) != Keys.None)
                foreach (var other in _binds)
                    if (other != bind && other.Get() == k) { other.Set(Keys.None); other.Btn.Key = Keys.None; }
            set(k);
            QueueSave();
            RefreshAll();
        };
        _refreshers.Add(() => btn.Key = get());
        _binds.Add(bind);
        p.Controls.Add(btn);
        _y += 38;
    }

    // ================= pages =================
    void BuildPages()
    {
        _content = new Panel { Bounds = new Rectangle(SideW, TitleH, ContentW, H - TitleH), BackColor = Theme.Bg };
        Controls.Add(_content);

        // ---------- CROSSHAIR ----------
        var p = NewPage();
        Header(p, "STYLE");
        var styles = new[] { CrossStyle.Cross, CrossStyle.TShape, CrossStyle.Dot, CrossStyle.Circle, CrossStyle.CircleDot, CrossStyle.XShape };
        for (int i = 0; i < styles.Length; i++)
        {
            var st = styles[i];
            var b = new SinButton(StyleName(st)) { Bounds = new Rectangle(24 + (i % 3) * 102, _y + (i / 3) * 36, 96, 30) };
            b.Click += (_, _) => { S.Style = st; Changed(); RefreshAll(); };
            _refreshers.Add(() => { b.Selected = S.Style == st; b.Invalidate(); });
            p.Controls.Add(b);
        }
        _y += 80;
        Header(p, "SHAPE");
        Slider(p, "LENGTH", 0, 60, () => S.Length, v => S.Length = v, " px");
        Slider(p, "THICKNESS", 1, 12, () => S.Thickness, v => S.Thickness = v, " px");
        Slider(p, "GAP", 0, 40, () => S.Gap, v => S.Gap = v, " px");
        Slider(p, "CIRCLE RADIUS", 2, 60, () => S.CircleRadius, v => S.CircleRadius = v, " px");
        Slider(p, "DOT SIZE", 1, 12, () => S.DotSize, v => S.DotSize = v, " px");
        Toggle(p, "CENTER DOT", () => S.CenterDot, v => { S.CenterDot = v; Changed(); });

        // ---------- COLOR ----------
        p = NewPage();
        Header(p, "CROSSHAIR COLOR");
        Swatches(p, new[]
        {
            Color.FromArgb(255, 255, 255), Color.FromArgb(0, 255, 0), Color.FromArgb(0, 255, 255), Color.FromArgb(255, 255, 0),
            Color.FromArgb(255, 0, 255), Color.FromArgb(255, 34, 54), Color.FromArgb(255, 140, 0), Color.FromArgb(255, 105, 180)
        }, () => S.ColorArgb, v => S.ColorArgb = v);
        Button(p, "CUSTOM COLOR...", () => PickColor(false));
        Slider(p, "OPACITY", 10, 100, () => S.Opacity, v => S.Opacity = v, "%");
        _y += 6;
        Header(p, "OUTLINE");
        Toggle(p, "OUTLINE", () => S.Outline, v => { S.Outline = v; Changed(); });
        Slider(p, "OUTLINE THICKNESS", 1, 4, () => S.OutlineThickness, v => S.OutlineThickness = v, " px");
        Swatches(p, new[] { Color.FromArgb(0, 0, 0), Color.FromArgb(255, 255, 255), Color.FromArgb(60, 60, 60) },
            () => S.OutlineArgb, v => S.OutlineArgb = v);
        Button(p, "CUSTOM OUTLINE COLOR...", () => PickColor(true));

        // ---------- POSITION ----------
        p = NewPage();
        Header(p, "OFFSET FROM SCREEN CENTER");
        Slider(p, "OFFSET X", -300, 300, () => S.OffsetX, v => S.OffsetX = v, " px");
        Slider(p, "OFFSET Y", -300, 300, () => S.OffsetY, v => S.OffsetY = v, " px");
        Button(p, "RESET TO CENTER", () => { S.OffsetX = 0; S.OffsetY = 0; Changed(); RefreshAll(); });
        _y += 10;
        Header(p, "NOTES");
        SmallLabel(p,
            "• The crosshair draws on your primary monitor.\n\n" +
            "• Games must be in Borderless or Windowed mode.\n   Exclusive fullscreen hides overlays.\n\n" +
            "• Closing this window keeps the crosshair running.\n   Use the tray icon to exit.", 150);

        // ---------- PROFILES ----------
        p = NewPage();
        Header(p, "SAVED PROFILES");
        for (int i = 0; i < AppConfig.SlotCount; i++)
        {
            int slot = i;
            p.Controls.Add(new Label { Text = $"SLOT {i + 1}", Font = Theme.Header, ForeColor = Theme.Text, BackColor = Theme.Bg, AutoSize = true, Location = new Point(24, _y + 8) });
            var status = new Label { Font = Theme.Tiny, BackColor = Theme.Bg, AutoSize = true, Location = new Point(76, _y + 9) };
            _refreshers.Add(() =>
            {
                bool has = _cfg.Slots[slot] != null;
                status.Text = has ? "SAVED" : "EMPTY";
                status.ForeColor = has ? Theme.Text : Theme.TextDim;
            });
            p.Controls.Add(status);
            Button(p, "SAVE", () => { _cfg.Slots[slot] = S.Clone(); _cfg.Save(); RefreshAll(); Status($"Saved to slot {slot + 1}."); }, 134, 60, false);
            Button(p, "LOAD", () =>
            {
                var s = _cfg.Slots[slot];
                if (s == null) { Status($"Slot {slot + 1} is empty."); return; }
                ApplySettings(s);
                Status($"Loaded slot {slot + 1}.");
            }, 198, 60, false);
            Button(p, "SHARE", () =>
            {
                var s = _cfg.Slots[slot];
                if (s == null) { Status($"Slot {slot + 1} is empty."); return; }
                var code = s.ToShareCode();
                _shareBox.Box.Text = code;
                Status(TryCopy(code) ? $"Slot {slot + 1} code copied. Send it to the gang." : "Code is in the box. Copy it from there.");
            }, 262, 62);
        }

        _y += 4;
        Header(p, "SHARE CODES");
        _shareBox = new SinTextBox("Paste a YUREI- code here...") { Bounds = new Rectangle(24, _y, 300, 34) };
        p.Controls.Add(_shareBox);
        _y += 40;
        Button(p, "COPY MY CROSSHAIR", () =>
        {
            var code = S.ToShareCode();
            _shareBox.Box.Text = code;
            Status(TryCopy(code) ? "Copied. Send it to the gang." : "Code is in the box. Copy it from there.");
        }, 24, 146, false);
        Button(p, "IMPORT CODE", () =>
        {
            string text = _shareBox.Box.Text.Trim();
            if (text.Length == 0) try { text = Clipboard.GetText().Trim(); } catch { }
            var s = CrosshairSettings.FromShareCode(text);
            if (s == null) { Status("That's not a valid Yurei code."); return; }
            ApplySettings(s);
            Status("Imported. Save it to a slot to keep it.");
        }, 178, 146);
        _shareStatus = SmallLabel(p, "", 18, Theme.Text);

        _y += 4;
        Header(p, "RESET");
        Button(p, "RESET CROSSHAIR TO DEFAULT", () => { ApplySettings(new CrosshairSettings()); Status("Reset to default."); });

        // ---------- SETTINGS ----------
        p = NewPage();
        Header(p, "KEYBINDS");
        Keybind(p, HK_MENU, "OPEN / CLOSE MENU", () => (Keys)Opt.KeyMenu, k => Opt.KeyMenu = (int)k);
        Keybind(p, HK_TOGGLE, "CROSSHAIR ON / OFF", () => (Keys)Opt.KeyToggle, k => Opt.KeyToggle = (int)k);
        Keybind(p, HK_TASKBAR, "HIDE FROM TASKBAR", () => (Keys)Opt.KeyTaskbar, k => Opt.KeyTaskbar = (int)k);
        SmallLabel(p, "Click a bind, then press a key. Ctrl / Shift / Alt combos work.\nESC = cancel     BACKSPACE = unbind", 34);
        _bindStatus = SmallLabel(p, "", 32, Theme.Text);
        Button(p, "RESET KEYBINDS", () =>
        {
            var d = new AppOptions();
            Opt.KeyMenu = d.KeyMenu; Opt.KeyToggle = d.KeyToggle; Opt.KeyTaskbar = d.KeyTaskbar;
            RegisterHotkeys(); QueueSave(); RefreshAll();
        });

        _y += 6;
        Header(p, "WINDOW");
        Toggle(p, "HIDE FROM TASKBAR", () => Opt.HideFromTaskbar, v => BeginInvoke(new Action(() => SetHideFromTaskbar(v))));
        SmallLabel(p, "When hidden, the app only lives in the tray.\nOpen the menu with your menu key or the tray icon.", 34);
        SmallLabel(p, "Settings save to %AppData%\\YureiCrosshair\\config.ini", 18);
    }

    void ApplySettings(CrosshairSettings s)
    {
        bool en = S.Enabled;
        _cfg.Current = s.Clone();
        S.Enabled = en;
        Changed();
        RefreshAll();
    }

    void PickColor(bool outline)
    {
        using var dlg = new ColorDialog { FullOpen = true, Color = Color.FromArgb(outline ? S.OutlineArgb : S.ColorArgb) };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        int argb = Color.FromArgb(255, dlg.Color).ToArgb();
        if (outline) S.OutlineArgb = argb; else S.ColorArgb = argb;
        Changed(); RefreshAll();
    }

    void BuildPreview()
    {
        var col = new Panel { Bounds = new Rectangle(SideW + ContentW, TitleH, W - SideW - ContentW, H - TitleH), BackColor = Theme.Bg };
        col.Controls.Add(new Label { Text = "PREVIEW", Font = Theme.Header, ForeColor = Theme.Accent, BackColor = Theme.Bg, AutoSize = true, Location = new Point(10, 22) });
        _preview = new PreviewPanel(() => S) { Bounds = new Rectangle(10, 46, 240, 240) };
        col.Controls.Add(_preview);

        _info = new Label
        {
            Font = new Font("Consolas", 8.5f, FontStyle.Bold), ForeColor = Theme.TextDim, BackColor = Theme.Bg,
            Location = new Point(10, 298), Size = new Size(240, 60)
        };
        col.Controls.Add(_info);

        var exit = new SinButton("EXIT APP") { Bounds = new Rectangle(10, col.Height - 50, 240, 32) };
        exit.Click += (_, _) => ExitApp();
        col.Controls.Add(exit);
        Controls.Add(col);
    }

    void BuildTray()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false, BackColor = Theme.PanelBg, ForeColor = Theme.Text };
        _trayOpen = new ToolStripMenuItem("Open menu");
        _trayOpen.Click += (_, _) => ShowMenu();
        _trayToggle = new ToolStripMenuItem("Crosshair on");
        _trayToggle.Click += (_, _) => { S.Enabled = !S.Enabled; Changed(); RefreshAll(); };
        _trayTaskbar = new ToolStripMenuItem("Hide from taskbar");
        _trayTaskbar.Click += (_, _) => SetHideFromTaskbar(!Opt.HideFromTaskbar);
        menu.Items.Add(_trayOpen);
        menu.Items.Add(_trayToggle);
        menu.Items.Add(_trayTaskbar);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "Yurei Crosshair",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowMenu();
    }
}
