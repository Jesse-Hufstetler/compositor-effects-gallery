using System.Runtime.InteropServices;
using Windows.Graphics.Effects;
using WinRT;

namespace Gallery;

// A hand-written stand-in for a Win2D effect: a Direct2D effect id, property values in index order, and inputs.
// Property values are float, uint, int, bool or float[] (vectors, colours, matrices, tables).
sealed class D2DEffect : IGraphicsEffect, IGraphicsEffectSource, ICustomQueryInterface
{
    static readonly Guid InteropIid = new("2FC57384-A068-44D7-A331-30982FCF7177");
    static readonly Guid PropertyValueIid = new("4BD682DD-7554-40E9-9A9B-82654EDE7E62");

    readonly Interop _interop;

    public D2DEffect(Guid id, IGraphicsEffectSource[] sources, object[] props)
    {
        Id = id;
        Sources = sources;
        Props = props;
        _interop = new Interop(this);
    }

    public Guid Id { get; }
    public IGraphicsEffectSource[] Sources { get; }
    public object[] Props { get; }
    public Dictionary<string, uint> Animatable { get; } = new();
    public string Name { get; set; } = "";

    // Names the effect and says which properties can be changed after the brush is built ("Name.Property").
    public D2DEffect Named(string name, params (string Property, uint Index)[] animatable)
    {
        Name = name;
        foreach (var (property, index) in animatable) Animatable[property] = index;
        return this;
    }

    CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid == InteropIid)
        {
            ppv = Marshal.GetComInterfaceForObject(_interop, typeof(IGraphicsEffectD2D1Interop));
            return CustomQueryInterfaceResult.Handled;
        }
        ppv = 0;
        return CustomQueryInterfaceResult.NotHandled;
    }

    sealed class Interop(D2DEffect owner) : IGraphicsEffectD2D1Interop
    {
        const int E_INVALIDARG = unchecked((int)0x80070057);
        const int Direct = 1;

        public int GetEffectId(out Guid id)
        {
            id = owner.Id;
            return 0;
        }

        public int GetNamedPropertyMapping(string name, out uint index, out int mapping)
        {
            mapping = Direct;
            return owner.Animatable.TryGetValue(name, out index) ? 0 : E_INVALIDARG;
        }

        public int GetPropertyCount(out uint count)
        {
            count = (uint)owner.Props.Length;
            return 0;
        }

        public int GetProperty(uint index, out IntPtr value)
        {
            value = 0;
            if (index >= owner.Props.Length) return E_INVALIDARG;
            var boxed = MarshalInspectable<object>.FromManaged(owner.Props[index]);
            var iid = PropertyValueIid;
            var hr = Marshal.QueryInterface(boxed, in iid, out value);
            Marshal.Release(boxed);
            return hr;
        }

        public int GetSource(uint index, out IntPtr source)
        {
            source = 0;
            if (index >= owner.Sources.Length) return E_INVALIDARG;
            if (owner.Sources[index] is { } s) source = MarshalInterface<IGraphicsEffectSource>.FromManaged(s);
            return 0;
        }

        public int GetSourceCount(out uint count)
        {
            count = (uint)owner.Sources.Length;
            return 0;
        }
    }
}
