using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DMVideoPlayer
{
    // Windows API interop for getting default audio device
    internal static class WindowsAudio
    {
        private const int DEVICE_STATE_ACTIVE = 0x00000001;
        private const int STGM_READ = 0x00000000;

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            
            [PreserveSig]
            int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
            
            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        }

        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig]
            int GetCount(out int count);

            [PreserveSig]
            int GetAt(int iProp, out PropertyKey pkey);

            [PreserveSig]
            int GetValue(ref PropertyKey key, out PropVariant pv);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int NotImpl1();
            
            [PreserveSig]
            int NotImpl2();
            
            [PreserveSig]
            int GetChannelCount(out uint pnChannelCount);
            
            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            
            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            
            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);
            
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);
            
            [PreserveSig]
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            
            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            
            [PreserveSig]
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            
            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        private static readonly Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static readonly Guid GUID_NULL = Guid.Empty;

        public static string? GetWindowsDefaultAudioDeviceId()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                var enumerator = new MMDeviceEnumerator() as IMMDeviceEnumerator;
                if (enumerator == null)
                    return null;

                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
                if (hr != 0 || device == null)
                    return null;

                hr = device.GetId(out string deviceId);
                if (hr != 0 || string.IsNullOrEmpty(deviceId))
                    return null;

                Debug.WriteLine($"Windows default audio device ID: {deviceId}");
                return deviceId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows default audio device: {ex.Message}");
                return null;
            }
        }

        public static string? GetWindowsDefaultAudioDeviceName()
        {
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                var enumerator = new MMDeviceEnumerator() as IMMDeviceEnumerator;
                if (enumerator == null)
                    return null;

                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
                if (hr != 0 || device == null)
                    return null;

                hr = device.OpenPropertyStore(STGM_READ, out IPropertyStore propertyStore);
                if (hr != 0 || propertyStore == null)
                    return null;

                var propertyKey = new PropertyKey
                {
                    fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
                    pid = 14
                };

                hr = propertyStore.GetValue(ref propertyKey, out PropVariant value);
                if (hr != 0)
                    return null;

                string? deviceName = Marshal.PtrToStringUni(value.pwszVal);
                Debug.WriteLine($"Windows default audio device name: {deviceName}");
                return deviceName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Windows default audio device name: {ex.Message}");
                return null;
            }
        }

        public static bool SetAudioBalance(float balance)
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                var enumerator = new MMDeviceEnumerator() as IMMDeviceEnumerator;
                if (enumerator == null)
                    return false;

                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
                if (hr != 0 || device == null)
                    return false;

                Guid iid = IID_IAudioEndpointVolume;
                hr = device.Activate(ref iid, 0, IntPtr.Zero, out object obj);
                if (hr != 0 || obj == null)
                    return false;

                var endpointVolume = obj as IAudioEndpointVolume;
                if (endpointVolume == null)
                    return false;

                hr = endpointVolume.GetChannelCount(out uint channelCount);
                if (hr != 0 || channelCount < 2)
                    return false;

                // Clamp balance entre -1.0 (gauche) et 1.0 (droite)
                balance = Math.Clamp(balance, -1.0f, 1.0f);

                // Calculer les niveaux de volume pour chaque canal
                float leftVolume = 1.0f;
                float rightVolume = 1.0f;

                if (balance < 0)
                {
                    // Balance vers la gauche : réduire le canal droit
                    rightVolume = 1.0f + balance;
                }
                else if (balance > 0)
                {
                    // Balance vers la droite : réduire le canal gauche
                    leftVolume = 1.0f - balance;
                }

                Guid eventContext = GUID_NULL;
                hr = endpointVolume.SetChannelVolumeLevelScalar(0, leftVolume, ref eventContext);
                if (hr != 0)
                    return false;

                hr = endpointVolume.SetChannelVolumeLevelScalar(1, rightVolume, ref eventContext);
                if (hr != 0)
                    return false;

                Debug.WriteLine($"Audio balance set to: {balance} (L: {leftVolume}, R: {rightVolume})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting audio balance: {ex.Message}");
                return false;
            }
        }

        public static bool SetAudioBalanceLeft()
        {
            return SetAudioBalance(-1.0f);
        }

        public static bool SetAudioBalanceCenter()
        {
            return SetAudioBalance(0.0f);
        }

        public static bool SetAudioBalanceRight()
        {
            return SetAudioBalance(1.0f);
        }
    }
}
