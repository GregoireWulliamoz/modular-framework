namespace Modular.Infrastructure.Messaging.Outbox;

public class InboxTypeRegistry
{
    private readonly Dictionary<string, Type> _types = new();

    private static string GetKey<T>() => GetKey(typeof(T));

    private static string GetKey(Type type)
        => type.IsGenericType
            ? $"{type.GenericTypeArguments[0].GetModuleName()}"
            : $"{type.GetModuleName()}";

    public void Register<T>() where T : IInbox => _types[GetKey<T>()] = typeof(T);

    public Type Resolve<T>() => _types.TryGetValue(GetKey<T>(), out Type type) ? type : null;
}