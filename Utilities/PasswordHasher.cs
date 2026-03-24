using System.Security.Cryptography;
using System.Text;

namespace Eventify.Utilities;

public static class PasswordHasher
{
    public static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
