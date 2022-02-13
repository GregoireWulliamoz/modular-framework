using Microsoft.Extensions.DependencyInjection;
using Modular.Abstractions.Messaging;

namespace Modular.Infrastructure.Messaging.Outbox;

public class OutboxBroker : IOutboxBroker
{
    private readonly OutboxTypeRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    public bool Enabled { get; }

    public OutboxBroker(IServiceProvider serviceProvider, OutboxTypeRegistry registry, OutboxOptions options)
    {
        _serviceProvider = serviceProvider;
        _registry = registry;
        Enabled = options.Enabled;
    }

    public async Task SendAsync(params IMessage[] messages)
    {
        IMessage message = messages[0]; // Not possible to send messages from different modules at once
        Type outboxType = _registry.Resolve(message);
        if (outboxType is null)
        {
            throw new InvalidOperationException($"Outbox is not registered for module: '{message.GetModuleName()}'.");
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        var outbox = (IOutbox)scope.ServiceProvider.GetRequiredService(outboxType);
        await outbox.SaveAsync(messages);
    }
}