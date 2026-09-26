using System;
using System.Collections.Generic;
using System.Text;
using Tracker.Dto;

namespace Tracker.Api
{
    public interface IApiClient
    {
        Task<bool> IsApiOnlineAsync(CancellationToken ct = default);

        Task<PinLoginResponse> PinLoginAsync(PinLoginRequest request, CancellationToken ct = default);

        Task SendAttendanceEventAsync(List<AttendanceEventRequest> request,  CancellationToken ct = default);

        Task SendHeartbeatAsync(CancellationToken cancellationToken = default);

    }
}
