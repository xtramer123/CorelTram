using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VectorTram;

public partial class MainWindow
{
    private sealed class TraceResult
    {
        public List<(Color Color, PathGeometry Geometry)> ColorPaths = new();
        public string Svg = "";
        public int Width;
        public int Height;
    }

    // ========== IMAGE -> SVG TRACING ==========
    private void TraceImageToSvg_Click(object sender, RoutedEventArgs e)
    {
        var open = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Elegir imagen para trazar",
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff|Todos los archivos (*.*)|*.*"
        };
        if (open.ShowDialog() != true) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var source = LoadBitmapForTrace(open.FileName);
            var result = VectorizeImage(source, 500, 16);

            if (result.ColorPaths.Count == 0)
            {
                StatusText.Text = "No se detectaron formas para trazar";
                return;
            }

            AddTracedToCanvas(result);

            var save = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar SVG",
                Filter = "SVG (*.svg)|*.svg",
                FileName = System.IO.Path.GetFileNameWithoutExtension(open.FileName) + ".svg"
            };
            if (save.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(save.FileName, result.Svg, new UTF8Encoding(false));
                StatusText.Text = $"Trazado y guardado: {System.IO.Path.GetFileName(save.FileName)}";
            }
            else
            {
                StatusText.Text = $"Trazado: {result.ColorPaths.Count} formas añadidas al lienzo";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al trazar la imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static BitmapSource LoadBitmapForTrace(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bmp.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private void AddTracedToCanvas(TraceResult result)
    {
        double paperW = PaperBorder.Width;
        double paperH = PaperBorder.Height;
        double fit = Math.Min(1.0, Math.Min((paperW - 40) / result.Width, (paperH - 40) / result.Height));
        double offX = _pageOriginX + (paperW - result.Width * fit) / 2.0;
        double offY = _pageOriginY + (paperH - result.Height * fit) / 2.0;

        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(fit, fit));
        transform.Children.Add(new TranslateTransform(offX, offY));

        foreach (var (color, geometry) in result.ColorPaths)
        {
            var pg = (PathGeometry)geometry.Clone();
            pg.Transform = transform;
            pg.Freeze();

            var path = new System.Windows.Shapes.Path
            {
                Data = pg,
                Fill = new SolidColorBrush(color),
                Stroke = null,
                StrokeThickness = 0,
                SnapsToDevicePixels = false
            };

            _objectCounter++;
            path.Name = $"Trazado{_objectCounter}";
            DrawingCanvas.Children.Add(path);
            AddToCurrentLayer(path);
        }
        UpdateLayersList();
    }

    private TraceResult VectorizeImage(BitmapSource source, int maxDim, int colorCount)
    {
        int sw = source.PixelWidth, sh = source.PixelHeight;
        double scale = Math.Min(1.0, (double)maxDim / Math.Max(sw, sh));
        int tw = Math.Max(1, (int)Math.Round(sw * scale));
        int th = Math.Max(1, (int)Math.Round(sh * scale));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new Rect(0, 0, tw, th));
        var rtb = new RenderTargetBitmap(tw, th, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

        var working = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
        working.Freeze();

        int w = working.PixelWidth, h = working.PixelHeight;
        int stride = w * 4;
        var raw = new byte[h * stride];
        working.CopyPixels(raw, stride, 0);

        int n = w * h;
        var rr = new byte[n];
        var gg = new byte[n];
        var bb = new byte[n];
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            byte cb = raw[o], cg = raw[o + 1], cr = raw[o + 2], ca = raw[o + 3];
            if (ca >= 250)
            {
                rr[i] = cr; gg[i] = cg; bb[i] = cb;
            }
            else
            {
                double a = ca / 255.0;
                rr[i] = (byte)(cr * a + 255 * (1 - a));
                gg[i] = (byte)(cg * a + 255 * (1 - a));
                bb[i] = (byte)(cb * a + 255 * (1 - a));
            }
        }

        var palette = BuildPalette(rr, gg, bb, n, colorCount);
        var label = new int[n];
        var counts = new int[palette.Length];
        for (int i = 0; i < n; i++)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int c = 0; c < palette.Length; c++)
            {
                int dr = rr[i] - palette[c].R;
                int dg = gg[i] - palette[c].G;
                int db = bb[i] - palette[c].B;
                int d = dr * dr + dg * dg + db * db;
                if (d < bestDist) { bestDist = d; best = c; }
            }
            label[i] = best;
            counts[best]++;
        }

        var order = Enumerable.Range(0, palette.Length)
            .Where(c => counts[c] > 0)
            .OrderByDescending(c => counts[c])
            .ToList();

        const double simplifyTol = 1.2;
        const double minArea = 6.0;

        var result = new TraceResult { Width = w, Height = h };
        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(w)
           .Append("\" height=\"").Append(h)
           .Append("\" viewBox=\"0 0 ").Append(w).Append(' ').Append(h).Append("\">\n");

        foreach (var ci in order)
        {
            var rawLoops = TraceColorLoops(label, w, h, ci);
            var figures = new List<List<Point>>();
            var d = new StringBuilder();

            foreach (var loop in rawLoops)
            {
                if (loop.Count < 3) continue;
                if (PolygonArea(loop) < minArea) continue;
                var simp = SimplifyLoop(loop, simplifyTol);
                if (simp.Count < 3) continue;

                figures.Add(simp);
                d.Append('M').Append(Fmt(simp[0].X)).Append(',').Append(Fmt(simp[0].Y));
                for (int i = 1; i < simp.Count; i++)
                    d.Append('L').Append(Fmt(simp[i].X)).Append(',').Append(Fmt(simp[i].Y));
                d.Append('Z');
            }

            if (figures.Count == 0) continue;

            var col = palette[ci];
            var color = Color.FromRgb(col.R, col.G, col.B);

            var pg = new PathGeometry { FillRule = FillRule.EvenOdd };
            foreach (var fig in figures)
            {
                var pf = new PathFigure { IsClosed = true, IsFilled = true, StartPoint = fig[0] };
                for (int i = 1; i < fig.Count; i++)
                    pf.Segments.Add(new LineSegment(fig[i], true));
                pg.Figures.Add(pf);
            }
            result.ColorPaths.Add((color, pg));

            svg.Append("  <path fill=\"#").Append($"{col.R:X2}{col.G:X2}{col.B:X2}")
               .Append("\" fill-rule=\"evenodd\" d=\"").Append(d).Append("\"/>\n");
        }

        svg.Append("</svg>\n");
        result.Svg = svg.ToString();
        return result;
    }

    private static (byte R, byte G, byte B)[] BuildPalette(byte[] r, byte[] g, byte[] b, int n, int colorCount)
    {
        var samples = new List<int>();
        int step = Math.Max(1, n / 20000);
        for (int i = 0; i < n; i += step)
            samples.Add((r[i] << 16) | (g[i] << 8) | b[i]);
        if (samples.Count == 0) samples.Add(0xFFFFFF);

        var boxes = new List<List<int>> { samples };
        while (boxes.Count < colorCount)
        {
            int best = -1, bestRange = -1, bestCh = 0;
            for (int bi = 0; bi < boxes.Count; bi++)
            {
                var box = boxes[bi];
                if (box.Count < 2) continue;

                int mn0 = 255, mx0 = 0, mn1 = 255, mx1 = 0, mn2 = 255, mx2 = 0;
                foreach (var c in box)
                {
                    int cr = (c >> 16) & 255, cg = (c >> 8) & 255, cb = c & 255;
                    if (cr < mn0) mn0 = cr; if (cr > mx0) mx0 = cr;
                    if (cg < mn1) mn1 = cg; if (cg > mx1) mx1 = cg;
                    if (cb < mn2) mn2 = cb; if (cb > mx2) mx2 = cb;
                }
                int r0 = mx0 - mn0, r1 = mx1 - mn1, r2 = mx2 - mn2;
                int mx = Math.Max(r0, Math.Max(r1, r2));
                if (mx > bestRange) { bestRange = mx; best = bi; bestCh = mx == r0 ? 0 : (mx == r1 ? 1 : 2); }
            }
            if (best < 0 || bestRange <= 0) break;

            var b0 = boxes[best];
            int shift = (2 - bestCh) * 8;
            b0.Sort((x, y) => ((x >> shift) & 255).CompareTo((y >> shift) & 255));
            int mid = b0.Count / 2;
            boxes[best] = b0.GetRange(0, mid);
            boxes.Add(b0.GetRange(mid, b0.Count - mid));
        }

        var pal = new (byte R, byte G, byte B)[boxes.Count];
        for (int i = 0; i < boxes.Count; i++)
        {
            long sr = 0, sg = 0, sb = 0;
            foreach (var c in boxes[i]) { sr += (c >> 16) & 255; sg += (c >> 8) & 255; sb += c & 255; }
            int cnt = Math.Max(1, boxes[i].Count);
            pal[i] = ((byte)(sr / cnt), (byte)(sg / cnt), (byte)(sb / cnt));
        }
        return pal;
    }

    private static List<List<Point>> TraceColorLoops(int[] label, int w, int h, int colorIndex)
    {
        var adj = new Dictionary<long, List<long>>();
        var edges = new HashSet<(long, long)>();

        bool In(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && label[y * w + x] == colorIndex;

        static long Key(double x, double y)
        {
            long kx = (long)Math.Round(x * 2);
            long ky = (long)Math.Round(y * 2);
            return kx * 100000L + ky;
        }

        void AddEdge(double x1, double y1, double x2, double y2)
        {
            long a = Key(x1, y1), b = Key(x2, y2);
            if (a == b) return;
            var e = a < b ? (a, b) : (b, a);
            if (!edges.Add(e)) return;
            if (!adj.TryGetValue(a, out var la)) { la = new List<long>(); adj[a] = la; } la.Add(b);
            if (!adj.TryGetValue(b, out var lb)) { lb = new List<long>(); adj[b] = lb; } lb.Add(a);
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool a = In(x, y), b = In(x + 1, y), c = In(x + 1, y + 1), d = In(x, y + 1);
                int idx = (a ? 8 : 0) | (b ? 4 : 0) | (c ? 2 : 0) | (d ? 1 : 0);
                if (idx == 0 || idx == 15) continue;

                double tx = x + 0.5, ty = y;
                double rx = x + 1, ry = y + 0.5;
                double bx = x + 0.5, by = y + 1;
                double lx = x, ly = y + 0.5;

                switch (idx)
                {
                    case 1: AddEdge(lx, ly, bx, by); break;
                    case 2: AddEdge(bx, by, rx, ry); break;
                    case 3: AddEdge(lx, ly, rx, ry); break;
                    case 4: AddEdge(tx, ty, rx, ry); break;
                    case 5: AddEdge(tx, ty, rx, ry); AddEdge(lx, ly, bx, by); break;
                    case 6: AddEdge(tx, ty, bx, by); break;
                    case 7: AddEdge(tx, ty, lx, ly); break;
                    case 8: AddEdge(tx, ty, lx, ly); break;
                    case 9: AddEdge(tx, ty, bx, by); break;
                    case 10: AddEdge(tx, ty, lx, ly); AddEdge(bx, by, rx, ry); break;
                    case 11: AddEdge(tx, ty, rx, ry); break;
                    case 12: AddEdge(lx, ly, rx, ry); break;
                    case 13: AddEdge(bx, by, rx, ry); break;
                    case 14: AddEdge(lx, ly, bx, by); break;
                }
            }
        }

        var loops = new List<List<Point>>();
        var used = new HashSet<(long, long)>();

        foreach (var startKey in adj.Keys.ToList())
        {
            while (true)
            {
                bool has = false;
                foreach (var nb in adj[startKey])
                {
                    var e = startKey < nb ? (startKey, nb) : (nb, startKey);
                    if (!used.Contains(e)) { has = true; break; }
                }
                if (!has) break;

                var keys = new List<long> { startKey };
                long cur = startKey;
                int guard = 0;
                while (guard++ < 2_000_000)
                {
                    long next = -1;
                    foreach (var nb in adj[cur])
                    {
                        var e = cur < nb ? (cur, nb) : (nb, cur);
                        if (!used.Contains(e)) { used.Add(e); next = nb; break; }
                    }
                    if (next == -1) break;
                    cur = next;
                    if (cur == startKey) break;
                    keys.Add(cur);
                }

                if (keys.Count >= 3)
                {
                    var pts = new List<Point>(keys.Count);
                    foreach (var k in keys)
                    {
                        long kx = k / 100000L;
                        long ky = k % 100000L;
                        pts.Add(new Point(kx / 2.0, ky / 2.0));
                    }
                    loops.Add(pts);
                }
            }
        }
        return loops;
    }

    private static List<Point> SimplifyLoop(List<Point> pts, double tol)
    {
        if (pts.Count < 4) return pts;
        int m = pts.Count + 1;
        var ring = new List<Point>(m);
        ring.AddRange(pts);
        ring.Add(pts[0]);

        var keep = new bool[m];
        keep[0] = true;
        keep[m - 1] = true;
        DouglasPeucker(ring, 0, m - 1, tol, keep);

        var res = new List<Point>();
        for (int i = 0; i < m - 1; i++)
            if (keep[i]) res.Add(ring[i]);
        return res;
    }

    private static void DouglasPeucker(List<Point> pts, int a, int b, double tol, bool[] keep)
    {
        if (b <= a + 1) return;
        double maxDist = -1;
        int idx = -1;
        for (int i = a + 1; i < b; i++)
        {
            double dist = PerpendicularDistance(pts[i], pts[a], pts[b]);
            if (dist > maxDist) { maxDist = dist; idx = i; }
        }
        if (maxDist > tol && idx > a)
        {
            keep[idx] = true;
            DouglasPeucker(pts, a, idx, tol, keep);
            DouglasPeucker(pts, idx, b, tol, keep);
        }
    }

    private static double PerpendicularDistance(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return (p - a).Length;
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
        t = Math.Max(0, Math.Min(1, t));
        double projX = a.X + t * dx, projY = a.Y + t * dy;
        double ex = p.X - projX, ey = p.Y - projY;
        return Math.Sqrt(ex * ex + ey * ey);
    }

    private static double PolygonArea(List<Point> p)
    {
        double a = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var q = p[(i + 1) % p.Count];
            a += p[i].X * q.Y - q.X * p[i].Y;
        }
        return Math.Abs(a) / 2.0;
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
