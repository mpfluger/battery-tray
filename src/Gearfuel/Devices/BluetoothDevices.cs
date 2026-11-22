using System.Runtime.InteropServices;
using System.Text;

namespace Gearfuel.Devices;

/// <summary>
/// Connected Bluetooth devices whose battery level Windows tracks itself: the ones
/// Settings → Bluetooth shows a level for (mostly Bluetooth LE mice, keyboards and headsets).
/// </summary>
sealed partial class BluetoothDevices : IBatterySource
{
    static readonly string[] Enumerators = ["BTHLE", "BTHENUM"];

    static readonly DevPropKey BatteryLevel = new(new("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    static readonly DevPropKey IsConnected = new(new("83DA6326-97A6-4088-9453-A1923F573B29"), 15);
    static readonly DevPropKey FriendlyName = new(new("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    public Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices)
    {
        var readings = new List<BatteryReading>();
        foreach (var id in Enumerators.SelectMany(PresentDeviceIds))
        {
            if (CM_Locate_DevNodeW(out var node, id, 0) != 0) continue;
            if (GetProperty(node, BatteryLevel) is [var percent]
                && GetProperty(node, IsConnected) is [not 0]
                && GetProperty(node, FriendlyName) is { } name)
            {
                readings.Add(new(id, Encoding.Unicode.GetString(name).TrimEnd('\0'), percent, Charging: false));
            }
        }
        return Task.FromResult<IReadOnlyList<BatteryReading>>(readings);
    }

    static string[] PresentDeviceIds(string enumerator)
    {
        const int filter = 0x1 | 0x100; // by enumerator, present devices only
        while (true)
        {
            CM_Get_Device_ID_List_SizeW(out var length, enumerator, filter);
            var buffer = new char[length];
            var result = CM_Get_Device_ID_ListW(enumerator, buffer, length, filter);
            if (result == CrBufferSmall) continue; // a device arrived in between
            return result == 0 ? new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries) : [];
        }
    }

    static byte[]? GetProperty(int node, DevPropKey key)
    {
        var size = 0;
        if (CM_Get_DevNode_PropertyW(node, key, out _, null, ref size, 0) != CrBufferSmall) return null;
        var value = new byte[size];
        return CM_Get_DevNode_PropertyW(node, key, out _, value, ref size, 0) == 0 ? value : null;
    }

    const int CrBufferSmall = 0x1A;

    readonly record struct DevPropKey(Guid Category, int Id);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CM_Get_Device_ID_List_SizeW(out int length, string filter, int flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, int length, int flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CM_Locate_DevNodeW(out int node, string deviceId, int flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial int CM_Get_DevNode_PropertyW(int node, in DevPropKey key, out int type, [Out] byte[]? buffer, ref int size, int flags);
}
