using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AegisVault.Core.Services;

public enum TotpAlgorithm
{
    Sha1,
    Sha256,
    Sha512,
}

/// <summary>
/// RFC 6238 (TOTP) and RFC 4648 Base32 helpers.
/// </summary>
public static class TotpService
{
    public const int DefaultDigits = 6;
    public const int DefaultPeriodSeconds = 30;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateCode(
        ReadOnlySpan<byte> secret,
        DateTimeOffset timestamp,
        int digits = DefaultDigits,
        int periodSeconds = DefaultPeriodSeconds,
        TotpAlgorithm algorithm = TotpAlgorithm.Sha1)
    {
        if (digits is < 6 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be between 6 and 8.");
        }

        if (periodSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodSeconds), "Period must be positive.");
        }

        if (secret.IsEmpty)
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secret));
        }

        var counter = timestamp.ToUnixTimeSeconds() / periodSeconds;

        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        Span<byte> hash = stackalloc byte[64];
        using var hmac = CreateHmac(algorithm, secret);
        hmac.TryComputeHash(counterBytes, hash, out var written);

        var offset = hash[written - 1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var modulus = (int)Math.Pow(10, digits);
        var otp = binary % modulus;
        return otp.ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    public static string GenerateCode(
        string base32Secret,
        DateTimeOffset timestamp,
        int digits = DefaultDigits,
        int periodSeconds = DefaultPeriodSeconds,
        TotpAlgorithm algorithm = TotpAlgorithm.Sha1)
    {
        var secret = DecodeBase32(base32Secret);
        try
        {
            return GenerateCode(secret, timestamp, digits, periodSeconds, algorithm);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>Seconds until the current TOTP period expires.</summary>
    public static int GetRemainingSeconds(DateTimeOffset timestamp, int periodSeconds = DefaultPeriodSeconds)
    {
        if (periodSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodSeconds), "Period must be positive.");
        }

        var elapsed = timestamp.ToUnixTimeSeconds() % periodSeconds;
        return periodSeconds - (int)elapsed;
    }

    /// <summary>Decodes a Base32 string (RFC 4648, padding and spaces tolerated).</summary>
    public static byte[] DecodeBase32(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var normalized = input.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new FormatException("Base32 input is empty.");
        }

        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var character in normalized)
        {
            var value = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException($"Invalid Base32 character '{character}'.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;

            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }

    public static string EncodeBase32(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;

            while (bits >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        }

        return builder.ToString();
    }

    private static HMAC CreateHmac(TotpAlgorithm algorithm, ReadOnlySpan<byte> secret)
    {
        var key = secret.ToArray();
        try
        {
            return algorithm switch
            {
                TotpAlgorithm.Sha1 => new HMACSHA1(key),
                TotpAlgorithm.Sha256 => new HMACSHA256(key),
                TotpAlgorithm.Sha512 => new HMACSHA512(key),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
