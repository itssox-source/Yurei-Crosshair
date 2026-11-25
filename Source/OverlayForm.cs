using System.Drawing.Imaging;

namespace YureiCrosshair;

/// Invisible, click-through, always-on-top window that only draws the crosshair.
class OverlayForm : Form
{
    const int SZ = 512;
    readonly Func<CrosshairSettings> _get;
    readonly System.Windows.Forms.Timer _topTimer;

    public OverlayForm(Func<CrosshairSettings> get)
    {
        _get = get;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(SZ, SZ);

        // games love stealing topmost, so keep re-asserting it
        _topTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _topTimer.Tick += (_, _) =>
        {
            if (IsHandleCreated && !Native.IsMono)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        };
        _topTimer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                        | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Redraw();
    }

    public void Redraw()
    {
        if (!IsHandleCreated) return;
        var s = _get();
        var scr = Screen.PrimaryScreen!.Bounds;
        int x = scr.X + scr.Width / 2 - SZ / 2 + s.OffsetX;
        int y = scr.Y + scr.Height / 2 - SZ / 2 + s.OffsetY;

        using var bmp = new Bitmap(SZ, SZ, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            if (s.Enabled) CrosshairRenderer.Draw(g, s, SZ / 2, SZ / 2);
        }
        Push(bmp, x, y);
    }

    void Push(Bitmap bmp, int x, int y)
    {
        if (Native.IsMono) return;
        IntPtr screenDc = Native.GetDC(IntPtr.Zero);
        IntPtr memDc = Native.CreateCompatibleDC(screenDc);
        IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            old = Native.SelectObject(memDc, hBmp);
            var size = new Native.SIZE(bmp.Width, bmp.Height);
            var src = new Native.POINT(0, 0);
            var dst = new Native.POINT(x, y);
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, screenDc);
            if (hBmp != IntPtr.Zero)
            {
                Native.SelectObject(memDc, old);
                Native.DeleteObject(hBmp);
            }
            Native.DeleteDC(memDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _topTimer.Dispose();
        base.Dispose(disposing);
    }
}
