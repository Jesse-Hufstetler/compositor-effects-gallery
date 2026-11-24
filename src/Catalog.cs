using System.Numerics;
using Windows.Graphics.Effects;
using Windows.UI.Composition;
using Windows.UI.Composition.Effects;

namespace Gallery;

sealed record TileDef(string Group, string Name, string Params, Func<Ctx, CompositionBrush> Make,
    bool Big = false, Action<Ctx, SpriteVisual>? After = null);

// What a tile needs to build its brush: the compositor, the named input brushes and the slider values.
sealed class Ctx(Compositor compositor)
{
    readonly List<string> _normalMaps = [];

    public Compositor C { get; } = compositor;
    public Dictionary<string, CompositionBrush> Sources { get; } = new();
    public List<(CompositionEffectBrush Brush, string Property, Func<Ctx, float> Value)> Live { get; } = new();
    public Vector2 Size;
    public float Blur = 30, Sat = 1.5f, Tint = 0.3f;

    public CompositionEffectBrush Brush(IGraphicsEffect effect, params string[] animatable)
    {
        var names = SourceNames(effect).Concat(_normalMaps).Distinct().ToList();
        _normalMaps.Clear();
        var factory = animatable.Length > 0 ? C.CreateEffectFactory(effect, animatable) : C.CreateEffectFactory(effect);
        if (factory.LoadStatus is CompositionEffectFactoryLoadStatus.EffectTooComplex or CompositionEffectFactoryLoadStatus.Other)
            throw new InvalidOperationException($"LoadStatus {factory.LoadStatus}: {factory.ExtendedError?.Message}");
        var brush = factory.CreateBrush();
        foreach (var name in names) brush.SetSourceParameter(name, Sources[name]);
        return brush;
    }

    public void Track(CompositionEffectBrush brush, string property, Func<Ctx, float> value) => Live.Add((brush, property, value));

    // Composition's own lighting effect. Its normal map parameter is noted here for the next Brush call.
    public SceneLightingEffect Scene(string normalMap, float ambient, float diffuse, float specular, float shine)
    {
        _normalMaps.Add(normalMap);
        return new SceneLightingEffect
        {
            AmbientAmount = ambient, DiffuseAmount = diffuse, SpecularAmount = specular, SpecularShine = shine,
            NormalMapSource = Fx.Src(normalMap),
        };
    }

    static IEnumerable<string> SourceNames(IGraphicsEffectSource? source) => source switch
    {
        CompositionEffectSourceParameter p => [p.Name],
        D2DEffect e => e.Sources.SelectMany(SourceNames),
        _ => [],
    };
}

static class Catalog
{
    public static readonly Vector2 Small = new(176, 120), Large = new(424, 240);

    const float Degrees = MathF.PI / 180;

    static IGraphicsEffectSource Bg => Fx.Src("bg");

    static readonly string[] BlendModes =
    [
        "Multiply", "Screen", "Darken", "Lighten", "Dissolve", "ColorBurn", "LinearBurn", "DarkerColor", "LighterColor",
        "ColorDodge", "LinearDodge", "Overlay", "SoftLight", "HardLight", "VividLight", "LinearLight", "PinLight", "HardMix",
        "Difference", "Exclusion", "Hue", "Saturation", "Color", "Luminosity", "Subtract", "Division",
    ];

    static readonly string[] CompositeModes =
    [
        "SourceOver", "DestinationOver", "SourceIn", "DestinationIn", "SourceOut", "DestinationOut", "SourceAtop",
        "DestinationAtop", "Xor", "Plus", "SourceCopy", "BoundedSourceCopy", "MaskInvert",
    ];

    public static List<TileDef> Build()
    {
        var list = new List<TileDef>();
        var group = "";
        void Add(string name, string pars, Func<Ctx, IGraphicsEffect> effect) => list.Add(new(group, name, pars, c => c.Brush(effect(c))));

        group = "Combined looks (the three sliders drive these)";
        list.Add(new(group, "Frosted", "blur > saturation > tint > noise", c =>
        {
            var blur = Fx.Blur(Bg, c.Blur).Named("Blur", ("BlurAmount", 0));
            var sat = Fx.Saturation(blur, c.Sat).Named("Sat", ("Saturation", 0));
            var tint = Fx.Opacity(Fx.Flood(0.93f, 0.95f, 1f, 1f), c.Tint).Named("Tint", ("Opacity", 0));
            var brush = c.Brush(Fx.Composite(0, sat, tint, Fx.Src("noise")), "Blur.BlurAmount", "Sat.Saturation", "Tint.Opacity");
            c.Track(brush, "Blur.BlurAmount", x => x.Blur);
            c.Track(brush, "Sat.Saturation", x => x.Sat);
            c.Track(brush, "Tint.Opacity", x => x.Tint);
            return brush;
        }, Big: true, After: (c, v) => Round(c, v, 18)));

        list.Add(new(group, "Clear heavy-blur glass", "blur at 2x the slider, no tint", c =>
        {
            var brush = c.Brush(Fx.Blur(Bg, c.Blur * 2).Named("Blur", ("BlurAmount", 0)), "Blur.BlurAmount");
            c.Track(brush, "Blur.BlurAmount", x => x.Blur * 2);
            return brush;
        }, Big: true, After: (c, v) => Round(c, v, 18)));

        list.Add(new(group, "Lit rounded glass", "blur x SceneLightingEffect (pill normal map; ambient, distant and point light)", c =>
        {
            var glass = Fx.Saturation(Fx.Blur(Bg, c.Blur * 0.5f).Named("Blur", ("BlurAmount", 0)), 1.3f);
            var brush = c.Brush(Fx.Arithmetic(glass, c.Scene("normalBig", 1, 0.2f, 0.9f, 40), 1, 0, 0, 0), "Blur.BlurAmount");
            c.Track(brush, "Blur.BlurAmount", x => x.Blur * 0.5f);
            return brush;
        }, Big: true, After: (c, v) =>
        {
            Round(c, v, 40);
            Lights(c, v);
        }));

        list.Add(new(group, "Rim-lit clear glass", "no added blur; bevel = exposure + saturation through a mask; SceneLightingEffect highlights", c =>
        {
            var rim = Fx.AlphaMask(Fx.Exposure(Fx.Saturation(Bg, 1.8f), 0.7f), Fx.Src("rimBig"));
            var glass = Fx.Composite(0, Bg, rim);
            return c.Brush(Fx.Arithmetic(glass, c.Scene("normalBig", 1, 0.2f, 0.9f, 40), 1, 0, 0, 0));
        }, Big: true, After: (c, v) =>
        {
            Round(c, v, 40);
            Lights(c, v);
        }));

        group = "Blur";
        list.Add(new(group, "Host backdrop, no effect", "CreateHostBackdropBrush as is", c => c.Sources["bg"]));
        foreach (var sigma in new[] { 2f, 8f, 30f, 80f })
            Add("GaussianBlur", $"BlurAmount={sigma}", _ => Fx.Blur(Bg, sigma));
        Add("DirectionalBlur", "sigma=14 angle=0", _ => Fx.DirectionalBlur(Bg, 14, 0));
        Add("Shadow", "sigma=4 colour=blue", _ => Fx.Shadow(Bg, 4, [0, 0.2f, 1, 1]));
        list.Add(new(group, "In-window BackdropBrush", "CreateBackdropBrush + GaussianBlur 6", c =>
        {
            var blur = c.C.CreateEffectFactory(Fx.Blur(Fx.Src("x"), 6)).CreateBrush();
            blur.SetSourceParameter("x", c.C.CreateBackdropBrush());
            return blur;
        }));

        group = "Colour";
        Add("Saturation", "0", _ => Fx.Saturation(Bg, 0));
        Add("Saturation", "2", _ => Fx.Saturation(Bg, 2));
        Add("Tint", "colour=(1, 0.6, 0.3)", _ => Fx.Tint(Bg, [1, 0.6f, 0.3f, 1]));
        Add("ColorMatrix", "R<G, G<B, B<R", _ => Fx.ColorMatrix(Bg, [0, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0]));
        Add("Contrast", "+0.8", _ => Fx.Contrast(Bg, 0.8f));
        Add("Contrast", "-0.7", _ => Fx.Contrast(Bg, -0.7f));
        Add("Exposure", "+1.5", _ => Fx.Exposure(Bg, 1.5f));
        Add("Exposure", "-1.5", _ => Fx.Exposure(Bg, -1.5f));
        Add("HueRotation", "120 degrees", _ => Fx.HueRotation(Bg, 120));
        Add("Invert", "", _ => Fx.Invert(Bg));
        Add("Grayscale", "", _ => Fx.Grayscale(Bg));
        Add("Sepia", "intensity=1", _ => Fx.Sepia(Bg, 1));
        Add("TemperatureAndTint", "temperature=+0.8", _ => Fx.TemperatureTint(Bg, 0.8f, 0));
        Add("TemperatureAndTint", "temperature=-0.8 tint=+0.6", _ => Fx.TemperatureTint(Bg, -0.8f, 0.6f));
        Add("Opacity", "0.4", _ => Fx.Opacity(Bg, 0.4f));
        Add("LinearTransfer", "slope=2 offset=-0.5", _ => Fx.LinearTransfer(Bg, -0.5f, 2));
        Add("GammaTransfer", "exponent=2.5", _ => Fx.GammaTransfer(Bg, 2.5f));
        Add("LuminanceToAlpha", "", _ => Fx.LuminanceToAlpha(Bg));
        Add("ColorSource (Flood)", "colour=(0.2, 0.5, 1, 0.6)", _ => Fx.Flood(0.2f, 0.5f, 1, 0.6f));
        Add("Posterize", "3 levels", _ => Fx.Posterize(Bg, 3));
        Add("Brightness", "white=(0.6,1) black=(0,0.2)", _ => Fx.Brightness(Bg, [0.6f, 1], [0, 0.2f]));
        Add("TableTransfer", "table=1,0 (invert)", _ => Fx.TableTransfer(Bg, [1, 0]));
        Add("DiscreteTransfer", "table=0,0.5,1", _ => Fx.DiscreteTransfer(Bg, [0, 0.5f, 1]));
        Add("Premultiply", "", _ => Fx.Premultiply(Bg));
        Add("UnPremultiply", "", _ => Fx.UnPremultiply(Bg));
        Add("HighlightsAndShadows", "highlights=-1 shadows=+1", _ => Fx.HighlightsShadows(Bg, -1, 1, 0));
        Add("ChromaKey", "colour=white tolerance=0.3", _ => Fx.ChromaKey(Bg, [1, 1, 1], 0.3f));
        Add("RgbToHue", "HSV", _ => Fx.RgbToHue(Bg));
        Add("HueToRgb", "HSV", _ => Fx.HueToRgb(Bg));
        Add("WhiteLevelAdjustment", "in=80 out=240", _ => Fx.WhiteLevel(Bg, 80, 240));
        Add("HdrToneMap", "in=4000 out=300", _ => Fx.HdrToneMap(Bg, 4000, 300));

        group = "Blend modes (BlendEffect: backdrop under a rainbow)";
        for (var i = 0u; i < BlendModes.Length; i++)
        {
            var mode = i;
            Add("Blend", $"Mode={BlendModes[i]}", _ => Fx.Blend(mode, Bg, Fx.Src("fg")));
        }

        group = "Combining inputs (CompositeEffect modes: backdrop + rainbow disc; then the other two-input effects)";
        for (var i = 0u; i < CompositeModes.Length; i++)
        {
            var mode = i;
            Add("Composite", $"Mode={CompositeModes[i]}", _ => Fx.Composite(mode, Bg, Fx.Src("disc")));
        }
        Add("ArithmeticComposite", "multiply=1 (backdrop x rainbow)", _ => Fx.Arithmetic(Bg, Fx.Src("fg"), 1, 0, 0, 0));
        Add("ArithmeticComposite", "0.5 backdrop + 0.5 rainbow", _ => Fx.Arithmetic(Bg, Fx.Src("fg"), 0, 0.5f, 0.5f, 0));
        Add("CrossFade", "weight=0.5, backdrop / rainbow", _ => Fx.CrossFade(Bg, Fx.Src("fg"), 0.5f));
        Add("AlphaMask", "backdrop, soft round mask", _ => Fx.AlphaMask(Bg, Fx.Src("mask")));

        group = "Lighting (Direct2D lights take no input here, so the surface is flat: diffuse is multiplied into the backdrop, specular is added)";
        Add("DistantDiffuse", "no input; azimuth=225 elevation=40", _ => Fx.Arithmetic(Bg, Fx.DistantDiffuse(null!, 225, 40, 1), 1, 0, 0, 0));
        Add("PointDiffuse", "no input; light=(40,20,40)", _ => Fx.Arithmetic(Bg, Fx.PointDiffuse(null!, new(40, 20, 40), 1), 1, 0, 0, 0));
        Add("SpotDiffuse", "no input; light=(10,10,90) cone=35", _ => Fx.Arithmetic(Bg, Fx.SpotDiffuse(null!, new(10, 10, 90), new(88, 60, 0), 1), 1, 0, 0, 0));
        Add("DistantSpecular", "no input; elevation=40 exp=16", _ => Fx.Arithmetic(Bg, Fx.DistantSpecular(null!, 225, 40, 16, 1), 0, 1, 1, 0));
        Add("PointSpecular", "no input; light=(40,20,40) exp=16", _ => Fx.Arithmetic(Bg, Fx.PointSpecular(null!, new(40, 20, 40), 16, 1), 0, 1, 1, 0));
        Add("SpotSpecular", "no input; light=(10,10,90) exp=16", _ => Fx.Arithmetic(Bg, Fx.SpotSpecular(null!, new(10, 10, 90), new(88, 60, 0), 16, 1), 0, 1, 1, 0));
        Add("PointSpecular + height map", "pill-shaped height map as the input", _ => Fx.Arithmetic(Bg, Fx.PointSpecular(Fx.Src("bump"), new(40, 20, 40), 16, 10), 0, 1, 1, 0));
        Add("PointSpecular on backdrop", "backdrop itself as the height map", _ => Fx.PointSpecular(Bg, new(88, 40, 30), 8, 10));
        list.Add(new(group, "SceneLightingEffect", "x backdrop; pill normal map + 3 lights",
            c => c.Brush(Fx.Arithmetic(Bg, c.Scene("normal", 1, 0.2f, 0.9f, 30), 1, 0, 0, 0)), After: Lights));

        group = "Geometry and distortion (Transform2D is accepted over a GaussianBlur, but the backdrop still comes out untransformed)";
        Add("Transform2D on backdrop", "scale 2x, backdrop as the direct input", c => Fx.Transform2D(Bg, Matrix3x2.CreateScale(2, c.Size / 2)));
        Add("Transform2D over Blur 0", "scale 2x: accepted, no visible change", c => Zoom(2, c.Size));
        Add("Transform2D over Blur 0", "scale 0.5x: accepted, no visible change", c => Zoom(0.5f, c.Size));
        Add("Transform2D over Blur 0", "rotate 20 degrees: accepted, no change", c => Fx.Transform2D(Fx.Blur(Bg, 0), Matrix3x2.CreateRotation(20 * Degrees, c.Size / 2)));
        Add("Transform2D over Blur 0", "skew x 30 degrees: accepted, no change", c => Fx.Transform2D(Fx.Blur(Bg, 0), Matrix3x2.CreateSkew(30 * Degrees, 0, c.Size / 2)));
        Add("Transform2D over Blur 0", "mirror: accepted, no visible change", c => Fx.Transform2D(Fx.Blur(Bg, 0), Matrix3x2.CreateScale(-1, 1, c.Size / 2)));
        Add("Transform2D over Blur 3", "scale 3x: accepted, only the blur shows", c => Fx.Transform2D(Fx.Blur(Bg, 3), Matrix3x2.CreateScale(3, c.Size / 2)));
        Add("Transform2D over Saturation", "scale 2x; an effect other than blur in between", c => Fx.Transform2D(Fx.Saturation(Bg, 1), Matrix3x2.CreateScale(2, c.Size / 2)));
        Add("Transform2D on a surface", "rainbow rotated 20 degrees (control)", c => Fx.Transform2D(Fx.Src("fg"), Matrix3x2.CreateRotation(20 * Degrees, c.Size / 2)));
        Add("Border", "Mirror on backdrop, then GaussianBlur 30", _ => Fx.Blur(Fx.Border(Bg, 2, 2), 30));
        Add("Border", "Wrap on a 32 px rainbow surface", _ => Fx.Border(Fx.Src("small"), 1, 1));
        Add("Border", "Mirror on a 32 px rainbow surface", _ => Fx.Border(Fx.Src("small"), 2, 2));
        Add("Border over an effect", "Mirror over Transform2D", c => Fx.Border(Zoom(0.5f, c.Size), 2, 2));
        list.Add(new(group, "Visual.RotationAngle", "sprite turns 15 degrees, backdrop in it stays put", c => c.Sources["bg"], After: (c, v) =>
        {
            var inner = c.C.CreateSpriteVisual();
            inner.Size = v.Size * 0.6f;
            inner.Offset = new(v.Size * 0.2f, 0);
            inner.CenterPoint = new(inner.Size / 2, 0);
            inner.RotationAngleInDegrees = 15;
            inner.Brush = c.Brush(Fx.Invert(Bg));
            v.Children.InsertAtTop(inner);
        }));
        list.Add(new(group, "Visual.Scale", "sprite grows 2x, backdrop in it stays put", c => c.Sources["bg"], After: (c, v) =>
        {
            var inner = c.C.CreateSpriteVisual();
            inner.Size = v.Size * 0.3f;
            inner.Offset = new(v.Size * 0.2f, 0);
            inner.Scale = new(2, 2, 1);
            inner.Brush = c.Brush(Fx.Invert(Bg));
            v.Children.InsertAtTop(inner);
        }));
        list.Add(new(group, "CompositionVisualSurface", "surface of a backdrop sprite: comes out black", c =>
        {
            var sprite = c.C.CreateSpriteVisual();
            sprite.Size = c.Size;
            sprite.Brush = c.Sources["bg"];
            var surface = c.C.CreateVisualSurface();
            surface.SourceVisual = sprite;
            surface.SourceSize = c.Size;
            var brush = c.C.CreateSurfaceBrush(surface);
            brush.Stretch = CompositionStretch.None;
            brush.Scale = new(2, 2);
            return brush;
        }));
        Add("DisplacementMap", "scale=40, sine ripple map", _ => Fx.DisplacementMap(Bg, Fx.Src("disp"), 40));
        Add("DisplacementMap", "scale=40, over GaussianBlur 0", _ => Fx.DisplacementMap(Fx.Blur(Bg, 0), Fx.Src("disp"), 40));
        Add("Turbulence", "noise generator, frequency=0.03", c => Fx.Turbulence(c.Size.X, c.Size.Y, 0.03f));
        Add("3DTransform", "rotate Y 35 degrees + perspective", c =>
        {
            var centre = new Vector3(c.Size / 2, 0);
            var perspective = Matrix4x4.Identity;
            perspective.M34 = -1f / 400;
            var m = Matrix4x4.CreateTranslation(-centre) * Matrix4x4.CreateRotationY(35 * Degrees) * perspective * Matrix4x4.CreateTranslation(centre);
            return Fx.Transform3D(Fx.Blur(Bg, 0), m);
        });
        Add("3DPerspectiveTransform", "rotate Y 35 degrees depth=400", c => Fx.Perspective3D(Fx.Blur(Bg, 0), 400, c.Size / 2, new(c.Size / 2, 0), new(0, 35, 0)));
        Add("Scale", "2x about centre", c => Fx.Scale(Fx.Blur(Bg, 0), 2, c.Size / 2));
        Add("Crop", "middle half", c => Fx.Crop(Bg, c.Size.X / 4, c.Size.Y / 4, c.Size.X * 3 / 4, c.Size.Y * 3 / 4));
        Add("Tile", "40x40 from the top left", _ => Fx.Tile(Bg, 0, 0, 40, 40));
        Add("Atlas", "middle half", c => Fx.Atlas(Bg, c.Size.X / 4, c.Size.Y / 4, c.Size.X * 3 / 4, c.Size.Y * 3 / 4));
        Add("Straighten", "angle=15", _ => Fx.Straighten(Bg, 15));
        Add("DpiCompensation", "input dpi=192", _ => Fx.DpiCompensation(Bg, 192));

        group = "Filters";
        Add("Morphology", "Dilate 7x7", _ => Fx.Morphology(Bg, 1, 7));
        Add("Morphology", "Erode 7x7", _ => Fx.Morphology(Bg, 0, 7));
        Add("ConvolveMatrix", "3x3 sharpen kernel", _ => Fx.Convolve3x3(Bg, [0, -1, 0, -1, 5, -1, 0, -1, 0], 1));
        Add("Emboss", "height=2 direction=45", _ => Fx.Emboss(Bg, 2, 45));
        Add("EdgeDetection", "strength=0.8 Sobel", _ => Fx.EdgeDetection(Bg, 0.8f));
        Add("Sharpen", "sharpness=8", _ => Fx.Sharpen(Bg, 8));
        Add("Vignette", "strength=0.8", _ => Fx.Vignette(Bg, 0.8f));
        Add("OpacityMetadata", "", _ => Fx.OpacityMetadata(Bg));
        Add("Histogram", "256 bins", _ => Fx.Histogram(Bg));
        Add("YCbCr", "backdrop as both planes", _ => Fx.YCbCr(Bg, Bg));

        return list;
    }

    // The compositor refuses Transform2D straight on a backdrop brush. It accepts it over a GaussianBlur, then ignores it.
    static D2DEffect Zoom(float scale, Vector2 size) => Fx.Transform2D(Fx.Blur(Bg, 0), Matrix3x2.CreateScale(scale, size / 2));

    static void Lights(Ctx c, SpriteVisual v)
    {
        var white = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        var ambient = c.C.CreateAmbientLight();
        ambient.Color = Windows.UI.Color.FromArgb(255, 190, 190, 190);
        ambient.Targets.Add(v);
        var distant = c.C.CreateDistantLight();
        distant.CoordinateSpace = v;
        distant.Direction = Vector3.Normalize(new(0.5f, 0.6f, -0.6f));
        distant.Color = Windows.UI.Color.FromArgb(255, 150, 150, 150);
        distant.Targets.Add(v);
        var point = c.C.CreatePointLight();
        point.CoordinateSpace = v;
        point.Offset = new(v.Size.X * 0.25f, -20, 60);
        point.Color = Windows.UI.Color.FromArgb(255, 200, 192, 172);
        point.Targets.Add(v);
    }

    static void Round(Ctx c, Visual v, float radius)
    {
        var geometry = c.C.CreateRoundedRectangleGeometry();
        geometry.Size = v.Size;
        geometry.CornerRadius = new(MathF.Max(radius, 0));
        v.Clip = c.C.CreateGeometricClip(geometry);
    }
}
