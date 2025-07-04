using System;
using System.Runtime.InteropServices;
using System.Text;
using palew1n;

namespace palew1n
{
    public static class UsbHelper
    {
        const int DIGCF_PRESENT = 0x02;
        const int DIGCF_DEVICEINTERFACE = 0x10;
        static readonly Guid GUID_DEVINTERFACE_USB_DEVICE = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        const int SPDRP_FRIENDLYNAME = 0x0C;

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid,
            uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
            IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property,
            out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("winusb.dll", SetLastError = true)]
        static extern bool WinUsb_Initialize(IntPtr deviceHandle, out IntPtr interfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        static extern bool WinUsb_GetDescriptor(IntPtr interfaceHandle, byte descriptorType, byte descriptorIndex,
            byte[] buffer, int bufferLength, out int lengthTransferred);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        const byte USB_CONFIGURATION_DESCRIPTOR_TYPE = 2;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x1;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint OPEN_EXISTING = 3;

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        public static string ListUsbDevicesAndMaxPower()
        {
            StringBuilder sb = new StringBuilder();

            Guid usbGuid = GUID_DEVINTERFACE_USB_DEVICE;

            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref usbGuid, IntPtr.Zero, IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == IntPtr.Zero)
            {
                return "Failed to get device info set";
            }

            uint index = 0;
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);

            while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref usbGuid, index, ref deviceInterfaceData))
            {
                int requiredSize = 0;

                SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                IntPtr detailDataBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    if (IntPtr.Size == 8)
                        Marshal.WriteInt32(detailDataBuffer, 8);
                    else
                        Marshal.WriteInt32(detailDataBuffer, 5);

                    bool success = SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, detailDataBuffer, requiredSize, out _, IntPtr.Zero);
                    if (!success)
                    {
                        sb.AppendLine("Failed to get device interface detail.");
                        continue;
                    }

                    IntPtr pDevicePathName = IntPtr.Add(detailDataBuffer, 4);
                    string devicePath = Marshal.PtrToStringAuto(pDevicePathName) ?? "";

                    SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                    devInfoData.cbSize = Marshal.SizeOf(devInfoData);
                    SetupDiEnumDeviceInfo(deviceInfoSet, index, ref devInfoData);

                    uint regDataType;
                    uint requiredSize2 = 0;
                    byte[] buffer = new byte[256];
                    bool gotName = SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref devInfoData, 0x0C, out regDataType, buffer, (uint)buffer.Length, out requiredSize2);
                    string friendlyName = gotName ? Encoding.Unicode.GetString(buffer).TrimEnd('\0') : "(no friendly name)";

                    int maxPower = GetMaxPowerFromDevice(devicePath);

                    sb.AppendLine($"Device #{index}: {friendlyName}, Max Power: {maxPower} mA, Path: {devicePath}");
                }
                finally
                {
                    Marshal.FreeHGlobal(detailDataBuffer);
                }

                index++;
                deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);
            }

            return sb.ToString();
        }


        static int GetMaxPowerFromDevice(string devicePath)
        {
            IntPtr deviceHandle = CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            if (deviceHandle == IntPtr.Zero || deviceHandle == new IntPtr(-1))
            {
                System.Diagnostics.Debug.WriteLine("Failed to open device.");
                return -1;
            }

            if (!WinUsb_Initialize(deviceHandle, out IntPtr winUsbHandle))
            {
                System.Diagnostics.Debug.WriteLine("WinUSB initialize failed.");
                CloseHandle(deviceHandle);
                return -1;
            }

            byte[] buffer = new byte[256];
            bool gotDescriptor = WinUsb_GetDescriptor(winUsbHandle, USB_CONFIGURATION_DESCRIPTOR_TYPE, 0, buffer, buffer.Length, out int length);

            CloseHandle(deviceHandle);

            if (gotDescriptor)
            {
                int maxPower = buffer[8] * 2;
                return maxPower;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to get descriptor.");
                return -1;
            }
        }
    }
}
