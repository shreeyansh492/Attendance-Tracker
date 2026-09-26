using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Data.Entity
{
    public enum EventTypes
    {
        HEARTBEAT = 0,
        CHECK_IN = 1,
        CHECK_OUT = 2,
        BREAK_START = 3,
        BREAK_END = 4,
        IDLE_START = 5,
        IDLE_END = 6,
    }
}
