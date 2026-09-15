using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Nexo.Windows.Recording;

/// <summary>
/// 2026-09-15 — el puente entre Direct3D 11 (Vortice) y las API de WinRT de captura y codificación.
///
/// WinRT no expone el dispositivo ni las texturas de Direct3D directamente: hay que pasar por tres
/// funciones de d3d11.dll y dos interfaces COM de «interop». Las interfaces se llaman por su tabla
/// virtual con punteros a función, sin <c>[ComImport]</c>, porque en .NET moderno los objetos WinRT
/// los administra CsWinRT y la conversión clásica de COM no siempre los reconoce.
/// </summary>
internal static unsafe class CaptureInterop
{
    private static readonly Guid GraphicsCaptureItemInteropIid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid DxgiInterfaceAccessIid = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    public static ID3D11Device CreateDevice()
    {
        DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;
        FeatureLevel[] levels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1];

        var result = D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware, flags, levels, out var device);
        if (result.Failure || device is null)
        {
            // Sin tarjeta compatible (escritorio remoto, máquina virtual): el adaptador por software.
            D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Warp, DeviceCreationFlags.BgraSupport, levels, out device)
                .CheckError();
        }

        return device!;
    }

    public static IDirect3DDevice CreateWinRtDevice(ID3D11Device device)
    {
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var inspectable));
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    public static IDirect3DSurface CreateWinRtSurface(ID3D11Texture2D texture)
    {
        using var dxgiSurface = texture.QueryInterface<IDXGISurface>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11SurfaceFromDXGISurface(dxgiSurface.NativePointer, out var inspectable));
        try
        {
            return MarshalInterface<IDirect3DSurface>.FromAbi(inspectable);
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>La textura de Direct3D que hay detrás de un fotograma de captura.</summary>
    public static ID3D11Texture2D TextureFrom(IDirect3DSurface surface)
    {
        var unknown = ((IWinRTObject)surface).NativeObject.ThisPtr;
        var accessIid = DxgiInterfaceAccessIid;
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in accessIid, out var access));
        try
        {
            var textureIid = typeof(ID3D11Texture2D).GUID;
            IntPtr texture;
            // IDirect3DDxgiInterfaceAccess::GetInterface es la entrada 3 de su tabla (tras IUnknown).
            var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)(*(IntPtr**)access)[3];
            Marshal.ThrowExceptionForHR(getInterface(access, &textureIid, &texture));
            return new ID3D11Texture2D(texture);
        }
        finally
        {
            Marshal.Release(access);
        }
    }

    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interopIid = GraphicsCaptureItemInteropIid;
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(factory.ThisPtr, in interopIid, out var interop));
        try
        {
            var itemIid = GraphicsCaptureItemIid;
            IntPtr item;
            // IGraphicsCaptureItemInterop: 3 = CreateForWindow, 4 = CreateForMonitor.
            var createForMonitor = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)(*(IntPtr**)interop)[4];
            Marshal.ThrowExceptionForHR(createForMonitor(interop, monitor, &itemIid, &item));
            try
            {
                return GraphicsCaptureItem.FromAbi(item);
            }
            finally
            {
                Marshal.Release(item);
            }
        }
        finally
        {
            Marshal.Release(interop);
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);
}
