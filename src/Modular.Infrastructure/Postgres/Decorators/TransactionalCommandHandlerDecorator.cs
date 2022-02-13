using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modular.Abstractions.Commands;

namespace Modular.Infrastructure.Postgres.Decorators;

[Decorator]
public class TransactionalCommandHandlerDecorator<T> : ICommandHandler<T> where T : class, ICommand
{
    private readonly ICommandHandler<T> _handler;
    private readonly ILogger<TransactionalCommandHandlerDecorator<T>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly UnitOfWorkTypeRegistry _unitOfWorkTypeRegistry;

    public TransactionalCommandHandlerDecorator(ICommandHandler<T> handler, UnitOfWorkTypeRegistry unitOfWorkTypeRegistry,
        IServiceProvider serviceProvider, ILogger<TransactionalCommandHandlerDecorator<T>> logger)
    {
        _handler = handler;
        _unitOfWorkTypeRegistry = unitOfWorkTypeRegistry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(T command, CancellationToken cancellationToken = default)
    {
        Type unitOfWorkType = _unitOfWorkTypeRegistry.Resolve<T>();
        if (unitOfWorkType is null)
        {
            await _handler.HandleAsync(command, cancellationToken);
            return;
        }

        var unitOfWork = (IUnitOfWork)_serviceProvider.GetRequiredService(unitOfWorkType);
        string unitOfWorkName = unitOfWorkType.Name;
        string name = command.GetType().Name.Underscore();
        _logger.LogInformation("Handling: {Name} using TX ({UnitOfWorkName})...", name, unitOfWorkName);
        await unitOfWork.ExecuteAsync(() => _handler.HandleAsync(command, cancellationToken));
        _logger.LogInformation("Handled: {Name} using TX ({UnitOfWorkName})", name, unitOfWorkName);
    }
}