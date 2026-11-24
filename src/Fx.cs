using System.Numerics;
using Windows.Graphics.Effects;
using Windows.UI.Composition;

namespace Gallery;

// One constructor per Direct2D built-in effect. Ids and property order come from d2d1effects.h / d2d1effects_2.h.
static class Fx
{
    public static IGraphicsEffectSource Src(string name) => new CompositionEffectSourceParameter(name);

    static D2DEffect E(string clsid, IGraphicsEffectSource[] sources, params object[] props) => new(new Guid(clsid), sources, props);
    static float[] V(params float[] v) => v;

    const uint Linear = 1, HardBorder = 1, Premultiplied = 1;

    // Blur
    public static D2DEffect Blur(IGraphicsEffectSource s, float sigma) => E("1feb6d69-2fe6-4ac9-8c58-1d7f93e7a6a5", [s], sigma, 1u, HardBorder);
    public static D2DEffect DirectionalBlur(IGraphicsEffectSource s, float sigma, float angle) => E("174319a6-58e9-49b2-bb63-caf2c811a3db", [s], sigma, angle, 1u, HardBorder);
    public static D2DEffect Shadow(IGraphicsEffectSource s, float sigma, float[] rgba) => E("C67EA361-1863-4e69-89DB-695D3E9A5B6B", [s], sigma, rgba, 1u);

    // Colour
    public static D2DEffect Saturation(IGraphicsEffectSource s, float v) => E("5cb2d9cf-327d-459f-a0ce-40c0b2086bf7", [s], v);
    public static D2DEffect HueRotation(IGraphicsEffectSource s, float degrees) => E("0f4458ec-4b32-491b-9e85-bd73f44d3eb6", [s], degrees);
    public static D2DEffect ColorMatrix(IGraphicsEffectSource s, float[] m5x4) => E("921F03D6-641C-47DF-852D-B4BB6153AE11", [s], m5x4, Premultiplied, false);
    public static D2DEffect Tint(IGraphicsEffectSource s, float[] rgba) => E("36312b17-f7dd-4014-915d-ffca768cf211", [s], rgba, false);
    public static D2DEffect Contrast(IGraphicsEffectSource s, float v) => E("b648a78a-0ed5-4f80-a94a-8e825aca6b77", [s], v, false);
    public static D2DEffect Exposure(IGraphicsEffectSource s, float v) => E("b56c8cfa-f634-41ee-bee0-ffa617106004", [s], v);
    public static D2DEffect Grayscale(IGraphicsEffectSource s) => E("36DDE0EB-3725-42E0-836D-52FB20AEE644", [s]);
    public static D2DEffect Invert(IGraphicsEffectSource s) => E("e0c3784d-cb39-4e84-b6fd-6b72f0810263", [s]);
    public static D2DEffect Sepia(IGraphicsEffectSource s, float v) => E("3a1af410-5f1d-4dbe-84df-915da79b7153", [s], v, Premultiplied);
    public static D2DEffect TemperatureTint(IGraphicsEffectSource s, float temperature, float tint) => E("89176087-8AF9-4A08-AEB1-895F38DB1766", [s], temperature, tint);
    public static D2DEffect Opacity(IGraphicsEffectSource s, float v) => E("811d79a4-de28-4454-8094-c64685f8bd4c", [s], v);
    public static D2DEffect Posterize(IGraphicsEffectSource s, uint levels) => E("2188945e-33a3-4366-b7bc-086bd02d0884", [s], levels, levels, levels);
    public static D2DEffect Brightness(IGraphicsEffectSource s, float[] white, float[] black) => E("8cea8d1e-77b0-4986-b3b9-2f0c0eae7887", [s], white, black);
    public static D2DEffect LinearTransfer(IGraphicsEffectSource s, float intercept, float slope) =>
        E("ad47c8fd-63ef-4acc-9b51-67979c036c06", [s], intercept, slope, false, intercept, slope, false, intercept, slope, false, 0f, 1f, true, true);
    public static D2DEffect GammaTransfer(IGraphicsEffectSource s, float exponent) =>
        E("409444c4-c419-41a0-b0c1-8cd0c0a18e42", [s], 1f, exponent, 0f, false, 1f, exponent, 0f, false, 1f, exponent, 0f, false, 1f, 1f, 0f, true, true);
    public static D2DEffect TableTransfer(IGraphicsEffectSource s, float[] table) =>
        E("5bf818c3-5e43-48cb-b631-868396d6a1d4", [s], table, false, table, false, table, false, V(0, 1), true, true);
    public static D2DEffect DiscreteTransfer(IGraphicsEffectSource s, float[] table) =>
        E("90866fcd-488e-454b-af06-e5041b66c36c", [s], table, false, table, false, table, false, V(0, 1), true, true);
    public static D2DEffect LuminanceToAlpha(IGraphicsEffectSource s) => E("41251ab7-0beb-46f8-9da7-59e93fcce5de", [s]);
    public static D2DEffect Premultiply(IGraphicsEffectSource s) => E("06eab419-deed-4018-80d2-3e1d471adeb2", [s]);
    public static D2DEffect UnPremultiply(IGraphicsEffectSource s) => E("fb9ac489-ad8d-41ed-9999-bb6347d110f7", [s]);
    public static D2DEffect HighlightsShadows(IGraphicsEffectSource s, float highlights, float shadows, float clarity) =>
        E("CADC8384-323F-4C7E-A361-2E2B24DF6EE4", [s], highlights, shadows, clarity, 1u, 1.25f);
    public static D2DEffect ChromaKey(IGraphicsEffectSource s, float[] rgb, float tolerance) => E("74C01F5B-2A0D-408C-88E2-C7A3C7197742", [s], rgb, tolerance, false, false);
    public static D2DEffect RgbToHue(IGraphicsEffectSource s) => E("23f3e5ec-91e8-4d3d-ad0a-afadc1004aa1", [s], 0u);
    public static D2DEffect HueToRgb(IGraphicsEffectSource s) => E("7b78a6bd-0141-4def-8a52-6356ee0cbdd5", [s], 0u);
    public static D2DEffect WhiteLevel(IGraphicsEffectSource s, float input, float output) => E("44a1cadb-6cdd-4818-8ff4-26c1cfe95bdb", [s], input, output);
    public static D2DEffect HdrToneMap(IGraphicsEffectSource s, float input, float output) => E("7b0b748d-4610-4486-a90c-999d9a2e2b11", [s], input, output, 0u);

    // Generators
    public static D2DEffect Flood(float r, float g, float b, float a) => E("61c23c20-ae69-4d8e-94cf-50078df638f2", [], V(r, g, b, a));
    public static D2DEffect Turbulence(float w, float h, float frequency) =>
        E("CF2BB6AE-889A-4ad7-BA29-A2FD732C9FC9", [], V(0, 0), V(w, h), V(frequency, frequency), 2u, 0, 0u, false);

    // Two or more inputs
    public static D2DEffect Blend(uint mode, IGraphicsEffectSource background, IGraphicsEffectSource foreground) => E("81c5b77b-13f8-4cdd-ad20-c890547ac65d", [background, foreground], mode);
    public static D2DEffect Composite(uint mode, params IGraphicsEffectSource[] sources) => E("48fc9f51-f6ac-48f1-8b58-3b28ac46f76d", sources, mode);
    public static D2DEffect Arithmetic(IGraphicsEffectSource a, IGraphicsEffectSource b, float multiply, float aAmount, float bAmount, float offset) =>
        E("fc151437-049a-4784-a24a-f1c4daf20987", [a, b], V(multiply, aAmount, bAmount, offset), true);
    public static D2DEffect CrossFade(IGraphicsEffectSource a, IGraphicsEffectSource b, float weight) => E("12f575e8-4db1-485f-9a84-03a07dd3829f", [a, b], weight);
    public static D2DEffect AlphaMask(IGraphicsEffectSource s, IGraphicsEffectSource mask) => E("c80ecff0-3fd5-4f05-8328-c5d1724b4f0a", [s, mask]);
    public static D2DEffect DisplacementMap(IGraphicsEffectSource s, IGraphicsEffectSource map, float scale) => E("edc48364-0417-4111-9450-43845fa9f890", [s, map], scale, 0u, 1u);

    // Lighting. The input's alpha channel is the height map.
    public static D2DEffect DistantDiffuse(IGraphicsEffectSource height, float azimuth, float elevation, float scale) =>
        E("3e7efd62-a32d-46d4-a83c-5278889ac954", [height], azimuth, elevation, 1f, scale, V(1, 1, 1), V(1, 1), Linear);
    public static D2DEffect DistantSpecular(IGraphicsEffectSource height, float azimuth, float elevation, float exponent, float scale) =>
        E("428c1ee5-77b8-4450-8ab5-72219c21abda", [height], azimuth, elevation, exponent, 1f, scale, V(1, 1, 1), V(1, 1), Linear);
    public static D2DEffect PointDiffuse(IGraphicsEffectSource height, Vector3 position, float scale) =>
        E("b9e303c3-c08c-4f91-8b7b-38656bc48c20", [height], V(position.X, position.Y, position.Z), 1f, scale, V(1, 1, 1), V(1, 1), Linear);
    public static D2DEffect PointSpecular(IGraphicsEffectSource height, Vector3 position, float exponent, float scale) =>
        E("09c3ca26-3ae2-4f09-9ebc-ed3865d53f22", [height], V(position.X, position.Y, position.Z), exponent, 1f, scale, V(1, 1, 1), V(1, 1), Linear);
    public static D2DEffect SpotDiffuse(IGraphicsEffectSource height, Vector3 position, Vector3 target, float scale) =>
        E("818a1105-7932-44f4-aa86-08ae7b2f2c93", [height], V(position.X, position.Y, position.Z), V(target.X, target.Y, target.Z), 1f, 35f, 1f, scale, V(1, 1, 1), V(1, 1), Linear);
    public static D2DEffect SpotSpecular(IGraphicsEffectSource height, Vector3 position, Vector3 target, float exponent, float scale) =>
        E("edae421e-7654-4a37-9db8-71acc1beb3c1", [height], V(position.X, position.Y, position.Z), V(target.X, target.Y, target.Z), 1f, 35f, exponent, 1f, scale, V(1, 1, 1), V(1, 1), Linear);

    // Geometry
    public static D2DEffect Transform2D(IGraphicsEffectSource s, Matrix3x2 m) =>
        E("6AA97485-6354-4cfc-908C-E4A74F62C96C", [s], Linear, 0u, V(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32), 0f);
    public static D2DEffect Transform3D(IGraphicsEffectSource s, Matrix4x4 m) =>
        E("e8467b04-ec61-4b8a-b5de-d4d73debea5a", [s], Linear, 0u,
            V(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44));
    public static D2DEffect Perspective3D(IGraphicsEffectSource s, float depth, Vector2 origin, Vector3 rotationOrigin, Vector3 rotation) =>
        E("C2844D0B-3D86-46e7-85BA-526C9240F3FB", [s], Linear, 0u, depth, V(origin.X, origin.Y), V(0, 0, 0), V(0, 0, 0),
            V(rotationOrigin.X, rotationOrigin.Y, rotationOrigin.Z), V(rotation.X, rotation.Y, rotation.Z));
    public static D2DEffect Scale(IGraphicsEffectSource s, float scale, Vector2 center) => E("9daf9369-3846-4d0e-a44e-0c607934a5d7", [s], V(scale, scale), V(center.X, center.Y), Linear, 0u, 0f);
    public static D2DEffect Crop(IGraphicsEffectSource s, float l, float t, float r, float b) => E("E23F7110-0E9A-4324-AF47-6A2C0C46F35B", [s], V(l, t, r, b), 0u);
    public static D2DEffect Border(IGraphicsEffectSource s, uint edgeX, uint edgeY) => E("2A2D49C0-4ACF-43c7-8C6A-7C4A27874D27", [s], edgeX, edgeY);
    public static D2DEffect Tile(IGraphicsEffectSource s, float l, float t, float r, float b) => E("B0784138-3B76-4bc5-B13B-0FA2AD02659F", [s], V(l, t, r, b));
    public static D2DEffect Atlas(IGraphicsEffectSource s, float l, float t, float r, float b) => E("913e2be4-fdcf-4fe2-a5f0-2454f14ff408", [s], V(l, t, r, b), V(l, t, r, b));
    public static D2DEffect Straighten(IGraphicsEffectSource s, float degrees) => E("4da47b12-79a3-4fb0-8237-bbc3b2a4de08", [s], degrees, true, Linear);
    public static D2DEffect DpiCompensation(IGraphicsEffectSource s, float dpi) => E("6c26c5c7-34e0-46fc-9cfd-e5823706e228", [s], Linear, 0u, V(dpi, dpi));

    // Neighbourhood filters
    public static D2DEffect Morphology(IGraphicsEffectSource s, uint mode, uint size) => E("eae6c40d-626a-4c2d-bfcb-391001abe202", [s], mode, size, size);
    public static D2DEffect Convolve3x3(IGraphicsEffectSource s, float[] kernel, float divisor) =>
        E("407f8c08-5533-4331-a341-23cc3877843e", [s], V(1, 1), Linear, 3u, 3u, kernel, divisor, 0f, V(0, 0), true, HardBorder, true);
    public static D2DEffect Emboss(IGraphicsEffectSource s, float height, float direction) => E("b1c5eb2b-0348-43f0-8107-4957cacba2ae", [s], height, direction);
    public static D2DEffect EdgeDetection(IGraphicsEffectSource s, float strength) => E("EFF583CA-CB07-4AA9-AC5D-2CC44C76460F", [s], strength, 0f, 0u, false, Premultiplied);
    public static D2DEffect Sharpen(IGraphicsEffectSource s, float sharpness) => E("C9B887CB-C5FF-4DC5-9779-273DCF417C7D", [s], sharpness, 0f);
    public static D2DEffect Vignette(IGraphicsEffectSource s, float strength) => E("c00c40be-5e67-4ca3-95b4-f4b02c115135", [s], V(0, 0, 0, 1), 0.3f, strength);

    // Odd ones, tried for completeness
    public static D2DEffect OpacityMetadata(IGraphicsEffectSource s) => E("6c53006a-4450-4199-aa5b-ad1656fece5e", [s], V(0, 0, 100, 100));
    public static D2DEffect Histogram(IGraphicsEffectSource s) => E("881db7d0-f7ee-4d4d-a6d2-4697acc66ee8", [s], 256u, 0u);
    public static D2DEffect YCbCr(IGraphicsEffectSource y, IGraphicsEffectSource cbcr) => E("99503cc1-66c7-45c9-a875-8ad8a7914401", [y, cbcr], 0u, V(1, 0, 0, 1, 0, 0), Linear);
}
