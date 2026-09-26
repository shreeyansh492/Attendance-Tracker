using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Machine
{
    public interface IIdleDetectionService
    {
        TimeSpan GetIdleTime();

        bool IsIdle(TimeSpan threshold);
    }
}
