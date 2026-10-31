using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NowPlaying
{
    static class W32
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string n);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr val);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);

        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        public static bool IsFullscreenForeground()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == FindWindow("Shell_TrayWnd", null)) return false;

            // окна рабочего стола и обоев (в т.ч. Wallpaper Engine) — не «полный экран»
            var cls = new System.Text.StringBuilder(256);
            GetClassName(fg, cls, cls.Capacity);
            string cn = cls.ToString();
            if (cn == "Progman" || cn == "WorkerW" || cn == "Shell_TrayWnd" ||
                cn == "Windows.UI.Core.CoreWindow" || cn == "SysListView32")
                return false;

            uint pid;
            GetWindowThreadProcessId(fg, out pid);
            try
            {
                string exe = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
                if (exe == "wallpaper32" || exe == "wallpaper64" ||
                    exe == "explorer" || exe == "dwm" || exe == "searchhost")
                    return false;
            }
            catch { }
            RECT r;
            if (!GetWindowRect(fg, out r)) return false;
            if (r.Right - r.Left < 100 || r.Bottom - r.Top < 100) return false;
            IntPtr mon = MonitorFromWindow(fg, 2);
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(mon, ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left + 1 && r.Top <= mi.rcMonitor.Top + 1
                && r.Right >= mi.rcMonitor.Right - 1 && r.Bottom >= mi.rcMonitor.Bottom - 1;
        }
    }

    class Widget : Form
    {
        public Config Cfg;
        TrackInfo track;
        Image thumb;
        string thumbKey = "";
        // отложенная утилизация обложек: нельзя Dispose'ить картинку, пока UI-поток её рисует
        readonly System.Collections.Generic.List<Image> graveyard = new System.Collections.Generic.List<Image>();
        DateTime lastGoodTrack = DateTime.MinValue;
        string curKey = "";
        int thumbAttempts;
        DateTime lastThumbTry = DateTime.MinValue;

        readonly object sync = new object();
        Bitmap backBuffer;

        float scrollPx;
        int scrollPause;
        float dpiScale = 1f;

        NotifyIcon trayIcon;
        System.Windows.Forms.Timer uiTick, pollTick, posTick;
        SettingsForm settings;
        Thread worker;
        volatile bool workerAlive = true;
        volatile bool wantPoll = true;
        DateTime lastTopmost = DateTime.MinValue;
        volatile bool needRender = true;

        public Widget(Config cfg)
        {
            Cfg = cfg;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Black;
            Visible = false;

            BuildTray();

            uiTick = new System.Windows.Forms.Timer { Interval = 40 };
            uiTick.Tick += UiTick;
            uiTick.Start();

            pollTick = new System.Windows.Forms.Timer { Interval = 700 };
            pollTick.Tick += (s, e) => { wantPoll = true; };
            pollTick.Start();

            posTick = new System.Windows.Forms.Timer { Interval = 800 };
            posTick.Tick += (s, e) => { UpdatePosition(); };
            posTick.Start();

            worker = new Thread(WorkerLoop);
            worker.IsBackground = true;
            worker.Start();

            var ipc = new Thread(IpcLoop);
            ipc.IsBackground = true;
            ipc.Start();

            wantPoll = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000;   // WS_EX_LAYERED
                cp.ExStyle |= 0x08000000;   // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // ---------- трей ----------
        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9f);

            var miSettings = new ToolStripMenuItem("Настройки…") { Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            miSettings.Click += (s, e) => OpenSettings();

            var miToggle = new ToolStripMenuItem("Пауза / воспроизведение");
            miToggle.Click += (s, e) => { var t = track; if (t != null) MediaApi.TogglePlayPause(t.Session); };
            var miPrev = new ToolStripMenuItem("Предыдущий трек");
            miPrev.Click += (s, e) => { var t = track; if (t != null) MediaApi.Skip(t.Session, false); };
            var miNext = new ToolStripMenuItem("Следующий трек");
            miNext.Click += (s, e) => { var t = track; if (t != null) MediaApi.Skip(t.Session, true); };

            var miVisible = new ToolStripMenuItem("Показать виджет") { CheckOnClick = true, Checked = true };
            miVisible.Click += (s, e) =>
            {
                if (miVisible.Checked) { UpdatePosition(); }
                else { Hide(); }
            };

            var miAuto = new ToolStripMenuItem("Запускать при входе в Windows") { CheckOnClick = true, Checked = Autostart.Enabled };
            miAuto.Click += (s, e) => Autostart.Enabled = miAuto.Checked;

            var miExit = new ToolStripMenuItem("Выход");
            miExit.Click += (s, e) => Close();

            menu.Items.Add(miSettings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miToggle);
            menu.Items.Add(miPrev);
            menu.Items.Add(miNext);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miVisible);
            menu.Items.Add(miAuto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            trayIcon = new NotifyIcon();
            try { trayIcon.Icon = new Icon(Application.StartupPath + "\\icon.ico"); }
            catch { trayIcon.Icon = SystemIcons.Application; }
            trayIcon.Text = "Music Bar Win11 — сейчас играет";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => OpenSettings();
            trayIcon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) OpenSettings(); };

            // меню и на самом виджете
            ContextMenuStrip = menu;
        }

        public void OpenSettings()
        {
            if (settings != null && !settings.IsDisposed)
            {
                settings.Activate();
                return;
            }
            SettingsForm s;
            try { s = new SettingsForm(Cfg, track, thumb); }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.StartupPath, "error.log"), DateTime.Now + " OPEN SETTINGS: " + ex + Environment.NewLine); } catch { }
                MessageBox.Show(ex.ToString(), "Ошибка настроек");
                return;
            }
            settings = s;
            s.LiveChanged += () =>
            {
                needRender = true;
                UpdatePosition();
            };
            s.ClosedEvent += () =>
            {
                Cfg.Save();
                settings = null;
                needRender = true;
                UpdatePosition();
            };
            s.Show();
            s.Activate();
        }

        // ---------- рабочий поток ----------
        void WorkerLoop()
        {
            while (workerAlive)
            {
                try
                {
                    bool poll = wantPoll;
                    wantPoll = false;

                    TrackInfo best = null;
                    string newThumbKey = null;

                    if (poll)
                    {
                        var list = MediaApi.Read();
                        if (list != null) best = Pick(list);
                        if (best != null) lastTrackSnapshot = best;
                        lock (sync)
                        {
                            track = best;
                            if (best != null) curKey = best.Key; else curKey = "";
                        }
                        if (best != null) newThumbKey = best.Key;
                    }
                    else
                    {
                        lock (sync) { best = track; }
                        if (best != null) newThumbKey = best.Key;
                    }

                    // --- обложка: грузим (и повторяем, если не пришла) ---
                    if (best != null && Cfg.ThumbSide != "none")
                    {
                        bool need = false;
                        lock (sync) { need = (thumb == null || thumbKey != best.Key); }
                        if (need)
                        {
                            var now = DateTime.UtcNow;
                            double waitSec = thumbAttempts < 3 ? 0.0 : (thumbAttempts < 10 ? 2.0 : 8.0);
                            if (thumbAttempts == 0 || (now - lastThumbTry).TotalSeconds >= waitSec)
                            {
                                lastThumbTry = now;
                                thumbAttempts++;
                                var img = MediaApi.ReadThumbnail(best.Session, 2000);
                                if (img != null)
                                {
                                    lock (sync)
                                    {
                                        if (thumb != null) graveyard.Add(thumb);   // не Dispose сразу
                                        thumb = img;
                                        thumbKey = best.Key;
                                    }
                                    thumbAttempts = 0;
                                    needRender = true;
                                }
                            }
                        }
                    }
                    else if (best == null)
                    {
                        lock (sync)
                        {
                            if (thumb != null) { graveyard.Add(thumb); thumb = null; }
                            thumbKey = "";
                        }
                        thumbAttempts = 0;
                    }

                    // Обложка может отсутствовать у нового источника - не спешим её выбрасывать

                    // сменился трек → сбросить прокрутку
                    string k;
                    lock (sync) { k = curKey; }
                    if (k != _lastRenderedKey)
                    {
                        _lastRenderedKey = k;
                        scrollPx = 0;
                        scrollPause = 20;
                        needRender = true;
                    }

                    // обновляем превью в настройках
                    if (settings != null && !settings.IsDisposed && poll)
                    {
                        try
                        {
                            TrackInfo tt; Image th;
                            lock (sync) { tt = track; th = thumb; }
                            settings.UpdatePreviewTrack(tt, th);
                        }
                        catch { }
                    }
                }
                catch { }

                for (int i = 0; i < 12 && workerAlive; i++)
                {
                    if (wantPoll) break;
                    Thread.Sleep(40);
                }
            }
        }

        void IpcLoop()
        {
            try
            {
                var ev = EventWaitHandle.OpenExisting("MusicBarWin11_ShowSettings_9F2A");
                while (workerAlive)
                {
                    if (!ev.WaitOne(500)) continue;
                    try { BeginInvoke((Action)OpenSettings); } catch { }
                }
            }
            catch { }
        }

        string _lastRenderedKey = "";

        TrackInfo Pick(List<TrackInfo> list)
        {
            TrackInfo firstAny = null, playing = null, paused = null, lastSession = null;
            foreach (var t in list)
            {
                if (t == null) continue;
                bool hasText = t.Title.Length > 0 || t.Artist.Length > 0;
                if (!hasText && !Cfg.AlwaysVisible) continue;
                if (t.Session == lastGoodSession) lastSession = t;
                if (firstAny == null) firstAny = t;
                if (t.IsPlaying && playing == null) playing = t;
                if (t.Status == 5 && paused == null) paused = t;
            }

            TrackInfo result = null;
            if (playing != null) result = playing;
            else if (Cfg.ShowPaused && paused != null) result = paused;
            else if (!Cfg.HideWhenIdle) result = firstAny;

            if (result != null)
            {
                lastGoodSession = result.Session;
                lastGoodTrack = DateTime.UtcNow;
                return result;
            }

            // источник на мгновение «пропал» (Spotify при переключении отдаёт пустые
            // свойства, браузер пересоздаёт сессию) — держим прошлый трек ещё 8 секунд,
            // чтобы виджет не мигал и не исчезал.
            if (lastSession != null) return lastSession;
            if ((DateTime.UtcNow - lastGoodTrack).TotalSeconds < 8 && lastTrackSnapshot != null)
                return lastTrackSnapshot;
            return null;
        }

        object lastGoodSession = null;
        TrackInfo lastTrackSnapshot = null;

        // ---------- отрисовка ----------
        void UiTick(object sender, EventArgs e)
        {
            TrackInfo t;
            Image th;
            lock (sync) { t = track; th = thumb; }

            bool overflow = false;
            if (t != null)
            {
                // прокрутка
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                using (var f = new Font(Cfg.FontName, Cfg.TitleSize * dpiScale, Cfg.TitleBold ? FontStyle.Bold : FontStyle.Regular))
                {
                    float w = 0;
                    try { w = g.MeasureString(t.Title, f).Width; } catch { }
                    int textW = TextAreaWidth(th);
                    overflow = w > textW;
                }
                if (overflow && Cfg.Marquee)
                {
                    if (scrollPause > 0) scrollPause--;
                    else { scrollPx += Cfg.MarqueeSpeed * 0.04f; needRender = true; }
                }
                else if (scrollPx != 0) { scrollPx = 0; needRender = true; }
            }

            if (Cfg.Progress && t != null && t.DurationSec > 0 && t.IsPlaying) needRender = true;

            // утилизируем снятые обложки — только здесь, когда точно никто их не рисует
            lock (sync)
            {
                if (graveyard.Count > 0)
                {
                    foreach (var img in graveyard) { try { img.Dispose(); } catch { } }
                    graveyard.Clear();
                }
            }

            if (!needRender) return;
            needRender = false;
            try { Render(); }
            catch
            {
                // любая ошибка отрисовки не должна «убивать» виджет
                needRender = true;
            }
        }

        int TextAreaWidth(Image th)
        {
            int w = (int)Math.Round(Cfg.DrawWidth * dpiScale);
            int pad = (int)Math.Round(Cfg.Padding * dpiScale);
            int side = 0;
            if (Cfg.ThumbSide != "none" && (th != null || Cfg.ThumbPlaceholder))
            {
                int avail = (int)Math.Round(Cfg.DrawHeight * dpiScale) - pad * 2;
                int size = Cfg.ThumbSize > 0 ? (int)Math.Round(Cfg.ThumbSize * dpiScale) : avail;
                if (size > avail) size = avail;
                side = size + (int)Math.Round(6 * dpiScale);
            }
            return Math.Max(8, w - pad * 2 - side);
        }

        void Render()
        {
            TrackInfo t;
            Image th;
            lock (sync) { t = track; th = thumb; }

            if (t == null && Cfg.HideWhenIdle && !Cfg.AlwaysVisible)
            {
                if (Visible) Hide();
                return;
            }

            int w = Math.Max(1, (int)Math.Round(Cfg.DrawWidth * dpiScale));
            int h = Math.Max(1, (int)Math.Round(Cfg.DrawHeight * dpiScale));

            var bmp = Renderer.Render(Cfg, t, th, scrollPx, dpiScale);
            if (backBuffer != null) backBuffer.Dispose();
            backBuffer = bmp;

            if (Width != w || Height != h) SetBounds(Bounds.X, Bounds.Y, w, h);

            ApplyLayered(bmp);
            UpdatePosition();
        }

        void ApplyLayered(Image bmp)
        {
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memDc = W32.CreateCompatibleDC(screenDc);
                hBitmap = ((Bitmap)bmp).GetHbitmap(Color.FromArgb(0));
                oldBitmap = W32.SelectObject(memDc, hBitmap);

                var size = new W32.SIZE(bmp.Width, bmp.Height);
                var ptDst = new W32.POINT(Left, Top);
                var ptSrc = new W32.POINT(0, 0);
                var blend = new W32.BLENDFUNCTION
                {
                    BlendOp = W32.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = W32.AC_SRC_ALPHA
                };
                W32.UpdateLayeredWindow(Handle, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, W32.ULW_ALPHA);
            }
            catch { }
            finally
            {
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    W32.SelectObject(memDc, oldBitmap);
                    W32.DeleteObject(hBitmap);
                }
                if (memDc != IntPtr.Zero) W32.DeleteDC(memDc);
            }
        }

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);

        // ---------- позиция ----------
        void UpdatePosition()
        {
            IntPtr tb = W32.FindWindow("Shell_TrayWnd", null);
            if (tb == IntPtr.Zero) { HideWidget(); return; }
            W32.RECT r;
            if (!W32.GetWindowRect(tb, out r)) { HideWidget(); return; }

            var scr = Screen.FromHandle(tb).Bounds;
            bool off = r.Top >= scr.Bottom || r.Bottom <= scr.Top || r.Left >= scr.Right || r.Right <= scr.Left;
            bool hidden = !W32.IsWindowVisible(tb) || off;
            bool full = Cfg.HideFullscreen && W32.IsFullscreenForeground();

            if (hidden || full) { HideWidget(); return; }

            int tbH = r.Bottom - r.Top;
            if (tbH <= 0 || tbH > 200) tbH = 48;

            float scale;
            using (var g = Graphics.FromHwnd(tb)) scale = g.DpiX / 96f;
            dpiScale = scale;

            // ---- адаптация размеров под конкретный ПК ----
            int effW = Cfg.Width;
            int effH = Cfg.Height;

            if (Cfg.MatchTaskbar)
            {
                // высота плашки = рабочей высоте панели задач минус запас
                effH = Math.Max(16, (int)Math.Round(tbH / scale) - 4);
            }
            if (Cfg.CompactNarrow)
            {
                // узкая панель задач (маленький экран) — ужимаем плашку
                int availCss = (int)((r.Right - r.Left) / scale);
                int maxW = Math.Max(120, availCss / 5);
                if (effW > maxW) effW = maxW;
            }

            Cfg.EffWidth = effW;
            Cfg.EffHeight = effH;

            int margin = (int)Math.Round(Cfg.RightMargin * scale);
            if (margin < 0) margin = 0;                       // не заезжать на значки

            int trayLeft = r.Right - 350;
            IntPtr tnw = W32.FindWindowEx(tb, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tnw != IntPtr.Zero)
            {
                W32.RECT tr;
                if (W32.GetWindowRect(tnw, out tr) && tr.Left > r.Left) trayLeft = tr.Left;
            }

            int w = Math.Max(1, (int)Math.Round(effW * scale));
            int h = Math.Max(1, (int)Math.Round(effH * scale));

            int x = trayLeft - w - margin;
            if (x < r.Left + Math.Max(8, (int)(8 * scale))) x = r.Left + Math.Max(8, (int)(8 * scale));
            int y = h <= tbH ? r.Top + (tbH - h) / 2 : r.Bottom - h;   // выше панели — прижимаем к низу
            y += (int)Math.Round(Cfg.VertOffset * scale);
            if (x + w > r.Right) x = r.Right - w;
            if (x < r.Left - w / 2) x = r.Left - w / 2;

            if (Bounds.X != x || Bounds.Y != y)
            {
                SetBounds(x, y, Bounds.Width, Bounds.Height);
                needRender = true;
            }

            if (!Visible)
            {
                Show();
                if (backBuffer != null) ApplyLayered(backBuffer);
            }

            if ((DateTime.UtcNow - lastTopmost).TotalSeconds > 2)
            {
                lastTopmost = DateTime.UtcNow;
                W32.SetWindowPos(Handle, W32.HWND_TOPMOST, 0, 0, 0, 0,
                    W32.SWP_NOMOVE | W32.SWP_NOSIZE | W32.SWP_NOACTIVATE);
            }
        }

        void HideWidget()
        {
            if (Visible) Hide();
        }

        // ---------- мышь ----------
        void DoAction(string action)
        {
            var t = track;
            switch (action)
            {
                case "toggle": if (t != null) MediaApi.TogglePlayPause(t.Session); break;
                case "next": if (t != null) MediaApi.Skip(t.Session, true); break;
                case "prev": if (t != null) MediaApi.Skip(t.Session, false); break;
                case "settings": OpenSettings(); break;
            }
        }

        /// <summary>Индекс кнопки управления под курсором или -1.</summary>
        int NavHit(Point p)
        {
            if (!Cfg.NavButtons) return -1;
            int w = (int)Math.Round(Cfg.DrawWidth * dpiScale);
            int h = (int)Math.Round(Cfg.DrawHeight * dpiScale);
            int inset = 0;
            if (Cfg.ThumbSide == "right" && (thumb != null || Cfg.ThumbPlaceholder))
            {
                int pad2 = (int)Math.Round(Cfg.Padding * dpiScale);
                int avail2 = h - pad2 * 2;
                int sz = Cfg.ThumbSize > 0 ? (int)Math.Round(Cfg.ThumbSize * dpiScale) : avail2;
                if (sz > avail2) sz = avail2;
                inset = sz + (int)Math.Round(Cfg.TextGap * dpiScale);
            }
            var rects = Renderer.NavRects(Cfg, new Rectangle(0, 0, w, h), dpiScale, inset);
            var kinds = Renderer.NavKinds(Cfg);
            for (int i = 0; i < rects.Length; i++)
                if (rects[i].Contains(p)) return i < kinds.Length ? kinds[i] : -1;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = NavHit(e.Location) >= 0;
            Cursor = over ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && NavHit(e.Location) >= 0)
            {
                _clickTimer.Stop();
                int i = NavHit(e.Location);
                DoAction(i == 0 ? "prev" : (i == 1 ? "toggle" : "next"));
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks >= 2)
                {
                    _clickTimer.Stop();
                    DoAction(Cfg.DoubleClick);
                }
                else
                {
                    _clickTimer.Stop();
                    _clickTimer.Start();
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Middle)
            {
                var t = track;
                if (t != null) MediaApi.TogglePlayPause(t.Session);
            }
        }

        readonly System.Windows.Forms.Timer _clickTimer = new System.Windows.Forms.Timer { Interval = 260 };
        bool _clickTimerHooked;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_clickTimerHooked)
            {
                _clickTimerHooked = true;
                _clickTimer.Tick += (s, a) =>
                {
                    _clickTimer.Stop();
                    DoAction(Cfg.SingleClick);
                };
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var t = track;
            if (t == null) return;
            if (e.Delta > 0) MediaApi.Skip(t.Session, true);
            else MediaApi.Skip(t.Session, false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                workerAlive = false;
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                if (backBuffer != null) { backBuffer.Dispose(); backBuffer = null; }
                lock (sync) { if (thumb != null) { thumb.Dispose(); thumb = null; } }
                if (uiTick != null) uiTick.Dispose();
                if (pollTick != null) pollTick.Dispose();
                if (posTick != null) posTick.Dispose();
                if (_clickTimer != null) _clickTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e) { }
    }

    static class Autostart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "MusicBarWin11";

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key))
                        return k != null && k.GetValue(Name) != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Key))
                    {
                        if (value) k.SetValue(Name, "\"" + Application.ExecutablePath + "\"");
                        else if (k.GetValue(Name) != null) k.DeleteValue(Name, false);
                    }
                }
                catch { }
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mtx = new Mutex(true, "MusicBarWin11_SingleInstance_9F2A", out created))
            {
                EventWaitHandle ev = null;
                try { ev = EventWaitHandle.OpenExisting("MusicBarWin11_ShowSettings_9F2A"); } catch { }
                if (!created)
                {
                    if (ev != null) { ev.Set(); ev.Dispose(); }
                    return;
                }
                try { ev = new EventWaitHandle(false, EventResetMode.AutoReset, "MusicBarWin11_ShowSettings_9F2A"); } catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (sx, ex) =>
                {
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.StartupPath, "error.log"), DateTime.Now + " THREAD: " + ex.Exception + Environment.NewLine); } catch { }
                };
                try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); } catch { }

                var cfg = Config.Load();
                var w = new Widget(cfg);
                Application.Run(w);
                if (ev != null) ev.Dispose();
            }
        }
    }
}
