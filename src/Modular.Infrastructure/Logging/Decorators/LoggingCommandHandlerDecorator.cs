using Humanizer;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Commands;
using Modular.Abstractions.Contexts;
using Modular.Abstractions.Messaging;

namespace Modular.Infrastructure.Logging.Decorators;

[Decorator]
public sealed class LoggingCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly IContext _context;
    private readonly ICommandHandler<T> _handler;
    private readonly ILogger<LoggingCommandHandlerDecorator<T>> _logger;
    private readonly IMessageContextProvider _messageContextProvider;

    public LoggingCommandHandlerDecorator(ICommandHandler<T> handler, IMessageContextProvider messageContextProvider,
        IContext context, ILogger<LoggingCommandHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _messageContextProvider = messageContextProvider;
        _context = context;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        string module = command.GetModuleName();
        string name = command.GetType().Name.Underscore();
        IMessageContext messageContext = _messageContextProvider.Get(command);
        Guid requestId = _context.RequestId;
        string traceId = _context.TraceId;
        Guid? userId = _context.Identity?.Id;
        Guid? messageId = messageContext?.MessageId;
        Guid correlationId = messageContext?.Context.CorrelationId ?? _context.CorrelationId;
        _logger.LogInformation(
            "Handling a command: {Name} ({Module}) [Request ID: {RequestId}, Message ID: {MessageId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}]'...",
            name, module, requestId, messageId, correlationId, traceId, userId);
        await _handler.HandleAsync(command, cancellationToken);
        _logger.LogInformation(
            "Handled a command: {Name} ({Module}) [Request ID: {RequestId}, Message ID: {MessageId}, Correlation ID: {CorrelationId}, Trace ID: '{TraceId}', User ID: '{UserId}']",
            name, module, requestId, messageId, correlationId, traceId, userId);
    }
}