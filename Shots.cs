using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace NowPlaying
{
    // =======================================================================
    //  Генератор картинок для README.
    //
    //  Рисует виджет тем же Renderer.Render(), что и настоящее приложение,
    //  но с разными настройками и разными треками — чтобы на странице
    //  проекта была видна вариативность оформления.
    //
    //  Обложки: сначала пробуем взять из живого SMTC, если не получилось —
    //  рисуем синтетические обложки (градиенты/фигуры), чтобы картинки
    //  собирались на любой машине без запущенного плеера.
    // =======================================================================
    static class Shots
    {
        static readonly Color[] Palette =
        {
            Color.FromArgb(233, 88, 61),
            Color.FromArgb(64, 132, 220),
            Color.FromArgb(126, 92, 214),
            Color.FromArgb(38, 168, 130),
            Color.FromArgb(226, 154, 44),
            Color.FromArgb(214, 72, 138),
            Color.FromArgb(52, 178, 200),
            Color.FromArgb(198, 190, 52),
        };

        // ---------------- синтетические обложки ----------------

        /// <summary>Абстрактная обложка: градиент + фигуры. Выглядит как настоящий арт.</summary>
        static Image MakeArt(int seed, int size)
        {
            var rnd = new Random(seed);
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Color c1 = Palette[seed % Palette.Length];
                Color c2 = Palette[(seed * 3 + 5) % Palette.Length];
                Color c3 = Palette[(seed * 7 + 2) % Palette.Length];

                using (var br = new LinearGradientBrush(new Rectangle(0, 0, size, size),
                           c1, c2, (float)(rnd.Next(360))))
                    g.FillRectangle(br, 0, 0, size, size);

                // большие полупрозрачные круги
                for (int i = 0; i < 4; i++)
                {
                    int r = rnd.Next(size / 3, size);
                    int x = rnd.Next(-size / 4, size);
                    int y = rnd.Next(-size / 4, size);
                    using (var br = new SolidBrush(Color.FromArgb(rnd.Next(35, 95), c3)))
                        g.FillEllipse(br, x, y, r, r);
                }

                // диагональные полосы
                for (int i = 0; i < 3; i++)
                {
                    var pts = new[]
                    {
                        new PointF(rnd.Next(-size / 2, size), rnd.Next(-size / 2, size)),
                        new PointF(rnd.Next(0, size * 2), rnd.Next(-size / 2, size)),
                        new PointF(rnd.Next(0, size * 2), rnd.Next(size, size * 2)),
                    };
                    using (var br = new SolidBrush(Color.FromArgb(rnd.Next(25, 70), Color.White)))
                        g.FillPolygon(br, pts);
                }

                // виньетка
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(-size / 3, -size / 3, size * 5 / 3, size * 5 / 3);
                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                        pgb.SurroundColors = new[] { Color.FromArgb(150, 0, 0, 0) };
                        g.FillRectangle(pgb, 0, 0, size, size);
                    }
                }
            }
            return bmp;
        }

        // ---------------- описания треков ----------------

        class Demo
        {
            public string Title, Second, Album, App;
            public int Status;
            public double Pos, Len;
            public int Art;
        }

        static readonly Demo[] Demos =
        {
            new Demo { Title = "euphoria",            Second = "dOP, Sylvan Paul",        Album = "Euphoria",      App = "Spotify.exe",   Status = 4, Pos = 96,  Len = 214, Art = 1 },
            new Demo { Title = "Ночной эфир",          Second = "Мумий Тролль",            Album = "Владивосток",   App = "YandexMusic",   Status = 4, Pos = 38,  Len = 262, Art = 2 },
            new Demo { Title = "Weightless",          Second = "Marconi Union",           Album = "Distance",      App = "Chrome.exe",    Status = 4, Pos = 213, Len = 480, Art = 3 },
            new Demo { Title = "Пыль",                Second = "Дельфин",                 Album = "Юность",        App = "AIMP.exe",      Status = 5, Pos = 74,  Len = 231, Art = 4 },
            new Demo { Title = "Strobe",              Second = "deadmau5",                Album = "For Lack of a Better Name", App = "foobar2000.exe", Status = 4, Pos = 402, Len = 637, Art = 5 },
            new Demo { Title = "Кино",                Second = "Земфира",                 Album = "Бордерлайн",    App = "AppleMusic",    Status = 4, Pos = 12,  Len = 198, Art = 6 },
            new Demo { Title = "Teardrop",            Second = "Massive Attack",          Album = "Mezzanine",     App = "VLC.exe",       Status = 4, Pos = 168, Len = 330, Art = 7 },
            new Demo { Title = "Рассвет",             Second = "Сплин",                   Album = "Резонанс",      App = "VK Music",      Status = 4, Pos = 57,  Len = 244, Art = 8 },
        };

        static TrackInfo ToTrack(Demo d)
        {
            var t = new TrackInfo();
            t.Title = d.Title;
            t.Artist = d.Second;
            t.Album = d.Album;
            t.App = d.App;
            t.Status = d.Status;
            t.PositionSec = d.Pos;
            t.DurationSec = d.Len;
            return t;
        }

        // ---------------- базовый конфиг ----------------

        static Config Base()
        {
            var c = new Config();
            c.Width = 380;
            c.Height = 58;
            c.Corner = 10;
            c.Padding = 9;
            c.BgEnabled = true;
            c.BgR = 28; c.BgG = 28; c.BgB = 32;
            c.OpacityPercent = 100;
            c.BorderEnabled = false;
            c.BorderR = 255; c.BorderG = 255; c.BorderB = 255;
            c.BorderAlpha = 45;
            c.BorderWidth = 1;
            c.Gradient = false;
            c.BgR2 = 22; c.BgG2 = 22; c.BgB2 = 26;
            c.GradientAngle = 90;
            c.FontName = "Segoe UI";
            c.TitleSize = 10.5f;
            c.TitleBold = true;
            c.ArtistSize = 8.75f;
            c.TitleColor = "255,255,255";
            c.ArtistColor = "190,190,198";
            c.Align = "left";
            c.SecondLine = "artist";
            c.Marquee = false;
            c.DimPaused = true;
            c.TextShadow = false;
            c.LineSpacing = 1;
            c.VAlign = "center";
            c.ThumbSide = "right";
            c.ThumbSize = 0;
            c.ThumbCorner = 5;
            c.ThumbPlaceholder = true;
            c.Progress = false;
            c.ProgressColor = "255,255,255";
            c.ProgressHeight = 2;
            c.ProgressAlpha = 150;
            c.ProgressOffset = -5;
            c.BgMode = "color";
            c.CoverBlur = 24;
            c.CoverDark = 45;
            c.CoverSaturation = 120;
            return c;
        }

        // ---------------- набор стилей ----------------

        delegate void Tune(Config c);

        class Style
        {
            public string File, Alt;
            public int Demo;
            public Tune Setup;
        }

        static readonly Style[] Styles =
        {
            // 1. строгая тёмная плашка, обложка справа
            new Style { File = "01-dark", Alt = "Тёмная плашка", Demo = 0, Setup = c =>
            {
                c.Corner = 10; c.BorderEnabled = true; c.BorderAlpha = 38;
                c.ThumbSide = "right"; c.ThumbCorner = 5;
                c.Progress = true; c.ProgressHeight = 2; c.ProgressOffset = -6;
                c.ArtistColor = "188,188,196";
            }},

            // 2. обложка слева, без рамки, акцент на тексте
            new Style { File = "02-left-no-border", Alt = "Обложка слева", Demo = 1, Setup = c =>
            {
                c.ThumbSide = "left"; c.ThumbCorner = 8;
                c.Corner = 12; c.BorderEnabled = false;
                c.BgR = 24; c.BgG = 24; c.BgB = 28;
                c.Progress = true; c.ProgressHeight = 3; c.ProgressOffset = -6;
                c.ProgressColor = "120,180,255";
            }},

            // 3. размытая обложка
            new Style { File = "03-blur", Alt = "Размытая обложка", Demo = 2, Setup = c =>
            {
                c.BgMode = "cover_blur"; c.CoverBlur = 30; c.CoverDark = 58;
                c.BorderEnabled = false; c.Corner = 12;
                c.ThumbSide = "right"; c.ThumbCorner = 6;
                c.Progress = true; c.ProgressHeight = 2; c.ProgressOffset = -6;
                c.ArtistColor = "205,205,212";
            }},

            // 4. размытая обложка + цветной оттенок
            new Style { File = "04-blur-tint", Alt = "Размытая обложка с оттенком", Demo = 3, Setup = c =>
            {
                c.BgMode = "cover_blur_tint"; c.CoverBlur = 34; c.CoverDark = 62;
                c.Gradient = true;
                c.BgR = 96; c.BgG = 34; c.BgB = 128;
                c.BgR2 = 16; c.BgG2 = 42; c.BgB2 = 96;
                c.GradientAngle = 115;
                c.BorderEnabled = false; c.Corner = 14;
                c.ThumbSide = "none";
                c.TitleSize = 11.5f;
                c.Progress = true; c.ProgressHeight = 3; c.ProgressOffset = -7;
                c.ProgressColor = "180,150,255";
            }},

            // 5. цвет из обложки, без обложки, крупный текст
            new Style { File = "05-cover-color", Alt = "Цвет из обложки", Demo = 4, Setup = c =>
            {
                c.BgMode = "cover_color"; c.CoverDark = 66; c.CoverSaturation = 165;
                c.BorderEnabled = false; c.Corner = 14;
                c.ThumbSide = "none";
                c.TitleSize = 11.5f; c.ArtistSize = 9f;
                c.Progress = true; c.ProgressHeight = 3; c.ProgressOffset = -7;
            }},

            // 6. светлая тема
            new Style { File = "06-light", Alt = "Светлая тема", Demo = 5, Setup = c =>
            {
                c.BgR = 246; c.BgG = 246; c.BgB = 248;
                c.Gradient = true; c.BgR2 = 226; c.BgG2 = 228; c.BgB2 = 234;
                c.GradientAngle = 90;
                c.BorderEnabled = true; c.BorderR = 0; c.BorderG = 0; c.BorderB = 0; c.BorderAlpha = 26;
                c.TitleColor = "24,24,28"; c.ArtistColor = "96,96,104";
                c.ThumbSide = "left"; c.ThumbCorner = 7;
                c.Progress = true; c.ProgressHeight = 2; c.ProgressOffset = -6;
                c.ProgressColor = "40,40,48"; c.ProgressAlpha = 110;
            }},

            // 7. полупрозрачное «стекло» — плотная обложка слева, приглушённая плашка
            new Style { File = "07-glass", Alt = "Полупрозрачное стекло", Demo = 6, Setup = c =>
            {
                c.OpacityPercent = 55;
                c.BgR = 18; c.BgG = 20; c.BgB = 28;
                c.BorderEnabled = true; c.BorderAlpha = 70;
                c.ThumbSide = "left"; c.ThumbCorner = 6;
                c.TextShadow = true;
                c.Corner = 12;
                c.Progress = true; c.ProgressHeight = 2; c.ProgressOffset = -6;
                c.ProgressColor = "140,230,255";
                c.Width = 360;
            }},

            // 8. с обложкой-фоном + крупная обложка справа + тень текста
            new Style { File = "08-blur-shadow", Alt = "Обложка-фон и тень текста", Demo = 7, Setup = c =>
            {
                c.BgMode = "cover_blur"; c.CoverBlur = 22; c.CoverDark = 64;
                c.BorderEnabled = false; c.Corner = 16;
                c.ThumbSide = "right"; c.ThumbCorner = 10; c.ThumbSize = 40;
                c.TextShadow = true;
                c.TitleSize = 11f;
                c.Progress = true; c.ProgressHeight = 3; c.ProgressOffset = -7;
                c.ProgressColor = "255,220,180";
            }},

            // 9. компактный, центр текста, обложки нет, толстый прогресс
            new Style { File = "09-compact", Alt = "Компактный по центру", Demo = 0, Setup = c =>
            {
                c.Width = 300; c.Height = 50;
                c.Corner = 8;
                c.ThumbSide = "none";
                c.Align = "center";
                c.TitleSize = 9.5f; c.ArtistSize = 8f;
                c.SecondLine = "artist_app";
                c.LineSpacing = 0;
                c.BgR = 20; c.BgG = 20; c.BgB = 24;
                c.BorderEnabled = true; c.BorderAlpha = 60;
                c.Progress = true; c.ProgressHeight = 4; c.ProgressOffset = -6;
                c.ProgressColor = "120,200,140";
            }},

            // 10. градиент + рамка-акцент + обложка слева
            new Style { File = "10-gradient-border", Alt = "Градиент с рамкой", Demo = 2, Setup = c =>
            {
                c.Gradient = true;
                c.BgR = 44; c.BgG = 26; c.BgB = 72;
                c.BgR2 = 16; c.BgG2 = 44; c.BgB2 = 78;
                c.GradientAngle = 35;
                c.BorderEnabled = true; c.BorderR = 150; c.BorderG = 130; c.BorderB = 255;
                c.BorderAlpha = 120; c.BorderWidth = 1;
                c.Corner = 12;
                c.ThumbSide = "left"; c.ThumbCorner = 6;
                c.Progress = true; c.ProgressHeight = 2; c.ProgressOffset = -6;
                c.ProgressColor = "170,150,255";
            }},
        };

        static string OutDir;

        static void Emit(Style s)
        {
            var cfg = Base();
            s.Setup(cfg);
            var d = Demos[s.Demo % Demos.Length];
            var t = ToTrack(d);

            using (var art = MakeArt(d.Art, 300))
            {
                try
                {
                    using (var bmp = Renderer.Render(cfg, t, art, 0f, 2f))
                    {
                        string path = Path.Combine(OutDir, s.File + ".png");
                        bmp.Save(path, ImageFormat.Png);
                        Console.WriteLine("ok  " + s.File + "  (" + d.Title + ")");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERR " + s.File + ": " + ex.Message);
                }
            }
        }

        static void Main()
        {
            OutDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("art: синтетические обложки (живой SMTC не нужен)");
            foreach (var s in Styles) Emit(s);
            Console.WriteLine("done -> " + OutDir);
        }
    }
}
