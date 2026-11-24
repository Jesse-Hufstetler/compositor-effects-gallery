using System.Runtime.InteropServices;

namespace Gallery;

[ComImport, Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICompositorDesktopInterop
{
    void CreateDesktopWindowTarget(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool isTopmost, out IntPtr target);
}

[ComImport, Guid("25297D5C-3AD4-4C9C-B5CF-E36A38512330"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICompositorInterop
{
    void CreateCompositionSurfaceForHandle(IntPtr swapChain, out IntPtr result);
    void CreateCompositionSurfaceForSwapChain(IntPtr swapChain, out IntPtr result);
    void CreateGraphicsDevice(IntPtr renderingDevice, out IntPtr result);
}

[ComImport, Guid("FD04E6E3-FE0C-4C3C-AB19-A07601A576EE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ICompositionDrawingSurfaceInterop
{
    void BeginDraw(IntPtr updateRect, ref Guid iid, out IntPtr updateObject, out Native.POINT updateOffset);
    void EndDraw();
}

// What the compositor asks an effect description for: a Direct2D effect id, its property values and its inputs.
[ComImport, Guid("2FC57384-A068-44D7-A331-30982FCF7177"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IGraphicsEffectD2D1Interop
{
    [PreserveSig] int GetEffectId(out Guid id);
    [PreserveSig] int GetNamedPropertyMapping([MarshalAs(UnmanagedType.LPWStr)] string name, out uint index, out int mapping);
    [PreserveSig] int GetPropertyCount(out uint count);
    [PreserveSig] int GetProperty(uint index, out IntPtr value);
    [PreserveSig] int GetSource(uint index, out IntPtr source);
    [PreserveSig] int GetSourceCount(out uint count);
}

static class Native
{
    public struct POINT { public int X, Y; }
    public struct D3D11_BOX { public uint left, top, front, right, bottom, back; }

    struct DispatcherQueueOptions { public int dwSize, threadType, apartmentType; }

    [DllImport("CoreMessaging.dll")]
    static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr controller);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("d3d11.dll")]
    public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    static IntPtr _queueController;

    // Windows.UI.Composition needs a dispatcher queue on the thread that creates the Compositor.
    public static void EnsureDispatcherQueue()
    {
        if (_queueController != 0) return;
        var options = new DispatcherQueueOptions { dwSize = 12, threadType = 2 /* current thread */, apartmentType = 2 /* STA */ };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out _queueController));
    }

    const int DWMWA_USE_HOSTBACKDROPBRUSH = 17;

    public static int EnableHostBackdrop(IntPtr hwnd)
    {
        var on = 1;
        return DwmSetWindowAttribute(hwnd, DWMWA_USE_HOSTBACKDROPBRUSH, ref on, sizeof(int));
    }
}
