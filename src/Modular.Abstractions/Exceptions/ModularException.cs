using System.Runtime.Serialization;

namespace Modular.Abstractions.Exceptions;

[Serializable]
public abstract class ModularException : Exception
{
    protected ModularException(string message) : base(message)
    {
    }

    protected ModularException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}