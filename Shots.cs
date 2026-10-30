using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace NowPlaying
{
    // Утилита для README: рендерит виджет в PNG с прозрачным фоном,
    // используя тот же Renderer, что и настоящий виджет.
    static class Shots
    {
        static Config Base()
        {
            var c = new Config();
            c.Width = 360;
            c.Height = 56;
            c.Corner = 10;
            c.Padding = 8;
            c.BgEnabled = true;
            c.BgR = 28; c.BgG = 28; c.BgB = 30;
            c.OpacityPercent = 100;
            c.BorderEnabled = true;
            c.BorderAlpha = 55;
            c.BorderWidth = 1;
            c.Gradient = true;
            c.BgR2 = 21; c.BgG2 = 21; c.BgB2 = 21;
            c.GradientAngle = 90;
            c.TitleSize = 10.5f;
            c.TitleBold = true;
            c.ArtistSize = 8.75f;
            c.ArtistColor = "200,200,204";
            c.ThumbSide = "right";
            c.ThumbCorner = 4;
            c.Progress = true;
            c.ProgressOffset = -6;
            c.LineSpacing = 1;
            c.VAlign = "center";
            return c;
        }

        static void Save(Config cfg, TrackInfo t, Image thumb, string file, string label)
        {
            try
            {
                using (var bmp = Renderer.Render(cfg, t, thumb, 0f, 2f))
                {
                    bmp.Save(file, ImageFormat.Png);
                }
                Console.WriteLine("ok  " + label + " -> " + file);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERR " + label + ": " + ex.Message);
            }
        }

        static void Main()
        {
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            TrackInfo t = null;
            Image thumb = null;
            try
            {
                var list = MediaApi.Read();
                foreach (var x in list)
                {
                    if (x == null) continue;
                    if (x.Title.Length > 0) { t = x; if (x.IsPlaying) break; }
                }
                if (t != null) thumb = MediaApi.ReadThumbnail(t.Session, 8000);
            }
            catch (Exception ex) { Console.WriteLine("smtc: " + ex.Message); }

            if (t == null)
            {
                t = new TrackInfo();
                t.Title = "euphoria";
                t.Artist = "dOP, Sylvan Paul";
                t.Album = "Euphoria";
                t.App = "Spotify.exe";
                t.Status = 4;
                t.PositionSec = 96; t.DurationSec = 214;
                Console.WriteLine("smtc: пусто, использую демо-трек");
            }
            Console.WriteLine("track: " + t.Title + " / " + t.Artist + "  thumb=" + (thumb != null));

            // 1. базовый вид
            var a = Base();
            Save(a, t, thumb, Path.Combine(dir, "shot-widget.png"), "default");

            // 2. размытая обложка как фон
            var b = Base();
            b.BgMode = "cover_blur";
            b.CoverBlur = 26;
            b.CoverDark = 55;
            b.BorderEnabled = false;
            Save(b, t, thumb, Path.Combine(dir, "shot-blur.png"), "cover_blur");

            // 3. размытая обложка + оттенок
            var c = Base();
            c.BgMode = "cover_blur_tint";
            c.CoverBlur = 30;
            c.CoverDark = 50;
            c.Gradient = true;
            c.BgR = 40; c.BgG = 24; c.BgB = 60;
            c.BgR2 = 18; c.BgG2 = 20; c.BgB2 = 45;
            c.BorderEnabled = false;
            Save(c, t, thumb, Path.Combine(dir, "shot-tint.png"), "cover_blur_tint");

            // 4. цвет из обложки
            var d = Base();
            d.BgMode = "cover_color";
            d.CoverDark = 62;
            d.CoverSaturation = 150;
            d.Gradient = false;
            d.BorderEnabled = false;
            d.Progress = false;
            Save(d, t, thumb, Path.Combine(dir, "shot-color.png"), "cover_color");
        }
    }
}
