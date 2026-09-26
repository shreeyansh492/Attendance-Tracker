using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Data.Entity
{
    public enum AppState
    {
        LoggedOut = 0,
        Working = 1,
        IdleLocked = 2,
        CheckedOut = 3,
        Starting = 4,
        Error = 5,
        OnBreak = 6,
       
    }
}
