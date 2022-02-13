using Humanizer;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Contexts;
using Modular.Abstractions.Events;
using Modular.Abstractions.Messaging;

namespace Modular.Infrastructure.Logging.Decorators;

[Decorator]
public sealed class LoggingEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IContext _context;
    private readonly IEventHandler<T> _handler;
    private readonly ILogger<LoggingEventHandlerDecorator<T>> _logger;
    private readonly IMessageContextProvider _messageContextProvider;

    public LoggingEventHandlerDecorator(IEventHandler<T> handler, IMessageContextProvider messageContextProvider,
        IContext context, ILogger<LoggingEventHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _messageContextProvider = messageContextProvider;
        _context = context;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        string module = @event.GetModuleName();
        string name = @event.GetType().Name.Underscore();
        IMessageContext messageContext = _messageContextProvider.Get(@event);
        Guid requestId = _context.RequestId;
        string traceId = _context.TraceId;
        Guid? userId = _context.Identity?.Id;
        Guid? messageId = messageContext?.MessageId;
        Guid correlationId = messageContext?.Context.CorrelationId ?? _context.CorrelationId;
        _logger.LogInformation(
            "Handling an event: {Name} ({Module}) [Request ID: {RequestId}, Message ID: {MessageId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}]...",
            name, module, requestId, messageId, correlationId, traceId, userId);
        await _handler.HandleAsync(@event, cancellationToken);
        _logger.LogInformation(
            "Handled an event: {Name} ({Module}) [Request ID: {RequestId}, Message ID: {MessageId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}]",
            name, module, requestId, messageId, correlationId, traceId, userId);
    }
}