using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuelIntake.Core.Import;

/// <summary>A scanned page as grey levels, 0 = black … 255 = white, row by row.</summary>
public sealed class GrayImage(int width, int height, byte[] pixels)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public byte[] Pixels { get; } = pixels.Length == width * height ? pixels : throw new ArgumentException("Pixel count doesn't match the size.");

    public byte At(int x, int y) => Pixels[y * Width + x];

    /// <summary>Dark enough to be ink (pen or print), allowing for grey scanner backgrounds.</summary>
    public const byte InkLevel = 140;

    public bool IsInk(double x, double y)
    {
        var ix = (int)x; var iy = (int)y;
        return ix >= 0 && iy >= 0 && ix < Width && iy < Height && Pixels[iy * Width + ix] < InkLevel;
    }

    /// <summary>A rectangle of the image (clipped to it).</summary>
    public GrayImage Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width - 1); y = Math.Clamp(y, 0, Height - 1);
        w = Math.Clamp(w, 1, Width - x); h = Math.Clamp(h, 1, Height - y);
        var p = new byte[w * h];
        for (var r = 0; r < h; r++) Array.Copy(Pixels, (y + r) * Width + x, p, r * w, w);
        return new GrayImage(w, h, p);
    }

    /// <summary>
    /// Just the writing, with a white margin: the columns and rows that hold ink (ignoring specks), keeping the
    /// biggest run of writing when there's a stray mark well away from it. Null when there's no writing at all.
    /// A text reader works best on one tight line of writing.
    /// </summary>
    public GrayImage? TrimToInk(int margin)
    {
        int Count(int fixedIdx, bool row) => row
            ? Enumerable.Range(0, Width).Count(x => Pixels[fixedIdx * Width + x] < InkLevel)
            : Enumerable.Range(0, Height).Count(y => Pixels[y * Width + fixedIdx] < InkLevel);
        var cols = Enumerable.Range(0, Width).Select(x => Count(x, false) >= 2).ToArray();
        // runs of inky columns, joined across gaps narrower than the writing is tall (spaces between words)
        var runs = new List<(int Start, int End, int Ink)>();
        var gapLimit = Math.Max(8, Height / 2);
        for (var x = 0; x < Width; x++)
        {
            if (!cols[x]) continue;
            if (runs.Count > 0 && x - runs[^1].End <= gapLimit) runs[^1] = (runs[^1].Start, x, runs[^1].Ink + 1);
            else runs.Add((x, x, 1));
        }
        if (runs.Count == 0) return null;
        var best = runs.MaxBy(r => r.Ink);
        if (best.Ink < 3) return null;
        var rowsInk = Enumerable.Range(0, Height)
            .Where(y => Enumerable.Range(best.Start, best.End - best.Start + 1).Count(x => Pixels[y * Width + x] < InkLevel) >= 2).ToList();
        if (rowsInk.Count == 0) return null;
        // the margin is white paper, even where the crop ran out
        int w = best.End - best.Start + 1 + 2 * margin, h = rowsInk[^1] - rowsInk[0] + 1 + 2 * margin;
        var px = Enumerable.Repeat((byte)255, w * h).ToArray();
        for (var y = rowsInk[0]; y <= rowsInk[^1]; y++)
            Array.Copy(Pixels, y * Width + best.Start, px, (y - rowsInk[0] + margin) * w + margin, best.End - best.Start + 1);
        return new GrayImage(w, h, px);
    }

    /// <summary>Every <paramref name="f"/>×<paramref name="f"/> block averaged into one pixel.</summary>
    public GrayImage Shrink(int f)
    {
        if (f <= 1) return this;
        int w = Width / f, h = Height / f;
        var p = new byte[w * h];
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var sum = 0;
            for (var dy = 0; dy < f; dy++)
            {
                var row = (y * f + dy) * Width + x * f;
                for (var dx = 0; dx < f; dx++) sum += Pixels[row + dx];
            }
            p[y * w + x] = (byte)(sum / (f * f));
        }
        return new GrayImage(w, h, p);
    }
}

/// <summary>Maps points on the printed page (inches) to the scan (pixels), allowing for skew, scaling and perspective.</summary>
public sealed class Homography
{
    private readonly double[] h; // 3×3, h[8] = 1

    private Homography(double[] h) => this.h = h;

    public (double X, double Y) Map(double x, double y)
    {
        var w = h[6] * x + h[7] * y + h[8];
        return ((h[0] * x + h[1] * y + h[2]) / w, (h[3] * x + h[4] * y + h[5]) / w);
    }

    /// <summary>The mapping that takes each of the four <paramref name="from"/> points to the matching <paramref name="to"/> point.</summary>
    public static Homography FromPoints(IReadOnlyList<(double X, double Y)> from, IReadOnlyList<(double X, double Y)> to)
    {
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            var (x, y) = from[i]; var (u, v) = to[i];
            double[] r1 = [x, y, 1, 0, 0, 0, -u * x, -u * y, u];
            double[] r2 = [0, 0, 0, x, y, 1, -v * x, -v * y, v];
            for (var k = 0; k < 9; k++) { a[2 * i, k] = r1[k]; a[2 * i + 1, k] = r2[k]; }
        }
        // Gaussian elimination with partial pivoting
        for (var c = 0; c < 8; c++)
        {
            var pivot = c;
            for (var r = c + 1; r < 8; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[pivot, c])) pivot = r;
            if (Math.Abs(a[pivot, c]) < 1e-12) throw new InvalidOperationException("The four points don't make a page.");
            if (pivot != c) for (var k = 0; k < 9; k++) (a[c, k], a[pivot, k]) = (a[pivot, k], a[c, k]);
            for (var r = 0; r < 8; r++)
            {
                if (r == c) continue;
                var f = a[r, c] / a[c, c];
                if (f == 0) continue;
                for (var k = c; k < 9; k++) a[r, k] -= f * a[c, k];
            }
        }
        var s = new double[9];
        for (var i = 0; i < 8; i++) s[i] = a[i, 8] / a[i, i];
        s[8] = 1;
        return new Homography(s);
    }
}

/// <summary>
/// The marks the app prints on forms meant to be filled in by hand and scanned: a solid square near each corner
/// and, beside the bottom-left one, a strip of ten small squares saying which form and layout it is. With them the
/// app knows exactly where every answer box is on the scan, however the page was fed, scaled or skewed.
/// </summary>
public static class ScanMarks
{
    /// <summary>Distance of each corner square from the page edges, in inches (inside what printers can print).</summary>
    public const double Inset = 0.30;
    public const double Size = 0.15;
    public const double Center = Inset + Size / 2;

    public const int CodeCells = 10;
    public const double CellSize = 0.10;
    public const double CellPitch = 0.14;
    public const double CodeStart = 0.65; // left edge of the first cell, inches from the page's left edge

    /// <summary>Where the corner squares' centres are on a page (TL, TR, BR, BL), inches.</summary>
    public static (double X, double Y)[] Corners(double pageW, double pageH) =>
        [(Center, Center), (pageW - Center, Center), (pageW - Center, pageH - Center), (Center, pageH - Center)];

    /// <summary>The code cells' top-left corners, inches.</summary>
    public static IEnumerable<(double X, double Y)> CodeCellsAt(double pageH) =>
        Enumerable.Range(0, CodeCells).Select(i => (CodeStart + i * CellPitch, pageH - Center - CellSize / 2));

    /// <summary>
    /// Form number (1–15) and layout version (0–3) as ten cells: on, six data bits, two check bits, on.
    /// The check bits catch a misread or a page that is upside down.
    /// </summary>
    public static bool[] Encode(int form, int version)
    {
        if (form is < 1 or > 15 || version is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(form));
        var data = (form << 2) | version;
        var bits = new bool[CodeCells];
        bits[0] = true; bits[^1] = true;
        for (var i = 0; i < 6; i++) bits[1 + i] = (data >> (5 - i) & 1) == 1;
        bits[7] = bits[1..7].Count(b => b) % 2 == 1;
        bits[8] = new[] { bits[1], bits[3], bits[5] }.Count(b => b) % 2 == 0;
        return bits;
    }

    public static (int Form, int Version)? Decode(IReadOnlyList<bool> bits)
    {
        if (bits.Count != CodeCells || !bits[0] || !bits[^1]) return null;
        var data = 0;
        for (var i = 0; i < 6; i++) data = data << 1 | (bits[1 + i] ? 1 : 0);
        var form = data >> 2;
        if (form == 0) return null;
        var expected = Encode(form, data & 3);
        return expected.SequenceEqual(bits) ? (form, data & 3) : null;
    }

    /// <summary>
    /// The four corner squares on a scan (image corners TL, TR, BR, BL, in pixels), or null when they aren't all
    /// there (a form the app didn't print, or a corner cut off). Looks in each corner of the image for the solid
    /// square nearest that corner.
    /// </summary>
    public static (double X, double Y)[]? FindCorners(GrayImage image)
    {
        var f = Math.Max(1, (int)Math.Ceiling(Math.Max(image.Width, image.Height) / 1400.0));
        var small = image.Shrink(f);
        var ppi = Math.Max(small.Width, small.Height) / 11.0; // the page roughly fills the scan
        var expected = Size * ppi;
        int ww = (int)(small.Width * 0.22), wh = (int)(small.Height * 0.22);
        var windows = new (int X, int Y, int CornerX, int CornerY)[]
        {
            (0, 0, 0, 0), (small.Width - ww, 0, small.Width, 0),
            (small.Width - ww, small.Height - wh, small.Width, small.Height), (0, small.Height - wh, 0, small.Height),
        };
        var found = new (double X, double Y)[4];
        for (var c = 0; c < 4; c++)
        {
            var (wx, wy, cx, cy) = windows[c];
            var best = (Dist: double.MaxValue, X: 0.0, Y: 0.0);
            foreach (var blob in Blobs(small, wx, wy, ww, wh))
            {
                if (blob.W < expected * 0.5 || blob.H < expected * 0.5 || blob.W > expected * 2.2 || blob.H > expected * 2.2) continue;
                var aspect = (double)blob.W / blob.H;
                if (aspect is < 0.6 or > 1.65) continue;
                if (blob.Count < blob.W * blob.H * 0.72) continue; // solid, not a letter or a box outline
                var mx = blob.X + blob.W / 2.0; var my = blob.Y + blob.H / 2.0;
                var d = Math.Sqrt((mx - cx) * (mx - cx) + (my - cy) * (my - cy));
                if (d < best.Dist) best = (d, mx, my);
            }
            if (best.Dist == double.MaxValue) return null;
            found[c] = (best.X * f + f / 2.0, best.Y * f + f / 2.0);
        }
        return found;
    }

    private static IEnumerable<(int X, int Y, int W, int H, int Count)> Blobs(GrayImage img, int x0, int y0, int w, int h)
    {
        var seen = new bool[w * h];
        var stack = new Stack<(int, int)>();
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            if (seen[y * w + x] || img.At(x0 + x, y0 + y) >= 128) continue;
            int minX = x, maxX = x, minY = y, maxY = y, count = 0;
            stack.Push((x, y)); seen[y * w + x] = true;
            while (stack.Count > 0)
            {
                var (px, py) = stack.Pop();
                count++;
                if (px < minX) minX = px; if (px > maxX) maxX = px; if (py < minY) minY = py; if (py > maxY) maxY = py;
                foreach (var (nx, ny) in new[] { (px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[ny * w + nx] || img.At(x0 + nx, y0 + ny) >= 128) continue;
                    seen[ny * w + nx] = true;
                    stack.Push((nx, ny));
                }
            }
            yield return (x0 + minX, y0 + minY, maxX - minX + 1, maxY - minY + 1, count);
        }
    }

    /// <summary>Share of a page rectangle (inches) that is ink on the scan.</summary>
    public static double Ink(GrayImage image, Homography pageToImage, double x, double y, double w, double h)
    {
        if (w <= 0 || h <= 0) return 0;
        // sample about every scan pixel
        var (ax, ay) = pageToImage.Map(x, y); var (bx, by) = pageToImage.Map(x + w, y + h);
        var ppi = Math.Max(Math.Abs(bx - ax) / w, Math.Abs(by - ay) / h);
        int nx = Math.Clamp((int)(w * ppi), 2, 2000), ny = Math.Clamp((int)(h * ppi), 2, 2000);
        var ink = 0;
        for (var j = 0; j < ny; j++)
        for (var i = 0; i < nx; i++)
        {
            var (px, py) = pageToImage.Map(x + w * (i + 0.5) / nx, y + h * (j + 0.5) / ny);
            if (image.IsInk(px, py)) ink++;
        }
        return (double)ink / (nx * ny);
    }
}

/// <summary>How a zone is read: writing (text recognition) or a tick (how much ink is on it).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ZoneKind>))]
public enum ZoneKind { Text, Tick, Cell }

/// <summary>
/// One answer box on a scannable form, in inches from the page's top-left corner. Text: an underlined blank;
/// Cell: a table cell; Tick: a "___ Option" blank that's ticked with an X (<see cref="Value"/> is what it means).
/// </summary>
public sealed record ScanZone(string Path, ZoneKind Kind, double X, double Y, double W, double H, string? Value = null);

/// <summary>A form the app prints for clients to fill in by hand: its code, page size and answer boxes.</summary>
public sealed record ScanFormDef(string Id, int Code, int Version, double PageWidth, double PageHeight, List<ScanZone> Zones);

/// <summary>The scannable forms (generated from the printed layout by <c>reference/forms/make_scan_layout.js</c>).</summary>
public static class ScanForms
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<ScanFormDef> All { get; } = Load();

    /// <summary>Form numbers in the corner code. Never reuse one; bump the layout version when a form's boxes move.</summary>
    public static readonly IReadOnlyDictionary<string, int> Codes = new Dictionary<string, int>
    {
        ["IntakePage1"] = 1, ["IntakePage2"] = 2, ["IncomeExpense"] = 3,
    };

    public const int Version = 1;

    public static ScanFormDef? Find(int code, int version) => All.FirstOrDefault(f => f.Code == code && f.Version == version);

    public static ScanFormDef? Find(string id) => All.FirstOrDefault(f => f.Id == id);

    private static List<ScanFormDef> Load()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("scan-forms.json");
        return s is null ? [] : JsonSerializer.Deserialize<List<ScanFormDef>>(s, Json) ?? [];
    }
}

/// <summary>A scan page matched to one of the app's forms.</summary>
public sealed record LocatedPage(ScanFormDef Form, Homography PageToImage, bool UpsideDown);

/// <summary>One answer read off a scannable form.</summary>
/// <param name="Text">What the text reader saw (Text/Cell zones); null for ticks.</param>
/// <param name="Ink">Share of the box that's ink.</param>
public sealed record ZoneValue(ScanZone Zone, string? Text, double Ink, ScanSpot Spot);

/// <summary>What was read off a page the app printed.</summary>
public sealed record FormReading(string FormId, int Version, bool UpsideDown, IReadOnlyList<ZoneValue> Values);

public static class ScanFormReader
{
    /// <summary>Below this share of ink, a blank or cell is taken as empty (stray dots, scanner dust).</summary>
    public const double WritingInk = 0.006;

    /// <summary>
    /// A tick blank has been marked when it has at least this much more ink above its line than the page's blanks
    /// usually do (most are empty, so their median is what an empty one looks like on this scan).
    /// </summary>
    public const double TickInk = 0.03;

    /// <summary>The ink an unmarked tick blank has on this page: the median over all of them.</summary>
    public static double TickBaseline(IEnumerable<ZoneValue> values)
    {
        var inks = values.Where(v => v.Zone.Kind == ZoneKind.Tick).Select(v => v.Ink).OrderBy(i => i).ToList();
        return inks.Count == 0 ? 0 : Math.Min(inks[inks.Count / 2], 0.05);
    }

    public static bool IsMarked(ZoneValue v, double baseline) => v.Ink >= baseline + TickInk;

    /// <summary>Finds the corner squares and the form code, trying the page each way round.</summary>
    public static LocatedPage? Locate(GrayImage image)
    {
        if (ScanMarks.FindCorners(image) is not { } corners) return null;
        var imageWide = image.Width > image.Height;
        for (var turn = 0; turn < 4; turn++)
        {
            // page corners TL, TR, BR, BL sit at these image corners when the page is turned `turn` quarter turns
            var at = Enumerable.Range(0, 4).Select(i => corners[(i + turn) % 4]).ToArray();
            var wide = turn % 2 == 0 ? imageWide : !imageWide;
            var (pw, ph) = wide ? (11.0, 8.5) : (8.5, 11.0);
            Homography h;
            try { h = Homography.FromPoints(ScanMarks.Corners(pw, ph), at); }
            catch (InvalidOperationException) { continue; }
            var bits = ScanMarks.CodeCellsAt(ph)
                .Select(c => ScanMarks.Ink(image, h, c.X + ScanMarks.CellSize * 0.25, c.Y + ScanMarks.CellSize * 0.25,
                    ScanMarks.CellSize * 0.5, ScanMarks.CellSize * 0.5) > 0.5)
                .ToList();
            if (ScanMarks.Decode(bits) is not { } code) continue;
            if (ScanForms.Find(code.Form, code.Version) is not { } form) continue;
            if (Math.Abs(form.PageWidth - pw) > 0.1) continue;
            return new LocatedPage(form, h, turn == 2);
        }
        return null;
    }

    /// <summary>
    /// Reads every box. Ticks by ink; writing with <paramref name="readText"/> (given just that box, straightened
    /// enough for reading) and only when there's ink in it, so empty table cells cost nothing.
    /// </summary>
    public static FormReading Read(GrayImage image, LocatedPage page, Func<GrayImage, string> readText, string file, int pageNumber)
    {
        var values = new List<ZoneValue>();
        foreach (var z in page.Form.Zones)
        {
            var (ix, iy, iw, ih) = InkArea(z);
            var ink = ScanMarks.Ink(image, page.PageToImage, ix, iy, iw, ih);
            var spot = Spot(image, page.PageToImage, z, file, pageNumber);
            if (z.Kind == ZoneKind.Tick) { values.Add(new ZoneValue(z, null, ink, spot)); continue; }
            if (ink < WritingInk) { values.Add(new ZoneValue(z, "", ink, spot)); continue; }
            var (cx, cy, cw, ch) = ReadArea(z);
            var crop = WithoutRules(Straighten(image, page.PageToImage, cx, cy, cw, ch, ScanPpi(page.PageToImage))).TrimToInk(6);
            values.Add(new ZoneValue(z, crop is null ? "" : readText(crop).Trim(), ink, spot));
        }
        return new FormReading(page.Form.Id, page.Form.Version, page.UpsideDown, values);
    }

    /// <summary>
    /// Where ink counts: above an underlined blank's line (writing rises above it) but not the line itself;
    /// inside a table cell but not its borders.
    /// </summary>
    public static (double X, double Y, double W, double H) InkArea(ScanZone z) => z.Kind switch
    {
        ZoneKind.Cell => (z.X + 0.03, z.Y + 0.03, z.W - 0.06, z.H - 0.06),
        // just above the blank's line: an X crosses it, but the border of a box around the choices sits higher
        ZoneKind.Tick => (z.X + z.W * 0.1, z.Y + z.H - 0.12, z.W * 0.8, 0.09),
        _ => (z.X + 0.02, z.Y - z.H * 0.6, z.W - 0.04, z.H * 1.42),
    };

    /// <summary>
    /// What's handed to the text reader: the cell, or the writing height just above a blank's line (not the line
    /// above it), with a little room either side.
    /// </summary>
    private static (double X, double Y, double W, double H) ReadArea(ScanZone z) => z.Kind switch
    {
        // a little past a cell's right border: writing runs over (the border itself is whited out)
        ZoneKind.Cell => (z.X, z.Y, z.W + 0.03, z.H),
        // not left of the blank: the label's colon sits right there
        _ => (z.X + 0.01, z.Y + z.H - 0.26, z.W, 0.30),
    };

    /// <summary>The scan's resolution, from how far apart the corner squares are.</summary>
    private static double ScanPpi(Homography h)
    {
        var (ax, ay) = h.Map(0, 0); var (bx, by) = h.Map(1, 0);
        return Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
    }

    /// <summary>
    /// A page rectangle (inches) cut out of the scan square to the page, so a tilted or shrunk scan reads like a
    /// straight one and printed lines come out level. Between 150 and 300 dots per inch.
    /// </summary>
    public static GrayImage Straighten(GrayImage image, Homography h, double x, double y, double w, double ht, double ppi)
    {
        var dpi = Math.Clamp(ppi, 150, 300);
        int W = Math.Max(1, (int)(w * dpi)), H = Math.Max(1, (int)(ht * dpi));
        var px = new byte[W * H];
        for (var j = 0; j < H; j++)
        for (var i = 0; i < W; i++)
        {
            var (sx, sy) = h.Map(x + (i + 0.5) / dpi, y + (j + 0.5) / dpi);
            px[j * W + i] = Sample(image, sx, sy);
        }
        return new GrayImage(W, H, px);
    }

    private static byte Sample(GrayImage g, double x, double y)
    {
        x -= 0.5; y -= 0.5;
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        if (x0 < 0 || y0 < 0 || x0 + 1 >= g.Width || y0 + 1 >= g.Height) return 255;
        double fx = x - x0, fy = y - y0;
        var v = g.At(x0, y0) * (1 - fx) * (1 - fy) + g.At(x0 + 1, y0) * fx * (1 - fy) + g.At(x0, y0 + 1) * (1 - fx) * fy + g.At(x0 + 1, y0 + 1) * fx * fy;
        return (byte)Math.Round(v);
    }

    /// <summary>
    /// Whites out printed rules: rows and columns that are mostly ink (a blank's underline, a cell's borders).
    /// Writing never fills most of a row, so it stays; the reader then sees only the answer.
    /// </summary>
    public static GrayImage WithoutRules(GrayImage g)
    {
        var px = (byte[])g.Pixels.Clone();
        bool Dark(int x, int y) => g.Pixels[y * g.Width + x] < GrayImage.InkLevel;
        // in the last few pixels a shorter run is enough: a cell border the straightening left a pixel off
        // (only there: further in, that would take the side of a letter written near the edge)
        bool Edge(int i, int n) => i < 5 || i >= n - 5;
        var rows = Enumerable.Range(0, g.Height)
            .Where(y => Enumerable.Range(0, g.Width).Count(x => Dark(x, y)) > g.Width * (Edge(y, g.Height) ? 0.3 : 0.5)).ToList();
        var cols = Enumerable.Range(0, g.Width)
            .Where(x => Enumerable.Range(0, g.Height).Count(y => Dark(x, y)) > g.Height * (Edge(x, g.Width) ? 0.3 : 0.6)).ToList();
        // the rule's soft edges too
        foreach (var y in rows.SelectMany(r => new[] { r - 1, r, r + 1 }).Where(r => r >= 0 && r < g.Height).Distinct())
            for (var x = 0; x < g.Width; x++) px[y * g.Width + x] = 255;
        foreach (var x in cols.SelectMany(c => new[] { c - 1, c, c + 1 }).Where(c => c >= 0 && c < g.Width).Distinct())
            for (var y = 0; y < g.Height; y++) px[y * g.Width + x] = 255;
        return new GrayImage(g.Width, g.Height, px);
    }

    private static ScanSpot Spot(GrayImage image, Homography h, ScanZone z, string file, int page)
    {
        var (x, y, w, ht) = z.Kind == ZoneKind.Cell ? (z.X, z.Y, z.W, z.H) : ReadArea(z);
        var pts = new[] { h.Map(x, y), h.Map(x + w, y), h.Map(x + w, y + ht), h.Map(x, y + ht) };
        double x0 = pts.Min(p => p.X), y0 = pts.Min(p => p.Y), x1 = pts.Max(p => p.X), y1 = pts.Max(p => p.Y);
        return new ScanSpot(file, page, x0 / image.Width, y0 / image.Height, (x1 - x0) / image.Width, (y1 - y0) / image.Height);
    }

    internal static string Invariant(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
}
