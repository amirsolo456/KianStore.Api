using KianStore.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace KianStore.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            await HandleNonExceptionResponseAsync(context);
        }
        catch (OperationCanceledException) when (
            context.RequestAborted.IsCancellationRequested)
        {
            await WriteResponseAsync(
                context,
                StatusCodes.Status499ClientClosedRequest,
                ApiResponse<object>.ErrorResult(
                    "REQUEST_CANCELLED",
                    "درخواست توسط کاربر لغو شد.",
                    traceId: context.TraceIdentifier));
        }
        catch (ApiException ex)
        {
            await HandleApiExceptionAsync(context, ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Database concurrency error. TraceId: {TraceId}",
                context.TraceIdentifier);

            await WriteResponseAsync(
                context,
                StatusCodes.Status409Conflict,
                ApiResponse<object>.ErrorResult(
                    "CONCURRENCY_ERROR",
                    "اطلاعات همزمان تغییر کرده است. دوباره تلاش کنید.",
                    traceId: context.TraceIdentifier));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database update error. TraceId: {TraceId}",
                context.TraceIdentifier);

            await WriteResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResult(
                    "DATABASE_UPDATE_ERROR",
                    "خطایی هنگام ذخیره اطلاعات رخ داد.",
                    traceId: context.TraceIdentifier));
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "SQL Server error. TraceId: {TraceId}",
                context.TraceIdentifier);

            await WriteResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResult(
                    "DATABASE_ERROR",
                    "ارتباط با پایگاه داده با خطا مواجه شد.",
                    traceId: context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception. TraceId: {TraceId}",
                context.TraceIdentifier);

            await WriteResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.ErrorResult(
                    "INTERNAL_SERVER_ERROR",
                    "خطای داخلی در سرور رخ داده است.",
                    traceId: context.TraceIdentifier));
        }
    }

    private async Task HandleApiExceptionAsync(
        HttpContext context,
        ApiException exception)
    {
        await WriteResponseAsync(
            context,
            exception.StatusCode,
            ApiResponse<object>.ErrorResult(
                exception.Code,
                exception.Message,
                exception.Errors,
                context.TraceIdentifier));
    }

    private static async Task HandleNonExceptionResponseAsync(
        HttpContext context)
    {
        if (context.Response.HasStarted)
            return;

        var statusCode = context.Response.StatusCode;

        /*
         * اگر Controller قبلاً Response استاندارد نوشته،
         * چیزی را تغییر نمی‌دهیم.
         */
        if (statusCode >= 200 && statusCode < 300)
            return;

        switch (statusCode)
        {
            case StatusCodes.Status400BadRequest:

                await WriteResponseAsync(
                    context,
                    400,
                    ApiResponse<object>.ErrorResult(
                        "BAD_REQUEST",
                        "درخواست ارسال‌شده معتبر نیست.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status401Unauthorized:

                await WriteResponseAsync(
                    context,
                    401,
                    ApiResponse<object>.ErrorResult(
                        "UNAUTHORIZED",
                        "برای انجام این عملیات وارد حساب کاربری شوید.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status403Forbidden:

                await WriteResponseAsync(
                    context,
                    403,
                    ApiResponse<object>.ErrorResult(
                        "FORBIDDEN",
                        "شما اجازه انجام این عملیات را ندارید.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status404NotFound:

                await WriteResponseAsync(
                    context,
                    404,
                    ApiResponse<object>.ErrorResult(
                        "NOT_FOUND",
                        "اطلاعات موردنظر پیدا نشد.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status405MethodNotAllowed:

                await WriteResponseAsync(
                    context,
                    405,
                    ApiResponse<object>.ErrorResult(
                        "METHOD_NOT_ALLOWED",
                        "این نوع درخواست پشتیبانی نمی‌شود.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status408RequestTimeout:

                await WriteResponseAsync(
                    context,
                    408,
                    ApiResponse<object>.ErrorResult(
                        "REQUEST_TIMEOUT",
                        "زمان درخواست به پایان رسید.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status409Conflict:

                await WriteResponseAsync(
                    context,
                    409,
                    ApiResponse<object>.ErrorResult(
                        "CONFLICT",
                        "درخواست با وضعیت فعلی اطلاعات سازگار نیست.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status415UnsupportedMediaType:

                await WriteResponseAsync(
                    context,
                    415,
                    ApiResponse<object>.ErrorResult(
                        "UNSUPPORTED_MEDIA_TYPE",
                        "نوع محتوای ارسال‌شده پشتیبانی نمی‌شود.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status422UnprocessableEntity:

                await WriteResponseAsync(
                    context,
                    422,
                    ApiResponse<object>.   ErrorResult    (
                        "UNPROCESSABLE_ENTITY",
                        "اطلاعات ارسال‌شده قابل پردازش نیست.",
                        traceId: context.TraceIdentifier));

                break;

            case StatusCodes.Status429TooManyRequests:

                await WriteResponseAsync(
                    context,
                    429,
                    ApiResponse<object>.ErrorResult(
                        "TOO_MANY_REQUESTS",
                        "تعداد درخواست‌ها بیش از حد مجاز است.",
                        traceId: context.TraceIdentifier));

                break;

            default:

                if (statusCode >= 400)
                {
                    await WriteResponseAsync(
                        context,
                        statusCode,
                        ApiResponse<object>.ErrorResult(
                            "HTTP_ERROR",
                            "در پردازش درخواست خطایی رخ داد.",
                            traceId: context.TraceIdentifier));
                }

                break;
        }
    }

    private static async Task WriteResponseAsync<T>(
        HttpContext context,
        int statusCode,
        ApiResponse<T> response)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType =
            "application/json; charset=utf-8";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                response,
                JsonOptions));
    }
}