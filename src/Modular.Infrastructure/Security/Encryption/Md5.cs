using System.Security.Cryptography;
using System.Text;

namespace Modular.Infrastructure.Security.Encryption;

public sealed class Md5 : IMd5
{
    public string Calculate(string value)
    {
        using var md5Generator = MD5.Create();
        byte[] hash = md5Generator.ComputeHash(Encoding.ASCII.GetBytes(value));
        var stringBuilder = new StringBuilder();
        foreach (byte @byte in hash)
        {
            stringBuilder.Append(@byte.ToString("X2"));
        }

        return stringBuilder.ToString().ToLowerInvariant();
    }
}