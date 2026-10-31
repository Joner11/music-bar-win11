using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NowPlaying
{
    // =====================================================================
    //  Плоский выпадающий список (рисуется сам, без системного вида)
    // =====================================================================
    class FlatCombo : Control
    {
        public readonly List<string> Items = new List<string>();
        int index = -1;
        bool hover;
        public bool PreviewsOwnFont = false;

        public event EventHandler SelectedIndexChanged;

        public FlatCombo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 28;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return index; }
            set
            {
                if (value < -1) value = -1;
                if (value >= Items.Count) value = Items.Count - 1;
                if (value == index) return;
                index = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public string SelectedText
        {
            get { return index >= 0 && index < Items.Count ? Items[index] : ""; }
            set
            {
                int i = Items.IndexOf(value);
                SelectedIndex = i;
            }
        }

        public void Fill(params string[] items)
        {
            Items.Clear();
            Items.AddRange(items);
            index = items.Length > 0 ? 0 : -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card))
                g.FillRectangle(b, ClientRectangle);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, 6, Theme.Field);
            Draw.StrokeRound(g, r, 6, hover || Focused ? Theme.Accent : Color.FromArgb(60, 60, 70));

            string txt = SelectedText;
            Font f = Theme.F(8.5f, false);
            Font use = f;
            if (PreviewsOwnFont && txt.Length > 0)
            {
                try { use = new Font(txt, 8.5f); } catch { use = f; }
            }
            Draw.Text(g, txt, use, Theme.Text,
                new Rectangle(9, 0, Width - 32, Height), StringAlignment.Near, StringAlignment.Center);
            if (use != f) use.Dispose();
            f.Dispose();

            Draw.Chevron(g, new Rectangle(Width - 22, 0, 18, Height), Theme.Sub);
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
            ShowDrop();
        }

        protected override bool IsInputKey(Keys k)
        {
            if (k == Keys.Space || k == Keys.Enter) return true;
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down) SelectedIndex = Math.Min(Items.Count - 1, index + 1);
            else if (e.KeyCode == Keys.Up) SelectedIndex = Math.Max(0, index - 1);
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) ShowDrop();
        }

        void ShowDrop()
        {
            if (Items.Count == 0) return;

            DropList dl = new DropList();
            dl.Items = new List<string>(Items);
            dl.PreviewsOwnFont = PreviewsOwnFont;
            dl.Selected = index;
            dl.BackColor = Theme.CardHi;

            int popW = Math.Max(Width, PreviewsOwnFont ? 320 : 210);
            int rowH = 30;
            int rows = Math.Min(Items.Count, 11);
            int popH = rows * rowH + 8;
            dl.Size = new Size(popW - 2, popH - 2);

            ToolStripDropDown pop = new ToolStripDropDown();
            pop.AutoSize = false;
            pop.Padding = new Padding(1);
            pop.BackColor = Color.FromArgb(58, 58, 69);
            pop.DropShadowEnabled = true;

            ScrollHost.WheelBlock++;
            ToolStripDropDown popRef = pop;
            ToolStripDropDownClosedEventHandler onClosed = null;
            onClosed = delegate
            {
                ScrollHost.WheelBlock = Math.Max(0, ScrollHost.WheelBlock - 1);
                popRef.Closed -= onClosed;
            };
            pop.Closed += onClosed;

            dl.ItemPicked += delegate(int i) { SelectedIndex = i; pop.Close(); };
            dl.Cancelled += delegate { pop.Close(); };

            ToolStripControlHost host = new ToolStripControlHost(dl);
            host.AutoSize = false;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.Size = dl.Size;
            pop.Items.Add(host);
            pop.Size = new Size(popW, popH);
            pop.Show(this, new Point(0, Height + 3));
            dl.Focus();
            dl.ScrollToSelected();
        }
    }

    // =====================================================================
    //  Список внутри выпадающего окна — свой рендер, без нативного ListBox
    // =====================================================================
    class DropList : Control
    {
        public List<string> Items = new List<string>();
        public bool PreviewsOwnFont;
        public int Selected = -1;

        public event Action<int> ItemPicked;
        public event Action Cancelled;

        int scroll;
        int hot = -1;
        const int RowH = 30;

        public DropList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        int MaxScroll { get { return Math.Max(0, Items.Count * RowH - Height); } }

        public void ScrollToSelected()
        {
            if (Selected < 0) return;
            int y = Selected * RowH;
            if (y < scroll) scroll = y;
            if (y + RowH > scroll + Height) scroll = y + RowH - Height;
            Clamp();
        }

        void Clamp()
        {
            if (scroll > MaxScroll) scroll = MaxScroll;
            if (scroll < 0) scroll = 0;
            Invalidate();
        }

        void ScrollBy(int dy) { scroll += dy; Clamp(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = (e.Y + scroll) / RowH;
            if (i >= Items.Count) i = -1;
            if (i != hot) { hot = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;
            int i = (e.Y + scroll) / RowH;
            if (i >= 0 && i < Items.Count && ItemPicked != null) ItemPicked(i);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(-(e.Delta / 120) * RowH * 2);
        }

        protected override bool IsInputKey(Keys k)
        {
            if (k == Keys.Up || k == Keys.Down || k == Keys.Enter || k == Keys.Escape) return true;
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { if (Cancelled != null) Cancelled(); return; }
            if (e.KeyCode == Keys.Enter)
            {
                if (Selected >= 0 && ItemPicked != null) ItemPicked(Selected);
                return;
            }
            if (e.KeyCode == Keys.Down && Selected < Items.Count - 1) Selected++;
            else if (e.KeyCode == Keys.Up && Selected > 0) Selected--;
            else if (e.KeyCode == Keys.PageDown) ScrollBy(Height);
            else if (e.KeyCode == Keys.PageUp) ScrollBy(-Height);
            else if (e.KeyCode == Keys.Home) { Selected = 0; scroll = 0; }
            else if (e.KeyCode == Keys.End) { Selected = Items.Count - 1; scroll = MaxScroll; }
            ScrollToSelected();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.CardHi);

            int first = scroll / RowH;
            int last = Math.Min(Items.Count - 1, (scroll + Height) / RowH + 1);

            for (int i = first; i <= last; i++)
            {
                int y = i * RowH - scroll;
                var row = new Rectangle(0, y, Width, RowH);
                bool sel = i == Selected;
                bool hov = i == hot;

                if (hov)
                    using (var b = new SolidBrush(Color.FromArgb(52, 52, 62)))
                        g.FillRectangle(b, row);

                string it = Items[i];
                Font baseF = Theme.F(9f, false);
                Font use = baseF;
                if (PreviewsOwnFont)
                {
                    try { use = new Font(it, 9.5f); } catch { use = baseF; }
                }
                var tr = new Rectangle(row.X + 12, row.Y, row.Width - (sel ? 40 : 20), row.Height);
                Draw.Text(g, it, use,
                    hov ? Theme.Text : (sel ? Theme.Accent : Color.FromArgb(212, 212, 222)),
                    tr, StringAlignment.Near, StringAlignment.Center);
                if (sel)
                    Draw.Check(g, new Rectangle(row.Right - 26, row.Y + 10, 12, 11), Theme.Accent);
                if (use != baseF) use.Dispose();
                baseF.Dispose();
            }

            // тонкий ползунок, только если нужно
            if (MaxScroll > 0)
            {
                int need = Math.Max(28, (int)((double)Height * Height / (Items.Count * RowH)));
                int y = (int)((double)scroll / MaxScroll * (Height - need));
                Draw.FillRound(g, new Rectangle(Width - 5, y, 3, need), 1, Color.FromArgb(90, 90, 102));
            }
        }
    }

    // =====================================================================
    //  Пилюля-кнопка для готовых пресетов
    // =====================================================================
    class Chip : Control
    {
        public bool Active;
        bool hover;

        public Chip(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 28;
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        public void Fit() { Width = Measure(); }

        int Measure()
        {
            using (var f = Theme.F(8.5f, false))
                return TextRenderer.MeasureText(Text, f).Width + 24;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card))
                g.FillRectangle(b, ClientRectangle);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Active ? Color.FromArgb(38, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)
                                : (hover ? Theme.CardHi : Theme.Field);
            Draw.FillRound(g, r, 8, fill);
            Draw.StrokeRound(g, r, 8, Active ? Color.FromArgb(120, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)
                                             : Color.FromArgb(58, 58, 68));
            using (var f = Theme.F(8.5f, Active))
                Draw.Text(g, Text, f, Active ? Theme.Accent : Theme.Text, r,
                    StringAlignment.Center, StringAlignment.Center);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    // =====================================================================
    //  Карточка-секция с сеткой внутри
    // =====================================================================
    class Card : Panel
    {
        public readonly TableLayoutPanel Body;
        public string Title = "";

        public Card(string title)
        {
            Title = title;
            BackColor = Theme.Bg;
            Body = new TableLayoutPanel();
            Body.BackColor = Color.FromArgb(30, 30, 35);
            Body.ColumnCount = 3;
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            Body.AutoSize = false;
            Body.Margin = new Padding(0);
            Body.Padding = new Padding(0);
            Controls.Add(Body);
        }

        int ContentTop { get { return Title.Length > 0 ? 46 : 14; } }

        public void Relayout(int width)
        {
            int pad = 18;
            Body.Left = pad;
            Body.Top = ContentTop;
            Body.Width = Math.Max(120, width - pad * 2);

            int bodyH = 0;
            for (int i = 0; i < Body.RowStyles.Count; i++)
                bodyH += (int)Body.RowStyles[i].Height;
            Body.Height = bodyH;
            Body.PerformLayout();

            int need = ContentTop + bodyH + 14;
            if (Height != need) Height = need;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, 10, Theme.Card);
            Draw.StrokeRound(g, r, 10, Theme.Sep);

            if (Title.Length > 0)
            {
                using (var f = Theme.F(9.5f, true))
                    Draw.Text(g, Title, f, Theme.Text,
                        new Rectangle(18, 10, Width - 36, 24), StringAlignment.Near, StringAlignment.Center);
            }
        }
    }

    // =====================================================================
    //  Кнопка бокового меню
    // =====================================================================
    class NavButton : Control
    {
        public bool Active;
        bool hover;

        public NavButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 38;
            Cursor = Cursors.Hand;
            BackColor = Theme.Sidebar;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Sidebar);

            Rectangle r = new Rectangle(8, 1, Width - 16, Height - 2);
            if (Active)
            {
                Draw.FillRound(g, r, 7, Color.FromArgb(36, 36, 42));
                Draw.FillRound(g, new Rectangle(8, 9, 3, Height - 18), 2, Theme.Accent);
            }
            else if (hover)
            {
                Draw.FillRound(g, r, 7, Color.FromArgb(26, 26, 31));
            }

            using (var f = Theme.F(9f, Active))
                Draw.Text(g, Text, f, Active ? Theme.Text : Theme.Sub,
                    new Rectangle(22, 0, Width - 30, Height), StringAlignment.Near, StringAlignment.Center);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); OnClick(EventArgs.Empty); }
    }

    // =====================================================================
    //  Прокрутка с тонким своим ползунком
    // =====================================================================
    class ScrollHost : Panel, IMessageFilter
    {
        public readonly Panel Canvas = new Panel();
        int scroll;
        int contentH = 0;
        bool barHot, dragging;
        int dragStartY, dragStartScroll;

        public ScrollHost()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Canvas.BackColor = Theme.Bg;
            Canvas.Location = new Point(0, 0);
            Controls.Add(Canvas);
            Application.AddMessageFilter(this);
        }

        public int ContentHeight
        {
            get { return contentH; }
            set { contentH = value; ClampScroll(); Invalidate(); }
        }

        public int ScrollY { get { return scroll; } }

        int ViewH { get { return Height; } }
        int MaxScroll { get { return Math.Max(0, contentH - ViewH); } }

        void ClampScroll()
        {
            if (scroll > MaxScroll) scroll = MaxScroll;
            if (scroll < 0) scroll = 0;
            Canvas.Top = -scroll;
        }

        public void ScrollTo(int y) { scroll = y; ClampScroll(); Invalidate(); }
        public void ScrollBy(int dy) { ScrollTo(scroll + dy); }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Canvas.Width = Width;
            ClampScroll();
            Invalidate();
        }

        /// <summary>Пока открыт выпадающий список, хост не должен крутить страницу.</summary>
        public static int WheelBlock;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x020A && WheelBlock > 0) return false;
            if (m.Msg == 0x020A && Visible && IsHandleCreated)
            {
                Point cp = PointToClient(Cursor.Position);
                if (ClientRectangle.Contains(cp))
                {
                    int delta = (short)((long)m.WParam >> 16);
                    ScrollBy(-delta / 120 * 96);
                    return true;
                }
            }
            return false;
        }

        Rectangle BarRect()
        {
            int need = (int)Math.Max(32, (double)ViewH * ViewH / Math.Max(1, contentH));
            int y = MaxScroll == 0 ? 0 : (int)((double)scroll / MaxScroll * (ViewH - need));
            return new Rectangle(Width - 8, y, 5, need);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            if (MaxScroll > 0 && contentH > 0)
            {
                Draw.FillRound(g, new Rectangle(Width - 8, 2, 5, Height - 4), 2, Color.FromArgb(30, 30, 36));
                Draw.FillRound(g, BarRect(), 2, barHot || dragging ? Theme.Accent : Color.FromArgb(72, 72, 82));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                int need = (int)Math.Max(32, (double)ViewH * ViewH / Math.Max(1, contentH));
                double span = Math.Max(1, ViewH - need);
                ScrollTo(dragStartScroll + (int)((e.Y - dragStartY) / span * MaxScroll));
                return;
            }
            bool hot = MaxScroll > 0 && e.X >= Width - 16;
            if (hot != barHot) { barHot = hot; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (MaxScroll <= 0 || e.X < Width - 16) return;
            Rectangle rb = BarRect();
            if (rb.Contains(e.Location))
            {
                dragging = true;
                dragStartY = e.Y;
                dragStartScroll = scroll;
            }
            else
            {
                int page = Math.Max(60, ViewH - 80);
                ScrollBy(e.Y < rb.Y ? -page : page);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Application.RemoveMessageFilter(this);
            base.Dispose(disposing);
        }
    }
}
