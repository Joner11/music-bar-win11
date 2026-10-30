using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NowPlaying
{
    /// <summary>Живой предпросмотр виджета внутри диалога настроек.</summary>
    class PreviewBox : Control
    {
        public Config Cfg;
        public TrackInfo Track;
        public Image Thumb;
        public float ScrollPx;
        const int TbH = 44;

        public PreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);

            if (Cfg == null) return;

            Rectangle card = new Rectangle(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, card, 10, Color.FromArgb(15, 15, 19));
            Draw.StrokeRound(g, card, 10, Theme.Sep);

            int padX = 18;
            int tbY = Height - TbH - 10;
            Rectangle bar = new Rectangle(padX, tbY, Width - padX * 2, TbH);
            Draw.FillRound(g, bar, 8, Color.FromArgb(32, 32, 38));

            // правый край панели: часы и значки
            using (var f = Theme.F(7.5f, false))
            {
                Draw.Text(g, "22:41", f, Color.FromArgb(200, 200, 208),
                    new Rectangle(bar.Right - 58, bar.Top, 44, TbH), StringAlignment.Far, StringAlignment.Center);
                Draw.Text(g, "РУС", f, Color.FromArgb(150, 150, 160),
                    new Rectangle(bar.Right - 108, bar.Top, 40, TbH), StringAlignment.Far, StringAlignment.Center);
            }
            for (int i = 0; i < 3; i++)
                Draw.FillRound(g, new Rectangle(bar.Right - 172 + i * 20, bar.Top + 15, 14, 14), 3,
                    Color.FromArgb(52, 52, 60));

            float avail = bar.Width - 200;
            float scale = Math.Min(1.0f, avail / Math.Max(1, Cfg.Width));
            int w = Math.Max(1, (int)Math.Round(Cfg.Width * scale));
            int h = Math.Max(1, (int)Math.Round(Cfg.Height * scale));

            using (var bmp = Renderer.Render(Cfg, Track, Thumb, ScrollPx, scale))
            {
                int x = bar.X + (bar.Width - 176 - w) / 2;
                if (x < bar.X + 10) x = bar.X + 10;
                int y = bar.Top + (TbH - h) / 2;
                g.DrawImageUnscaled(bmp, x, y);
            }

            using (var f = Theme.F(8f, false))
                Draw.Text(g, "Предпросмотр", f, Color.FromArgb(110, 110, 120),
                    new Rectangle(card.X + 18, 8, 200, 16), StringAlignment.Near, StringAlignment.Center);
            if (scale < 0.999f)
                using (var f = Theme.F(8f, false))
                    Draw.Text(g, (int)Math.Round(scale * 100) + "%", f, Color.FromArgb(90, 90, 100),
                        new Rectangle(card.Right - 80, 8, 62, 16), StringAlignment.Far, StringAlignment.Center);
        }
    }

    class SettingsForm : Form
    {
        public Config Cfg;
        public TrackInfo Track;
        public Image Thumb;
        public event Action LiveChanged;
        public event Action ClosedEvent;

        PreviewBox preview;
        System.Windows.Forms.Timer previewTick;
        float scroll;
        bool building;

        ScrollHost host;
        NavButton[] navs;
        int page;
        static readonly string[] PageNames = {
            "Размер и положение", "Фон", "Текст", "Обложка", "Поведение" };

        public SettingsForm(Config cfg, TrackInfo track, Image thumb)
        {
            building = true;
            Cfg = cfg; Track = track; Thumb = thumb;

            Text = "Music Bar Win11 — настройки";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 780);
            MinimumSize = new Size(900, 620);
            Font = Theme.F(9f, false);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildShell();
            building = false;
            ShowPage(0);

            previewTick = new System.Windows.Forms.Timer();
            previewTick.Interval = 40;
            previewTick.Tick += delegate
            {
                scroll += 0.6f;
                if (scroll > 9000f) scroll = 0f;
                preview.ScrollPx = scroll;
                preview.Invalidate();
            };
            previewTick.Start();

            int startTab = 0;
            int.TryParse(Environment.GetEnvironmentVariable("NP_START_TAB"), out startTab);
            if (startTab > 0 && startTab < PageNames.Length) ShowPage(startTab);

            if (Environment.GetEnvironmentVariable("NP_DUMP_LAYOUT") == "1")
                Shown += delegate { DumpLayout(); };

            FormClosed += delegate
            {
                previewTick.Stop();
                if (ClosedEvent != null) ClosedEvent();
            };
        }

        // ================= каркас окна =================

        void BuildShell()
        {
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 214;
            sidebar.BackColor = Theme.Sidebar;
            Controls.Add(sidebar);

            Label brand = new Label();
            brand.Text = "Music Bar";
            brand.Font = Theme.F(12f, true);
            brand.ForeColor = Theme.Text;
            brand.AutoSize = false;
            brand.Left = 22; brand.Top = 18; brand.Width = 180; brand.Height = 24;
            brand.BackColor = Theme.Sidebar;
            sidebar.Controls.Add(brand);

            Label brand2 = new Label();
            brand2.Text = "Windows 11 · виджет";
            brand2.Font = Theme.F(8f, false);
            brand2.ForeColor = Theme.Sub;
            brand2.AutoSize = false;
            brand2.Left = 22; brand2.Top = 42; brand2.Width = 180; brand2.Height = 18;
            brand2.BackColor = Theme.Sidebar;
            sidebar.Controls.Add(brand2);

            navs = new NavButton[PageNames.Length];
            for (int i = 0; i < PageNames.Length; i++)
            {
                NavButton nb = new NavButton(PageNames[i]);
                nb.Left = 10; nb.Top = 88 + i * 44; nb.Width = 194;
                int idx = i;
                nb.Click += delegate { ShowPage(idx); };
                sidebar.Controls.Add(nb);
                navs[i] = nb;
            }

            Label ver = new Label();
            ver.Text = "версия " + Application.ProductVersion;
            ver.Font = Theme.F(7.5f, false);
            ver.ForeColor = Color.FromArgb(90, 90, 100);
            ver.AutoSize = false;
            ver.Left = 22; ver.Width = 180; ver.Height = 18;
            ver.Top = sidebar.Height - 30;
            ver.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            ver.BackColor = Theme.Sidebar;
            sidebar.Controls.Add(ver);

            // ---- футер ----
            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 62;
            footer.BackColor = Theme.Bg;
            Controls.Add(footer);

            Panel footLine = new Panel();
            footLine.Dock = DockStyle.Top;
            footLine.Height = 1;
            footLine.BackColor = Theme.Sep;
            footer.Controls.Add(footLine);

            Button reset = FlatButton("Сбросить всё", 132, false);
            reset.Left = 20; reset.Top = 14; reset.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            reset.Click += delegate { ResetAll(); };
            footer.Controls.Add(reset);

            Button done = FlatButton("Готово", 118, true);
            done.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            done.Left = footer.Width - 138;
            done.Top = 14;
            done.Click += delegate { Close(); };
            footer.Controls.Add(done);
            footer.Resize += delegate { done.Left = footer.Width - 138; };

            // ---- предпросмотр ----
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 132;
            top.BackColor = Theme.Bg;
            Controls.Add(top);

            preview = new PreviewBox();
            preview.Dock = DockStyle.Fill;
            preview.Cfg = Cfg;
            preview.Track = Track;
            preview.Thumb = Thumb;
            preview.Margin = new Padding(0);
            top.Padding = new Padding(20, 6, 26, 4);
            top.Controls.Add(preview);

            // ---- содержимое ----
            host = new ScrollHost();
            host.Dock = DockStyle.Fill;
            Controls.Add(host);
            host.BringToFront();
        }

        Button FlatButton(string text, int width, bool accent)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 34;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = accent ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(64, 64, 74);
            b.BackColor = accent ? Theme.Accent : Theme.Card;
            b.ForeColor = accent ? Color.FromArgb(12, 20, 26) : Theme.Text;
            b.Font = Theme.F(9f, accent);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        void ResetAll()
        {
            Config d = new Config();
            Cfg.CopyFrom(d);
            ShowPage(page);
            Raise();
        }

        void ShowPage(int idx)
        {
            page = idx;
            for (int i = 0; i < navs.Length; i++) { navs[i].Active = (i == idx); navs[i].Invalidate(); }

            building = true;
            host.SuspendLayout();
            host.Canvas.SuspendLayout();
            host.Canvas.Controls.Clear();
            host.Canvas.Controls.Add(BuildPage(idx));
            host.Canvas.ResumeLayout(true);
            host.ResumeLayout(true);
            building = false;
            host.ScrollTo(0);
            LayoutPage();
        }

        Control BuildPage(int idx)
        {
            int width = Math.Max(420, host.Width - 8);
            Panel col = new Panel();
            col.BackColor = Theme.Bg;
            col.Left = 0; col.Top = 0;
            col.Width = width;
            col.Height = 10;

            List<Card> cards = new List<Card>();
            if (idx == 0) BuildSize(cards);
            else if (idx == 1) BuildBackground(cards);
            else if (idx == 2) BuildText(cards);
            else if (idx == 3) BuildCover(cards);
            else BuildBehavior(cards);

            int y = 8;
            foreach (Card c in cards)
            {
                c.Left = 0; c.Top = y; c.Width = width;
                c.Relayout(width);
                col.Controls.Add(c);
                y += c.Height + 12;
            }
            col.Height = y + 4;
            col.Tag = cards;
            return col;
        }

        void LayoutPage()
        {
            if (host == null || host.Canvas.Controls.Count == 0) return;
            Control col = host.Canvas.Controls[0];
            int width = Math.Max(420, host.Width - 8);
            if (col.Width != width)
            {
                col.Width = width;
                List<Card> cards = col.Tag as List<Card>;
                if (cards != null)
                {
                    int y = 8;
                    foreach (Card c in cards)
                    {
                        c.Width = width;
                        c.Relayout(width);
                        c.Top = y;
                        y += c.Height + 12;
                    }
                    col.Height = y + 4;
                }
            }
            host.Canvas.Height = col.Height;
            host.ContentHeight = col.Height;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (building) return;
            LayoutPage();
        }

        // ================= конструкторы страниц =================

        void BuildSize(List<Card> cards)
        {
            Card c1 = Card2("Размер и положение");
            Card c2 = Card2("Скругление и отступы");
            cards.Add(c1); cards.Add(c2);

            AddChips(c1, "Пресет", new string[] { "Мини", "Компактно", "Стандарт", "Широко" },
                delegate(int i)
                {
                    int[] w = { 140, 185, 240, 320 };
                    int[] h = { 28, 40, 48, 48 };
                    Cfg.Width = w[i]; Cfg.Height = h[i];
                },
                delegate
                {
                    if (Cfg.Width == 140 && Cfg.Height == 28) return 0;
                    if (Cfg.Width == 185 && Cfg.Height == 40) return 1;
                    if (Cfg.Width == 240 && Cfg.Height == 48) return 2;
                    if (Cfg.Width == 320 && Cfg.Height == 48) return 3;
                    return -1;
                });

            AddSlider(c1, "Ширина виджета", 120, 700, 1, 1, 0, " px",
                delegate { return Cfg.Width; }, delegate(int v) { Cfg.Width = v; });
            AddSlider(c1, "Высота виджета", 20, 120, 1, 1, 0, " px",
                delegate { return Cfg.Height; }, delegate(int v) { Cfg.Height = v; });
            AddSlider(c1, "Отступ справа", -200, 400, 1, 1, 0, " px",
                delegate { return Cfg.RightMargin; }, delegate(int v) { Cfg.RightMargin = v; });
            AddSlider(c1, "Смещение по вертикали", -40, 40, 1, 1, 0, " px",
                delegate { return Cfg.VertOffset; }, delegate(int v) { Cfg.VertOffset = v; });

            AddSlider(c2, "Радиус скругления", 0, 40, 1, 1, 0, " px",
                delegate { return Cfg.Corner; }, delegate(int v) { Cfg.Corner = v; });
            AddSlider(c2, "Внутренний отступ", 0, 24, 1, 1, 0, " px",
                delegate { return Cfg.Padding; }, delegate(int v) { Cfg.Padding = v; });
            AddSlider(c2, "Ширина обложки", 0, 96, 1, 1, 0, " px",
                delegate { return Cfg.ThumbSize; }, delegate(int v) { Cfg.ThumbSize = v; });
            AddHint(c2, "0 — размер по высоте виджета");
        }

        void BuildBackground(List<Card> cards)
        {
            Card c1 = Card2("Фон");
            Card c2 = Card2("Градиент и рамка");
            Card c3 = Card2("Фон из обложки");
            cards.Add(c1); cards.Add(c2); cards.Add(c3);

            AddToggle(c1, "Свой цвет фона", delegate { return Cfg.BgEnabled; },
                delegate(bool v) { Cfg.BgEnabled = v; });
            AddColor(c1, "Цвет фона", delegate { return Cfg.BgR + "," + Cfg.BgG + "," + Cfg.BgB; },
                delegate(string v) { Cfg.BgR = Config.ParseColor(v, Color.Black).R; Cfg.BgG = Config.ParseColor(v, Color.Black).G; Cfg.BgB = Config.ParseColor(v, Color.Black).B; });
            AddSlider(c1, "Прозрачность фона", 0, 100, 1, 1, 0, " %",
                delegate { return Cfg.OpacityPercent; }, delegate(int v) { Cfg.OpacityPercent = v; });

            AddToggle(c2, "Градиент", delegate { return Cfg.Gradient; },
                delegate(bool v) { Cfg.Gradient = v; });
            AddColor(c2, "Второй цвет", delegate { return Cfg.BgR2 + "," + Cfg.BgG2 + "," + Cfg.BgB2; },
                delegate(string v) { Color k = Config.ParseColor(v, Color.Black); Cfg.BgR2 = k.R; Cfg.BgG2 = k.G; Cfg.BgB2 = k.B; });
            AddSlider(c2, "Угол градиента", 0, 360, 1, 1, 0, "°",
                delegate { return Cfg.GradientAngle; }, delegate(int v) { Cfg.GradientAngle = v; });
            AddToggle(c2, "Рамка", delegate { return Cfg.BorderEnabled; },
                delegate(bool v) { Cfg.BorderEnabled = v; });
            AddColor(c2, "Цвет рамки", delegate { return Cfg.BorderR + "," + Cfg.BorderG + "," + Cfg.BorderB; },
                delegate(string v) { Color k = Config.ParseColor(v, Color.White); Cfg.BorderR = k.R; Cfg.BorderG = k.G; Cfg.BorderB = k.B; });
            AddSlider(c2, "Яркость рамки", 0, 255, 1, 1, 0, "",
                delegate { return Cfg.BorderAlpha; }, delegate(int v) { Cfg.BorderAlpha = v; });
            AddSlider(c2, "Толщина рамки", 1, 6, 1, 1, 0, " px",
                delegate { return Cfg.BorderWidth; }, delegate(int v) { Cfg.BorderWidth = v; });

            AddChips(c3, "Готовый стиль", new string[] { "Тёмный", "Стекло", "Обложка", "Светлый" },
                delegate(int i)
                {
                    Cfg.BgEnabled = true; Cfg.Gradient = true;
                    if (i == 0)
                    {
                        Cfg.BgMode = "color"; Cfg.OpacityPercent = 82;
                        Cfg.BgR = 28; Cfg.BgG = 28; Cfg.BgB = 30;
                        Cfg.BgR2 = 21; Cfg.BgG2 = 21; Cfg.BgB2 = 21;
                        Cfg.TextColorMode = "auto";
                    }
                    else if (i == 1)
                    {
                        Cfg.BgMode = "cover_blur_tint"; Cfg.OpacityPercent = 62;
                        Cfg.CoverDark = 45; Cfg.CoverBlur = 24; Cfg.TextColorMode = "auto";
                    }
                    else if (i == 2)
                    {
                        Cfg.BgMode = "cover_blur"; Cfg.OpacityPercent = 100;
                        Cfg.CoverDark = 22; Cfg.CoverBlur = 18; Cfg.TextColorMode = "auto";
                    }
                    else
                    {
                        Cfg.BgMode = "color"; Cfg.OpacityPercent = 96;
                        Cfg.BgR = 242; Cfg.BgG = 242; Cfg.BgB = 246;
                        Cfg.BgR2 = 224; Cfg.BgG2 = 226; Cfg.BgB2 = 232;
                        Cfg.BorderR = 30; Cfg.BorderG = 30; Cfg.BorderB = 34;
                        Cfg.TextColorMode = "auto";
                    }
                },
                null);

            AddCombo(c3, "Режим фона",
                new string[] { "Свой цвет", "Размытие обложки", "Размытие + оттенок", "Цвет обложки" },
                new string[] { "color", "cover_blur", "cover_blur_tint", "cover_color" },
                delegate { return Cfg.BgMode; }, delegate(string v) { Cfg.BgMode = v; });
            AddSlider(c3, "Сила размытия", 1, 64, 1, 1, 0, "",
                delegate { return Cfg.CoverBlur; }, delegate(int v) { Cfg.CoverBlur = v; });
            AddSlider(c3, "Затемнение", 0, 95, 1, 1, 0, " %",
                delegate { return Cfg.CoverDark; }, delegate(int v) { Cfg.CoverDark = v; });
            AddSlider(c3, "Насыщенность", 0, 300, 1, 1, 0, " %",
                delegate { return Cfg.CoverSaturation; }, delegate(int v) { Cfg.CoverSaturation = v; });
        }

        void BuildText(List<Card> cards)
        {
            Card c1 = Card2("Шрифт");
            Card c2 = Card2("Строки");
            Card c3 = Card2("Бегущая строка");
            cards.Add(c1); cards.Add(c2); cards.Add(c3);

            AddChips(c1, "Размер текста", new string[] { "Мелкий", "Обычный", "Крупный" },
                delegate(int i)
                {
                    if (i == 0) { Cfg.TitleSize = 8.5f; Cfg.ArtistSize = 7.25f; }
                    else if (i == 1) { Cfg.TitleSize = 9.75f; Cfg.ArtistSize = 8.25f; }
                    else { Cfg.TitleSize = 12f; Cfg.ArtistSize = 10f; }
                },
                delegate
                {
                    if (Cfg.TitleSize == 8.5f) return 0;
                    if (Cfg.TitleSize == 9.75f) return 1;
                    if (Cfg.TitleSize == 12f) return 2;
                    return -1;
                });
            AddFontCombo(c1, "Шрифт", delegate { return Cfg.FontName; },
                delegate(string v) { Cfg.FontName = v; });
            AddSlider(c1, "Размер названия", 20, 120, 1, 4, 2, " pt",
                delegate { return (int)Math.Round(Cfg.TitleSize * 4); },
                delegate(int v) { Cfg.TitleSize = v / 4f; });
            AddToggle(c1, "Название жирным", delegate { return Cfg.TitleBold; },
                delegate(bool v) { Cfg.TitleBold = v; });
            AddSlider(c1, "Размер исполнителя", 16, 96, 1, 4, 2, " pt",
                delegate { return (int)Math.Round(Cfg.ArtistSize * 4); },
                delegate(int v) { Cfg.ArtistSize = v / 4f; });
            AddToggle(c1, "Исполнитель жирным", delegate { return Cfg.ArtistBold; },
                delegate(bool v) { Cfg.ArtistBold = v; });

            AddCombo(c2, "Цвет текста",
                new string[] { "Адаптивный к фону", "Свой цвет" },
                new string[] { "auto", "custom" },
                delegate { return Cfg.TextColorMode == "custom" ? "custom" : "auto"; },
                delegate(string v) { Cfg.TextColorMode = v; RebuildPage(); });
            if (Cfg.TextColorMode == "custom")
            {
                AddColor(c2, "Цвет названия", delegate { return Cfg.TitleColor; },
                    delegate(string v) { Cfg.TitleColor = v; });
                AddColor(c2, "Цвет исполнителя", delegate { return Cfg.ArtistColor; },
                    delegate(string v) { Cfg.ArtistColor = v; });
                AddHint(c2, "Цвет полосы прогресса берётся отдельно, на странице «Обложка»");
            }
            else
                AddHint(c2, "Сам подбирает светлый или тёмный оттенок под текущий фон — всегда читаемо");
            AddCombo(c2, "Вторая строка",
                new string[] { "Исполнитель", "Альбом", "Плеер", "Исполнитель · плеер", "Плеер · исполнитель", "Нет" },
                new string[] { "artist", "album", "app", "artist_app", "app_artist", "none" },
                delegate { return Cfg.SecondLine; }, delegate(string v) { Cfg.SecondLine = v; });
            AddCombo(c2, "Выравнивание",
                new string[] { "По левому краю", "По центру", "По правому краю" },
                new string[] { "left", "center", "right" },
                delegate { return Cfg.Align; }, delegate(string v) { Cfg.Align = v; });
            AddCombo(c2, "Выравнивание по вертикали",
                new string[] { "Сверху", "По центру", "Снизу" },
                new string[] { "top", "center", "bottom" },
                delegate { return Cfg.VAlign; }, delegate(string v) { Cfg.VAlign = v; });
            AddSlider(c2, "Расстояние между строками", -4, 16, 1, 1, 0, " px",
                delegate { return Cfg.LineSpacing; }, delegate(int v) { Cfg.LineSpacing = v; });
            AddToggle(c2, "Тень текста", delegate { return Cfg.TextShadow; },
                delegate(bool v) { Cfg.TextShadow = v; });
            AddToggle(c2, "Приглушать на паузе", delegate { return Cfg.DimPaused; },
                delegate(bool v) { Cfg.DimPaused = v; });

            AddToggle(c3, "Бегущая строка", delegate { return Cfg.Marquee; },
                delegate(bool v) { Cfg.Marquee = v; });
            AddSlider(c3, "Скорость", 10, 400, 5, 1, 0, " px/с",
                delegate { return Cfg.MarqueeSpeed; }, delegate(int v) { Cfg.MarqueeSpeed = v; });
            AddSlider(c3, "Промежуток", 0, 200, 5, 1, 0, " px",
                delegate { return Cfg.MarqueeGap; }, delegate(int v) { Cfg.MarqueeGap = v; });
            AddHint(c3, "Прокрутка длинных названий по кругу");
        }

        void BuildCover(List<Card> cards)
        {
            Card c1 = Card2("Обложка");
            Card c2 = Card2("Полоса прогресса");
            cards.Add(c1); cards.Add(c2);

            AddCombo(c1, "Сторона обложки",
                new string[] { "Справа", "Слева", "Скрыть" },
                new string[] { "right", "left", "none" },
                delegate { return Cfg.ThumbSide; }, delegate(string v) { Cfg.ThumbSide = v; });
            AddSlider(c1, "Размер", 0, 96, 1, 1, 0, " px",
                delegate { return Cfg.ThumbSize; }, delegate(int v) { Cfg.ThumbSize = v; });
            AddSlider(c1, "Скругление", 0, 40, 1, 1, 0, " px",
                delegate { return Cfg.ThumbCorner; }, delegate(int v) { Cfg.ThumbCorner = v; });
            AddCombo(c1, "Выравнивание по вертикали",
                new string[] { "По центру", "Сверху", "Снизу" },
                new string[] { "center", "top", "bottom" },
                delegate { return Cfg.ThumbVAlign; }, delegate(string v) { Cfg.ThumbVAlign = v; });
            AddSlider(c1, "Сдвиг от края", -60, 60, 1, 1, 0, " px",
                delegate { return Cfg.ThumbOffset; }, delegate(int v) { Cfg.ThumbOffset = v; });
            AddToggle(c1, "Заглушка без обложки", delegate { return Cfg.ThumbPlaceholder; },
                delegate(bool v) { Cfg.ThumbPlaceholder = v; });

            AddToggle(c2, "Показывать прогресс", delegate { return Cfg.Progress; },
                delegate(bool v) { Cfg.Progress = v; });
            AddColor(c2, "Цвет полосы", delegate { return Cfg.ProgressColor; },
                delegate(string v) { Cfg.ProgressColor = v; });
            AddSlider(c2, "Толщина полосы", 1, 8, 1, 1, 0, " px",
                delegate { return Cfg.ProgressHeight; }, delegate(int v) { Cfg.ProgressHeight = v; });
            AddSlider(c2, "Яркость полосы", 0, 255, 1, 1, 0, "",
                delegate { return Cfg.ProgressAlpha; }, delegate(int v) { Cfg.ProgressAlpha = v; });
            AddSlider(c2, "Сдвиг от края", -80, 40, 1, 1, 0, " px",
                delegate { return Cfg.ProgressOffset; }, delegate(int v) { Cfg.ProgressOffset = v; });
        }

        void BuildBehavior(List<Card> cards)
        {
            Card c1 = Card2("Видимость");
            Card c2 = Card2("Действия мышью");
            cards.Add(c1); cards.Add(c2);

            AddToggle(c1, "Показывать на паузе", delegate { return Cfg.ShowPaused; },
                delegate(bool v) { Cfg.ShowPaused = v; });
            AddToggle(c1, "Скрывать без музыки", delegate { return Cfg.HideWhenIdle; },
                delegate(bool v) { Cfg.HideWhenIdle = v; });
            AddToggle(c1, "Всегда поверх панели задач", delegate { return Cfg.AlwaysVisible; },
                delegate(bool v) { Cfg.AlwaysVisible = v; });
            AddToggle(c1, "Скрывать в полноэкранных играх", delegate { return Cfg.HideFullscreen; },
                delegate(bool v) { Cfg.HideFullscreen = v; });

            bool autostart = false;
            try { autostart = Autostart.Enabled; } catch { }
            AddToggle(c1, "Запускать с Windows", delegate { return autostart; },
                delegate(bool v) { autostart = v; try { Autostart.Enabled = v; } catch { } });

            AddCombo(c2, "Один клик",
                new string[] { "Настройки", "Пауза / играть", "Следующий трек", "Предыдущий", "Ничего" },
                new string[] { "settings", "toggle", "next", "prev", "none" },
                delegate { return Cfg.SingleClick; }, delegate(string v) { Cfg.SingleClick = v; });
            AddCombo(c2, "Двойной клик",
                new string[] { "Пауза / играть", "Настройки", "Ничего" },
                new string[] { "toggle", "settings", "none" },
                delegate { return Cfg.DoubleClick; }, delegate(string v) { Cfg.DoubleClick = v; });
            AddHint(c2, "Все изменения применяются сразу — окно можно не закрывать");
        }

        // ================= строки карточки =================

        Card Card2(string title) { return new Card(title); }

        void AddRow(Card card, string label, Control ctl, Control right)
        {
            TableLayoutPanel b = card.Body;
            int row = b.RowCount;
            b.RowCount = row + 1;
            b.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            Label l = new Label();
            l.Text = label;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Theme.Sub;
            l.BackColor = Color.FromArgb(30, 30, 35);
            l.Font = Theme.F(9f, false);
            l.Margin = new Padding(0, 0, 12, 0);
            b.Controls.Add(l, 0, row);

            ctl.Dock = DockStyle.Fill;
            ctl.Margin = new Padding(0, 8, 12, 6);
            ctl.BackColor = Color.FromArgb(30, 30, 35);
            b.Controls.Add(ctl, 1, row);

            if (right != null)
            {
                right.Dock = DockStyle.Fill;
                right.Margin = new Padding(0, 0, 0, 0);
                b.Controls.Add(right, 2, row);
            }
        }

        void AddSlider(Card card, string label, int min, int max, int step, int div,
                       int decimals, string suffix,
                       Func<int> get, Action<int> set)
        {
            Slider s = new Slider();
            s.Min = min; s.Max = max; s.Step = step; s.Divider = div;
            s.Decimals = decimals; s.Suffix = suffix;
            s.RawValue = Math.Max(min, Math.Min(max, get()));
            s.BackColor = Color.FromArgb(30, 30, 35);

            Label v = new Label();
            v.Text = s.DisplayValue();
            v.TextAlign = ContentAlignment.MiddleRight;
            v.ForeColor = Theme.Text;
            v.Font = Theme.F(8.5f, false);
            v.BackColor = Color.FromArgb(30, 30, 35);

            s.ValueChanged += delegate
            {
                set(s.RawValue);
                v.Text = s.DisplayValue();
                if (!building) Raise();
            };

            AddRow(card, label, s, v);
        }

        void AddToggle(Card card, string label, Func<bool> get, Action<bool> set)
        {
            ToggleSwitch t = new ToggleSwitch();
            t.SetSilent(get());
            t.BackColor = Color.FromArgb(30, 30, 35);
            t.CheckedChanged += delegate
            {
                set(t.Checked);
                if (!building) Raise();
            };
            AddRow(card, label, t, null);
        }

        void AddColor(Card card, string label, Func<string> get, Action<string> set)
        {
            ColorSwatch c = new ColorSwatch();
            c.SetSilent(Config.ParseColor(get(), Color.White));
            c.BackColor = Color.FromArgb(30, 30, 35);
            c.ValueChanged += delegate
            {
                set(Config.ColorToString(c.Value));
                if (!building) Raise();
            };
            AddRow(card, label, c, null);
        }

        void AddCombo(Card card, string label, string[] names, string[] values,
                      Func<string> get, Action<string> set)
        {
            FlatCombo c = new FlatCombo();
            c.Fill(names);
            c.SelectedIndex = Math.Max(0, Array.IndexOf(values, get()));
            c.BackColor = Color.FromArgb(30, 30, 35);
            c.SelectedIndexChanged += delegate
            {
                int i = c.SelectedIndex;
                if (i >= 0 && i < values.Length) set(values[i]);
                if (!building) Raise();
            };
            AddRow(card, label, c, null);
        }

        void AddFontCombo(Card card, string label, Func<string> get, Action<string> set)
        {
            FlatCombo c = new FlatCombo();
            c.PreviewsOwnFont = true;
            List<string> names = FontList.Get();
            c.Items.AddRange(names);
            int i = names.IndexOf(get());
            if (i < 0) i = names.IndexOf("Segoe UI");
            c.SelectedIndex = i < 0 ? 0 : i;
            c.BackColor = Color.FromArgb(30, 30, 35);
            c.SelectedIndexChanged += delegate
            {
                if (c.SelectedIndex >= 0) set(c.Items[c.SelectedIndex]);
                if (!building) Raise();
            };
            AddRow(card, label, c, null);
        }

        void AddChips(Card card, string label, string[] names, Action<int> onClick, Func<int> active)
        {
            FlowLayoutPanel fl = new FlowLayoutPanel();
            fl.Dock = DockStyle.Fill;
            fl.WrapContents = false;
            fl.AutoScroll = false;
            fl.BackColor = Color.FromArgb(30, 30, 35);
            for (int i = 0; i < names.Length; i++)
            {
                Chip ch = new Chip(names[i]);
                ch.Fit();
                ch.Margin = new Padding(0, 0, 6, 0);
                ch.Active = active != null && active() == i;
                int idx = i;
                ch.Click += delegate { onClick(idx); RebuildPage(); };
                fl.Controls.Add(ch);
            }
            AddRow(card, label, fl, null);
        }

        void AddHint(Card card, string text)
        {
            TableLayoutPanel b = card.Body;
            int row = b.RowCount;
            b.RowCount = row + 1;
            b.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            Label l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Color.FromArgb(105, 105, 116);
            l.BackColor = Color.FromArgb(30, 30, 35);
            l.Font = Theme.F(8f, false);
            l.Margin = new Padding(0, 0, 12, 0);
            b.Controls.Add(l, 0, row);
            b.SetColumnSpan(l, 3);
        }

        // ================= общее =================

        void RebuildPage()
        {
            int keep = host.ScrollY;
            ShowPage(page);
            host.ScrollTo(keep);
        }

        void Raise()
        {
            preview.Cfg = Cfg;
            preview.Invalidate();
            if (LiveChanged != null) LiveChanged();
        }

        public void UpdatePreviewTrack(TrackInfo t, Image thumb)
        {
            Track = t; Thumb = thumb;
            preview.Track = t; preview.Thumb = thumb;
            preview.Invalidate();
        }

        void DumpLayout()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("FORM client=" + ClientSize.Width + "x" + ClientSize.Height);
            DumpCtrl(sb, this, 0);
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.StartupPath, "layout.txt"), sb.ToString());
            }
            catch { }
        }

        void DumpCtrl(System.Text.StringBuilder sb, Control parent, int depth)
        {
            foreach (Control c in parent.Controls)
            {
                string extra = "";
                if (c is Label) extra = " text=\"" + ((Label)c).Text + "\"";
                else if (c is Slider) extra = " val=" + ((Slider)c).DisplayValue();
                else if (c is ToggleSwitch) extra = " checked=" + ((ToggleSwitch)c).Checked;
                else if (c is FlatCombo) extra = " sel=\"" + ((FlatCombo)c).SelectedText + "\"";
                sb.AppendLine(new string(' ', depth * 2) + c.GetType().Name +
                    " b=" + c.Left + "," + c.Top + "," + c.Width + "x" + c.Height +
                    " dock=" + c.Dock + extra);
                if (c.Controls.Count > 0) DumpCtrl(sb, c, depth + 1);
            }
        }
    }

    static class FontList
    {
        static List<string> cache;

        public static List<string> Get()
        {
            if (cache != null) return cache;
            List<string> list = new List<string>();
            try
            {
                using (InstalledFontCollection col = new InstalledFontCollection())
                {
                    foreach (FontFamily f in col.Families)
                    {
                        string n = f.Name;
                        if (n.Length == 0) continue;
                        if (n.StartsWith("@")) continue;
                        list.Add(n);
                    }
                }
            }
            catch { }
            list.Sort(StringComparer.CurrentCultureIgnoreCase);
            if (!list.Contains("Segoe UI")) list.Insert(0, "Segoe UI");
            cache = list;
            return cache;
        }
    }
}
