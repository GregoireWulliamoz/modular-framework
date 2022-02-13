using MessagePack;
using MessagePack.Resolvers;

namespace Modular.Infrastructure.Modules;

public class MessagePackModuleSerializer : IModuleSerializer
{
    private readonly MessagePackSerializerOptions _options =
        ContractlessStandardResolverAllowPrivate.Options;

    public byte[] Serialize<T>(T value) => MessagePackSerializer.Serialize(value, _options);

    public T Deserialize<T>(byte[] value) => MessagePackSerializer.Deserialize<T>(value, _options);

    public object Deserialize(byte[] value, Type type) => MessagePackSerializer.Deserialize(type, value, _options);
}