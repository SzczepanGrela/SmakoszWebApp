namespace Smakosz.API.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
}

public class ApiError
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public object? Details { get; init; }
}
