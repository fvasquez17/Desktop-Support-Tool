using System.Runtime.InteropServices;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Provides audio device enumeration and control via Windows Core Audio COM APIs.
/// Enumerates playback/recording endpoints, sets default device, and controls volume.
/// </summary>
public static class AudioHelper
{
    // ═════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets all active audio playback (render) devices.
    /// </summary>
    public static List<AudioDeviceInfo> GetPlaybackDevices()
        => GetDevices(EDataFlow.eRender);

    /// <summary>
    /// Gets all active audio recording (capture) devices.
    /// </summary>
    public static List<AudioDeviceInfo> GetRecordingDevices()
        => GetDevices(EDataFlow.eCapture);

    /// <summary>
    /// Sets the default audio device for all roles (console, multimedia, communications).
    /// </summary>
    public static bool SetDefaultDevice(string deviceId)
    {
        try
        {
            var policyConfig = (IPolicyConfig)new PolicyConfigCoClass();
            policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
            policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the master volume level (0.0 to 1.0) for the default playback device.
    /// </summary>
    public static float GetMasterVolume()
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eRender);
            if (volumeControl == null) return 0;
            volumeControl.GetMasterVolumeLevelScalar(out float level);
            return level;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Sets the master volume level (0.0 to 1.0) for the default playback device.
    /// </summary>
    public static void SetMasterVolume(float level)
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eRender);
            if (volumeControl == null) return;
            var guid = Guid.Empty;
            volumeControl.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref guid);
        }
        catch { }
    }

    /// <summary>
    /// Gets the mute state of the default playback device.
    /// </summary>
    public static bool GetMasterMute()
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eRender);
            if (volumeControl == null) return false;
            volumeControl.GetMute(out bool muted);
            return muted;
        }
        catch { return false; }
    }

    /// <summary>
    /// Sets the mute state of the default playback device.
    /// </summary>
    public static void SetMasterMute(bool mute)
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eRender);
            if (volumeControl == null) return;
            var guid = Guid.Empty;
            volumeControl.SetMute(mute, ref guid);
        }
        catch { }
    }

    /// <summary>
    /// Gets the master volume for the default recording device (0.0 to 1.0).
    /// </summary>
    public static float GetMicrophoneVolume()
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eCapture);
            if (volumeControl == null) return 0;
            volumeControl.GetMasterVolumeLevelScalar(out float level);
            return level;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Sets the master volume for the default recording device (0.0 to 1.0).
    /// </summary>
    public static void SetMicrophoneVolume(float level)
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eCapture);
            if (volumeControl == null) return;
            var guid = Guid.Empty;
            volumeControl.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref guid);
        }
        catch { }
    }

    /// <summary>
    /// Gets the mute state of the default recording device.
    /// </summary>
    public static bool GetMicrophoneMute()
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eCapture);
            if (volumeControl == null) return false;
            volumeControl.GetMute(out bool muted);
            return muted;
        }
        catch { return false; }
    }

    /// <summary>
    /// Sets the mute state of the default recording device.
    /// </summary>
    public static void SetMicrophoneMute(bool mute)
    {
        try
        {
            var volumeControl = GetDefaultVolumeControl(EDataFlow.eCapture);
            if (volumeControl == null) return;
            var guid = Guid.Empty;
            volumeControl.SetMute(mute, ref guid);
        }
        catch { }
    }

    // ═════════════════════════════════════════════════════════════
    //  PRIVATE IMPLEMENTATION
    // ═════════════════════════════════════════════════════════════

    private static List<AudioDeviceInfo> GetDevices(EDataFlow dataFlow)
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();

            // Get default device ID for comparison
            string defaultId = "";
            try
            {
                enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eConsole, out IMMDevice defaultDevice);
                if (defaultDevice != null)
                {
                    defaultDevice.GetId(out IntPtr pDefaultId);
                    defaultId = Marshal.PtrToStringUni(pDefaultId) ?? "";
                    Marshal.FreeCoTaskMem(pDefaultId);
                }
            }
            catch { }

            // Enumerate active devices
            enumerator.EnumAudioEndpoints(dataFlow, DEVICE_STATE_ACTIVE, out IMMDeviceCollection collection);
            if (collection == null) return devices;

            collection.GetCount(out int count);
            for (int i = 0; i < count; i++)
            {
                collection.Item(i, out IMMDevice device);
                if (device == null) continue;

                try
                {
                    // Get device ID
                    device.GetId(out IntPtr pId);
                    string id = Marshal.PtrToStringUni(pId) ?? "";
                    Marshal.FreeCoTaskMem(pId);

                    // Get friendly name from property store
                    string name = GetDeviceFriendlyName(device);

                    devices.Add(new AudioDeviceInfo
                    {
                        Id = id,
                        Name = name,
                        DeviceType = dataFlow == EDataFlow.eRender ? "Playback" : "Recording",
                        IsDefault = id == defaultId,
                        IsActive = true
                    });
                }
                catch { }
            }
        }
        catch { }

        return devices;
    }

    private static string GetDeviceFriendlyName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(STGM_READ, out IPropertyStore propStore);
            if (propStore == null) return "Unknown Device";

            var pkey = PKEY_Device_FriendlyName;
            propStore.GetValue(ref pkey, out PropVariant nameVariant);

            string name = nameVariant.AsString;
            nameVariant.Dispose();
            return string.IsNullOrEmpty(name) ? "Unknown Device" : name;
        }
        catch { return "Unknown Device"; }
    }

    private static IAudioEndpointVolume? GetDefaultVolumeControl(EDataFlow dataFlow)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
            enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eConsole, out IMMDevice device);
            if (device == null) return null;

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out IntPtr ppInterface);
            return Marshal.GetObjectForIUnknown(ppInterface) as IAudioEndpointVolume;
        }
        catch { return null; }
    }

    // ═════════════════════════════════════════════════════════════
    //  COM CONSTANTS
    // ═════════════════════════════════════════════════════════════

    private const int DEVICE_STATE_ACTIVE = 0x00000001;
    private const int STGM_READ = 0x00000000;
    private const int CLSCTX_ALL = 0x17;

    // PKEY_Device_FriendlyName = {A45C254E-DF1C-4EFD-8020-67D146A850E0}, 14
    private static PropertyKey PKEY_Device_FriendlyName = new()
    {
        formatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        propertyId = 14
    };

    // ═════════════════════════════════════════════════════════════
    //  COM ENUMS
    // ═════════════════════════════════════════════════════════════

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    // ═════════════════════════════════════════════════════════════
    //  COM STRUCTS
    // ═════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid formatId;
        public int propertyId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant : IDisposable
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr data1;
        public IntPtr data2;

        // VT_LPWSTR = 31
        public string AsString => vt == 31 ? Marshal.PtrToStringUni(data1) ?? "" : "";

        public void Dispose()
        {
            PropVariantClear(ref this);
        }

        [DllImport("Ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }

    // ═════════════════════════════════════════════════════════════
    //  COM INTERFACES — Core Audio API
    // ═════════════════════════════════════════════════════════════

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorCoClass { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask,
            out IMMDeviceCollection ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role,
            out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
            out IMMDevice ppDevice);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int pcDevices);
        [PreserveSig] int Item(int nDevice, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
            out IntPtr ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
        [PreserveSig] int GetId(out IntPtr ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int cProps);
        [PreserveSig] int GetAt(int iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant propvar);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    // ═══ IPolicyConfig — Undocumented but stable COM for setting defaults ═══

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigCoClass { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, long pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFx, IntPtr key, out IntPtr pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFx, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }
}
