using System.Net;

namespace WhatsAppCloudApi.Shared.Responses;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? CorrelationId { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null, string? correlationId = null) => new()
    {
        Success = true,
        Message = message,
        Data = data,
        CorrelationId = correlationId
    };

    public static ApiResponse<T> Fail(string message, HttpStatusCode statusCode, string? correlationId = null, string? details = null) => new()
    {
        Success = false,
        Message = message,
        Error = new ApiError
        {
            StatusCode = (int)statusCode,
            Details = details
        },
        CorrelationId = correlationId
    };
}

public sealed class ApiError
{
    public int StatusCode { get; init; }
    public string? Details { get; init; }
}
