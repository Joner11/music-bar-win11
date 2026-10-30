using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NowPlaying
{
    // =====================================================================
    //  Палитра и мелкие помощники рисования
    // =====================================================================
    static class Theme
    {
        public static readonly Color Bg      = Color.FromArgb(20, 20, 23);
        public static readonly Color Sidebar = Color.FromArgb(15, 15, 18);
        public static readonly Color Card    = Color.FromArgb(30, 30, 35);
        public static readonly Color CardHi  = Color.FromArgb(37, 37, 43);
        public static readonly Color Field   = Color.FromArgb(24, 24, 28);
        public static readonly Color Text    = Color.FromArgb(240, 240, 244);
        public static readonly Color Sub     = Color.FromArgb(140, 140, 150);
        public static readonly Color Accent  = Color.FromArgb(76, 194, 255);
        public static readonly Color Track   = Color.FromArgb(58, 58, 66);
        public static readonly Color Sep     = Color.FromArgb(44, 44, 51);

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Font F(float px, bool bold)
        {
            return new Font("Segoe UI", px, bold ? FontStyle.Bold : FontStyle.Regular,
                            GraphicsUnit.Point);
        }
    }

    static class Draw
    {
        public static GraphicsPath Round(Rectangle r, int radius)
        {
            return Renderer.RoundRect(new RectangleF(r.X, r.Y, r.Width, r.Height), radius);
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color c)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius))
            using (var pen = new Pen(c))
                g.DrawPath(pen, p);
        }

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r,
                                StringAlignment h, StringAlignment v)
        {
            using (var sf = new StringFormat())
            {
                sf.Alignment = h; sf.LineAlignment = v;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (var b = new SolidBrush(c))
                    g.DrawString(s, f, b, r, sf);
            }
        }

        // Треугольник вниз (для выпадающих списков)
        public static void Chevron(Graphics g, Rectangle r, Color c)
        {
            using (var pen = new Pen(c, 1.6f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                g.DrawLines(pen, new Point[] {
                    new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
        }

        public static void Check(Graphics g, Rectangle r, Color c)
        {
            using (var pen = new Pen(c, 2f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new Point[] {
                    new Point(r.X, r.Y + r.Height / 2),
                    new Point(r.X + r.Width / 3, r.Y + r.Height - 2),
                    new Point(r.Right, r.Y + 2) });
            }
        }
    }

    // =====================================================================
    //  Слайдер вместо NumericUpDown
    // =====================================================================
    class Slider : Control
    {
        public int Min = 0, Max = 100, Step = 1, Divider = 1;
        public string Suffix = "";
        public int Decimals = 0;

        int val = 50;
        bool drag, hover;

        public event EventHandler ValueChanged;

        public Slider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 26;
            Min = 0; Max = 100; Step = 1; Divider = 1;
        }

        public int RawValue
        {
            get { return val; }
            set
            {
                int v = value;
                if (v < Min) v = Min;
                if (v > Max) v = Max;
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public double Value
        {
            get { return (double)val / Divider; }
            set
            {
                int v = (int)Math.Round(value * Divider);
                RawValue = v;
            }
        }

        public string DisplayValue()
        {
            double d = (double)val / Divider;
            return d.ToString("F" + Decimals.ToString(), System.Globalization.CultureInfo.InvariantCulture) + Suffix;
        }

        Rectangle TrackRect()
        {
            int knob = 16;
            return new Rectangle(knob / 2 + 1, Height / 2 - 2, Width - knob - 2, 4);
        }

        int KnobX()
        {
            var tr = TrackRect();
            double t = (double)(val - Min) / Math.Max(1, Max - Min);
            return tr.X + (int)Math.Round(t * tr.Width);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card))
                g.FillRectangle(bg, ClientRectangle);

            var tr = TrackRect();
            Draw.FillRound(g, tr, 2, Theme.Track);
            int kx = KnobX();
            if (kx > tr.X)
            {
                int w = kx - tr.X;
                Draw.FillRound(g, new Rectangle(tr.X, tr.Y, w, tr.Height), 2,
                    Enabled ? Theme.Accent : Color.FromArgb(80, 80, 90));
            }
            int kr = (hover || drag) ? 8 : 7;
            Color knob = Enabled ? Color.White : Color.FromArgb(150, 150, 158);
            using (var b = new SolidBrush(knob))
                g.FillEllipse(b, kx - kr, Height / 2 - kr, kr * 2, kr * 2);
            if (Focused)
            {
                using (var b = new SolidBrush(Color.FromArgb(70, Theme.Accent)))
                    g.FillEllipse(b, kx - kr - 3, Height / 2 - kr - 3, (kr + 3) * 2, (kr + 3) * 2);
            }
        }

        void SetFromX(int x)
        {
            var tr = TrackRect();
            double t = (double)(x - tr.X) / Math.Max(1, tr.Width);
            if (t < 0) t = 0; if (t > 1) t = 1;
            int v = Min + (int)Math.Round(t * (Max - Min));
            if (Step > 1) v = Min + (int)Math.Round((double)(v - Min) / Step) * Step;
            RawValue = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();
            drag = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag) SetFromX(e.X);
            bool h = ClientRectangle.Contains(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            drag = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = false; Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hover = true; Invalidate();
        }

        protected override bool IsInputKey(Keys k)
        {
            if (k == Keys.Left || k == Keys.Right || k == Keys.Home || k == Keys.End) return true;
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int s = Math.Max(1, Step);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) RawValue = val - s;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) RawValue = val + s;
            else if (e.KeyCode == Keys.Home) RawValue = Min;
            else if (e.KeyCode == Keys.End) RawValue = Max;
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // =====================================================================
    //  Переключатель
    // =====================================================================
    class ToggleSwitch : Control
    {
        bool check;
        float anim;          // 0..1
        Timer timer;
        bool hover;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Height = 26;
            Width = 120;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            timer = new Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
        }

        public bool Checked
        {
            get { return check; }
            set
            {
                if (check == value) return;
                check = value;
                timer.Start();
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Установить без события (при загрузке конфига).</summary>
        public void SetSilent(bool v)
        {
            check = v; anim = v ? 1f : 0f; Invalidate();
        }

        void OnTick(object s, EventArgs e)
        {
            float target = check ? 1f : 0f;
            anim += (target - anim) * 0.35f;
            if (Math.Abs(target - anim) < 0.02f) { anim = target; timer.Stop(); }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Parent != null)
            {
                using (var b = new SolidBrush(Parent.BackColor))
                    g.FillRectangle(b, ClientRectangle);
            }

            int w = 42, h = 22;
            int x = 1, y = (Height - h) / 2;
            Color off = Theme.Mix(Theme.Track, Theme.Track, 1f);
            Color on = Theme.Accent;
            if (!Enabled) { off = Color.FromArgb(45, 45, 52); on = Color.FromArgb(80, 100, 115); }
            Color track = Theme.Mix(off, on, anim);

            Draw.FillRound(g, new Rectangle(x, y, w, h), h / 2, track);
            if (hover || Focused)
                Draw.StrokeRound(g, new Rectangle(x, y, w, h), h / 2,
                    Color.FromArgb(Focused ? 140 : 70, Theme.Accent));

            int kr = 9;
            int cx = x + kr + 2 + (int)Math.Round(anim * (w - 2 * kr - 4));
            using (var b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(150, 150, 158)))
                g.FillEllipse(b, cx - kr, y + h / 2 - kr, kr * 2, kr * 2);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();
            Checked = !check;
        }

        protected override bool IsInputKey(Keys k) { if (k == Keys.Space) return true; return base.IsInputKey(k); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) Checked = !check;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // =====================================================================
    //  Образец цвета
    // =====================================================================
    class ColorSwatch : Control
    {
        public Color Value = Color.White;
        public event EventHandler ValueChanged;
        bool hover;

        public ColorSwatch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 26;
            Cursor = Cursors.Hand;
        }

        public void SetSilent(Color c) { Value = c; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card))
                g.FillRectangle(b, ClientRectangle);

            int h = 22;
            int y = (Height - h) / 2;
            int w = Math.Min(Width - 1, 96);
            var r = new Rectangle(1, y, w, h);

            // шахматка под полупрозрачные цвета
            using (var b = new SolidBrush(Color.FromArgb(60, 60, 68)))
                g.FillRectangle(b, r);

            Draw.FillRound(g, r, 5, Color.FromArgb(255, Value));
            Draw.StrokeRound(g, r, 5, hover ? Theme.Accent : Color.FromArgb(70, 70, 80));

            string hex = "#" + Value.R.ToString("X2") + Value.G.ToString("X2") + Value.B.ToString("X2");
            using (var f = Theme.F(7.5f, false))
            {
                bool dark = (Value.R * 299 + Value.G * 587 + Value.B * 114) / 1000 > 140;
                Draw.Text(g, hex, f, dark ? Color.FromArgb(20, 20, 20) : Color.White,
                    new Rectangle(r.X + 7, r.Y, r.Width - 10, r.Height),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            using (var d = new ColorDialog())
            {
                d.Color = Value;
                d.FullOpen = true;
                d.AnyColor = true;
                if (d.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    Value = d.Color;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }
    }
}
