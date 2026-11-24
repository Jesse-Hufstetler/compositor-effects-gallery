using System.Numerics;
using Windows.Graphics.Effects;
using Windows.UI.Composition;
using Windows.UI.Composition.Effects;

namespace Gallery;

// Headless: asks the compositor whether it accepts each graph shape and writes the answers to a file. No window.
static class Probe
{
    public static void Run(string path)
    {
        var c = new Compositor();
        var ctx = new Ctx(c);
        var lines = new List<string>();
        var bg = Fx.Src("bg");
        var surface = Fx.Src("surface");
        var size = new Vector2(176, 120);

        void Factory(string what, Func<IGraphicsEffect> effect)
        {
            try
            {
                var factory = c.CreateEffectFactory(effect());
                lines.Add($"OK       {what}  (LoadStatus {factory.LoadStatus})");
            }
            catch (Exception ex)
            {
                lines.Add($"REFUSED  {what}  0x{ex.HResult:X8} {ex.Message.Replace("\r", "").Replace("\n", " ")}");
            }
        }

        void Try(string what, Action action)
        {
            try
            {
                action();
                lines.Add($"OK       {what}");
            }
            catch (Exception ex)
            {
                lines.Add($"REFUSED  {what}  0x{ex.HResult:X8} {ex.Message.Replace("\r", "").Replace("\n", " ")}");
            }
        }

        Factory("DistantDiffuse(source parameter) alone", () => Fx.DistantDiffuse(surface, 225, 40, 10));
        Factory("DistantDiffuse(Opacity(source parameter))", () => Fx.DistantDiffuse(Fx.Opacity(surface, 1), 225, 40, 10));
        Factory("DistantDiffuse(LuminanceToAlpha(source parameter))", () => Fx.DistantDiffuse(Fx.LuminanceToAlpha(surface), 225, 40, 10));
        Factory("DistantDiffuse(GaussianBlur(source parameter))", () => Fx.DistantDiffuse(Fx.Blur(surface, 2), 225, 40, 10));
        Factory("PointSpecular(source parameter) alone", () => Fx.PointSpecular(surface, new(40, 20, 60), 16, 10));
        Factory("SpotSpecular(source parameter) alone", () => Fx.SpotSpecular(surface, new(10, 10, 90), new(88, 60, 0), 16, 10));
        Factory("Composite Plus(bg, PointSpecular(source parameter))", () => Fx.Composite(9, bg, Fx.PointSpecular(surface, new(40, 20, 60), 16, 10)));
        Factory("Blend Screen(bg, PointSpecular(source parameter))", () => Fx.Blend(1, bg, Fx.PointSpecular(surface, new(40, 20, 60), 16, 10)));
        Factory("Arithmetic(bg, PointSpecular(source parameter))", () => Fx.Arithmetic(bg, Fx.PointSpecular(surface, new(40, 20, 60), 16, 10), 0, 1, 1, 0));
        Factory("Arithmetic(bg, SceneLightingEffect)", () => Fx.Arithmetic(bg, new SceneLightingEffect { NormalMapSource = Fx.Src("normal") }, 1, 0, 0, 0));
        Factory("SceneLightingEffect alone", () => new SceneLightingEffect());

        Factory("DistantDiffuse scale=1 (height map defaults)", () => Fx.DistantDiffuse(surface, 225, 40, 1));
        Factory("PointSpecular scale=1", () => Fx.PointSpecular(surface, new(40, 20, 60), 16, 1));
        Factory("SpotDiffuse scale=1", () => Fx.SpotDiffuse(surface, new(10, 10, 90), new(88, 60, 0), 1));
        Factory("Arithmetic(bg, PointSpecular scale=1)", () => Fx.Arithmetic(bg, Fx.PointSpecular(surface, new(40, 20, 60), 16, 1), 0, 1, 1, 0));
        Factory("PointSpecular scale=1 on GaussianBlur(source parameter)", () => Fx.PointSpecular(Fx.Blur(surface, 2), new(40, 20, 60), 16, 1));
        Factory("Saturation with a wrong property type (control)", () => new D2DEffect(new Guid("5cb2d9cf-327d-459f-a0ce-40c0b2086bf7"), [surface], [7u]));
        Factory("Transform2D(GaussianBlur 0) ", () => Fx.Transform2D(Fx.Blur(surface, 0), Matrix3x2.CreateScale(2, size / 2)));
        Factory("Transform2D(Transform2D(source parameter))", () => Fx.Transform2D(Fx.Transform2D(surface, Matrix3x2.CreateScale(2)), Matrix3x2.CreateScale(2)));
        Factory("Composite(Transform2D(GaussianBlur), Transform2D(GaussianBlur))", () => Fx.Composite(0, Fx.Transform2D(Fx.Blur(surface, 1), Matrix3x2.CreateScale(2)), Fx.Transform2D(Fx.Blur(surface, 1), Matrix3x2.CreateScale(3))));
        Factory("AlphaMask(Transform2D(GaussianBlur(bg)), mask)", () => Fx.AlphaMask(Fx.Transform2D(Fx.Blur(bg, 1), Matrix3x2.CreateScale(2)), surface));
        Factory("GaussianBlur(Transform2D(GaussianBlur))", () => Fx.Blur(Fx.Transform2D(Fx.Blur(surface, 1), Matrix3x2.CreateScale(2)), 3));
        Factory("PointSpecular with a null source", () => Fx.PointSpecular(null!, new(40, 20, 60), 16, 1));
        Factory("DistantDiffuse with a null source", () => Fx.DistantDiffuse(null!, 225, 40, 1));
        Factory("SpotSpecular with a null source", () => Fx.SpotSpecular(null!, new(10, 10, 90), new(88, 60, 0), 16, 1));
        Factory("Arithmetic(bg, PointSpecular null source)", () => Fx.Arithmetic(bg, Fx.PointSpecular(null!, new(40, 20, 60), 16, 1), 0, 1, 1, 0));
        Factory("PointSpecular with no sources at all", () => new D2DEffect(new Guid("09c3ca26-3ae2-4f09-9ebc-ed3865d53f22"), [], [new float[] { 40, 20, 60 }, 16f, 1f, 1f, new float[] { 1, 1, 1 }, new float[] { 1, 1 }, 1u]));
        Factory("Transform2D(source parameter)", () => Fx.Transform2D(surface, Matrix3x2.CreateScale(2, size / 2)));
        Factory("Transform2D(GaussianBlur(source parameter))", () => Fx.Transform2D(Fx.Blur(surface, 4), Matrix3x2.CreateScale(2, size / 2)));
        Factory("Border Wrap(source parameter)", () => Fx.Border(surface, 1, 1));
        Factory("GaussianBlur(Border Mirror(source parameter))", () => Fx.Blur(Fx.Border(surface, 2, 2), 8));
        Factory("Transform2D(Border Mirror(source parameter))", () => Fx.Transform2D(Fx.Border(surface, 2, 2), Matrix3x2.CreateScale(0.5f)));

        // The refusals above that mention the backdrop only show up once a backdrop brush is bound.
        var backdrop = c.CreateHostBackdropBrush();
        var inWindowBackdrop = c.CreateBackdropBrush();
        CompositionEffectBrush Make(IGraphicsEffect effect) => c.CreateEffectFactory(effect).CreateBrush();

        Try("bind host backdrop to Transform2D(source parameter)", () => Make(Fx.Transform2D(surface, Matrix3x2.CreateScale(2))).SetSourceParameter("surface", backdrop));
        Try("bind host backdrop to Transform2D(GaussianBlur(source parameter))", () => Make(Fx.Transform2D(Fx.Blur(surface, 4), Matrix3x2.CreateScale(2))).SetSourceParameter("surface", backdrop));
        Try("bind host backdrop to Transform2D(Saturation(source parameter))", () => Make(Fx.Transform2D(Fx.Saturation(surface, 1), Matrix3x2.CreateScale(2))).SetSourceParameter("surface", backdrop));
        Try("bind host backdrop to GaussianBlur(Transform2D(...)) inner", () => Make(Fx.Blur(Fx.Transform2D(surface, Matrix3x2.CreateScale(2)), 4)).SetSourceParameter("surface", backdrop));
        Try("bind in-window backdrop (CreateBackdropBrush) to Transform2D", () => Make(Fx.Transform2D(surface, Matrix3x2.CreateScale(2))).SetSourceParameter("surface", inWindowBackdrop));
        Try("bind host backdrop to Border Mirror", () => Make(Fx.Border(surface, 2, 2)).SetSourceParameter("surface", backdrop));
        Try("bind host backdrop to DistantDiffuse", () => Make(Fx.DistantDiffuse(surface, 225, 40, 10)).SetSourceParameter("surface", backdrop));
        Try("bind an effect brush (blurred backdrop) to Transform2D", () =>
        {
            var blurred = Make(Fx.Blur(surface, 4));
            blurred.SetSourceParameter("surface", backdrop);
            Make(Fx.Transform2D(Fx.Src("x"), Matrix3x2.CreateScale(2))).SetSourceParameter("x", blurred);
        });
        Try("CompositionNineGridBrush.Source = host backdrop", () => c.CreateNineGridBrush().Source = backdrop);
        Try("CompositionMaskBrush.Source = host backdrop", () => c.CreateMaskBrush().Source = backdrop);
        Try("CompositionMaskBrush.Mask = host backdrop", () => c.CreateMaskBrush().Mask = backdrop);
        Try("CompositionVisualSurface of a host-backdrop sprite (creation only)", () =>
        {
            var sprite = c.CreateSpriteVisual();
            sprite.Size = size;
            sprite.Brush = backdrop;
            var visualSurface = c.CreateVisualSurface();
            visualSurface.SourceVisual = sprite;
            visualSurface.SourceSize = size;
            c.CreateSurfaceBrush(visualSurface);
        });

        File.WriteAllLines(path, lines);
    }
}
