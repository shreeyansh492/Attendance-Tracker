using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tracker.Machine
{
    public sealed class IdleDetectionService : IIdleDetectionService
    {
        public TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LastInputInfo
            {
                CbSize = (uint)Marshal.SizeOf<LastInputInfo>()
            };

            if (!GetLastInputInfo(ref lastInputInfo))
            {
                return TimeSpan.Zero;
            }

            var currentTick = unchecked((uint)Environment.TickCount);
            var idleMilliseconds = unchecked(currentTick - lastInputInfo.DwTime);

            return TimeSpan.FromMilliseconds(idleMilliseconds);
        }

        public bool IsIdle(TimeSpan threshold)
        {
            if (threshold <= TimeSpan.Zero)
            {
                return false;
            }
            return GetIdleTime() >= threshold;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint CbSize;
            public uint DwTime;
        }
    }
}
