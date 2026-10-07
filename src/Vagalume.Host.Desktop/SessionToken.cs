using System.Security.Cryptography;
using System.Text;

namespace Vagalume.Host.Desktop;

/// <summary>
/// Random secret generated at every start that proves a request comes from the application window.
/// </summary>
public sealed class SessionToken
{
    private readonly byte[] _bytes;

    private SessionToken(string value)
    {
        Value = value;
        _bytes = Encoding.UTF8.GetBytes(value);
    }

    public string Value { get; }

    public static SessionToken Generate() => new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)));

    /// <summary>Compares in constant time so the check does not leak how much of the token matched.</summary>
    public bool Matches(string? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(candidate);
        return bytes.Length == _bytes.Length && CryptographicOperations.FixedTimeEquals(bytes, _bytes);
    }
}
