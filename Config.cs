using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NowPlaying
{
    class Config
    {
        // ---- размеры ----
        public int Width = 185;
        public int Height = 40;
        public int RightMargin = 6;
        public int VertOffset = 0;
        public int Corner = 5;
        public bool AutoHeight = false;      // высота по содержимому
        public bool MatchTaskbar = false;    // высота = высоте панели задач
        public bool CompactNarrow = true;    // узкий экран — компактнее
        public int Padding = 4;

        // ---- фон ----
        public bool BgEnabled = true;
        public int BgR = 28, BgG = 28, BgB = 30;
        public int OpacityPercent = 82;

        // ---- рамка ----
        public bool BorderEnabled = true;
        public int BorderR = 255, BorderG = 255, BorderB = 255;
        public int BorderAlpha = 35;
        public int BorderWidth = 1;
        public bool Gradient = true;
        public int BgR2 = 21, BgG2 = 21, BgB2 = 21;
        public int GradientAngle = 90;

        // ---- текст ----
        public string FontName = "Segoe UI";
        public float TitleSize = 9.75f;
        public bool TitleBold = true;
        public float ArtistSize = 8.25f;
        public bool ArtistBold = false;
        public string TitleColor = "255,255,255";
        public string ArtistColor = "216,216,218";
        public string Align = "left";         // left | center | right
        public string SecondLine = "artist";  // none | artist | album | app | artist_app | app_artist
        public bool Marquee = false;
        public int MarqueeSpeed = 90;         // px/сек
        public int MarqueeGap = 40;
        public bool DimPaused = false;
        public bool TextShadow = false;
        public string TextColorMode = "auto";  // auto | custom
        public bool NavButtons = true;        // мастер-выключатель кнопок
        public bool NavPrev = true;           // кнопка «назад»
        public bool NavPlay = true;           // кнопка «пауза / играть»
        public bool NavNext = true;           // кнопка «вперёд»
        public string NavSide = "right";      // right | left — где группа кнопок
        public bool NavBackdrop = false;      // подложка под кнопками
        public int NavCorner = 30;            // скругление подложки, % от размера
        public string NavColorMode = "auto"; // auto | custom
        public string NavColor = "230,230,236";
        public int NavWeight = 100;          // толщина иконок, %
        public int NavOffset = 0;            // сдвиг группы от края, px
        public int NavOpacity = 100;         // прозрачность иконок, %
        public string NavOrder = "prev_play_next";
        public bool NavHover = true;         // подсветка под курсором
        public int NavSize = 15;              // размер кнопок, px
        public int NavGap = 4;                // отступ между кнопками, px
        public int TextGap = 6;               // отступ текста от обложки, px
        public string TextCase = "normal";    // normal | upper | lower
        public bool TextOutline = false;      // обводка текста контрастным цветом
        public int OutlineWidth = 1;          // толщина обводки, px
        public int ShadowStrength = 120;      // сила тени, 0..255
        public int LineSpacing = -2;           // дополнительный отступ между строками, px
        public string VAlign = "center";      // top | center | bottom

        // ---- обложка ----
        public string ThumbSide = "right";    // none | left | right
        public int ThumbSize = 27;            // 0 = авто (по высоте)
        public int ThumbCorner = 2;           // px (0 = квадрат)
        public int ThumbOffset = 0;           // сдвиг обложки от края, px
        public string ThumbVAlign = "center"; // top | center | bottom
        public bool ThumbPlaceholder = true;  // рисовать «ноту», если обложки нет
        public bool Progress = true;
        public string ProgressColor = "255,255,255";
        public int ProgressHeight = 2;
        public int ProgressAlpha = 120;
        public int ProgressOffset = -1;       // сдвиг полосы вверх от нижнего края, px
        public bool ProgressRounded = true;   // скруглённые концы полосы
        public bool SeekOnClick = true;       // клик по полосе перематывает трек
        public bool ThumbShadow = false;      // мягкая тень под обложкой
        public bool ThumbCircle = false;      // обложка кругом
        public bool ThumbBorder = false;      // тонкая рамка вокруг обложки
        public bool WidgetShadow = false;     // мягкая тень под всей плашкой
        public int TextOpacity = 100;         // прозрачность текста, %

        // ---- фон из обложки ----
        public string BgMode = "cover_blur_tint"; // color | cover_blur | cover_blur_tint | cover_color
        public int CoverBlur = 24;            // сила размытия 1..64
        public int CoverDark = 45;            // затемнение фона, %
        public int CoverSaturation = 120;     // насыщенность среднего цвета, %

        // ---- поведение ----
        public bool ShowPaused = true;
        public bool HideWhenIdle = true;
        public bool AlwaysVisible = true;
        public bool HideFullscreen = true;
        public string DoubleClick = "toggle"; // toggle | settings | none
        public string SingleClick = "settings"; // settings | toggle | next | prev | none

        // ---- вычисленные на лету (не сохраняются в ini) ----
        public int EffWidth = 185;
        public int EffHeight = 40;

        /// <summary>Реальная ширина с учётом адаптации под экран.</summary>
        public int DrawWidth { get { return EffWidth > 0 ? EffWidth : Width; } }
        public int DrawHeight { get { return EffHeight > 0 ? EffHeight : Height; } }

        public Config Clone()
        {
            var c = new Config();
            c.CopyFrom(this);
            return c;
        }

        public void CopyFrom(Config o)
        {
            Width = o.Width; Height = o.Height; RightMargin = o.RightMargin;
            VertOffset = o.VertOffset; Corner = o.Corner; Padding = o.Padding;
            AutoHeight = o.AutoHeight; MatchTaskbar = o.MatchTaskbar;
            CompactNarrow = o.CompactNarrow;
            NavButtons = o.NavButtons; NavSize = o.NavSize; NavGap = o.NavGap;
            NavPrev = o.NavPrev; NavPlay = o.NavPlay; NavNext = o.NavNext;
            NavBackdrop = o.NavBackdrop; NavCorner = o.NavCorner;
            NavSide = o.NavSide; NavColorMode = o.NavColorMode;
            NavColor = o.NavColor; NavWeight = o.NavWeight;
            NavOffset = o.NavOffset; NavOpacity = o.NavOpacity;
            NavOrder = o.NavOrder; NavHover = o.NavHover;
            ThumbCircle = o.ThumbCircle; ThumbBorder = o.ThumbBorder;
            WidgetShadow = o.WidgetShadow; TextOpacity = o.TextOpacity;
            BgEnabled = o.BgEnabled; BgR = o.BgR; BgG = o.BgG; BgB = o.BgB;
            OpacityPercent = o.OpacityPercent;
            BorderEnabled = o.BorderEnabled;
            BorderR = o.BorderR; BorderG = o.BorderG; BorderB = o.BorderB;
            BorderAlpha = o.BorderAlpha; BorderWidth = o.BorderWidth;
            Gradient = o.Gradient; BgR2 = o.BgR2; BgG2 = o.BgG2; BgB2 = o.BgB2;
            GradientAngle = o.GradientAngle;
            FontName = o.FontName;
            TitleSize = o.TitleSize; TitleBold = o.TitleBold;
            ArtistSize = o.ArtistSize; ArtistBold = o.ArtistBold;
            TitleColor = o.TitleColor; ArtistColor = o.ArtistColor;
            Align = o.Align; SecondLine = o.SecondLine;
            Marquee = o.Marquee; MarqueeSpeed = o.MarqueeSpeed; MarqueeGap = o.MarqueeGap;
            DimPaused = o.DimPaused;
            TextShadow = o.TextShadow; TextColorMode = o.TextColorMode;
            TextGap = o.TextGap; TextCase = o.TextCase;
            TextOutline = o.TextOutline; OutlineWidth = o.OutlineWidth;
            ShadowStrength = o.ShadowStrength;
            LineSpacing = o.LineSpacing; VAlign = o.VAlign;
            ThumbSide = o.ThumbSide; ThumbSize = o.ThumbSize; ThumbCorner = o.ThumbCorner;
            ThumbPlaceholder = o.ThumbPlaceholder;
            ThumbOffset = o.ThumbOffset; ThumbVAlign = o.ThumbVAlign;
            Progress = o.Progress; ProgressColor = o.ProgressColor;
            ProgressHeight = o.ProgressHeight; ProgressAlpha = o.ProgressAlpha;
            ProgressOffset = o.ProgressOffset;
            ProgressRounded = o.ProgressRounded; ThumbShadow = o.ThumbShadow;
            SeekOnClick = o.SeekOnClick;

            BgMode = o.BgMode; CoverBlur = o.CoverBlur;
            CoverDark = o.CoverDark; CoverSaturation = o.CoverSaturation;
            ShowPaused = o.ShowPaused; HideWhenIdle = o.HideWhenIdle;
            AlwaysVisible = o.AlwaysVisible; HideFullscreen = o.HideFullscreen;
            DoubleClick = o.DoubleClick; SingleClick = o.SingleClick;
        }

        public static Color ParseColor(string s, Color def)
        {
            try
            {
                var p = s.Split(',');
                if (p.Length >= 3)
                    return Color.FromArgb(
                        Math.Max(0, Math.Min(255, int.Parse(p[0].Trim()))),
                        Math.Max(0, Math.Min(255, int.Parse(p[1].Trim()))),
                        Math.Max(0, Math.Min(255, int.Parse(p[2].Trim()))));
            }
            catch { }
            return def;
        }

        public static string ColorToString(Color c)
        {
            return c.R + "," + c.G + "," + c.B;
        }

        // ---------------- файл ----------------

        /// <summary>
        /// Конфиг лежит в %APPDATA%\MusicBarWin11\nowplaying.ini.
        /// Папка установки (Program Files) только для чтения для обычного пользователя,
        /// поэтому хранить ini рядом с exe нельзя. При первом запуске ini переносится
        /// из папки программы (если там остался от «портативной» версии).
        /// </summary>
        static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MusicBarWin11");
                try { Directory.CreateDirectory(dir); } catch { }
                return Path.Combine(dir, "nowplaying.ini");
            }
        }

        /// <summary>Путь в папке программы — для миграции старого конфига.</summary>
        static string LegacyPath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "nowplaying.ini"); }
        }

        public static string ConfigPath { get { return FilePath; } }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                // миграция: старый «портативный» конфиг рядом с exe
                try
                {
                    if (!File.Exists(FilePath) && File.Exists(LegacyPath))
                        File.Copy(LegacyPath, FilePath, false);
                }
                catch { }

                if (!File.Exists(FilePath)) { c.Save(); return c; }
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    int iv; float fv; bool bv;
                    switch (k)
                    {
                        case "width": if (int.TryParse(v, out iv)) c.Width = Clamp(iv, 120, 1600); break;
                        case "height": if (int.TryParse(v, out iv)) c.Height = Clamp(iv, 20, 160); break;
                        case "rightmargin": if (int.TryParse(v, out iv)) c.RightMargin = Clamp(iv, -400, 600); break;
                        case "vertoffset": if (int.TryParse(v, out iv)) c.VertOffset = Clamp(iv, -60, 60); break;
                        case "corner": if (int.TryParse(v, out iv)) c.Corner = Clamp(iv, 0, 40); break;
                        case "autoheight": if (bool.TryParse(v, out bv)) c.AutoHeight = bv; break;
                        case "matchtaskbar": if (bool.TryParse(v, out bv)) c.MatchTaskbar = bv; break;
                        case "compactnarrow": if (bool.TryParse(v, out bv)) c.CompactNarrow = bv; break;
                        case "navbuttons": if (bool.TryParse(v, out bv)) c.NavButtons = bv; break;
                        case "navprev": if (bool.TryParse(v, out bv)) c.NavPrev = bv; break;
                        case "navplay": if (bool.TryParse(v, out bv)) c.NavPlay = bv; break;
                        case "navnext": if (bool.TryParse(v, out bv)) c.NavNext = bv; break;
                        case "navbackdrop": if (bool.TryParse(v, out bv)) c.NavBackdrop = bv; break;
                        case "navside": if (v == "left" || v == "right") c.NavSide = v; break;
                        case "navcolormode": if (v == "auto" || v == "custom") c.NavColorMode = v; break;
                        case "navcolor": if (v.Length > 0) c.NavColor = v; break;
                        case "navweight": if (int.TryParse(v, out iv)) c.NavWeight = Clamp(iv, 50, 200); break;
                        case "navoffset": if (int.TryParse(v, out iv)) c.NavOffset = Clamp(iv, -40, 60); break;
                        case "navopacity": if (int.TryParse(v, out iv)) c.NavOpacity = Clamp(iv, 20, 100); break;
                        case "navorder": if (v == "prev_play_next" || v == "play_prev_next" || v == "next_play_prev") c.NavOrder = v; break;
                        case "navhover": if (bool.TryParse(v, out bv)) c.NavHover = bv; break;
                        case "navcorner": if (int.TryParse(v, out iv)) c.NavCorner = Clamp(iv, 0, 50); break;
                        case "thumbcircle": if (bool.TryParse(v, out bv)) c.ThumbCircle = bv; break;
                        case "thumbborder": if (bool.TryParse(v, out bv)) c.ThumbBorder = bv; break;
                        case "widgetshadow": if (bool.TryParse(v, out bv)) c.WidgetShadow = bv; break;
                        case "textopacity": if (int.TryParse(v, out iv)) c.TextOpacity = Clamp(iv, 10, 100); break;
                        case "navsize": if (int.TryParse(v, out iv)) c.NavSize = Clamp(iv, 8, 32); break;
                        case "navgap": if (int.TryParse(v, out iv)) c.NavGap = Clamp(iv, 0, 16); break;
                        case "padding": if (int.TryParse(v, out iv)) c.Padding = Clamp(iv, 0, 24); break;
                        case "bgenabled": if (bool.TryParse(v, out bv)) c.BgEnabled = bv; break;
                        case "background": ApplyColor(v, ref c.BgR, ref c.BgG, ref c.BgB); break;
                        case "opacity": if (int.TryParse(v, out iv)) c.OpacityPercent = Clamp(iv, 0, 100); break;
                        case "borderenabled": if (bool.TryParse(v, out bv)) c.BorderEnabled = bv; break;
                        case "bordercolor": ApplyColor(v, ref c.BorderR, ref c.BorderG, ref c.BorderB); break;
                        case "borderalpha": if (int.TryParse(v, out iv)) c.BorderAlpha = Clamp(iv, 0, 255); break;
                        case "borderwidth": if (int.TryParse(v, out iv)) c.BorderWidth = Clamp(iv, 1, 6); break;
                        case "font": if (v.Length > 0) c.FontName = v; break;
                        case "titlesize": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) c.TitleSize = Math.Max(5, Math.Min(30, fv)); break;
                        case "titlebold": if (bool.TryParse(v, out bv)) c.TitleBold = bv; break;
                        case "artistsize": if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) c.ArtistSize = Math.Max(4, Math.Min(24, fv)); break;
                        case "artistbold": if (bool.TryParse(v, out bv)) c.ArtistBold = bv; break;
                        case "titlecolor": if (v.Length > 0) c.TitleColor = v; break;
                        case "artistcolor": if (v.Length > 0) c.ArtistColor = v; break;
                        case "align": if (ValidAlign(v)) c.Align = v; break;
                        case "secondline": if (ValidSecond(v)) c.SecondLine = v; break;
                        case "marquee": if (bool.TryParse(v, out bv)) c.Marquee = bv; break;
                        case "marqueespeed": if (int.TryParse(v, out iv)) c.MarqueeSpeed = Clamp(iv, 10, 400); break;
                        case "marqueegap": if (int.TryParse(v, out iv)) c.MarqueeGap = Clamp(iv, 0, 200); break;
                        case "dimpaused": if (bool.TryParse(v, out bv)) c.DimPaused = bv; break;
                        case "textshadow": if (bool.TryParse(v, out bv)) c.TextShadow = bv; break;
                        case "textcolormode": if (v == "auto" || v == "light" || v == "dark" || v == "custom") c.TextColorMode = v; break;
                        case "textgap": if (int.TryParse(v, out iv)) c.TextGap = Clamp(iv, 0, 40); break;
                        case "textcase": if (v == "normal" || v == "upper" || v == "lower") c.TextCase = v; break;
                        case "textoutline": if (bool.TryParse(v, out bv)) c.TextOutline = bv; break;
                        case "outlinewidth": if (int.TryParse(v, out iv)) c.OutlineWidth = Clamp(iv, 1, 4); break;
                        case "shadowstrength": if (int.TryParse(v, out iv)) c.ShadowStrength = Clamp(iv, 0, 255); break;
                        case "progressrounded": if (bool.TryParse(v, out bv)) c.ProgressRounded = bv; break;
                        case "seekonclick": if (bool.TryParse(v, out bv)) c.SeekOnClick = bv; break;
                        case "thumbshadow": if (bool.TryParse(v, out bv)) c.ThumbShadow = bv; break;
                        case "thumboffset": if (int.TryParse(v, out iv)) c.ThumbOffset = Clamp(iv, -60, 60); break;
                        case "thumbvalign": if (v == "top" || v == "center" || v == "bottom") c.ThumbVAlign = v; break;
                        case "linespacing": if (int.TryParse(v, out iv)) c.LineSpacing = Clamp(iv, -4, 16); break;
                        case "valign": if (v == "top" || v == "center" || v == "bottom") c.VAlign = v; break;
                        case "gradient": if (bool.TryParse(v, out bv)) c.Gradient = bv; break;
                        case "background2": ApplyColor(v, ref c.BgR2, ref c.BgG2, ref c.BgB2); break;
                        case "gradientangle": if (int.TryParse(v, out iv)) c.GradientAngle = Clamp(iv, 0, 360); break;
                        case "thumbplaceholder": if (bool.TryParse(v, out bv)) c.ThumbPlaceholder = bv; break;
                        case "progress": if (bool.TryParse(v, out bv)) c.Progress = bv; break;
                        case "progresscolor": if (v.Length > 0) c.ProgressColor = v; break;
                        case "progressheight": if (int.TryParse(v, out iv)) c.ProgressHeight = Clamp(iv, 1, 8); break;
                        case "progressalpha": if (int.TryParse(v, out iv)) c.ProgressAlpha = Clamp(iv, 0, 255); break;
                        case "progressoffset": if (int.TryParse(v, out iv)) c.ProgressOffset = Clamp(iv, -80, 40); break;
                        case "bgmode": if (v == "color" || v == "cover_blur" || v == "cover_blur_tint" || v == "cover_color") c.BgMode = v; break;
                        case "coverblur": if (int.TryParse(v, out iv)) c.CoverBlur = Clamp(iv, 1, 64); break;
                        case "coverdark": if (int.TryParse(v, out iv)) c.CoverDark = Clamp(iv, 0, 95); break;
                        case "coversaturation": if (int.TryParse(v, out iv)) c.CoverSaturation = Clamp(iv, 0, 300); break;
                        case "singleclick": if (v == "settings" || v == "toggle" || v == "next" || v == "prev" || v == "none") c.SingleClick = v; break;
                        case "thumbside": if (v == "none" || v == "left" || v == "right") c.ThumbSide = v; break;
                        case "thumbsize": if (int.TryParse(v, out iv)) c.ThumbSize = Clamp(iv, 0, 96); break;
                        case "thumbcorner": if (int.TryParse(v, out iv)) c.ThumbCorner = Clamp(iv, 0, 40); break;
                        case "showpaused": if (bool.TryParse(v, out bv)) c.ShowPaused = bv; break;
                        case "hidewhenidle": if (bool.TryParse(v, out bv)) c.HideWhenIdle = bv; break;
                        case "alwaysvisible": if (bool.TryParse(v, out bv)) c.AlwaysVisible = bv; break;
                        case "hidefullscreen": if (bool.TryParse(v, out bv)) c.HideFullscreen = bv; break;
                        case "doubleclick": if (v == "toggle" || v == "settings" || v == "none") c.DoubleClick = v; break;
                    }
                }
            }
            catch { }
            return c;
        }

        static int Clamp(int v, int a, int b) { return v < a ? a : (v > b ? b : v); }
        static bool ValidAlign(string v) { return v == "left" || v == "center" || v == "right"; }
        static bool ValidSecond(string v) { return v == "none" || v == "artist" || v == "album" || v == "app" || v == "artist_app" || v == "app_artist"; }

        static void ApplyColor(string v, ref int r, ref int g, ref int b)
        {
            try
            {
                var p = v.Split(',');
                if (p.Length >= 3) { r = Clamp(int.Parse(p[0].Trim()), 0, 255); g = Clamp(int.Parse(p[1].Trim()), 0, 255); b = Clamp(int.Parse(p[2].Trim()), 0, 255); }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Music Bar Win11 — виджет «сейчас играет» на панели задач");
                sb.AppendLine("# Файл перезаписывается программой при сохранении настроек.");
                sb.AppendLine();
                sb.AppendLine("[window]");
                sb.AppendLine("width=" + Width);
                sb.AppendLine("height=" + Height);
                sb.AppendLine("rightmargin=" + RightMargin);
                sb.AppendLine("vertoffset=" + VertOffset);
                sb.AppendLine("corner=" + Corner);
                sb.AppendLine("autoHeight=" + AutoHeight);
                sb.AppendLine("matchTaskbar=" + MatchTaskbar);
                sb.AppendLine("compactNarrow=" + CompactNarrow);
                sb.AppendLine("navButtons=" + NavButtons);
                sb.AppendLine("navPrev=" + NavPrev);
                sb.AppendLine("navPlay=" + NavPlay);
                sb.AppendLine("navNext=" + NavNext);
                sb.AppendLine("navBackdrop=" + NavBackdrop);
                sb.AppendLine("navSide=" + NavSide);
                sb.AppendLine("navColorMode=" + NavColorMode);
                sb.AppendLine("navColor=" + NavColor);
                sb.AppendLine("navWeight=" + NavWeight);
                sb.AppendLine("navOffset=" + NavOffset);
                sb.AppendLine("navOpacity=" + NavOpacity);
                sb.AppendLine("navOrder=" + NavOrder);
                sb.AppendLine("navHover=" + NavHover);
                sb.AppendLine("navCorner=" + NavCorner);
                sb.AppendLine("navSize=" + NavSize);
                sb.AppendLine("navGap=" + NavGap);
                sb.AppendLine("padding=" + Padding);
                sb.AppendLine();
                sb.AppendLine("[background]");
                sb.AppendLine("bgenabled=" + BgEnabled);
                sb.AppendLine("background=" + BgR + "," + BgG + "," + BgB);
                sb.AppendLine("opacity=" + OpacityPercent);
                sb.AppendLine("borderenabled=" + BorderEnabled);
                sb.AppendLine("bordercolor=" + BorderR + "," + BorderG + "," + BorderB);
                sb.AppendLine("borderalpha=" + BorderAlpha);
                sb.AppendLine("borderwidth=" + BorderWidth);
                sb.AppendLine("gradient=" + Gradient);
                sb.AppendLine("background2=" + BgR2 + "," + BgG2 + "," + BgB2);
                sb.AppendLine("gradientAngle=" + GradientAngle);
                sb.AppendLine();
                sb.AppendLine("[text]");
                sb.AppendLine("font=" + FontName);
                sb.AppendLine("titleSize=" + TitleSize.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("titleBold=" + TitleBold);
                sb.AppendLine("artistSize=" + ArtistSize.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("artistBold=" + ArtistBold);
                sb.AppendLine("titleColor=" + TitleColor);
                sb.AppendLine("artistColor=" + ArtistColor);
                sb.AppendLine("align=" + Align);
                sb.AppendLine("secondline=" + SecondLine);
                sb.AppendLine("marquee=" + Marquee);
                sb.AppendLine("marqueeSpeed=" + MarqueeSpeed);
                sb.AppendLine("marqueeGap=" + MarqueeGap);
                sb.AppendLine("dimPaused=" + DimPaused);
                sb.AppendLine("textShadow=" + TextShadow);
                sb.AppendLine("textColorMode=" + TextColorMode);
                sb.AppendLine("textGap=" + TextGap);
                sb.AppendLine("textCase=" + TextCase);
                sb.AppendLine("textOutline=" + TextOutline);
                sb.AppendLine("outlineWidth=" + OutlineWidth);
                sb.AppendLine("shadowStrength=" + ShadowStrength);
                sb.AppendLine("textOpacity=" + TextOpacity);
                sb.AppendLine("progressRounded=" + ProgressRounded);
                sb.AppendLine("seekOnClick=" + SeekOnClick);
                sb.AppendLine("thumbShadow=" + ThumbShadow);
                sb.AppendLine("lineSpacing=" + LineSpacing);
                sb.AppendLine("valign=" + VAlign);
                sb.AppendLine();
                sb.AppendLine("[thumbnail]");
                sb.AppendLine("thumbside=" + ThumbSide);
                sb.AppendLine("thumbsize=" + ThumbSize);
                sb.AppendLine("thumbcorner=" + ThumbCorner);
                sb.AppendLine("thumbCircle=" + ThumbCircle);
                sb.AppendLine("thumbBorder=" + ThumbBorder);
                sb.AppendLine("widgetShadow=" + WidgetShadow);
                sb.AppendLine("thumbOffset=" + ThumbOffset);
                sb.AppendLine("thumbValign=" + ThumbVAlign);
                sb.AppendLine("thumbPlaceholder=" + ThumbPlaceholder);
                sb.AppendLine("progress=" + Progress);
                sb.AppendLine("progressColor=" + ProgressColor);
                sb.AppendLine("progressHeight=" + ProgressHeight);
                sb.AppendLine("progressAlpha=" + ProgressAlpha);
                sb.AppendLine("progressOffset=" + ProgressOffset);
                sb.AppendLine("bgMode=" + BgMode);
                sb.AppendLine("coverBlur=" + CoverBlur);
                sb.AppendLine("coverDark=" + CoverDark);
                sb.AppendLine("coverSaturation=" + CoverSaturation);
                sb.AppendLine();
                sb.AppendLine("[behavior]");
                sb.AppendLine("showPaused=" + ShowPaused);
                sb.AppendLine("hideWhenIdle=" + HideWhenIdle);
                sb.AppendLine("alwaysVisible=" + AlwaysVisible);
                sb.AppendLine("hideFullscreen=" + HideFullscreen);
                sb.AppendLine("doubleclick=" + DoubleClick);
                sb.AppendLine("singleclick=" + SingleClick);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }
    }
}
