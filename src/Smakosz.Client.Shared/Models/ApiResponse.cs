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

    public string GetDisplayMessage()
    {
        if (Details is not System.Text.Json.JsonElement element
            || element.ValueKind != System.Text.Json.JsonValueKind.Object)
            return Message;

        var messages = new List<string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
                continue;
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                    messages.Add(item.GetString()!);
            }
        }

        return messages.Count > 0 ? string.Join(" ", messages) : Message;
    }
}
