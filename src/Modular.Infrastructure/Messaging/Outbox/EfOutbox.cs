using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Messaging;
using Modular.Abstractions.Modules;
using Modular.Abstractions.Time;
using Modular.Infrastructure.Contexts;
using Modular.Infrastructure.Messaging.Contexts;
using Modular.Infrastructure.Messaging.Dispatchers;
using Modular.Infrastructure.Serialization;

namespace Modular.Infrastructure.Messaging.Outbox;

public sealed class EfOutbox<T> : IOutbox where T : DbContext
{
    private readonly IAsyncMessageDispatcher _asyncMessageDispatcher;
    private readonly IClock _clock;
    private readonly T _dbContext;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly ILogger<EfOutbox<T>> _logger;
    private readonly IMessageContextProvider _messageContextProvider;
    private readonly IMessageContextRegistry _messageContextRegistry;
    private readonly MessagingOptions _messagingOptions;
    private readonly IModuleClient _moduleClient;
    private readonly DbSet<OutboxMessage> _set;

    public bool Enabled { get; }

    public EfOutbox(T dbContext, IMessageContextRegistry messageContextRegistry,
        IMessageContextProvider messageContextProvider, IClock clock, IModuleClient moduleClient,
        IAsyncMessageDispatcher asyncMessageDispatcher, IJsonSerializer jsonSerializer,
        MessagingOptions messagingOptions, OutboxOptions outboxOptions, ILogger<EfOutbox<T>> logger)
    {
        _dbContext = dbContext;
        _set = dbContext.Set<OutboxMessage>();
        _messageContextRegistry = messageContextRegistry;
        _messageContextProvider = messageContextProvider;
        _clock = clock;
        _moduleClient = moduleClient;
        _asyncMessageDispatcher = asyncMessageDispatcher;
        _jsonSerializer = jsonSerializer;
        _messagingOptions = messagingOptions;
        _logger = logger;
        Enabled = outboxOptions.Enabled;
    }

    public async Task SaveAsync(params IMessage[] messages)
    {
        string module = _dbContext.GetModuleName();
        if (!Enabled)
        {
            _logger.LogWarning($"Outbox is disabled ('{module}'), outgoing messages won't be saved.");
            return;
        }

        if (messages is null || !messages.Any())
        {
            _logger.LogWarning($"No messages have been provided to be saved to the outbox ('{module}').");
            return;
        }

        OutboxMessage[] outboxMessages = messages.Where(x => x is not null)
            .Select(x =>
            {
                IMessageContext context = _messageContextProvider.Get(x);
                return new OutboxMessage
                {
                    Id = context.MessageId,
                    CorrelationId = context.Context.CorrelationId,
                    Name = x.GetType().Name.Underscore(),
                    Data = _jsonSerializer.Serialize((object)x),
                    Type = x.GetType().AssemblyQualifiedName,
                    CreatedAt = _clock.CurrentDate(),
                    TraceId = context.Context.TraceId,
                    UserId = context.Context.Identity.Id
                };
            }).ToArray();

        if (!outboxMessages.Any())
        {
            _logger.LogWarning($"No messages have been provided to be saved to the outbox ('{module}').");
            return;
        }

        await _set.AddRangeAsync(outboxMessages);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation($"Saved {outboxMessages.Length} messages to the outbox ('{module}').");
    }

    public async Task PublishUnsentAsync()
    {
        string module = _dbContext.GetModuleName();
        if (!Enabled)
        {
            _logger.LogWarning($"Outbox is disabled ('{module}'), outgoing messages won't be sent.");
            return;
        }

        List<OutboxMessage> unsentMessages = await _set.Where(x => x.SentAt == null).ToListAsync();
        if (!unsentMessages.Any())
        {
            _logger.LogTrace($"No unsent messages found in outbox ('{module}').");
            return;
        }

        _logger.LogTrace($"Found {unsentMessages.Count} unsent messages in outbox ('{module}'), sending...");
        foreach (OutboxMessage outboxMessage in unsentMessages)
        {
            var type = Type.GetType(outboxMessage.Type);
            var message = _jsonSerializer.Deserialize(outboxMessage.Data, type) as IMessage;
            if (message is null)
            {
                _logger.LogError($"Invalid message type in outbox ('{module}'): '{type.Name}', name: '{outboxMessage.Name}', " +
                                 $"ID: '{outboxMessage.Id}' ('{module}').");
                continue;
            }

            Guid messageId = outboxMessage.Id;
            Guid correlationId = outboxMessage.CorrelationId;
            DateTime sentAt = _clock.CurrentDate();
            string name = message.GetType().Name.Underscore();
            _messageContextRegistry.Set(message, new MessageContext(messageId, new Context(correlationId, outboxMessage.TraceId,
                new IdentityContext(outboxMessage.UserId))));

            _logger.LogInformation("Publishing a message from outbox ('{Module}'): {Name} [Message ID: {MessageId}, Correlation ID: {CorrelationId}]...",
                module, name, messageId, correlationId);

            if (_messagingOptions.UseAsyncDispatcher)
            {
                await _asyncMessageDispatcher.PublishAsync(message);
            }
            else
            {
                await _moduleClient.PublishAsync(message);
            }

            outboxMessage.SentAt = sentAt;
            _set.Update(outboxMessage);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task CleanupAsync(DateTime? to = null)
    {
        string module = _dbContext.GetModuleName();
        if (!Enabled)
        {
            _logger.LogWarning($"Outbox is disabled ('{module}'), outgoing messages won't be cleaned up.");
            return;
        }

        DateTime dateTo = to ?? _clock.CurrentDate();
        List<OutboxMessage> sentMessages = await _set.Where(x => x.SentAt != null && x.CreatedAt <= dateTo).ToListAsync();
        if (!sentMessages.Any())
        {
            _logger.LogTrace($"No sent messages found in outbox ('{module}') till: {dateTo}.");
            return;
        }

        _logger.LogTrace($"Found {sentMessages.Count} sent messages in outbox ('{module}') till: {dateTo}, cleaning up...");
        _set.RemoveRange(sentMessages);
        await _dbContext.SaveChangesAsync();
        _logger.LogTrace($"Removed {sentMessages.Count} sent messages from outbox ('{module}') till: {dateTo}.");
    }
}