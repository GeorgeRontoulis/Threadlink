namespace Threadlink.Core.NativeSubsystems.Sentinel
{
    public enum SentinelError : byte
    {
        None = 0,
        NotInitialized,
        Unsupported,
        InvalidArgument,
        NotFound,
        Busy,
        Cancelled,
        PermissionDenied,
        NetworkUnavailable,
        QuotaExceeded,
        Conflict,
        CorruptData,
        AccountUnavailable,
        NativeFailure,
        Unknown
    }

    public readonly struct SentinelResult
    {
        public bool Succeeded { get; }
        public SentinelError Error { get; }
        public string Message { get; }
        public long NativeCode { get; }

        private SentinelResult(bool succeeded, SentinelError error, string message, long nativeCode)
        {
            Succeeded = succeeded;
            Error = error;
            Message = message;
            NativeCode = nativeCode;
        }

        public static SentinelResult Success() => new(true, SentinelError.None, null, 0);

        public static SentinelResult Failure(
            SentinelError error,
            string message = null,
            long nativeCode = 0)
        {
            if (error is SentinelError.None)
                error = SentinelError.Unknown;

            return new(false, error, message, nativeCode);
        }

        public override string ToString()
        {
            if (Succeeded)
                return "Success";

            return string.IsNullOrEmpty(Message)
                ? $"{Error} ({NativeCode})"
                : $"{Error} ({NativeCode}): {Message}";
        }
    }

    public readonly struct SentinelResult<T>
    {
        public bool Succeeded { get; }
        public SentinelError Error { get; }
        public string Message { get; }
        public long NativeCode { get; }
        public T Value { get; }

        private SentinelResult(bool succeeded, T value, SentinelError error, string message, long nativeCode)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
            Message = message;
            NativeCode = nativeCode;
        }

        public static SentinelResult<T> Success(T value) =>
            new(true, value, SentinelError.None, null, 0);

        public static SentinelResult<T> Failure(
            SentinelError error,
            string message = null,
            long nativeCode = 0)
        {
            if (error is SentinelError.None)
                error = SentinelError.Unknown;

            return new(false, default, error, message, nativeCode);
        }

        public SentinelResult Untyped() =>
            Succeeded
                ? SentinelResult.Success()
                : SentinelResult.Failure(Error, Message, NativeCode);

        public override string ToString() =>
            Succeeded
                ? $"Success: {Value}"
                : SentinelResult.Failure(Error, Message, NativeCode).ToString();
    }
}
