using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX;
using Windows.UI.Composition;
using WinRT;

namespace Gallery;

// Puts app-made pixels (noise, height maps, the caption layer) into composition surfaces through Direct3D.
sealed unsafe class Surfaces
{
    static readonly Guid Texture2DIid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    const int UpdateSubresourceSlot = 48;

    readonly IntPtr _context;
    readonly CompositionGraphicsDevice _graphicsDevice;

    public Surfaces(Compositor compositor)
    {
        const int hardware = 1;
        const uint bgraSupport = 0x20, sdkVersion = 7;
        Marshal.ThrowExceptionForHR(Native.D3D11CreateDevice(0, hardware, 0, bgraSupport, 0, 0, sdkVersion, out var device, out _, out _context));
        compositor.As<ICompositorInterop>().CreateGraphicsDevice(device, out var raw);
        _graphicsDevice = CompositionGraphicsDevice.FromAbi(raw);
        Marshal.Release(raw);
    }

    // data is premultiplied BGRA, top row first.
    public CompositionDrawingSurface Create(int width, int height, IntPtr data, int stride)
    {
        var surface = _graphicsDevice.CreateDrawingSurface(new Windows.Foundation.Size(width, height),
            DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
        var interop = surface.As<ICompositionDrawingSurfaceInterop>();
        var iid = Texture2DIid;
        interop.BeginDraw(0, ref iid, out var texture, out var offset);
        try
        {
            var box = new Native.D3D11_BOX
            {
                left = (uint)offset.X, top = (uint)offset.Y, front = 0,
                right = (uint)(offset.X + width), bottom = (uint)(offset.Y + height), back = 1,
            };
            var vtable = *(void***)_context;
            var update = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, Native.D3D11_BOX*, void*, uint, uint, void>)vtable[UpdateSubresourceSlot];
            update(_context, texture, 0, &box, (void*)data, (uint)stride, 0);
        }
        finally
        {
            Marshal.Release(texture);
            interop.EndDraw();
        }
        return surface;
    }

    public CompositionDrawingSurface Create(Bitmap bitmap)
    {
        var bits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { return Create(bitmap.Width, bitmap.Height, bits.Scan0, bits.Stride); }
        finally { bitmap.UnlockBits(bits); }
    }

    // pixel(x, y) returns straight (r, g, b, a) in 0..1.
    public CompositionDrawingSurface Create(int width, int height, Func<int, int, (float R, float G, float B, float A)> pixel)
    {
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (r, g, b, a) = pixel(x, y);
            var i = (y * width + x) * 4;
            data[i] = ToByte(b * a);
            data[i + 1] = ToByte(g * a);
            data[i + 2] = ToByte(r * a);
            data[i + 3] = ToByte(a);
        }
        fixed (byte* p = data) return Create(width, height, (IntPtr)p, width * 4);
    }

    static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
}

static class Patterns
{
    // Height of a flat-topped pill: 0 outside, rising over `bevel` pixels to 1.
    public static float RoundedHeight(float x, float y, float width, float height, float radius, float bevel)
    {
        var qx = MathF.Abs(x - width / 2) - (width / 2 - radius);
        var qy = MathF.Abs(y - height / 2) - (height / 2 - radius);
        var outside = MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qy, 0) * MathF.Max(qy, 0));
        var inside = -(outside + MathF.Min(MathF.Max(qx, qy), 0) - radius);
        if (inside <= 0) return 0;
        var t = MathF.Min(inside / bevel, 1);
        return MathF.Sqrt(1 - (1 - t) * (1 - t));
    }

    public static (float, float, float, float) Rainbow(float t, float alpha = 1)
    {
        var h = (t % 1 + 1) % 1 * 6;
        var x = 1 - MathF.Abs(h % 2 - 1);
        var (r, g, b) = (int)h switch { 0 => (1f, x, 0f), 1 => (x, 1f, 0f), 2 => (0f, 1f, x), 3 => (0f, x, 1f), 4 => (x, 0f, 1f), _ => (1f, 0f, x) };
        return (r, g, b, alpha);
    }
}
