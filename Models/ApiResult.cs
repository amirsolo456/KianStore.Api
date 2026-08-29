
public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public string? Code { get; init; }
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T data)
        => new()
        {
            Success = true,
            Data = data
        };

    public static ApiResult<T> ErrorResult(string code)
        => new()
        {
            Success = false,
            Code = code
        };
}



public sealed class NotFoundException : ApiException
{
    public NotFoundException(
        string code,
        string message)
        : base(
            StatusCodes.Status404NotFound,
            code,
            message)
    {
    }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(
        string code,
        string message)
        : base(
            StatusCodes.Status409Conflict,
            code,
            message)
    {
    }
}

public sealed class BusinessException : ApiException
{
    public BusinessException(
        string code,
        string message,
        object? errors = null)
        : base(
            StatusCodes.Status422UnprocessableEntity,
            code,
            message,
            errors)
    {
    }
}