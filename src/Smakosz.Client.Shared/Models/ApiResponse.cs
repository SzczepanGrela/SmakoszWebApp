namespace Smakosz.Client.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
}

public class ApiError
{
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
    public object? Details { get; set; }
}
