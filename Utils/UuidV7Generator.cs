using System.Security.Cryptography;

namespace DataPersistentApi.Utils;

public static class UuidV7Generator
{
    public static string NewUuidV7()
    {
        // epoch milliseconds (48 bits)
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var bytes = new byte[16];
        // put milliseconds into first 6 bytes (big-endian)
        bytes[0] = (byte)((ms >> 40) & 0xFF);
        bytes[1] = (byte)((ms >> 32) & 0xFF);
        bytes[2] = (byte)((ms >> 24) & 0xFF);
        bytes[3] = (byte)((ms >> 16) & 0xFF);
        bytes[4] = (byte)((ms >> 8) & 0xFF);
        bytes[5] = (byte)(ms & 0xFF);
        // fill remaining 10 bytes with crypto-random
        RandomNumberGenerator.Fill(bytes.AsSpan(6, 10));
        // set version 7 (top 4 bits of byte 6) and variant (RFC 4122)
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant
        var guid = new Guid(bytes);
        return guid.ToString();
    }
}





