using Humanizer;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Contexts;
using Modular.Abstractions.Queries;

namespace Modular.Infrastructure.Logging.Decorators;

[Decorator]
public sealed class LoggingQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : class, IQuery<TResult>
{
    private readonly IContext _context;
    private readonly IQueryHandler<TQuery, TResult> _handler;
    private readonly ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> _logger;

    public LoggingQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler,
        IContext context, ILogger<LoggingQueryHandlerDecorator<TQuery, TResult>> logger)
    {
        _handler = handler;
        _context = context;
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        string module = query.GetModuleName();
        string name = query.GetType().Name.Underscore();
        Guid requestId = _context.RequestId;
        Guid correlationId = _context.CorrelationId;
        string traceId = _context.TraceId;
        Guid? userId = _context.Identity?.Id;
        _logger.LogInformation(
            "Handling a query: {Name} ({Module}) [Request ID: {RequestId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}]...",
            name, module, requestId, correlationId, traceId, userId);
        TResult result = await _handler.HandleAsync(query, cancellationToken);
        _logger.LogInformation(
            "Handled a query: {Name} ({Module}) [Request ID: {RequestId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}]",
            name, module, requestId, correlationId, traceId, userId);

        return result;
    }
}