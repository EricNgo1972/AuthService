namespace AuthService.Shared.Models;

public sealed record OperationResult(bool Succeeded, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static OperationResult Success() => new(true);
    public static OperationResult Failure(string code, string message) => new(false, code, message);
}

public sealed record OperationResult<T>(bool Succeeded, T? Value = default, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static OperationResult<T> Success(T value) => new(true, value);
    public static OperationResult<T> Failure(string code, string message) => new(false, default, code, message);
}
