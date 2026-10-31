using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NowPlaying
{
    /// <summary>Единый код отрисовки — используется и виджетом, и предпросмотром в настройках.</summary>
    static class Renderer
    {
        public static Bitmap Render(Config cfg, TrackInfo t, Image thumb, float scrollPx, float scale)
        {
            int w = Math.Max(1, (int)Math.Round(cfg.DrawWidth * scale));
            int h = Math.Max(1, (int)Math.Round(cfg.DrawHeight * scale));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);
                Draw(g, new Rectangle(0, 0, w, h), cfg, t, thumb, scrollPx, scale);
            }
            return bmp;
        }

        /// <summary>Ширина блока кнопок управления (0, если он выключен).</summary>
        /// <summary>Какие кнопки включены: 0 = назад, 1 = пауза/играть, 2 = вперёд.</summary>
        public static int[] NavKinds(Config cfg)
        {
            if (!cfg.NavButtons) return new int[0];
            int[] order;
            if (cfg.NavOrder == "play_prev_next") order = new int[] { 1, 0, 2 };
            else if (cfg.NavOrder == "next_play_prev") order = new int[] { 2, 1, 0 };
            else order = new int[] { 0, 1, 2 };

            int n = 0;
            if (cfg.NavPrev) n++;
            if (cfg.NavPlay) n++;
            if (cfg.NavNext) n++;
            var k = new int[n];
            int i = 0;
            foreach (int kind in order)
            {
                if (kind == 0 && !cfg.NavPrev) continue;
                if (kind == 1 && !cfg.NavPlay) continue;
                if (kind == 2 && !cfg.NavNext) continue;
                k[i++] = kind;
            }
            return k;
        }

        /// <summary>Индекс кнопки под курсором (-1 = нет) — для подсветки.</summary>
        public static int HoverIndex = -1;

        public static int NavWidth(Config cfg, float scale)
        {
            var kinds = NavKinds(cfg);
            if (kinds.Length == 0) return 0;
            int size = Math.Max(6, (int)Math.Round(cfg.NavSize * scale));
            int gap = (int)Math.Round(cfg.NavGap * scale);
            int pad = (int)Math.Round(cfg.Padding * scale);
            return size * kinds.Length + gap * (kinds.Length - 1) + gap + pad / 2;
        }

        public static Rectangle[] NavRects(Config cfg, Rectangle bounds, float scale)
        {
            return NavRects(cfg, bounds, scale, 0);
        }

        /// <summary>rightInset — сколько места справа занято обложкой.</summary>
        public static Rectangle[] NavRects(Config cfg, Rectangle bounds, float scale, int rightInset)
        {
            var kinds = NavKinds(cfg);
            if (kinds.Length == 0) return new Rectangle[0];
            int size = Math.Max(6, (int)Math.Round(cfg.NavSize * scale));
            int gap = (int)Math.Round(cfg.NavGap * scale);
            int pad = (int)Math.Round(cfg.Padding * scale);
            int total = size * kinds.Length + gap * (kinds.Length - 1);
            int off = (int)Math.Round(cfg.NavOffset * scale);
            int x0 = cfg.NavSide == "left"
                ? bounds.X + pad + gap + off
                : bounds.Right - pad - rightInset - total - (rightInset > 0 ? 0 : gap) - off;
            int y = bounds.Y + (bounds.Height - size) / 2;
            var r = new Rectangle[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) r[i] = new Rectangle(x0 + i * (size + gap), y, size, size);
            return r;
        }

        public static void Draw(Graphics g, Rectangle bounds, Config cfg, TrackInfo t, Image thumb, float scrollPx, float scale)
        {
            int navInset = 0;
            int radius = (int)Math.Round(cfg.Corner * scale);
            var body = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height);

            // ---- тень под плашкой ----
            if (cfg.WidgetShadow)
            {
                for (int i = 4; i >= 1; i--)
                {
                    var sr = new RectangleF(bounds.X - i, bounds.Y + i, bounds.Width + i * 2, bounds.Height);
                    using (var br = new SolidBrush(Color.FromArgb(10, 0, 0, 0)))
                        g.FillPath(br, RoundRect(sr, radius + i));
                }
            }

            // ---- фон ----
            if (cfg.BgEnabled && cfg.OpacityPercent > 0)
            {
                int a = (int)Math.Round(255.0 * cfg.OpacityPercent / 100.0);
                using (var path = RoundRect(body, radius))
                {
                    bool fromCover = thumb != null && cfg.BgMode != "color";
                    if (fromCover && cfg.BgMode == "cover_color")
                    {
                        Color mc = AverageColor(thumb);
                        mc = Adjust(mc, cfg.CoverSaturation, cfg.CoverDark);
                        using (var br = new SolidBrush(Color.FromArgb(a, mc)))
                            g.FillPath(br, path);
                    }
                    else if (fromCover)
                    {
                        // размытая обложка, растянутая на весь фон
                        int bw = Math.Max(2, body.Width > 0 ? (int)body.Width : bounds.Width);
                        int bh = Math.Max(2, body.Height > 0 ? (int)body.Height : bounds.Height);
                        var blurred = BlurCache.Get(thumb, bw, bh, cfg.CoverBlur);
                        if (blurred != null)
                        {
                            var st = g.Save();
                            g.SetClip(path, CombineMode.Intersect);
                            g.DrawImage(blurred, bounds);
                            int a1 = (int)(255.0 * cfg.CoverDark / 100.0) * a / 255;
                            if (a1 > 0)
                                using (var br = new SolidBrush(Color.FromArgb(a1, 0, 0, 0)))
                                    g.FillRectangle(br, bounds);
                            g.Restore(st);
                            // общий уровень прозрачности подложки
                            if (a < 255)
                            {
                                using (var br = new SolidBrush(Color.FromArgb(255 - a, cfg.BgR, cfg.BgG, cfg.BgB)))
                                    g.FillPath(br, path);
                            }
                            if (cfg.BgMode == "cover_blur_tint" && cfg.Gradient)
                            {
                                Color c1 = Color.FromArgb((int)(a * 0.45), cfg.BgR, cfg.BgG, cfg.BgB);
                                Color c2 = Color.FromArgb((int)(a * 0.45), cfg.BgR2, cfg.BgG2, cfg.BgB2);
                                double rad = cfg.GradientAngle * Math.PI / 180.0;
                                float cx = body.Width / 2f, cy = body.Height / 2f;
                                float dx = (float)(Math.Cos(rad) * cx), dy = (float)(Math.Sin(rad) * cy);
                                if (Math.Abs(dx) < 0.01f) dx = 0.01f;
                                if (Math.Abs(dy) < 0.01f) dy = 0.01f;
                                using (var br = new LinearGradientBrush(
                                    new PointF(cx - dx, cy - dy), new PointF(cx + dx, cy + dy), c1, c2))
                                    g.FillPath(br, path);
                            }
                        }
                        else
                            using (var br = new SolidBrush(Color.FromArgb(a, cfg.BgR, cfg.BgG, cfg.BgB)))
                                g.FillPath(br, path);
                    }
                    else if (cfg.Gradient)
                    {
                        Color c1 = Color.FromArgb(a, cfg.BgR, cfg.BgG, cfg.BgB);
                        Color c2 = Color.FromArgb(a, cfg.BgR2, cfg.BgG2, cfg.BgB2);
                        double rad = cfg.GradientAngle * Math.PI / 180.0;
                        float cx = body.Width / 2f, cy = body.Height / 2f;
                        float dx = (float)(Math.Cos(rad) * cx), dy = (float)(Math.Sin(rad) * cy);
                        if (Math.Abs(dx) < 0.01f) dx = 0.01f;
                        if (Math.Abs(dy) < 0.01f) dy = 0.01f;
                        using (var br = new LinearGradientBrush(
                            new PointF(cx - dx, cy - dy), new PointF(cx + dx, cy + dy), c1, c2))
                            g.FillPath(br, path);
                    }
                    else
                    {
                        using (var br = new SolidBrush(Color.FromArgb(a, cfg.BgR, cfg.BgG, cfg.BgB)))
                            g.FillPath(br, path);
                    }
                }
            }

            // ---- рамка ----
            if (cfg.BorderEnabled && cfg.BorderWidth > 0 && cfg.BorderAlpha > 0)
            {
                float bw = cfg.BorderWidth * scale;
                if (bw < 1) bw = 1;
                var inner = new RectangleF(body.X + bw / 2f, body.Y + bw / 2f, body.Width - bw, body.Height - bw);
                using (var path = RoundRect(inner, Math.Max(0, radius - (int)(bw / 2))))
                using (var pen = new Pen(Color.FromArgb(cfg.BorderAlpha, cfg.BorderR, cfg.BorderG, cfg.BorderB), bw))
                    g.DrawPath(pen, path);
            }

            int pad = (int)Math.Round(cfg.Padding * scale);

            // ---- обложка ----
            bool showThumb = t != null && cfg.ThumbSide != "none" && (thumb != null || cfg.ThumbPlaceholder);
            int thumbSide = 0;
            if (showThumb)
            {
                int avail = bounds.Height - pad * 2;
                int size = cfg.ThumbSize > 0 ? (int)Math.Round(cfg.ThumbSize * scale) : avail;
                if (size > avail) size = avail;
                if (size < 4) size = 4;
                if (size > bounds.Width - pad * 2) size = Math.Max(4, bounds.Width - pad * 2);
                int ty;
                int availH = bounds.Height - pad * 2;
                if (cfg.ThumbVAlign == "top") ty = bounds.Y + pad;
                else if (cfg.ThumbVAlign == "bottom") ty = bounds.Y + pad + Math.Max(0, availH - size);
                else ty = bounds.Y + (bounds.Height - size) / 2;
                ty += (int)Math.Round(cfg.ThumbOffset * scale);
                int navW = NavWidth(cfg, scale);
                navInset = 0;
                int tx;
                if (cfg.ThumbSide == "right")
                    tx = bounds.Right - pad - (cfg.NavSide == "right" ? navW : 0) - size;
                else
                    tx = bounds.X + pad + (cfg.NavSide == "left" ? navW : 0);
                var tr2 = new Rectangle(tx, ty, size, size);
                if (cfg.ThumbShadow && size > 8)
                {
                    for (int i = 3; i >= 1; i--)
                    {
                        var sr = new Rectangle(tr2.X - i, tr2.Y + i, tr2.Width + i * 2, tr2.Height);
                        using (var br = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                            g.FillPath(br, RoundRect(sr, (int)Math.Round(cfg.ThumbCorner * scale) + i));
                    }
                }
                int corner = cfg.ThumbCircle ? tr2.Width / 2 : (int)Math.Round(cfg.ThumbCorner * scale);
                if (thumb != null)
                    DrawThumb(g, thumb, tr2, corner, cfg.ThumbCircle);
                else
                    DrawPlaceholder(g, tr2, corner, scale);
                if (cfg.ThumbBorder)
                {
                    using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, scale)))
                        if (cfg.ThumbCircle) g.DrawEllipse(pen, tr2);
                        else g.DrawPath(pen, RoundRect(tr2, corner));
                }
                thumbSide = size + (int)Math.Round(cfg.TextGap * scale);
            }

            // ---- текст ----
            string title = t == null ? "" : (t.Title.Length > 0 ? t.Title : "Без названия");
            string second = t == null ? "" : t.SecondText(cfg);
            if (cfg.TextCase == "upper") { title = title.ToUpperInvariant(); second = second.ToUpperInvariant(); }
            else if (cfg.TextCase == "lower") { title = title.ToLowerInvariant(); second = second.ToLowerInvariant(); }
            if (t != null && !t.IsPlaying && !cfg.DimPaused) { /* без затемнения */ }

            int textX, textW;
            if (showThumb && cfg.ThumbSide == "right")
            {
                textX = bounds.X + pad;
                textW = bounds.Width - pad * 2 - thumbSide;
            }
            else if (showThumb)
            {
                textX = bounds.X + pad + thumbSide;
                textW = bounds.Width - pad * 2 - thumbSide;
            }
            else
            {
                textX = bounds.X + pad;
                textW = bounds.Width - pad * 2;
            }
            int navW2 = NavWidth(cfg, scale);
            if (navW2 > 0)
            {
                if (cfg.NavSide == "left") { textX += navW2; }
                textW -= navW2;
            }
            if (textW < 8) return;

            Color titleCol = Config.ParseColor(cfg.TitleColor, Color.White);
            Color secondCol = Config.ParseColor(cfg.ArtistColor, Color.FromArgb(168, 168, 172));
            bool darkBg = Luminance(EffectiveBackground(cfg, thumb, bounds)) > 0.55f;
            bool darkText = darkBg;
            if (cfg.TextColorMode != "custom")
            {
                if (darkText)
                {
                    titleCol = Color.FromArgb(16, 16, 20);
                    secondCol = Color.FromArgb(92, 92, 102);
                }
                else
                {
                    titleCol = Color.White;
                    secondCol = Color.FromArgb(216, 216, 220);
                }
            }
            if (t != null && !t.IsPlaying && cfg.DimPaused)
            {
                titleCol = Color.FromArgb(190, titleCol.R, titleCol.G, titleCol.B);
                secondCol = Color.FromArgb(150, secondCol.R, secondCol.G, secondCol.B);
            }
            if (cfg.TextOpacity < 100)
            {
                titleCol = Color.FromArgb(titleCol.R, titleCol.G, titleCol.B,
                    titleCol.A * cfg.TextOpacity / 100);
                secondCol = Color.FromArgb(secondCol.R, secondCol.G, secondCol.B,
                    secondCol.A * cfg.TextOpacity / 100);
            }

            using (var fT = SafeFont(cfg.FontName, cfg.TitleSize * scale, cfg.TitleBold ? FontStyle.Bold : FontStyle.Regular))
            using (var fS = SafeFont(cfg.FontName, cfg.ArtistSize * scale, cfg.ArtistBold ? FontStyle.Bold : FontStyle.Regular))
            using (var brT = new SolidBrush(titleCol))
            using (var brS = new SolidBrush(secondCol))
            {
                bool fakeT = cfg.TitleBold && NeedsFakeBold(cfg.FontName, cfg.TitleSize);
                bool fakeS = cfg.ArtistBold && NeedsFakeBold(cfg.FontName, cfg.ArtistSize);
                bool twoLines = second.Length > 0 && cfg.SecondLine != "none";
                float hT = fT.GetHeight(g);
                float hS = fS.GetHeight(g);
                float spacing = cfg.LineSpacing * scale;
                float totalH = twoLines ? hT + hS * 0.92f + spacing : hT;
                float top;
                if (cfg.VAlign == "top") top = bounds.Y + (float)pad;
                else if (cfg.VAlign == "bottom") top = bounds.Bottom - (float)pad - totalH;
                else top = bounds.Y + (bounds.Height - totalH) / 2f;

                var sf = new StringFormat(StringFormat.GenericTypographic);
                sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                sf.Trimming = StringTrimming.None;
                switch (cfg.Align)
                {
                    case "center": sf.Alignment = StringAlignment.Center; break;
                    case "right": sf.Alignment = StringAlignment.Far; break;
                    default: sf.Alignment = StringAlignment.Near; break;
                }

                float titleW = g.MeasureString(title, fT, int.MaxValue, sf).Width;

                var oldClip = g.Clip;
                g.SetClip(new RectangleF(textX, bounds.Y, textW, bounds.Height), CombineMode.Intersect);

                if (cfg.TextOutline && cfg.OutlineWidth > 0)
                {
                    Color oc = darkText ? Color.FromArgb(150, 255, 255, 255) : Color.FromArgb(150, 0, 0, 0);
                    using (var op = new Pen(oc, cfg.OutlineWidth * scale))
                    using (var path = new GraphicsPath())
                    {
                        path.AddString(title, fT.FontFamily,
                            (int)(cfg.TitleBold ? FontStyle.Bold : FontStyle.Regular),
                            fT.Size, new PointF(textX, top - hT * 0.08f), sf);
                        g.DrawPath(op, path);
                    }
                }

                var rT = new RectangleF(textX, top - hT * 0.08f, textW, hT * 1.15f);
                bool overflow = titleW > textW - 1;

                if (overflow && cfg.Marquee)
                {
                    float gap = cfg.MarqueeGap * scale;
                    var r1 = new RectangleF(textX - scrollPx, rT.Y, titleW + 2, rT.Height);
                    var r2 = new RectangleF(textX - scrollPx + titleW + gap, rT.Y, titleW + 2, rT.Height);
                    if (cfg.TextShadow) { DrawShadow(g, title, fT, r1, sf, scale, cfg.ShadowStrength); DrawShadow(g, title, fT, r2, sf, scale, cfg.ShadowStrength); }
                    DrawStr(g, title, fT, brT, r1, sf, scale, fakeT);
                    DrawStr(g, title, fT, brT, r2, sf, scale, fakeT);
                }
                else
                {
                    var sf2 = (StringFormat)sf.Clone();
                    sf2.Trimming = StringTrimming.EllipsisCharacter;
                    if (overflow) sf2.FormatFlags &= ~StringFormatFlags.NoClip;
                    if (cfg.TextShadow) DrawShadow(g, title, fT, rT, sf2, scale, cfg.ShadowStrength);
                    DrawStr(g, title, fT, brT, rT, sf2, scale, fakeT);
                    sf2.Dispose();
                }

                if (twoLines)
                {
                    var rS = new RectangleF(textX, top + hT * 0.92f + spacing, textW, hS * 1.15f);
                    var sf3 = (StringFormat)sf.Clone();
                    sf3.Trimming = StringTrimming.EllipsisCharacter;
                    if (cfg.TextShadow) DrawShadow(g, second, fS, rS, sf3, scale, cfg.ShadowStrength);
                    DrawStr(g, second, fS, brS, rS, sf3, scale, fakeS);
                    sf3.Dispose();
                }

                g.Clip = oldClip;
                sf.Dispose();
            }

            if (cfg.NavButtons)
            {
                Color nc = Color.FromArgb(210, 210, 218);
                bool lightBg2 = Luminance(EffectiveBackground(cfg, thumb, bounds)) > 0.55f;
                if (lightBg2) nc = Color.FromArgb(70, 70, 82);
                if (cfg.NavColorMode == "custom") nc = Config.ParseColor(cfg.NavColor, nc);
                if (cfg.NavOpacity < 100)
                    nc = Color.FromArgb(nc.A * cfg.NavOpacity / 100, nc.R, nc.G, nc.B);
                if (t != null && !t.IsPlaying && cfg.DimPaused) nc = Color.FromArgb(130, nc);
                bool playing = t == null || t.IsPlaying;

                var kinds = NavKinds(cfg);
                var rects = NavRects(cfg, bounds, scale, navInset);
                for (int i = 0; i < rects.Length && i < kinds.Length; i++)
                {
                    var r = rects[i];
                    if (cfg.NavHover && HoverIndex == i && cfg.NavBackdrop)
                    {
                        int rad0 = (int)Math.Round(r.Width * cfg.NavCorner / 100.0);
                        using (var b = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
                            g.FillPath(b, RoundRect(new RectangleF(r.X, r.Y, r.Width, r.Height), rad0));
                    }
                    if (cfg.NavBackdrop)
                    {
                        int rad = (int)Math.Round(r.Width * cfg.NavCorner / 100.0);
                        using (var b = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                            g.FillPath(b, RoundRect(new RectangleF(r.X, r.Y, r.Width, r.Height), rad));
                    }
                    DrawNavGlyph(g, r, kinds[i], nc, playing, cfg.NavWeight / 100f);
                }
            }

            if (cfg.Progress && t != null && t.DurationSec > 0)
            {
                double frac = t.CurrentPosition / t.DurationSec;
                if (frac < 0) frac = 0;
                if (frac > 1) frac = 1;
                int ph = Math.Max(1, (int)Math.Round(cfg.ProgressHeight * scale));
                int inset = (int)Math.Round(cfg.Padding * scale);
                if (inset < 2) inset = 2;
                int rIns = Math.Max(inset, radius / 2 + 1);   // не залезать на скругление
                int off = (int)Math.Round(cfg.ProgressOffset * scale);
                var trackRect = new RectangleF(bounds.X + rIns, bounds.Bottom - inset - ph - off, bounds.Width - rIns * 2, ph);
                var fillRect = new RectangleF(trackRect.X, trackRect.Y, (float)(trackRect.Width * frac), ph);
                var pc = Config.ParseColor(cfg.ProgressColor, Color.White);
                int pa = cfg.ProgressAlpha;
                if (t != null && !t.IsPlaying && cfg.DimPaused) pa = pa * 6 / 10;
                bool lightBg = Luminance(EffectiveBackground(cfg, thumb, bounds)) > 0.55f;
                using (var br = new SolidBrush(lightBg ? Color.FromArgb(40, 0, 0, 0)
                                                       : Color.FromArgb(60, 255, 255, 255)))
                {
                    if (cfg.ProgressRounded) g.FillPath(br, RoundRect(trackRect, ph / 2));
                    else g.FillRectangle(br, trackRect);
                }
                if (lightBg) pc = Color.FromArgb(40, 40, 46);
                if (fillRect.Width > 0.5f)
                {
                    using (var br = new SolidBrush(Color.FromArgb(pa, pc.R, pc.G, pc.B)))
                    {
                        if (cfg.ProgressRounded) g.FillPath(br, RoundRect(fillRect, ph / 2));
                        else g.FillRectangle(br, fillRect);
                    }
                }
            }
        }

        static void DrawNavGlyph(Graphics g, Rectangle r, int kind, Color c, bool playing)
        {
            DrawNavGlyph(g, r, kind, c, playing, 1f);
        }

        static void DrawNavGlyph(Graphics g, Rectangle r, int kind, Color c, bool playing, float weight)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float u = r.Width / 3f;

            if (kind == 1)
            {
                if (playing)
                {
                    // пауза — две вертикальные черты
                    float bw = Math.Max(1.2f, u * 0.34f * weight);
                    float bh = u * 1.15f;
                    using (var b = new SolidBrush(c))
                    {
                        g.FillRectangle(b, cx - bw - u * 0.10f, cy - bh / 2, bw, bh);
                        g.FillRectangle(b, cx + u * 0.10f, cy - bh / 2, bw, bh);
                    }
                }
                else
                {
                    // play — треугольник
                    using (var b = new SolidBrush(c))
                        g.FillPolygon(b, new PointF[] {
                            new PointF(cx - u * 0.42f, cy - u * 0.62f),
                            new PointF(cx - u * 0.42f, cy + u * 0.62f),
                            new PointF(cx + u * 0.58f, cy) });
                }
                return;
            }

            bool next = kind == 2;
            float dir = next ? 1f : -1f;
            using (var b = new SolidBrush(c))
            {
                g.FillPolygon(b, new PointF[] {
                    new PointF(cx - dir * u * 0.52f, cy - u * 0.62f),
                    new PointF(cx - dir * u * 0.52f, cy + u * 0.62f),
                    new PointF(cx + dir * u * 0.46f, cy) });
                float bw = Math.Max(1.2f, u * 0.20f * weight);
                g.FillRectangle(b, cx + dir * u * 0.52f - (next ? 0 : bw), cy - u * 0.62f, bw, u * 1.24f);
            }
        }

        static void DrawShadow(Graphics g, string text, Font f, RectangleF r, StringFormat sf, float scale, int strength)
        {
            float off = Math.Max(1f, 1f * scale);
            int sa = strength;
            using (var br = new SolidBrush(Color.FromArgb(sa, 0, 0, 0)))
            {
                var rr = new RectangleF(r.X + off, r.Y + off, r.Width, r.Height);
                g.DrawString(text, f, br, rr, sf);
            }
        }

        static void DrawPlaceholder(Graphics g, Rectangle dst, int radius, float scale)
        {
            using (var path = RoundRect(dst, radius))
            {
                using (var br = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    g.FillPath(br, path);
                float cx = dst.X + dst.Width / 2f, cy = dst.Y + dst.Height / 2f;
                float r = Math.Min(dst.Width, dst.Height) * 0.20f;
                if (r < 2) return;
                using (var br = new SolidBrush(Color.FromArgb(130, 255, 255, 255)))
                {
                    g.FillEllipse(br, cx - r * 1.5f, cy + r * 0.45f, r, r * 0.9f);
                    g.FillEllipse(br, cx + r * 0.1f, cy + r * 0.05f, r, r * 0.9f);
                }
                using (var pen = new Pen(Color.FromArgb(130, 255, 255, 255), Math.Max(1f, r * 0.3f)))
                {
                    g.DrawLine(pen, cx - r * 0.55f, cy + r * 0.85f, cx - r * 0.55f, cy - r * 0.8f);
                    g.DrawLine(pen, cx + r * 1.05f, cy + r * 0.45f, cx + r * 1.05f, cy - r * 1.2f);
                    g.DrawLine(pen, cx - r * 0.55f, cy - r * 0.8f, cx + r * 1.05f, cy - r * 1.2f);
                }
            }
        }

        static void DrawThumb(Graphics g, Image thumb, Rectangle dst, int radius, bool circle)
        {
            if (circle)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(dst);
                    var st0 = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    float sc = Math.Max((float)dst.Width / thumb.Width, (float)dst.Height / thumb.Height);
                    int dw0 = (int)Math.Round(thumb.Width * sc);
                    int dh0 = (int)Math.Round(thumb.Height * sc);
                    g.DrawImage(thumb, new Rectangle(dst.X + (dst.Width - dw0) / 2,
                        dst.Y + (dst.Height - dh0) / 2, dw0, dh0));
                    g.Restore(st0);
                }
                return;
            }
            if (radius <= 0)
            {
                g.DrawImage(thumb, dst);
                return;
            }
            using (var path = RoundRect(dst, radius))
            {
                var state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                // заполняем по «cover», чтобы не растягивать обложку
                float scale = Math.Max((float)dst.Width / thumb.Width, (float)dst.Height / thumb.Height);
                int dw = (int)Math.Round(thumb.Width * scale);
                int dh = (int)Math.Round(thumb.Height * scale);
                int dx = dst.X + (dst.Width - dw) / 2;
                int dy = dst.Y + (dst.Height - dh) / 2;
                g.DrawImage(thumb, new Rectangle(dx, dy, dw, dh));
                g.Restore(state);
            }
        }

        public static GraphicsPath RoundRect(RectangleF r, int radius)
        {
            var p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static Font SafeFont(string name, float size, FontStyle style)
        {
            Font f = null;
            try { f = new Font(name, size, style); }
            catch { f = null; }

            if (f != null) return f;

            try { return new Font("Segoe UI", size, style); }
            catch { return new Font(SystemFonts.DefaultFont.FontFamily, size, style); }
        }

        /// <summary>
        /// Видна ли жирность на глаз. У некоторых семейств (Bahnschrift SemiBold и т.п.)
        /// «Bold» почти не отличается от Regular — тогда дорисовываем её сами.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, bool> fakeBoldCache =
            new System.Collections.Generic.Dictionary<string, bool>();

        static bool NeedsFakeBold(string family, float size)
        {
            string key = family + "|" + size.ToString("F2");
            bool cached;
            if (fakeBoldCache.TryGetValue(key, out cached)) return cached;
            bool res = ComputeFakeBold(family, size);
            if (fakeBoldCache.Count > 64) fakeBoldCache.Clear();
            fakeBoldCache[key] = res;
            return res;
        }

        static bool ComputeFakeBold(string family, float size)
        {
            try
            {
                using (var fam = new FontFamily(family))
                {
                    if (!fam.IsStyleAvailable(FontStyle.Bold)) return true;
                }
                using (var g = Graphics.FromImage(new Bitmap(1, 1)))
                using (var fr = new Font(family, size, FontStyle.Regular))
                using (var fb = new Font(family, size, FontStyle.Bold))
                {
                    float wr = g.MeasureString("Жизнь Wm", fr).Width;
                    float wb = g.MeasureString("Жизнь Wm", fb).Width;
                    return wb < wr * 1.02f;
                }
            }
            catch { return true; }
        }

        /// <summary>Рисует строку, при необходимости дорисовывая «жирность» вручную.</summary>
        static void DrawStr(Graphics g, string text, Font f, Brush br, RectangleF r, StringFormat sf,
                            float scale, bool fake)
        {
            g.DrawString(text, f, br, r, sf);
            if (!fake) return;
            float dx = Math.Max(0.6f, 0.7f * scale);
            var rr = new RectangleF(r.X + dx, r.Y, r.Width, r.Height);
            g.DrawString(text, f, br, rr, sf);
        }

        /// <summary>Приблизительный цвет подложки под текстом — для авто-контраста.</summary>
        public static Color EffectiveBackground(Config cfg, Image thumb, Rectangle bounds)
        {
            Color bg;
            bool fromCover = thumb != null && cfg.BgMode != "color";
            if (fromCover && cfg.BgMode == "cover_color")
            {
                bg = Adjust(AverageColor(thumb), cfg.CoverSaturation, cfg.CoverDark);
            }
            else if (fromCover)
            {
                Color mc = Adjust(AverageColor(thumb), cfg.CoverSaturation, cfg.CoverDark);
                // поверх размытия лежит затемнение + тинт
                float t = Math.Max(0f, Math.Min(1f, cfg.CoverDark / 100f));
                Color tint = Color.FromArgb(cfg.BgR, cfg.BgG, cfg.BgB);
                bg = MixColors(mc, Color.Black, t);
                if (cfg.BgMode == "cover_blur_tint" && cfg.Gradient)
                    bg = MixColors(bg, tint, 0.45f);
            }
            else if (cfg.Gradient)
            {
                bg = MixColors(Color.FromArgb(cfg.BgR, cfg.BgG, cfg.BgB),
                               Color.FromArgb(cfg.BgR2, cfg.BgG2, cfg.BgB2), 0.5f);
            }
            else
            {
                bg = Color.FromArgb(cfg.BgR, cfg.BgG, cfg.BgB);
            }

            if (!cfg.BgEnabled || cfg.OpacityPercent <= 0) bg = Color.FromArgb(30, 30, 34);
            else if (cfg.OpacityPercent < 100)
                bg = MixColors(Color.FromArgb(30, 30, 34), bg, cfg.OpacityPercent / 100f);
            return bg;
        }

        public static float Luminance(Color c)
        {
            return (c.R * 0.299f + c.G * 0.587f + c.B * 0.114f) / 255f;
        }

        public static Color MixColors(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                Clamp255((int)(a.R + (b.R - a.R) * t)),
                Clamp255((int)(a.G + (b.G - a.G) * t)),
                Clamp255((int)(a.B + (b.B - a.B) * t)));
        }

        /// <summary>Средний цвет изображения (по уменьшенной копии).</summary>
        public static Color AverageColor(Image img)
        {
            try
            {
                using (var small = new Bitmap(16, 16, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(img, new Rectangle(0, 0, 16, 16));
                    }
                    long r = 0, gg = 0, b = 0, n = 0;
                    for (int y = 0; y < 16; y++)
                        for (int x = 0; x < 16; x++)
                        {
                            var c = small.GetPixel(x, y);
                            if (c.A < 8) continue;
                            r += c.R; gg += c.G; b += c.B; n++;
                        }
                    if (n == 0) return Color.FromArgb(40, 40, 44);
                    return Color.FromArgb((int)(r / n), (int)(gg / n), (int)(b / n));
                }
            }
            catch { return Color.FromArgb(40, 40, 44); }
        }

        /// <summary>Насыщенность (%) и затемнение (%) среднего цвета.</summary>
        static Color Adjust(Color c, int saturationPercent, int darkPercent)
        {
            int gray = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
            float k = saturationPercent / 100f;
            int r = gray + (int)((c.R - gray) * k);
            int g = gray + (int)((c.G - gray) * k);
            int b = gray + (int)((c.B - gray) * k);
            float d = 1f - darkPercent / 100f;
            r = (int)(r * d); g = (int)(g * d); b = (int)(b * d);
            return Color.FromArgb(Clamp255(r), Clamp255(g), Clamp255(b));
        }

        static int Clamp255(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }

    /// <summary>Кэш размытых обложек: размытие дорогое, пересчитываем только при смене картинки/размера/силы.</summary>
    static class BlurCache
    {
        static readonly object sync = new object();
        static readonly System.Collections.Generic.Dictionary<string, Bitmap> cache =
            new System.Collections.Generic.Dictionary<string, Bitmap>();
        static readonly System.Collections.Generic.Queue<string> order =
            new System.Collections.Generic.Queue<string>();
        const int MaxItems = 8;

        static string Key(Image src, int w, int h, int radius)
        {
            return src.GetHashCode() + "|" + w + "x" + h + "|" + radius;
        }

        public static Bitmap Get(Image src, int w, int h, int radius)
        {
            if (src == null || w < 2 || h < 2) return null;
            // ограничим размер полотна, чтобы размытие не съело память на широких виджетах
            if (w > 1400) { h = (int)((long)h * 1400 / w); w = 1400; }
            if (h > 400) { w = (int)((long)w * 400 / h); h = 400; }

            string key = Key(src, w, h, radius);
            lock (sync)
            {
                Bitmap hit;
                if (cache.TryGetValue(key, out hit) && hit != null) return hit;
            }

            Bitmap result = null;
            try
            {
                // сильно уменьшаем — это и есть грубое размытие, потом растягиваем бикубикой
                int divisor = Math.Max(2, radius / 2);
                int sw = Math.Max(3, Math.Min(64, w / divisor));
                int sh = Math.Max(3, Math.Min(64, h / divisor));
                using (var tiny = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(tiny))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        // «cover» — заполнить весь кадр без полей
                        float sc = Math.Max((float)sw / src.Width, (float)sh / src.Height);
                        int dw = (int)Math.Ceiling(src.Width * sc);
                        int dh = (int)Math.Ceiling(src.Height * sc);
                        g.DrawImage(src, new Rectangle((sw - dw) / 2, (sh - dh) / 2, dw, dh));
                    }
                    result = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(result))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(tiny, new Rectangle(0, 0, w, h));
                    }
                }
            }
            catch { return null; }

            lock (sync)
            {
                cache[key] = result;
                order.Enqueue(key);
                while (order.Count > MaxItems)
                {
                    string oldKey = order.Dequeue();
                    Bitmap old;
                    if (cache.TryGetValue(oldKey, out old))
                    {
                        cache.Remove(oldKey);
                        try { if (old != null) old.Dispose(); } catch { }
                    }
                }
            }
            return result;
        }
    }
}
