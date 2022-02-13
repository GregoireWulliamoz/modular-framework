namespace Modular.Infrastructure.Postgres;

public class UnitOfWorkTypeRegistry
{
    private readonly Dictionary<string, Type> _types = new();

    private static string GetKey<T>() => $"{typeof(T).GetModuleName()}";

    public void Register<T>() where T : IUnitOfWork => _types[GetKey<T>()] = typeof(T);

    public Type Resolve<T>() => _types.TryGetValue(GetKey<T>(), out Type type) ? type : null;
}