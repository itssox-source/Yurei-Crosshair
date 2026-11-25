using System.Drawing.Drawing2D;

namespace YureiCrosshair;

static class CrosshairRenderer
{
    /// Draws the crosshair centered on (cx, cy). Ignores Enabled.
    public static void Draw(Graphics g, CrosshairSettings s, int cx, int cy)
    {
        int alpha = Util.Clamp(s.Opacity, 0, 100) * 255 / 100;
        var col = Color.FromArgb(alpha, Color.FromArgb(s.ColorArgb));
        var ocol = Color.FromArgb(alpha, Color.FromArgb(s.OutlineArgb));
        int o = s.Outline ? Math.Max(1, s.OutlineThickness) : 0;
        int t = Math.Max(1, s.Thickness);
        int L = Math.Max(0, s.Length);
        int G = Math.Max(0, s.Gap);

        // ---- pixel-perfect rectangles (cross arms + dot) ----
        var rects = new List<Rectangle>();
        int bx = cx - t / 2, by = cy - t / 2;

        if ((s.Style == CrossStyle.Cross || s.Style == CrossStyle.TShape) && L > 0)
        {
            rects.Add(new Rectangle(bx + t + G, by, L, t));   // right
            rects.Add(new Rectangle(bx - G - L, by, L, t));   // left
            rects.Add(new Rectangle(bx, by + t + G, t, L));   // down
            if (s.Style == CrossStyle.Cross)
                rects.Add(new Rectangle(bx, by - G - L, t, L)); // up
        }

        bool dot = s.Style == CrossStyle.Dot || s.Style == CrossStyle.CircleDot || s.CenterDot;
        if (dot)
        {
            int d = Math.Max(1, s.DotSize);
            rects.Add(new Rectangle(cx - d / 2, cy - d / 2, d, d));
        }

        // ---- smooth shapes (circle / X) ----
        bool circle = s.Style == CrossStyle.Circle || s.Style == CrossStyle.CircleDot;
        bool xshape = s.Style == CrossStyle.XShape && L > 0;
        int r = Math.Max(1, s.CircleRadius);
        const float D = 0.70710678f;
        (float dx, float dy)[] diag = { (D, D), (-D, D), (D, -D), (-D, -D) };

        // outlines first, so they never cover the fill
        if (o > 0)
        {
            g.SmoothingMode = SmoothingMode.None;
            using (var ob = new SolidBrush(ocol))
                foreach (var rc in rects) { var x = rc; x.Inflate(o, o); g.FillRectangle(ob, x); }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var op = new Pen(ocol, t + o * 2);
            if (circle) g.DrawEllipse(op, cx - r, cy - r, r * 2, r * 2);
            if (xshape)
                foreach (var (dx, dy) in diag)
                    g.DrawLine(op, cx + dx * (G - o), cy + dy * (G - o), cx + dx * (G + L + o), cy + dy * (G + L + o));
        }

        g.SmoothingMode = SmoothingMode.None;
        using (var b = new SolidBrush(col))
            foreach (var rc in rects) g.FillRectangle(b, rc);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = new Pen(col, t))
        {
            if (circle) g.DrawEllipse(p, cx - r, cy - r, r * 2, r * 2);
            if (xshape)
                foreach (var (dx, dy) in diag)
                    g.DrawLine(p, cx + dx * G, cy + dy * G, cx + dx * (G + L), cy + dy * (G + L));
        }
        g.SmoothingMode = SmoothingMode.None;
    }
}
