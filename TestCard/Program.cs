using System.Diagnostics;
using System.Drawing.Imaging;

namespace TestCard;

// Checks the gallery without looking at the desktop: shows an opaque striped card, puts the gallery on top of it,
// and saves a picture of the card's rectangle only.
// Usage: TestCard <gallery.exe> <output folder> <width> <height> <scroll> [<scroll> ...] [--activate] [--extra "<gallery arguments>"] [--tag <name>] [--bars]
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var gallery = args[0];
        var output = args[1];
        var size = new Size(int.Parse(args[2]), int.Parse(args[3]));
        var activate = args.Contains("--activate");
        var extra = Value(args, "--extra") ?? "";
        var tag = Value(args, "--tag") ?? "";
        var scrolls = args.Skip(4).TakeWhile(a => !a.StartsWith("--")).Select(int.Parse).ToArray();
        Directory.CreateDirectory(output);

        var card = new CardForm { Bounds = new Rectangle(Point.Empty, size), Bars = args.Contains("--bars") };
        card.Shown += async (_, _) =>
        {
            foreach (var scroll in scrolls)
            {
                var arguments = $"--rect 0,0,{size.Width},{size.Height} --topmost --scroll {scroll} --results \"{Path.Combine(output, "results.txt")}\""
                    + (activate ? "" : " --noactivate") + " " + extra;
                using var process = Process.Start(new ProcessStartInfo(gallery, arguments) { UseShellExecute = false })!;
                await Task.Delay(4000);
                using (var shot = new Bitmap(size.Width, size.Height))
                {
                    using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(card.Location, Point.Empty, size);
                    shot.Save(Path.Combine(output, $"scroll{scroll}{tag}.png"), ImageFormat.Png);
                    SaveQuarters(shot, Path.Combine(output, $"scroll{scroll}{tag}"));
                }
                if (!process.HasExited) process.Kill();
                await Task.Delay(300);
            }
            card.Close();
        };
        Application.Run(card);
    }

    static string? Value(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void SaveQuarters(Bitmap shot, string prefix)
    {
        int w = shot.Width / 2, h = shot.Height / 2;
        for (var i = 0; i < 4; i++)
        {
            using var part = shot.Clone(new Rectangle(i % 2 * w, i / 2 * h, w, h), shot.PixelFormat);
            part.Save($"{prefix}-{i}.png", ImageFormat.Png);
        }
    }
}

sealed class CardForm : Form
{
    public CardForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
    }

    // Plain vertical bars instead of blocks: a rotated or skewed backdrop shows as slanted bars.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Bars { get; init; }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color[] colours = [Color.White, Color.Gold, Color.DeepSkyBlue, Color.LimeGreen, Color.Magenta, Color.Red, Color.RoyalBlue, Color.Black];
        // Big blocks survive the backdrop's own blur, so zoom and rotation can be judged.
        const int block = 150;
        if (Bars)
        {
            Color[] four = [Color.Red, Color.LimeGreen, Color.RoyalBlue, Color.White];
            for (var x = 0; x < Width; x += 60)
            {
                using var brush = new SolidBrush(four[x / 60 % 4]);
                g.FillRectangle(brush, x, 0, 60, Height);
            }
            return;
        }
        for (var y = 0; y < Height; y += block)
        for (var x = 0; x < Width; x += block)
        {
            using var brush = new SolidBrush(colours[(x / block * 3 + y / block * 5) % colours.Length]);
            g.FillRectangle(brush, x, y, block, block);
        }
        // Thin rules and text show whether any sharp detail gets through.
        using var rule = new Pen(Color.Black, 2);
        for (var y = 30; y < Height; y += 60) g.DrawLine(rule, 0, y, Width, y);
        using var white = new Pen(Color.White, 2);
        for (var y = 60; y < Height; y += 60) g.DrawLine(white, 0, y, Width, y);
        using var font = new Font("Segoe UI", 9, FontStyle.Bold);
        for (var y = 6; y < Height; y += 60)
        for (var x = 4; x < Width; x += 132)
            g.DrawString($"TEST {x / 132},{y / 60}", font, Brushes.Black, x, y);
    }
}
