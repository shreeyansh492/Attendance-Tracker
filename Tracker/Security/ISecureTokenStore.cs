using System;
using System.Collections.Generic;
using System.Text;

namespace Tracker.Security
{
    public interface ISecureTokenStore
    {

        void SaveAccessToken(string accessToken);

        string? GetAccessToken();

        void ClearAccessToken();

        bool HasAccessToken {  get; }

    }
}
