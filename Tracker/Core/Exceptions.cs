using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Tracker.Core
{
    public sealed class SessionExpiredException : Exception
    {
        public SessionExpiredException(string message) : base(message)
        {
        }

        public SessionExpiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class ApiRequestException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? ResponseBody { get; }
        public ApiRequestException(string message, HttpStatusCode statusCode, string? responseBody = null) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public ApiRequestException(string message, HttpStatusCode statusCode, string? responseBody, Exception innerException) : base(message, innerException)
        {
            StatusCode=statusCode;
            ResponseBody=responseBody;
        }

        public bool IsBusinessError => StatusCode == HttpStatusCode.BadRequest ||
            StatusCode == HttpStatusCode.Forbidden ||
            (int)StatusCode == 422;
    }

    public sealed class AppConfigurationException : Exception
    {
        public AppConfigurationException(string message) : base(message)
        {

        }
    }

    public sealed class LocalStateException : Exception
    {
        public LocalStateException(string message) : base(message)
        {

        }

        public LocalStateException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    public sealed class AuthException : Exception
    {
        public AuthException(string message) : base(message)
        {

        }
        public AuthException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
