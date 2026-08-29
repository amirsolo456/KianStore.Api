namespace KianStore.Api.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public object? Errors { get; init; }
    public object? Warnings { get; init; }
    public string? TraceId { get; init; }

    public static ApiResponse<T> SuccessResult(
        T data,
        string message = "عملیات با موفقیت انجام شد.",
        string code = "SUCCESS",
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data,
            Errors = null,
            Warnings = null,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> SuccessWithWarningResult(
        T data,
        object warnings,
        string message,
        string code = "WARNING",
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data,
            Errors = null,
            Warnings = warnings,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> ErrorResult(
        string code,
        string message,
        object? errors = null,
        string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = default,
            Errors = errors,
            Warnings = null,
            TraceId = traceId
        };
    }
}