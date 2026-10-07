using System.Security.Cryptography;

namespace Vagalume.Postgres.Embedded;

/// <summary>
/// <see cref="ISecretGenerator"/> that returns 256 random bits as hexadecimal text, safe to embed in a connection string.
/// </summary>
public sealed class RandomSecretGenerator : ISecretGenerator
{
    public string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}
