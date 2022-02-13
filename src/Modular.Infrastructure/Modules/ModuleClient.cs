using System.Collections.Concurrent;
using System.Reflection;
using Modular.Abstractions.Commands;
using Modular.Abstractions.Messaging;
using Modular.Abstractions.Modules;
using Modular.Infrastructure.Messaging.Contexts;

namespace Modular.Infrastructure.Modules;

public sealed class ModuleClient : IModuleClient
{
    private readonly IMessageContextProvider _messageContextProvider;
    private readonly IMessageContextRegistry _messageContextRegistry;
    private readonly ConcurrentDictionary<Type, MessageAttribute> _messages = new();
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IModuleSerializer _moduleSerializer;

    public ModuleClient(IModuleRegistry moduleRegistry, IModuleSerializer moduleSerializer,
        IMessageContextRegistry messageContextRegistry, IMessageContextProvider messageContextProvider)
    {
        _moduleRegistry = moduleRegistry;
        _moduleSerializer = moduleSerializer;
        _messageContextRegistry = messageContextRegistry;
        _messageContextProvider = messageContextProvider;
    }

    public Task SendAsync(string path, object request, CancellationToken cancellationToken = default)
        => SendAsync<object>(path, request, cancellationToken);

    public async Task<TResult> SendAsync<TResult>(string path, object request,
        CancellationToken cancellationToken = default) where TResult : class
    {
        ModuleRequestRegistration registration = _moduleRegistry.GetRequestRegistration(path);
        if (registration is null)
        {
            throw new InvalidOperationException($"No action has been defined for path: '{path}'.");
        }

        object receiverRequest = TranslateType(request, registration.RequestType);
        object result = await registration.Action(receiverRequest, cancellationToken);

        return result is null ? null : TranslateType<TResult>(result);
    }

    public async Task PublishAsync(object message, CancellationToken cancellationToken = default)
    {
        string module = message.GetModuleName();
        string key = message.GetType().Name;
        IEnumerable<ModuleBroadcastRegistration> registrations = _moduleRegistry
            .GetBroadcastRegistrations(key)
            .Where(r => r.ReceiverType != message.GetType());

        var tasks = new List<Task>();

        foreach (ModuleBroadcastRegistration registration in registrations)
        {
            if (!_messages.TryGetValue(registration.ReceiverType, out MessageAttribute messageAttribute))
            {
                messageAttribute = registration.ReceiverType.GetCustomAttribute<MessageAttribute>();
                if (message is ICommand)
                {
                    messageAttribute = message.GetType().GetCustomAttribute<MessageAttribute>();
                    module = registration.ReceiverType.GetModuleName();
                }

                if (messageAttribute is not null)
                {
                    _messages.TryAdd(registration.ReceiverType, messageAttribute);
                }
            }

            if (messageAttribute is not null && !string.IsNullOrWhiteSpace(messageAttribute.Module) &&
                (!messageAttribute.Enabled || messageAttribute.Module != module))
            {
                continue;
            }

            Func<object, CancellationToken, Task> action = registration.Action;
            object receiverMessage = TranslateType(message, registration.ReceiverType);
            if (message is IMessage messageData)
            {
                IMessageContext messageContext = _messageContextProvider.Get(messageData);
                _messageContextRegistry.Set((IMessage)receiverMessage, messageContext);
            }

            tasks.Add(action(receiverMessage, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private T TranslateType<T>(object value)
        => _moduleSerializer.Deserialize<T>(_moduleSerializer.Serialize(value));

    private object TranslateType(object value, Type type)
        => _moduleSerializer.Deserialize(_moduleSerializer.Serialize(value), type);
}