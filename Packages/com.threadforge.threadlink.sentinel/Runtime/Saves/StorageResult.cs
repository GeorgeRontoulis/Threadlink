namespace Threadforge.Threadlink
{
    using System.Runtime.CompilerServices;

    public enum PersistenceError : byte
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
        Unknown,

        /// <summary>
        /// A previously available storage backend went away.
        /// </summary>
        PlatformLost,
    }

    public readonly struct PersistenceResult
    {
        public bool Succeeded { get; }
        public PersistenceError Error { get; }
        public string Message { get; }
        public long NativeCode { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PersistenceResult(
            bool succeeded,
            PersistenceError error,
            string message,
            long nativeCode
        )
        {
            Succeeded = succeeded;
            Error = error;
            Message = message;
            NativeCode = nativeCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PersistenceResult Success() => new(true, PersistenceError.None, null, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PersistenceResult Failure(
            PersistenceError error,
            string message = null,
            long nativeCode = 0
        )
        {
            if (error is PersistenceError.None)
                error = PersistenceError.Unknown;

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

    public readonly struct PersistenceResult<T>
    {
        public bool Succeeded { get; }
        public PersistenceError Error { get; }
        public string Message { get; }
        public long NativeCode { get; }
        public T Value { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PersistenceResult(
            bool succeeded,
            T value,
            PersistenceError error,
            string message,
            long nativeCode
        )
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
            Message = message;
            NativeCode = nativeCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PersistenceResult<T> Success(T value) =>
            new(true, value, PersistenceError.None, null, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PersistenceResult<T> Failure(
            PersistenceError error,
            string message = null,
            long nativeCode = 0
        )
        {
            if (error is PersistenceError.None)
                error = PersistenceError.Unknown;

            return new(false, default, error, message, nativeCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PersistenceResult Untyped() =>
            Succeeded
                ? PersistenceResult.Success()
                : PersistenceResult.Failure(Error, Message, NativeCode);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() =>
            Succeeded
                ? $"Success: {Value}"
                : PersistenceResult.Failure(Error, Message, NativeCode).ToString();
    }
}
