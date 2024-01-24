using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bolter
{

    internal class SoundBlocker : AbstractSecurity
    {
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const UInt32 KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const UInt32 KEYEVENTF_KEYUP = 0x0002;
        public static void Mute()
        {
            keybd_event(VK_VOLUME_MUTE, MapVirtualKey(VK_VOLUME_MUTE, 0), KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_VOLUME_MUTE, MapVirtualKey(VK_VOLUME_MUTE, 0), KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, UInt32 dwFlags, UInt32 dwExtraInfo);

        [DllImport("user32.dll")]
        static extern Byte MapVirtualKey(UInt32 uCode, UInt32 uMapType);
        public SoundBlocker(DateTime startDate, DateTime endDate) : base(startDate, endDate)
        {
        }

        protected override void _DisableSecurity()
        {
            throw new NotImplementedException();
        }

        protected override void _EnableSecurity()
        {
            throw new NotImplementedException();
        }

        protected override void _PrepareEnableSecurity()
        {
            throw new NotImplementedException();
        }
    }
}
