using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Machine
{
    public interface IMachineIdentityService
    {

        string MachineIdentifier { get; }

        string MachineName { get; }
    }
}
