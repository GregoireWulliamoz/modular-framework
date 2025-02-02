using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Time;

namespace Modular.Infrastructure.Messaging.Outbox;

public class EfInbox<T> : IInbox where T : DbContext
{
    private readonly IClock _clock;
    private readonly T _dbContext;
    private readonly ILogger<EfInbox<T>> _logger;
    private readonly DbSet<InboxMessage> _set;
    private readonly bool _transactionsEnabled;

    public bool Enabled { get; }

    public EfInbox(T dbContext, IClock clock, OutboxOptions outboxOptions, ILogger<EfInbox<T>> logger)
    {
        _dbContext = dbContext;
        _set = dbContext.Set<InboxMessage>();
        _clock = clock;
        _logger = logger;
        Enabled = outboxOptions.Enabled;
        _transactionsEnabled = !outboxOptions.TransactionsDisabled;
    }

    public async Task HandleAsync(Guid messageId, string name, Func<Task> handler)
    {
        string module = _dbContext.GetModuleName();
        if (!Enabled)
        {
            _logger.LogWarning("Outbox is disabled ('{module}'), incoming messages won't be processed.", module);
            return;
        }

        bool saveToInbox = messageId != Guid.Empty;
        var inboxMessage = new InboxMessage
        {
            Id = messageId,
            Name = name,
            ReceivedAt = _clock.CurrentDate()
        };
        if (saveToInbox)
        {
            _logger.LogTrace("Received a message with ID: '{messageId}' to be processed ('{module}').", messageId, module);
            if (await _set.AnyAsync(m => m.Id == messageId && m.ProcessedAt != null))
            {
                _logger.LogTrace("Message with ID: '{messageId}' was already processed ('{module}').", messageId, module);
                return;
            }

            _logger.LogTrace("Processing a message with ID: '{messageId}' ('{module}')...", messageId, module);
            await _set.AddAsync(inboxMessage);
            await _dbContext.SaveChangesAsync();
        }

        IDbContextTransaction transaction = _transactionsEnabled ? await _dbContext.Database.BeginTransactionAsync() : null;
        try
        {
            await handler();
            inboxMessage.ProcessedAt = _clock.CurrentDate();
            _set.Update(inboxMessage);
            await _dbContext.SaveChangesAsync();

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            if (saveToInbox)
            {
                _logger.LogTrace("Processed a message with ID: '{messageId}' ('{module}').", messageId, module);
            }
        }
        catch (Exception ex)
        {
            if (saveToInbox)
            {
                _logger.LogError(ex, "There was an error when processing a message with ID: '{messageId}' ('{module}').", messageId, module);
            }

            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task CleanupAsync(DateTime? to = null)
    {
        string module = _dbContext.GetModuleName();
        if (!Enabled)
        {
            _logger.LogWarning("Outbox is disabled ('{module}'), incoming messages won't be cleaned up.", module);
            return;
        }

        DateTime dateTo = to ?? _clock.CurrentDate();
        List<InboxMessage> sentMessages = await _set.Where(x => x.ReceivedAt <= dateTo).ToListAsync();
        if (!sentMessages.Any())
        {
            _logger.LogTrace("No received messages found in inbox ('{module}') till: {dateTo}.", module, dateTo);
            return;
        }

        _logger.LogInformation("Found {count} received messages in inbox ('{module}') till: {dateTo}, cleaning up...", sentMessages.Count, module, dateTo);
        _set.RemoveRange(sentMessages);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Removed {count} received messages from inbox ('{module}') till: {dateTo}.", sentMessages.Count, module, dateTo);
    }
}