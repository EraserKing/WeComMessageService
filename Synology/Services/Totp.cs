using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Synology.Services
{
    /// <summary>
    /// Minimal RFC 6238 TOTP generator, used when DSM 2-step verification is enabled with an authenticator app.
    /// </summary>
    public static class Totp
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string GenerateCode(string base32Secret, DateTimeOffset timestamp, int digits = 6, int period = 30)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
            ArgumentOutOfRangeException.ThrowIfLessThan(digits, 6);
            ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

            byte[] key = DecodeBase32(base32Secret);
            if (key.Length == 0)
            {
                throw new FormatException("OTP secret is empty after Base32 decoding.");
            }

            long counter = timestamp.ToUnixTimeSeconds() / period;
            Span<byte> counterBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

            byte[] hash = HMACSHA1.HashData(key, counterBytes);
            int offset = hash[^1] & 0x0F;
            int binary = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

            int modulus = (int)Math.Pow(10, digits);
            return (binary % modulus).ToString(new string('0', digits));
        }

        public static byte[] DecodeBase32(string input)
        {
            var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '=').ToArray()).ToUpperInvariant();
            var output = new List<byte>(cleaned.Length * 5 / 8);
            int buffer = 0;
            int bitsInBuffer = 0;

            foreach (char c in cleaned)
            {
                int index = Base32Alphabet.IndexOf(c);
                if (index < 0)
                {
                    throw new FormatException($"Invalid Base32 character '{c}' in OTP secret.");
                }

                buffer = (buffer << 5) | index;
                bitsInBuffer += 5;
                if (bitsInBuffer >= 8)
                {
                    bitsInBuffer -= 8;
                    output.Add((byte)(buffer >> bitsInBuffer));
                    buffer &= (1 << bitsInBuffer) - 1;
                }
            }

            return output.ToArray();
        }
    }
}
