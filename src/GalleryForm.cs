using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace Gallery;

sealed class Options
{
    public Rectangle? Rect;
    public bool NoActivate, Topmost;
    public int Scroll;
    public int? Blur, Sat, Tint;
    public string Results = Path.Combine(AppContext.BaseDirectory, "results.txt");

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--noactivate": o.NoActivate = true; break;
                case "--topmost": o.Topmost = true; break;
                case "--scroll": o.Scroll = int.Parse(args[++i]); break;
                case "--blur": o.Blur = int.Parse(args[++i]); break;
                case "--sat": o.Sat = int.Parse(args[++i]); break;
                case "--tint": o.Tint = int.Parse(args[++i]); break;
                case "--results": o.Results = args[++i]; break;
                case "--rect":
                    var p = args[++i].Split(',').Select(int.Parse).ToArray();
                    o.Rect = new Rectangle(p[0], p[1], p[2], p[3]);
                    break;
            }
        }
        return o;
    }
}

sealed class Tile(TileDef def, SpriteVisual visual)
{
    public TileDef Def { get; } = def;
    public SpriteVisual Visual { get; } = visual;
    public string? Error;
    public RectangleF Rect;
}

sealed class GalleryForm : Form
{
    const int HeaderH = 30, Gap = 6, CaptionH = 30, HeadingH = 20;

    readonly Options _options;
    readonly VScrollBar _scroll = new() { Dock = DockStyle.Right };
    readonly Label _info = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    readonly List<Tile> _tiles = [];
    readonly List<(string Text, float Y)> _headings = [];
    readonly Font _font = new("Segoe UI", 8.25f), _bold = new("Segoe UI", 8.25f, FontStyle.Bold);

    Compositor? _c;
    DesktopWindowTarget? _target;
    ContainerVisual? _root, _content;
    SpriteVisual? _background, _dim, _overlay;
    Surfaces? _surfaces;
    Ctx? _ctx;
    CompositionDrawingSurface? _overlaySurface;
    float _scale = 1;

    public GalleryForm(Options options)
    {
        _options = options;
        Text = "Compositor effects gallery";
        BackColor = Color.FromArgb(32, 32, 32);
        Font = _font;
        MinimumSize = new Size(500, 300);
        if (options.Rect is { } r)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = r;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1480, 960);
        }

        Controls.Add(_scroll);
        var x = 6;
        AddSlider("Blur", 0, 100, 30, options.Blur, v => { _ctx!.Blur = v; return $"{v}"; }, ref x);
        AddSlider("Saturation", 0, 300, 150, options.Sat, v => { _ctx!.Sat = v / 100f; return $"{v / 100f:0.00}"; }, ref x);
        AddSlider("Tint opacity", 0, 100, 30, options.Tint, v => { _ctx!.Tint = v / 100f; return $"{v / 100f:0.00}"; }, ref x);
        _info.Location = new Point(x + 10, 8);
        Controls.Add(_info);
        _scroll.ValueChanged += (_, _) => ApplyScroll();
    }

    protected override bool ShowWithoutActivation => _options?.NoActivate == true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            if (_options?.Topmost == true) cp.ExStyle |= WS_EX_TOPMOST;
            return cp;
        }
    }

    // `later` is a value to move the slider to once the brushes exist, which exercises the live update path.
    void AddSlider(string name, int min, int max, int value, int? later, Func<int, string> apply, ref int x)
    {
        var label = new Label { Text = name, AutoSize = true, ForeColor = Color.Gainsboro, Location = new Point(x, 8) };
        Controls.Add(label);
        x += label.PreferredWidth + 2;
        var bar = new TrackBar { Minimum = min, Maximum = max, Value = value, TickStyle = TickStyle.None, AutoSize = false, Bounds = new Rectangle(x, 4, 150, 22) };
        Controls.Add(bar);
        x += 152;
        var shown = new Label { AutoSize = false, ForeColor = Color.White, Bounds = new Rectangle(x, 8, 34, 16) };
        Controls.Add(shown);
        x += 44;
        bar.ValueChanged += (_, _) =>
        {
            if (_ctx is null) return;
            shown.Text = apply(bar.Value);
            foreach (var (brush, property, read) in _ctx.Live) brush.Properties.InsertScalar(property, read(_ctx));
        };
        Load += (_, _) =>
        {
            shown.Text = apply(bar.Value);
            if (later is { } v) bar.Value = Math.Clamp(v, min, max);
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        _scale = DeviceDpi / 96f;
        var backdropHr = Native.EnableHostBackdrop(Handle);

        _c = new Compositor();
        _c.As<ICompositorDesktopInterop>().CreateDesktopWindowTarget(Handle, false, out var raw);
        _target = DesktopWindowTarget.FromAbi(raw);
        Marshal.Release(raw);

        _root = _c.CreateContainerVisual();
        _root.Clip = _c.CreateInsetClip();
        _target.Root = _root;

        _ctx = new Ctx(_c);
        _ctx.Sources["bg"] = _c.CreateHostBackdropBrush();

        _background = _c.CreateSpriteVisual();
        _background.Brush = _ctx.Sources["bg"];
        _dim = _c.CreateSpriteVisual();
        _dim.Brush = _c.CreateColorBrush(Windows.UI.Color.FromArgb(120, 0, 0, 0));
        _content = _c.CreateContainerVisual();
        _overlay = _c.CreateSpriteVisual();
        _root.Children.InsertAtTop(_background);
        _root.Children.InsertAtTop(_dim);
        _root.Children.InsertAtTop(_content);

        string? surfaceError = null;
        try
        {
            _surfaces = new Surfaces(_c);
            CreateSources();
        }
        catch (Exception ex)
        {
            surfaceError = $"0x{ex.HResult:X8} {ex.Message}";
        }

        foreach (var def in Catalog.Build())
        {
            var visual = _c.CreateSpriteVisual();
            visual.Size = (def.Big ? Catalog.Large : Catalog.Small) * _scale;
            _ctx.Size = visual.Size;
            var tile = new Tile(def, visual);
            try
            {
                visual.Brush = def.Make(_ctx);
                def.After?.Invoke(_ctx, visual);
            }
            catch (Exception ex)
            {
                tile.Error = $"0x{ex.HResult:X8} {ex.Message.Trim()}";
                visual.Children.RemoveAll();
                visual.Clip = null;
                visual.Brush = _c.CreateColorBrush(Windows.UI.Color.FromArgb(255, 96, 18, 18));
            }
            _tiles.Add(tile);
            _content.Children.InsertAtTop(visual);
        }
        _content.Children.InsertAtTop(_overlay);

        var refused = _tiles.Count(t => t.Error is not null);
        _info.Text = $"{_tiles.Count - refused} tiles accepted, {refused} refused.  DWMWA_USE_HOSTBACKDROPBRUSH hr=0x{backdropHr:X8}"
            + (surfaceError is null ? "" : $"  Surfaces failed: {surfaceError}");
        WriteResults(backdropHr, surfaceError);

        base.OnLoad(e);
        Arrange();
        _scroll.Value = Math.Clamp(_options.Scroll, 0, Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1));
    }

    void CreateSources()
    {
        var c = _c!;
        var s = _surfaces!;
        var small = Catalog.Small * _scale;
        var large = Catalog.Large * _scale;
        int w = (int)small.X, h = (int)small.Y;

        CompositionBrush Fill(CompositionDrawingSurface surface)
        {
            var brush = c.CreateSurfaceBrush(surface);
            brush.Stretch = CompositionStretch.Fill;
            return brush;
        }

        _ctx!.Sources["fg"] = Fill(s.Create(w, h, (x, y) => Patterns.Rainbow((x + y) / 140f)));
        _ctx.Sources["disc"] = Fill(s.Create(w, h, (x, y) =>
        {
            float dx = x - w / 2f, dy = y - h / 2f;
            var edge = Math.Clamp((h * 0.42f - MathF.Sqrt(dx * dx + dy * dy)) / 2, 0, 1);
            return Patterns.Rainbow(MathF.Atan2(dy, dx) / MathF.Tau, edge * 0.85f);
        }));
        _ctx.Sources["mask"] = Fill(s.Create(w, h, (x, y) =>
        {
            float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f);
            return (1, 1, 1, Math.Clamp(1.6f - 1.7f * MathF.Sqrt(dx * dx + dy * dy), 0, 1));
        }));
        _ctx.Sources["disp"] = Fill(s.Create(w, h, (x, y) => (0.5f + 0.5f * MathF.Sin(y * 0.2f), 0.5f + 0.5f * MathF.Sin(x * 0.2f), 0, 1)));
        _ctx.Sources["bump"] = Fill(s.Create(w, h, (x, y) => (1, 1, 1, Patterns.RoundedHeight(x, y, w, h, 26, 14))));
        _ctx.Sources["bumpBig"] = Fill(s.Create((int)large.X, (int)large.Y, (x, y) => (1, 1, 1, Patterns.RoundedHeight(x, y, large.X, large.Y, 40, 22))));
        _ctx.Sources["normal"] = Fill(s.Create(w, h, (x, y) =>
        {
            float H(int px, int py) => Patterns.RoundedHeight(px, py, w, h, 26, 14);
            var n = Vector3.Normalize(new Vector3((H(x - 1, y) - H(x + 1, y)) * 6, (H(x, y - 1) - H(x, y + 1)) * 6, 1));
            return (n.X * 0.5f + 0.5f, n.Y * 0.5f + 0.5f, n.Z * 0.5f + 0.5f, 1);
        }));

        _ctx.Sources["normalBig"] = Fill(s.Create((int)large.X, (int)large.Y, (x, y) =>
        {
            float H(int px, int py) => Patterns.RoundedHeight(px, py, large.X, large.Y, 40, 22);
            var n = Vector3.Normalize(new Vector3((H(x - 1, y) - H(x + 1, y)) * 8, (H(x, y - 1) - H(x, y + 1)) * 8, 1));
            return (n.X * 0.5f + 0.5f, n.Y * 0.5f + 0.5f, n.Z * 0.5f + 0.5f, 1);
        }));
        _ctx.Sources["rimBig"] = Fill(s.Create((int)large.X, (int)large.Y, (x, y) => (1, 1, 1, 1 - Patterns.RoundedHeight(x, y, large.X, large.Y, 40, 22))));
        var tiny = c.CreateSurfaceBrush(s.Create(32, 32, (x, y) => Patterns.Rainbow((x + y) / 64f, x < 2 || y < 2 ? 0.3f : 1)));
        tiny.Stretch = CompositionStretch.None;
        tiny.HorizontalAlignmentRatio = 0;
        tiny.VerticalAlignmentRatio = 0;
        _ctx.Sources["small"] = tiny;

        var random = new Random(1);
        var noise = c.CreateSurfaceBrush(s.Create(512, 512, (_, _) =>
        {
            var v = random.NextSingle();
            return (v, v, v, 0.09f);
        }));
        noise.Stretch = CompositionStretch.None;
        noise.HorizontalAlignmentRatio = 0;
        noise.VerticalAlignmentRatio = 0;
        _ctx.Sources["noise"] = noise;
    }

    static string OneLine(string? text) => string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    void WriteResults(int backdropHr, string? surfaceError)
    {
        var lines = new List<string>
        {
            $"Windows {Environment.OSVersion.Version}, .NET {Environment.Version}",
            $"DWMWA_USE_HOSTBACKDROPBRUSH hr=0x{backdropHr:X8}",
            $"Surfaces: {surfaceError ?? "ok"}",
            "",
        };
        lines.AddRange(_tiles.Select(t => $"{(t.Error is null ? "OK     " : "REFUSED")}\t{t.Def.Name}\t{t.Def.Params}\t{OneLine(t.Error)}"));
        try { File.WriteAllLines(_options.Results, lines); }
        catch (IOException) { }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_root is not null && WindowState != FormWindowState.Minimized) Arrange();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var max = Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1);
        _scroll.Value = Math.Clamp(_scroll.Value - e.Delta, 0, max);
    }

    void ApplyScroll()
    {
        if (_content is not null) _content.Offset = new Vector3(0, -_scroll.Value, 0);
    }

    void Arrange()
    {
        var width = ClientSize.Width - _scroll.Width;
        var height = ClientSize.Height - HeaderH;
        if (width <= 0 || height <= 0) return;
        _root!.Offset = new Vector3(0, HeaderH, 0);
        _root.Size = new Vector2(width, height);
        _background!.Size = _root.Size;
        _dim!.Size = _root.Size;

        _headings.Clear();
        float x = Gap, y = 2, rowHeight = 0;
        string? group = null;
        foreach (var tile in _tiles)
        {
            var size = tile.Visual.Size;
            if (tile.Def.Group != group)
            {
                if (group is not null) y += rowHeight + Gap;
                group = tile.Def.Group;
                _headings.Add((group, y));
                y += HeadingH;
                x = Gap;
                rowHeight = 0;
            }
            else if (x + size.X > width - Gap && x > Gap)
            {
                x = Gap;
                y += rowHeight + Gap;
                rowHeight = 0;
            }
            tile.Rect = new RectangleF(x, y, size.X, size.Y);
            tile.Visual.Offset = new Vector3(x, y, 0);
            x += size.X + Gap;
            rowHeight = Math.Max(rowHeight, size.Y);
        }
        var contentHeight = (int)(y + rowHeight + Gap);

        _scroll.Minimum = 0;
        _scroll.LargeChange = height;
        _scroll.SmallChange = 40;
        _scroll.Maximum = Math.Max(contentHeight, height) - 1;
        _scroll.Value = Math.Clamp(_scroll.Value, 0, Math.Max(0, _scroll.Maximum - _scroll.LargeChange + 1));
        ApplyScroll();

        if (_surfaces is not null) DrawOverlay(width, Math.Max(contentHeight, height));
    }

    // Captions are drawn with GDI+ into one transparent picture laid over the tiles.
    void DrawOverlay(int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        using (var plate = new SolidBrush(Color.FromArgb(175, 0, 0, 0)))
        using (var refusedPlate = new SolidBrush(Color.FromArgb(255, 96, 18, 18)))
        using (var border = new Pen(Color.FromArgb(200, 150, 150, 150)))
        using (var grey = new SolidBrush(Color.FromArgb(215, 215, 215)))
        using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.None;

            foreach (var (text, y) in _headings)
            {
                var size = g.MeasureString(text, _bold);
                g.FillRectangle(plate, Gap, y + 2, size.Width + 6, HeadingH - 3);
                g.DrawString(text, _bold, Brushes.White, Gap + 3, y + 3);
            }

            foreach (var tile in _tiles)
            {
                var r = tile.Rect;
                if (tile.Error is not null)
                {
                    g.FillRectangle(refusedPlate, r);
                    g.DrawString("REFUSED: " + tile.Def.Name, _bold, Brushes.White, new RectangleF(r.X + 3, r.Y + 2, r.Width - 6, 14), format);
                    g.DrawString(tile.Def.Params, _font, grey, new RectangleF(r.X + 3, r.Y + 16, r.Width - 6, 14), format);
                    g.DrawString(tile.Error, _font, Brushes.White, new RectangleF(r.X + 3, r.Y + 32, r.Width - 6, r.Height - 34));
                }
                else
                {
                    var caption = tile.Def.Big
                        ? new RectangleF(r.X + 50, r.Bottom - CaptionH - 10, r.Width - 100, CaptionH)
                        : new RectangleF(r.X, r.Bottom - CaptionH, r.Width, CaptionH);
                    g.FillRectangle(plate, caption);
                    g.DrawString(tile.Def.Name, _bold, Brushes.White, new RectangleF(caption.X + 3, caption.Y + 1, caption.Width - 6, 14), format);
                    g.DrawString(tile.Def.Params, _font, grey, new RectangleF(caption.X + 3, caption.Y + 14, caption.Width - 6, 14), format);
                }
                if (!tile.Def.Big || tile.Error is not null) g.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
        }

        var old = _overlaySurface;
        _overlaySurface = _surfaces!.Create(bitmap);
        var brush = _c!.CreateSurfaceBrush(_overlaySurface);
        brush.Stretch = CompositionStretch.None;
        brush.HorizontalAlignmentRatio = 0;
        brush.VerticalAlignmentRatio = 0;
        _overlay!.Size = new Vector2(width, height);
        _overlay.Brush = brush;
        old?.Dispose();
    }
}
