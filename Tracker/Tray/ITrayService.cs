using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Tray
{
    public interface ITrayService
    {
        void Initialize();
        void Dispose();
    }
}
