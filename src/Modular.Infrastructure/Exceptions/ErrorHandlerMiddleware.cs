using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Exceptions;

namespace Modular.Infrastructure.Exceptions;

public class ErrorHandlerMiddleware : IMiddleware
{
    private readonly IExceptionCompositionRoot _exceptionCompositionRoot;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;

    public ErrorHandlerMiddleware(IExceptionCompositionRoot exceptionCompositionRoot,
        ILogger<ErrorHandlerMiddleware> logger)
    {
        _exceptionCompositionRoot = exceptionCompositionRoot;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "{message}",exception.Message);
            await HandleErrorAsync(context, exception);
        }
    }

    private async Task HandleErrorAsync(HttpContext context, Exception exception)
    {
        ExceptionResponse errorResponse = _exceptionCompositionRoot.Map(exception);
        context.Response.StatusCode = (int)(errorResponse?.StatusCode ?? HttpStatusCode.InternalServerError);
        object response = errorResponse?.Response;
        if (response is null)
        {
            return;
        }

        await context.Response.WriteAsJsonAsync(response);
    }
}