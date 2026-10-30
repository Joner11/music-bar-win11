using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        public PreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(22, 22, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            // имитация панели задач
            int tbH = 48;
            using (var br = new SolidBrush(Color.FromArgb(30, 30, 34)))
                g.FillRectangle(br, 0, Height - tbH, Width, tbH);
            using (var pen = new Pen(Color.FromArgb(52, 52, 58)))
                g.DrawLine(pen, 0, Height - tbH, Width, Height - tbH);

            if (Cfg == null) return;

            float avail = Width - 24;
            float scale = Math.Min(1.0f, avail / Math.Max(1, Cfg.Width));
            int w = (int)Math.Round(Cfg.Width * scale);
            int h = (int)Math.Round(Cfg.Height * scale);

            using (var bmp = Renderer.Render(Cfg, Track, Thumb, ScrollPx, scale))
            {
                int x = (Width - w) / 2;
                int y = Height - tbH + (tbH - h) / 2;
                g.DrawImageUnscaled(bmp, x, y);
            }

            using (var f = new Font("Segoe UI", 7.5f))
            using (var br = new SolidBrush(Color.FromArgb(110, 110, 118)))
                g.DrawString("предпросмотр (масштаб " + (int)Math.Round(scale * 100) + "%)", f, br, 8, Height - tbH - 18);
        }
    }

    class SettingsForm : Form
    {
        public Config Cfg;
        public TrackInfo Track;
        public Image Thumb;
        public event Action LiveChanged;
        public event Action ClosedEvent;

        bool loading = true;
        TabControl tabs;
        PreviewBox preview;
        System.Windows.Forms.Timer previewTick;
        float scroll;

        // контролы
        NumericUpDown nWidth, nHeight, nRightMargin, nVertOffset, nCorner, nPadding;
        CheckBox cBgEnabled, cGradient, cBorderEnabled;
        ComboBox cbBgMode;
        NumericUpDown nCoverBlur, nCoverDark, nCoverSat;
        NumericUpDown nOpacity, nBorderAlpha, nBorderWidth, nGradAngle;
        Button bBg, bBg2, bBorderCol;
        TextBox tbFont;
        NumericUpDown nTitleSize, nArtistSize, nLineSpacing;
        CheckBox cTitleBold, cArtistBold, cMarquee, cDimPaused, cTextShadow;
        Button bTitleCol, bArtistCol;
        ComboBox cbAlign, cbSecond, cbVAlign;
        NumericUpDown nMarqueeSpeed, nMarqueeGap;
        ComboBox cbThumbSide;
        NumericUpDown nThumbSize, nThumbCorner;
        CheckBox cThumbPlaceholder;
        CheckBox cProgress;
        Button bProgressCol;
        NumericUpDown nProgressHeight, nProgressAlpha, nProgressOffset;
        CheckBox cShowPaused, cHideIdle, cAlways, cHideFull, cAutostart;
        ComboBox cbDoubleClick, cbSingleClick;

        public SettingsForm(Config cfg, TrackInfo track, Image thumb)
        {
            Cfg = cfg; Track = track; Thumb = thumb;

            Text = "Music Bar Win11 — настройки";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 740);
            MinimumSize = new Size(660, 520);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(32, 32, 36);
            ForeColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ---- низ ----
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.FromArgb(40, 40, 45) };
            Controls.Add(bottom);

            var bReset = new Button
            {
                Text = "Сбросить всё",
                Left = 14, Top = 12, Width = 130, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 58, 64),
                ForeColor = Color.White
            };
            bReset.FlatAppearance.BorderColor = Color.FromArgb(84, 84, 92);
            bReset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Сбросить все настройки к значениям по умолчанию?", "Music Bar Win11",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Cfg.CopyFrom(new Config());
                    LoadFromConfig();
                    Raise();
                }
            };

            var bClose = new Button
            {
                Text = "Готово", Width = 120, Height = 32, Top = 11,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK
            };
            bClose.FlatAppearance.BorderSize = 0;
            bClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            bottom.Controls.Add(bReset);
            bottom.Controls.Add(bClose);
            bottom.Resize += (s, e) => { bClose.Left = bottom.Width - bClose.Width - 16; };
            bClose.Left = bottom.Width - bClose.Width - 16;

            // ---- предпросмотр ----
            var previewHost = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.FromArgb(22, 22, 24), Padding = new Padding(0) };
            preview = new PreviewBox { Dock = DockStyle.Fill, Cfg = cfg, Track = track, Thumb = thumb };
            previewHost.Controls.Add(preview);
            Controls.Add(previewHost);

            // ---- вкладки ----
            tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(131, 32),
                Padding = new Point(14, 6)
            };
            tabs.DrawItem += DrawTab;
            Controls.Add(tabs);
            tabs.BringToFront();

            BuildSizeTab();
            BuildBackgroundTab();
            BuildTextTab();
            BuildThumbTab();
            BuildBehaviorTab();

            LoadFromConfig();
            loading = false;

            previewTick = new System.Windows.Forms.Timer { Interval = 40 };
            previewTick.Tick += (s, e) =>
            {
                if (Cfg.Marquee && Cfg.MarqueeSpeed > 0 && Cfg.Align == "left") scroll += Cfg.MarqueeSpeed * 0.04f;
                preview.ScrollPx = scroll;
                preview.Invalidate();
            };
            previewTick.Start();

            int startTab = 0;
            int.TryParse(Environment.GetEnvironmentVariable("NP_START_TAB"), out startTab);
            if (startTab > 0 && startTab < tabs.TabPages.Count) tabs.SelectedIndex = startTab;

            if (Environment.GetEnvironmentVariable("NP_DUMP_LAYOUT") == "1")
            {
                Shown += (s2, e2) => DumpLayout();
            }

            FormClosed += (s, e) =>
            {
                previewTick.Stop();
                if (ClosedEvent != null) ClosedEvent();
            };
        }

        void DumpLayout()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("FORM client=" + ClientSize.Width + "x" + ClientSize.Height);
            DumpCtrl(sb, this, 0);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath, "layout.txt"), sb.ToString()); } catch { }
        }

        void DumpCtrl(System.Text.StringBuilder sb, Control parent, int depth)
        {
            foreach (Control c in parent.Controls)
            {
                sb.AppendLine(new string(' ', depth * 2) + c.GetType().Name + " [" + c.Name + "] " +
                    "b=" + c.Left + "," + c.Top + "," + c.Width + "x" + c.Height +
                    " dock=" + c.Dock + " vis=" + c.Visible +
                    (c is Label ? " text=\"" + ((Label)c).Text + "\"" : "") +
                    (c is NumericUpDown ? " val=" + ((NumericUpDown)c).Value : ""));
                if (c.Controls.Count > 0) DumpCtrl(sb, c, depth + 1);
            }
        }

        void DrawTab(object sender, DrawItemEventArgs e)
        {
            var page = tabs.TabPages[e.Index];
            bool sel = e.Index == tabs.SelectedIndex;
            using (var b = new SolidBrush(sel ? Color.FromArgb(46, 46, 52) : Color.FromArgb(36, 36, 40)))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (sel)
                using (var b = new SolidBrush(Color.FromArgb(0, 120, 212)))
                    e.Graphics.FillRectangle(b, new Rectangle(e.Bounds.X, e.Bounds.Bottom - 3, e.Bounds.Width, 3));
            TextRenderer.DrawText(e.Graphics, page.Text, new Font("Segoe UI", 9f), e.Bounds,
                sel ? Color.White : Color.FromArgb(165, 165, 172),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ================= построение вкладок =================

        TabPage NewPage(string name)
        {
            var p = new TabPage(name)
            {
                BackColor = Color.FromArgb(46, 46, 52),
                ForeColor = Color.White,
                AutoScroll = true,
                Padding = new Padding(0)
            };
            tabs.TabPages.Add(p);
            return p;
        }

        TableLayoutPanel NewGrid(TabPage page)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Padding = new Padding(18, 14, 18, 20),
                BackColor = Color.Transparent
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            page.Controls.Add(t);
            return t;
        }

        int NextRow(TableLayoutPanel t)
        {
            t.RowCount = t.RowStyles.Count + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return t.RowStyles.Count - 1;
        }

        void AddRow(TableLayoutPanel t, string label, Control c)
        {
            int row = NextRow(t);
            var l = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(214, 214, 220),
                Margin = new Padding(0, 4, 10, 4),
                Height = 26
            };
            if (c is CheckBox)
            {
                c.Dock = DockStyle.Fill;
            }
            else
            {
                c.Dock = DockStyle.None;
                c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                c.Width = 190;
                if (c is Button) c.Width = 120;
            }
            c.Margin = new Padding(0, 4, 0, 4);
            c.MinimumSize = new Size(0, 24);
            t.Controls.Add(l, 0, row);
            t.Controls.Add(c, 1, row);
        }

        void AddSection(TableLayoutPanel t, string text)
        {
            int row = NextRow(t);
            var l = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 30,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 170, 240),
                Margin = new Padding(0, 10, 0, 2)
            };
            t.Controls.Add(l, 0, row);
            t.SetColumnSpan(l, 2);
        }

        void AddHint(TableLayoutPanel t, string text)
        {
            int row = NextRow(t);
            var l = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(140, 140, 150),
                Margin = new Padding(0, 0, 0, 6)
            };
            t.Controls.Add(l, 0, row);
            t.SetColumnSpan(l, 2);
        }

        NumericUpDown Num(decimal min, decimal max, decimal step, int decimals)
        {
            var n = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = step,
                DecimalPlaces = decimals,
                BackColor = Color.FromArgb(36, 36, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Left
            };
            n.ValueChanged += (s, e) => Raise();
            return n;
        }

        CheckBox Chk(string text)
        {
            var c = new CheckBox
            {
                Text = text,
                ForeColor = Color.White,
                AutoSize = false,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft
            };
            c.CheckedChanged += (s, e) => Raise();
            return c;
        }

        Button ColorBtn()
        {
            var b = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Height = 24,
                Text = ""
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(104, 104, 112);
            b.FlatAppearance.BorderSize = 1;
            b.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = b.BackColor, FullOpen = true, AnyColor = true })
                {
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        SetColorBtn(b, cd.Color);
                        Raise();
                    }
                }
            };
            return b;
        }

        ComboBox Combo(string[] items, string[] values, bool editable)
        {
            var c = new ComboBox
            {
                DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(36, 36, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Tag = values
            };
            c.Items.AddRange(items);
            if (!editable) c.SelectedIndexChanged += (s, e) => Raise();
            else c.TextChanged += (s, e) => Raise();
            return c;
        }

        void BuildSizeTab()
        {
            var p = NewPage("Размер");
            var t = NewGrid(p);

            AddSection(t, "Габариты");
            nWidth = Num(120, 1600, 5, 0); AddRow(t, "Ширина, px", nWidth);
            nHeight = Num(20, 160, 1, 0); AddRow(t, "Высота, px", nHeight);
            nCorner = Num(0, 40, 1, 0); AddRow(t, "Скругление углов, px", nCorner);
            nPadding = Num(0, 24, 1, 0); AddRow(t, "Внутренние отступы, px", nPadding);

            AddSection(t, "Положение на панели задач");
            nRightMargin = Num(-400, 600, 5, 0); AddRow(t, "Отступ от значков трея", nRightMargin);
            nVertOffset = Num(-60, 60, 1, 0); AddRow(t, "Смещение по вертикали", nVertOffset);
            AddHint(t, "Отрицательный отступ сдвигает виджет вправо (за значки трея), отрицательное смещение — вверх.");
        }

        void BuildBackgroundTab()
        {
            var p = NewPage("Фон");
            var t = NewGrid(p);

            AddSection(t, "Подложка");
            cBgEnabled = Chk("Показывать подложку"); AddRow(t, "Подложка", cBgEnabled);
            cbBgMode = Combo(
                new[] { "Сплошной цвет / градиент", "Размытая обложка", "Размытая обложка + оттенок", "Цвет из обложки" },
                new[] { "color", "cover_blur", "cover_blur_tint", "cover_color" }, false);
            cbBgMode.DropDownWidth = 320;
            AddRow(t, "Фон из обложки", cbBgMode);
            bBg = ColorBtn(); AddRow(t, "Основной цвет (и оттенок)", bBg);
            nOpacity = Num(0, 100, 5, 0); AddRow(t, "Непрозрачность, %", nOpacity);
            AddHint(t, "0 % — виден только текст поверх панели задач, без плашки. " +
                        "«Размытая обложка» берёт текущую обложку трека, пока он играет.");

            AddSection(t, "Обработка обложки-фона");
            nCoverBlur = Num(1, 64, 1, 0); AddRow(t, "Сила размытия", nCoverBlur);
            nCoverDark = Num(0, 95, 5, 0); AddRow(t, "Затемнение, %", nCoverDark);
            nCoverSat = Num(0, 300, 10, 0); AddRow(t, "Насыщенность цвета, %", nCoverSat);
            AddHint(t, "Затемнение и насыщенность действуют для размытого фона и «цвета из обложки».");

            AddSection(t, "Градиент");
            cGradient = Chk("Градиентная заливка"); AddRow(t, "Градиент", cGradient);
            bBg2 = ColorBtn(); AddRow(t, "Второй цвет градиента", bBg2);
            nGradAngle = Num(0, 360, 15, 0); AddRow(t, "Угол градиента, °", nGradAngle);

            AddSection(t, "Рамка");
            cBorderEnabled = Chk("Показывать рамку"); AddRow(t, "Рамка", cBorderEnabled);
            bBorderCol = ColorBtn(); AddRow(t, "Цвет рамки", bBorderCol);
            nBorderWidth = Num(1, 6, 1, 0); AddRow(t, "Толщина рамки, px", nBorderWidth);
            nBorderAlpha = Num(0, 255, 5, 0); AddRow(t, "Яркость рамки (0–255)", nBorderAlpha);
        }

        void BuildTextTab()
        {
            var p = NewPage("Текст");
            var t = NewGrid(p);

            AddSection(t, "Шрифт");
            tbFont = new TextBox
            {
                Text = Cfg.FontName,
                BackColor = Color.FromArgb(36, 36, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            tbFont.TextChanged += (s, e) => Raise();
            AddRow(t, "Название шрифта", tbFont);
            AddHint(t, "Например: Segoe UI, Tahoma, Verdana, Arial, Calibri, Candara, Georgia, Consolas.");

            AddSection(t, "Название трека");
            nTitleSize = Num(5, 30, 0.25m, 1); AddRow(t, "Размер шрифта", nTitleSize);
            cTitleBold = Chk("Жирный"); AddRow(t, "Начертание", cTitleBold);
            bTitleCol = ColorBtn(); AddRow(t, "Цвет", bTitleCol);

            AddSection(t, "Вторая строка");
            cbSecond = Combo(
                new[] { "Исполнитель", "Альбом", "Приложение", "Исполнитель · Приложение", "Приложение · Исполнитель", "Не показывать" },
                new[] { "artist", "album", "app", "artist_app", "app_artist", "none" }, false);
            AddRow(t, "Содержимое", cbSecond);
            nArtistSize = Num(4, 24, 0.25m, 1); AddRow(t, "Размер шрифта", nArtistSize);
            cArtistBold = Chk("Жирный"); AddRow(t, "Начертание", cArtistBold);
            bArtistCol = ColorBtn(); AddRow(t, "Цвет", bArtistCol);

            AddSection(t, "Расположение");
            cbAlign = Combo(new[] { "По левому краю", "По центру", "По правому краю" },
                new[] { "left", "center", "right" }, false);
            AddRow(t, "Выравнивание по горизонтали", cbAlign);
            cbVAlign = Combo(new[] { "По верхнему краю", "По центру", "По нижнему краю" },
                new[] { "top", "center", "bottom" }, false);
            AddRow(t, "Выравнивание по вертикали", cbVAlign);
            nLineSpacing = Num(-4, 16, 1, 0); AddRow(t, "Интервал между строками, px", nLineSpacing);
            cTextShadow = Chk("Тень под текстом (для прозрачного фона)"); AddRow(t, "Тень", cTextShadow);

            AddSection(t, "Бегущая строка");
            cMarquee = Chk("Прокручивать длинные названия"); AddRow(t, "Бегущая строка", cMarquee);
            nMarqueeSpeed = Num(10, 400, 5, 0); AddRow(t, "Скорость, px/сек", nMarqueeSpeed);
            nMarqueeGap = Num(0, 200, 5, 0); AddRow(t, "Промежуток между повторами, px", nMarqueeGap);
            AddHint(t, "Бегущая строка работает при выравнивании по левому краю.");

            cDimPaused = Chk("Приглушать текст и обложку на паузе"); AddRow(t, "Пауза", cDimPaused);
        }

        void BuildThumbTab()
        {
            var p = NewPage("Обложка");
            var t = NewGrid(p);

            AddSection(t, "Обложка");
            cbThumbSide = Combo(new[] { "Слева от текста", "Справа от текста", "Не показывать" },
                new[] { "left", "right", "none" }, false);
            AddRow(t, "Положение", cbThumbSide);
            nThumbSize = Num(0, 96, 1, 0); AddRow(t, "Размер, px (0 = авто)", nThumbSize);
            nThumbCorner = Num(0, 40, 1, 0); AddRow(t, "Скругление, px", nThumbCorner);
            cThumbPlaceholder = Chk("Показывать заглушку, если обложки нет"); AddRow(t, "Заглушка", cThumbPlaceholder);
            AddHint(t, "Обложку отдают Spotify, Яндекс Музыка, браузеры (Chrome, Edge, Firefox) и большинство плееров. " +
                        "Некоторые приложения публикуют её с задержкой в 1–3 секунды — виджет подхватит автоматически.");

            AddSection(t, "Полоса прогресса");
            cProgress = Chk("Показывать прогресс воспроизведения"); AddRow(t, "Прогресс", cProgress);
            bProgressCol = ColorBtn(); AddRow(t, "Цвет полосы", bProgressCol);
            nProgressHeight = Num(1, 8, 1, 0); AddRow(t, "Толщина, px", nProgressHeight);
            nProgressAlpha = Num(0, 255, 5, 0); AddRow(t, "Яркость (0–255)", nProgressAlpha);
            nProgressOffset = Num(-80, 40, 1, 0); AddRow(t, "Сдвиг вверх от низа, px", nProgressOffset);
            AddHint(t, "Отрицательное значение опускает полосу ниже, положительное — поднимает её выше.");
        }

        void BuildBehaviorTab()
        {
            var p = NewPage("Поведение");
            var t = NewGrid(p);

            AddSection(t, "Видимость");
            cShowPaused = Chk("Показывать трек на паузе"); AddRow(t, "На паузе", cShowPaused);
            cHideIdle = Chk("Скрывать, когда ничего не играет"); AddRow(t, "Без трека", cHideIdle);
            cAlways = Chk("Всегда показывать виджет"); AddRow(t, "Всегда", cAlways);
            cHideFull = Chk("Скрывать в полноэкранных приложениях и играх"); AddRow(t, "Полный экран", cHideFull);

            AddSection(t, "Мышь");
            cbSingleClick = Combo(
                new[] { "Открыть настройки", "Пауза / воспроизведение", "Следующий трек", "Предыдущий трек", "Ничего" },
                new[] { "settings", "toggle", "next", "prev", "none" }, false);
            AddRow(t, "Одиночный клик", cbSingleClick);
            cbDoubleClick = Combo(
                new[] { "Пауза / воспроизведение", "Открыть настройки", "Следующий трек", "Предыдущий трек", "Ничего" },
                new[] { "toggle", "settings", "next", "prev", "none" }, false);
            AddRow(t, "Двойной клик", cbDoubleClick);
            AddHint(t, "Колесо мыши: вверх — следующий трек, вниз — предыдущий. Правый клик — меню.");

            AddSection(t, "Прочее");
            cAutostart = Chk("Запускать при входе в Windows"); AddRow(t, "Автозапуск", cAutostart);
            AddHint(t, "Изменения применяются сразу и видны на панели задач. Кнопка «Готово» просто закрывает окно.");
        }

        // ================= синхронизация =================

        static void SetColorBtn(Button b, Color c)
        {
            b.BackColor = c;
            b.ForeColor = c.GetBrightness() > 0.55f ? Color.Black : Color.White;
            b.Text = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        static void SetCombo(ComboBox c, string value)
        {
            var vals = (string[])c.Tag;
            for (int i = 0; i < vals.Length; i++)
                if (vals[i] == value) { c.SelectedIndex = i; return; }
            if (c.Items.Count > 0) c.SelectedIndex = 0;
        }

        static string ComboValue(ComboBox c)
        {
            var vals = (string[])c.Tag;
            int i = c.SelectedIndex;
            if (i < 0 || i >= vals.Length) return vals.Length > 0 ? vals[0] : "";
            return vals[i];
        }

        static decimal ClampD(decimal v, NumericUpDown n)
        {
            if (v < n.Minimum) return n.Minimum;
            if (v > n.Maximum) return n.Maximum;
            return v;
        }

        void LoadFromConfig()
        {
            loading = true;
            nWidth.Value = ClampD(Cfg.Width, nWidth);
            nHeight.Value = ClampD(Cfg.Height, nHeight);
            nRightMargin.Value = ClampD(Cfg.RightMargin, nRightMargin);
            nVertOffset.Value = ClampD(Cfg.VertOffset, nVertOffset);
            nCorner.Value = ClampD(Cfg.Corner, nCorner);
            nPadding.Value = ClampD(Cfg.Padding, nPadding);

            cBgEnabled.Checked = Cfg.BgEnabled;
            SetCombo(cbBgMode, Cfg.BgMode);
            nCoverBlur.Value = ClampD(Cfg.CoverBlur, nCoverBlur);
            nCoverDark.Value = ClampD(Cfg.CoverDark, nCoverDark);
            nCoverSat.Value = ClampD(Cfg.CoverSaturation, nCoverSat);
            SetColorBtn(bBg, Color.FromArgb(Cfg.BgR, Cfg.BgG, Cfg.BgB));
            nOpacity.Value = ClampD(Cfg.OpacityPercent, nOpacity);
            cGradient.Checked = Cfg.Gradient;
            SetColorBtn(bBg2, Color.FromArgb(Cfg.BgR2, Cfg.BgG2, Cfg.BgB2));
            nGradAngle.Value = ClampD(Cfg.GradientAngle, nGradAngle);

            cBorderEnabled.Checked = Cfg.BorderEnabled;
            SetColorBtn(bBorderCol, Color.FromArgb(Cfg.BorderR, Cfg.BorderG, Cfg.BorderB));
            nBorderWidth.Value = ClampD(Cfg.BorderWidth, nBorderWidth);
            nBorderAlpha.Value = ClampD(Cfg.BorderAlpha, nBorderAlpha);

            tbFont.Text = Cfg.FontName;
            nTitleSize.Value = ClampD((decimal)Cfg.TitleSize, nTitleSize);
            cTitleBold.Checked = Cfg.TitleBold;
            SetColorBtn(bTitleCol, Config.ParseColor(Cfg.TitleColor, Color.White));
            nArtistSize.Value = ClampD((decimal)Cfg.ArtistSize, nArtistSize);
            cArtistBold.Checked = Cfg.ArtistBold;
            SetColorBtn(bArtistCol, Config.ParseColor(Cfg.ArtistColor, Color.FromArgb(168, 168, 172)));
            SetCombo(cbAlign, Cfg.Align);
            SetCombo(cbVAlign, Cfg.VAlign);
            SetCombo(cbSecond, Cfg.SecondLine);
            nLineSpacing.Value = ClampD(Cfg.LineSpacing, nLineSpacing);
            cTextShadow.Checked = Cfg.TextShadow;
            cMarquee.Checked = Cfg.Marquee;
            nMarqueeSpeed.Value = ClampD(Cfg.MarqueeSpeed, nMarqueeSpeed);
            nMarqueeGap.Value = ClampD(Cfg.MarqueeGap, nMarqueeGap);
            cDimPaused.Checked = Cfg.DimPaused;

            SetCombo(cbThumbSide, Cfg.ThumbSide);
            nThumbSize.Value = ClampD(Cfg.ThumbSize, nThumbSize);
            nThumbCorner.Value = ClampD(Cfg.ThumbCorner, nThumbCorner);
            cThumbPlaceholder.Checked = Cfg.ThumbPlaceholder;
            cProgress.Checked = Cfg.Progress;
            SetColorBtn(bProgressCol, Config.ParseColor(Cfg.ProgressColor, Color.White));
            nProgressHeight.Value = ClampD(Cfg.ProgressHeight, nProgressHeight);
            nProgressAlpha.Value = ClampD(Cfg.ProgressAlpha, nProgressAlpha);
            nProgressOffset.Value = ClampD(Cfg.ProgressOffset, nProgressOffset);

            cShowPaused.Checked = Cfg.ShowPaused;
            cHideIdle.Checked = Cfg.HideWhenIdle;
            cAlways.Checked = Cfg.AlwaysVisible;
            cHideFull.Checked = Cfg.HideFullscreen;
            SetCombo(cbSingleClick, Cfg.SingleClick);
            SetCombo(cbDoubleClick, Cfg.DoubleClick);
            cAutostart.Checked = Autostart.Enabled;
            loading = false;

            preview.Cfg = Cfg;
            preview.Invalidate();
        }

        void Gather()
        {
            Cfg.Width = (int)nWidth.Value;
            Cfg.Height = (int)nHeight.Value;
            Cfg.RightMargin = (int)nRightMargin.Value;
            Cfg.VertOffset = (int)nVertOffset.Value;
            Cfg.Corner = (int)nCorner.Value;
            Cfg.Padding = (int)nPadding.Value;

            Cfg.BgEnabled = cBgEnabled.Checked;
            Cfg.BgMode = ComboValue(cbBgMode);
            Cfg.CoverBlur = (int)nCoverBlur.Value;
            Cfg.CoverDark = (int)nCoverDark.Value;
            Cfg.CoverSaturation = (int)nCoverSat.Value;
            Cfg.BgR = bBg.BackColor.R; Cfg.BgG = bBg.BackColor.G; Cfg.BgB = bBg.BackColor.B;
            Cfg.OpacityPercent = (int)nOpacity.Value;
            Cfg.Gradient = cGradient.Checked;
            Cfg.BgR2 = bBg2.BackColor.R; Cfg.BgG2 = bBg2.BackColor.G; Cfg.BgB2 = bBg2.BackColor.B;
            Cfg.GradientAngle = (int)nGradAngle.Value;

            Cfg.BorderEnabled = cBorderEnabled.Checked;
            Cfg.BorderR = bBorderCol.BackColor.R; Cfg.BorderG = bBorderCol.BackColor.G; Cfg.BorderB = bBorderCol.BackColor.B;
            Cfg.BorderWidth = (int)nBorderWidth.Value;
            Cfg.BorderAlpha = (int)nBorderAlpha.Value;

            string f = tbFont.Text.Trim();
            Cfg.FontName = f.Length > 0 ? f : "Segoe UI";
            Cfg.TitleSize = (float)nTitleSize.Value;
            Cfg.TitleBold = cTitleBold.Checked;
            Cfg.TitleColor = Config.ColorToString(bTitleCol.BackColor);
            Cfg.ArtistSize = (float)nArtistSize.Value;
            Cfg.ArtistBold = cArtistBold.Checked;
            Cfg.ArtistColor = Config.ColorToString(bArtistCol.BackColor);
            Cfg.Align = ComboValue(cbAlign);
            Cfg.VAlign = ComboValue(cbVAlign);
            Cfg.SecondLine = ComboValue(cbSecond);
            Cfg.LineSpacing = (int)nLineSpacing.Value;
            Cfg.TextShadow = cTextShadow.Checked;
            Cfg.Marquee = cMarquee.Checked;
            Cfg.MarqueeSpeed = (int)nMarqueeSpeed.Value;
            Cfg.MarqueeGap = (int)nMarqueeGap.Value;
            Cfg.DimPaused = cDimPaused.Checked;

            Cfg.ThumbSide = ComboValue(cbThumbSide);
            Cfg.ThumbSize = (int)nThumbSize.Value;
            Cfg.ThumbCorner = (int)nThumbCorner.Value;
            Cfg.ThumbPlaceholder = cThumbPlaceholder.Checked;
            Cfg.Progress = cProgress.Checked;
            Cfg.ProgressColor = Config.ColorToString(bProgressCol.BackColor);
            Cfg.ProgressHeight = (int)nProgressHeight.Value;
            Cfg.ProgressAlpha = (int)nProgressAlpha.Value;
            Cfg.ProgressOffset = (int)nProgressOffset.Value;

            Cfg.ShowPaused = cShowPaused.Checked;
            Cfg.HideWhenIdle = cHideIdle.Checked;
            Cfg.AlwaysVisible = cAlways.Checked;
            Cfg.HideFullscreen = cHideFull.Checked;
            Cfg.SingleClick = ComboValue(cbSingleClick);
            Cfg.DoubleClick = ComboValue(cbDoubleClick);

            bool wantAuto = cAutostart.Checked;
            if (wantAuto != Autostart.Enabled) Autostart.Enabled = wantAuto;
        }

        void Raise()
        {
            if (loading) return;
            Gather();
            preview.Cfg = Cfg;
            preview.Invalidate();
            if (LiveChanged != null) LiveChanged();
        }

        public void UpdatePreviewTrack(TrackInfo t, Image thumb)
        {
            Track = t; Thumb = thumb;
            preview.Track = t; preview.Thumb = thumb;
        }
    }
}
