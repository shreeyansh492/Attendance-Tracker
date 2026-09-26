using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Dto;

namespace Tracker.Services
{
    public interface IAuthService
    {
        Task<PinLoginResponse?> LoginAsync(string email, string pin, CancellationToken ct = default);
    }
}
