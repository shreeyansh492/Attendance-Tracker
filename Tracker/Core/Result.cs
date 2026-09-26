using System;

namespace Tracker.Core
{
    public sealed class Result
    {
        public bool Success { get; }
        public string? Message { get; }
        public string? ErrorCode { get; }

        public bool Failed => !Success;

        private Result(bool success, string? message, string? errorCode)
        {
            Success = success;
            Message = message;
            ErrorCode = errorCode;
        }

        public static Result Ok(string? message = null)
        {
            return new Result(success: true, message: message, errorCode: null);
        }

        public static Result Fail(string message, string? errorCode = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failure message cannot be empty.", nameof(message));
            }
            return new Result(success: false, message: message, errorCode: errorCode);
        }
    }

    public sealed class Result<T>
    {
        private Result(bool success, T? data, string? message, string? errorCode)
        {
            Success = success;
            Data = data;
            ErrorCode = errorCode;
            Message = message;
        }

        public bool Success { get; }
        public bool Failed => !Success;
        public T? Data { get; }
        public string? Message { get; }
        public string? ErrorCode { get; }

        public static Result<T> Fail(string message, string? errorCode = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Failure message cannot be empty.", nameof(message));
            }
            return new Result<T>(success: false, data: default, message: message, errorCode: errorCode);
        }

        public static Result<T> Ok(T data, string? message = null)
        {
            return new Result<T>(success: true, data: data, message: message, errorCode: null);
        }
    }
}