using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Modular.Infrastructure.Services;

public class DbContextAppInitializer : IHostedService
{
    private readonly ILogger<DbContextAppInitializer> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DbContextAppInitializer(IServiceProvider serviceProvider, ILogger<DbContextAppInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IEnumerable<Type> dbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(DbContext).IsAssignableFrom(x) && !x.IsInterface && x != typeof(DbContext));

        using IServiceScope scope = _serviceProvider.CreateScope();
        foreach (Type dbContextType in dbContextTypes)
        {
            var dbContext = scope.ServiceProvider.GetService(dbContextType) as DbContext;
            if (dbContext is null)
            {
                continue;
            }

            _logger.LogInformation("Running DB context for module {Module}...", dbContextType.GetModuleName());
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        IEnumerable<IInitializer> initializers = scope.ServiceProvider.GetServices<IInitializer>();
        foreach (IInitializer initializer in initializers)
        {
            try
            {
                _logger.LogInformation($"Running the initializer: {initializer.GetType().Name}...");
                await initializer.InitAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}