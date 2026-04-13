using System.Runtime.InteropServices;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// P/Invoke helpers for display resolution enumeration and changes.
/// Uses EnumDisplaySettings and ChangeDisplaySettingsEx Win32 APIs.
/// </summary>
public static class DisplayHelper
{
    // ═══════ PUBLIC API ═══════

    /// <summary>
    /// Gets all supported display modes for a given device name (e.g., "\\.\DISPLAY1").
    /// Deduplicates modes and returns unique Width x Height x RefreshRate combinations.
    /// </summary>
    public static List<DisplayModeInfo> GetSupportedModes(string deviceName)
    {
        var modes = new HashSet<DisplayModeInfo>();
        var dm = CreateDevMode();
        int modeNum = 0;

        while (EnumDisplaySettings(deviceName, modeNum++, ref dm))
        {
            if (dm.dmBitsPerPel >= 16 && dm.dmPelsWidth >= 800)
            {
                modes.Add(new DisplayModeInfo
                {
                    Width = (int)dm.dmPelsWidth,
                    Height = (int)dm.dmPelsHeight,
                    RefreshRate = (int)dm.dmDisplayFrequency,
                    BitsPerPixel = (int)dm.dmBitsPerPel
                });
            }
        }

        return modes
            .OrderByDescending(m => m.Width)
            .ThenByDescending(m => m.Height)
            .ThenByDescending(m => m.RefreshRate)
            .ToList();
    }

    /// <summary>
    /// Gets the current display mode for a device.
    /// </summary>
    public static DisplayModeInfo? GetCurrentMode(string deviceName)
    {
        var dm = CreateDevMode();
        if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm))
        {
            return new DisplayModeInfo
            {
                Width = (int)dm.dmPelsWidth,
                Height = (int)dm.dmPelsHeight,
                RefreshRate = (int)dm.dmDisplayFrequency,
                BitsPerPixel = (int)dm.dmBitsPerPel
            };
        }
        return null;
    }

    /// <summary>
    /// Changes the display resolution for a given device.
    /// </summary>
    public static bool ChangeResolution(string deviceName, int width, int height, int refreshRate)
    {
        var dm = CreateDevMode();

        // Find matching mode
        int modeNum = 0;
        while (EnumDisplaySettings(deviceName, modeNum++, ref dm))
        {
            if (dm.dmPelsWidth == width &&
                dm.dmPelsHeight == height &&
                dm.dmDisplayFrequency == refreshRate)
            {
                dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;

                int result = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
                    CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);

                if (result == DISP_CHANGE_SUCCESSFUL)
                {
                    // Apply all changes
                    ChangeDisplaySettingsEx(null!, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
                    return true;
                }
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Identifies a monitor by flashing its screen white briefly.
    /// </summary>
    public static void IdentifyMonitor(string deviceName)
    {
        // We'll use a simple approach: open a full-screen white window on that monitor
        // This is done in the UI layer, so just provide the device name
        // For now, open display settings which has a built-in identify feature
        ProcessHelper.OpenSettings("ms-settings:display");
    }

    // ═══════ P/INVOKE ═══════

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;
    private const int DM_DISPLAYFREQUENCY = 0x00400000;
    private const int CDS_UPDATEREGISTRY = 0x01;
    private const int CDS_NORESET = 0x10000000;
    private const int DISP_CHANGE_SUCCESSFUL = 0;

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettingsEx(string? deviceName, ref DEVMODE devMode,
        IntPtr hwnd, uint dwFlags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettingsEx(string? deviceName, IntPtr devMode,
        IntPtr hwnd, uint dwFlags, IntPtr lParam);

    private static DEVMODE CreateDevMode()
    {
        var dm = new DEVMODE();
        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
        return dm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}
