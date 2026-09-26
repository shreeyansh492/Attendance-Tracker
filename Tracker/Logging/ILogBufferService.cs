using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Logging
{
    public interface ILogBufferService
    {
        void Add(string line);

        IReadOnlyList<string> GetLatestLines();

        void Clear();
    }
}
