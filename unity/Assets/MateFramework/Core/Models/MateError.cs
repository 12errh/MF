namespace Mate.Core.Models
{
    /// <summary>
    /// Result type for operations that produce a value (ADR-011).
    /// </summary>
    public class Result<T>
    {
        public T Value { get; }
        public bool IsSuccess { get; }
        public string Error { get; }

        private Result(T value)
        {
            Value = value;
            IsSuccess = true;
            Error = null;
        }

        private Result(string error)
        {
            Value = default;
            IsSuccess = false;
            Error = error;
        }

        public static Result<T> Ok(T value) => new(value);
        public static Result<T> Fail(string error) => new(error);

        public static implicit operator Result<T>(T value) => Ok(value);
    }

    /// <summary>
    /// Result type for operations that produce no value (ADR-011).
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public string Error { get; }

        private Result(bool success, string error)
        {
            IsSuccess = success;
            Error = error;
        }

        public static Result Ok() => new(true, null);
        public static Result Fail(string error) => new(false, error);
    }
}