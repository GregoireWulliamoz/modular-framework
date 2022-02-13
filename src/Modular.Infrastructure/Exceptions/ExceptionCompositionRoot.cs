using Microsoft.Extensions.DependencyInjection;
using Modular.Abstractions.Exceptions;

namespace Modular.Infrastructure.Exceptions;

public class ExceptionCompositionRoot : IExceptionCompositionRoot
{
    private readonly IServiceProvider _serviceProvider;

    public ExceptionCompositionRoot(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ExceptionResponse Map(Exception exception)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IExceptionToResponseMapper[] mappers = scope.ServiceProvider.GetServices<IExceptionToResponseMapper>().ToArray();
        IEnumerable<IExceptionToResponseMapper> nonDefaultMappers = mappers.Where(x => x is not ExceptionToResponseMapper);
        ExceptionResponse result = nonDefaultMappers
            .Select(x => x.Map(exception))
            .SingleOrDefault(x => x is not null);

        if (result is not null)
        {
            return result;
        }

        IExceptionToResponseMapper defaultMapper = mappers.SingleOrDefault(x => x is ExceptionToResponseMapper);

        return defaultMapper?.Map(exception);
    }
}