using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Gearfuel.Devices;

/// <summary>A HID top-level collection, the unit Windows exposes HID devices as.</summary>
sealed partial record HidDevice(string Path, ushort VendorId, ushort ProductId, string ProductName, ushort UsagePage, ushort Usage, int InputReportLength, int FeatureReportLength)
{
    public static IReadOnlyList<HidDevice> All() =>
        InterfacePaths().Select(TryDescribe).OfType<HidDevice>().ToList();

    /// <summary>
    /// Opens without read/write access: enough for feature reports, and the only way to
    /// open mice and keyboards, which Windows holds exclusively.
    /// </summary>
    public SafeFileHandle OpenForFeatures() => Open(Path, access: 0, flags: 0);

    public FileStream OpenStream() => new(Open(Path, GenericRead | GenericWrite, FileFlagOverlapped), FileAccess.ReadWrite, bufferSize: 0, isAsync: true);

    public static bool GetFeature(SafeFileHandle handle, byte[] report) => HidD_GetFeature(handle, report, report.Length);

    public static bool SetFeature(SafeFileHandle handle, byte[] report) => HidD_SetFeature(handle, report, report.Length);

    static string[] InterfacePaths()
    {
        var hidInterface = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        while (true)
        {
            CM_Get_Device_Interface_List_SizeW(out var length, ref hidInterface, null, 0);
            var buffer = new char[length];
            var result = CM_Get_Device_Interface_ListW(ref hidInterface, null, buffer, length, 0);
            if (result == CrBufferSmall) continue; // a device arrived in between
            if (result != 0) throw new Win32Exception($"CM_Get_Device_Interface_List failed: {result}");
            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    static HidDevice? TryDescribe(string path)
    {
        using var handle = Open(path, access: 0, flags: 0, throwOnError: false);
        if (handle.IsInvalid) return null;

        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(handle, ref attributes) || !HidD_GetPreparsedData(handle, out var preparsed)) return null;
        try
        {
            if (HidP_GetCaps(preparsed, out var caps) != HidpStatusSuccess) return null;
            var name = new char[127]; // the longest string a USB descriptor holds, plus a terminator
            var productName = HidD_GetProductString(handle, name, name.Length * sizeof(char)) ? new string(name).TrimEnd('\0') : "";
            return new HidDevice(path, attributes.VendorId, attributes.ProductId, productName, caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.FeatureReportByteLength);
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    static SafeFileHandle Open(string path, uint access, uint flags, bool throwOnError = true)
    {
        var handle = CreateFileW(path, access, FileShare.ReadWrite, 0, FileMode.Open, flags, 0);
        if (handle.IsInvalid && throwOnError) throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Cannot open {path}");
        return handle;
    }

    const uint GenericRead = 0x80000000, GenericWrite = 0x40000000, FileFlagOverlapped = 0x40000000;
    const int CrBufferSmall = 0x1A, HidpStatusSuccess = 0x00110000;

    struct HiddAttributes
    {
        public int Size;
        public ushort VendorId, ProductId, VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
    }

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CM_Get_Device_Interface_List_SizeW(out int length, ref Guid interfaceClass, string? deviceId, int flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CM_Get_Device_Interface_ListW(ref Guid interfaceClass, string? deviceId, [Out] char[] buffer, int length, int flags);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string path, uint access, FileShare share, nint security, FileMode mode, uint flags, nint template);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetAttributes(SafeFileHandle handle, ref HiddAttributes attributes);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetPreparsedData(SafeFileHandle handle, out nint preparsed);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_FreePreparsedData(nint preparsed);

    [LibraryImport("hid.dll")]
    private static partial int HidP_GetCaps(nint preparsed, out HidpCaps caps);

    [LibraryImport("hid.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetProductString(SafeFileHandle handle, [Out] char[] buffer, int byteLength);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetFeature(SafeFileHandle handle, [Out] byte[] report, int length);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_SetFeature(SafeFileHandle handle, byte[] report, int length);
}
