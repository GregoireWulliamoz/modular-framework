using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Events;

namespace Modular.Infrastructure.Postgres.Decorators;

[Decorator]
public class TransactionalEventHandlerDecorator<T> : IEventHandler<T> where T : class, IEvent
{
    private readonly IEventHandler<T> _handler;
    private readonly ILogger<TransactionalEventHandlerDecorator<T>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly UnitOfWorkTypeRegistry _unitOfWorkTypeTypeRegistry;

    public TransactionalEventHandlerDecorator(IEventHandler<T> handler, UnitOfWorkTypeRegistry unitOfWorkTypeTypeRegistry,
        IServiceProvider serviceProvider, ILogger<TransactionalEventHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _unitOfWorkTypeTypeRegistry = unitOfWorkTypeTypeRegistry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(T @event, CancellationToken cancellationToken = default)
    {
        Type unitOfWorkType = _unitOfWorkTypeTypeRegistry.Resolve<T>();
        if (unitOfWorkType is null)
        {
            await _handler.HandleAsync(@event, cancellationToken);
            return;
        }

        var unitOfWork = (IUnitOfWork)_serviceProvider.GetRequiredService(unitOfWorkType);
        string unitOfWorkName = unitOfWorkType.Name;
        string name = @event.GetType().Name.Underscore();
        _logger.LogInformation("Handling: {Name} using TX ({UnitOfWorkName})...", name, unitOfWorkName);
        await unitOfWork.ExecuteAsync(() => _handler.HandleAsync(@event, cancellationToken));
        _logger.LogInformation("Handled: {Name} using TX ({UnitOfWorkName})", name, unitOfWorkName);
    }
}