using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Modular.Abstractions.Events;
using Modular.Abstractions.Messaging;

namespace Modular.Infrastructure.Messaging.Outbox;

[Decorator]
public class InboxEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly bool _enabled;
    private readonly IEventHandler<T> _handler;
    private readonly InboxTypeRegistry _inboxTypeRegistry;
    private readonly IMessageContextProvider _messageContextProvider;
    private readonly IServiceProvider _serviceProvider;

    public InboxEventHandlerDecorator(IEventHandler<T> handler, IServiceProvider serviceProvider,
        IMessageContextProvider messageContextProvider, InboxTypeRegistry inboxTypeRegistry, OutboxOptions options)
    {
        _handler = handler;
        _serviceProvider = serviceProvider;
        _messageContextProvider = messageContextProvider;
        _inboxTypeRegistry = inboxTypeRegistry;
        _enabled = options.Enabled;
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        if (_enabled)
        {
            Type inboxType = _inboxTypeRegistry.Resolve<T>();
            if (inboxType is null)
            {
                await _handler.HandleAsync(@event, cancellationToken);
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            var inbox = (IInbox)_serviceProvider.GetRequiredService(inboxType);
            IMessageContext context = _messageContextProvider.Get(@event);
            string name = @event.GetType().Name.Underscore();
            await inbox.HandleAsync(context.MessageId, name, () => _handler.HandleAsync(@event, cancellationToken));
            return;
        }

        await _handler.HandleAsync(@event, cancellationToken);
    }
}