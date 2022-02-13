using System.Security.Cryptography;
using System.Text;

namespace Modular.Infrastructure.Security.Encryption;

public sealed class Hasher : IHasher
{
    public string Hash(string data)
    {
        using var sha512 = SHA512.Create();
        byte[] bytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(data));
        var builder = new StringBuilder();
        foreach (byte @byte in bytes)
        {
            builder.Append(@byte.ToString("x2"));
        }

        return builder.ToString();
    }
}