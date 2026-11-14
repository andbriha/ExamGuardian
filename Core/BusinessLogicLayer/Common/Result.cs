namespace BusinessLogicLayer.Common;

public class Result<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public string[] Errors { get; init; } = Array.Empty<string>();
    public string? ErrorCode { get; init; }
}

public static class Result
{
    public static Result<T> Success<T>(T data) =>
        new() { Succeeded = true, Data = data };

    public static Result<T> Failure<T>(string error, string? errorCode = null) =>
        new() { Succeeded = false, Errors = new[] { error }, ErrorCode = errorCode };
}