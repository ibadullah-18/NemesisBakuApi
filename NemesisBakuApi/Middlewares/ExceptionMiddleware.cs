using System.Net;
using System.Text.Json;
using NemesisBakuApi.Helpers;

namespace NemesisBakuApi.Middlewares;

public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions
        JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;

    private readonly ILogger<ExceptionMiddleware>
        _logger;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted
                .IsCancellationRequested)
        {
            _logger.LogDebug(
                "HTTP request client tərəfindən dayandırıldı. " +
                "Path: {Path}",
                context.Request.Path);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "İcazəsiz giriş cəhdi. Path: {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.Unauthorized,
                "İcazəsiz giriş");
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Yanlış HTTP sorğusu. Path: {Path}",
                context.Request.Path);

            await WriteErrorAsync(
                context,
                HttpStatusCode.BadRequest,
                "Sorğu düzgün deyil");
        }
        catch (Exception ex)
        {
            var traceId =
                context.TraceIdentifier;

            _logger.LogError(
                ex,
                "Unhandled exception baş verdi. " +
                "TraceId: {TraceId}, Path: {Path}, " +
                "Method: {Method}",
                traceId,
                context.Request.Path,
                context.Request.Method);

            await WriteErrorAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Serverdə xəta baş verdi");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.Clear();

        context.Response.StatusCode =
            (int)statusCode;

        context.Response.ContentType =
            "application/json; charset=utf-8";

        var response =
            ApiResponse<string>.Fail(message);

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                response,
                JsonOptions),
            context.RequestAborted);
    }
}