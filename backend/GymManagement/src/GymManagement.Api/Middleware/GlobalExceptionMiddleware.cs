using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using GymManagement.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GymManagement.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Only genuinely unexpected failures are logged as errors.
            //
            // These four are the application saying no on purpose - a wrong surname at
            // sign-up, a member who already has an account, a validation failure. They were
            // all logged as "An unhandled exception occurred", which was tolerable while
            // every endpoint was admin-only and refusals were rare. Sign-up is public and
            // refuses routinely, so leaving it would fill the log with ERR lines that are
            // not faults - and the log is where the real ones are found.
            if (IsExpected(ex))
            {
                _logger.LogInformation("Request refused: {Message}", ex.Message);
            }
            else
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Whether this exception is the application deliberately refusing, rather than
    /// something going wrong. Mirrors the cases <see cref="HandleExceptionAsync"/> maps to
    /// a 4xx - anything that falls through to a 500 is a fault and is logged as one.
    /// </summary>
    private static bool IsExpected(Exception exception) =>
        exception is ValidationException
            or NotFoundException
            or UnauthorizedException
            or BusinessException;


    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Success = false,
            Message = "An error occurred while processing your request.",
            Errors = null
        };

        switch (exception)
        {
            case ValidationException validationException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = "Validation failed.";
                response.Errors = validationException.Errors;
                break;

            case NotFoundException notFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Message = notFoundException.Message;
                break;

            case UnauthorizedException unauthorizedException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Message = unauthorizedException.Message;
                break;

            case BusinessException businessException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Message = businessException.Message;
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Message = "An internal server error occurred. Please try again later.";
                break;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }

    private class ErrorResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Errors { get; set; }
    }
}
