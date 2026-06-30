using System;
using System.Collections.Generic;

public static class Bech32
{
    private const string Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    public static string Encode(byte[] data, bool isTestnet)
    {
        string hrp = isTestnet ? "addr_test" : "addr";
        byte[] values = ConvertBits(data, 8, 5);
        byte[] checksum = CreateChecksum(hrp, values);
        byte[] combined = new byte[values.Length + checksum.Length];
        Array.Copy(values, 0, combined, 0, values.Length);
        Array.Copy(checksum, 0, combined, values.Length, checksum.Length);
        char[] result = new char[combined.Length];
        for (int i = 0; i < combined.Length; i++) { result[i] = Charset[combined[i]]; }
        return hrp + "1" + new string(result);
    }

    public static string Encode(byte[] data, string hrp)
    {
        byte[] values = ConvertBits(data, 8, 5);
        byte[] checksum = CreateChecksum(hrp, values);
        byte[] combined = new byte[values.Length + checksum.Length];
        Array.Copy(values, 0, combined, 0, values.Length);
        Array.Copy(checksum, 0, combined, values.Length, checksum.Length);
        char[] result = new char[combined.Length];
        for (int i = 0; i < combined.Length; i++) { result[i] = Charset[combined[i]]; }
        return hrp + "1" + new string(result);
    }

    private static byte[] ConvertBits(byte[] data, int fromBits, int toBits)
    {
        int acc = 0;
        int bits = 0;
        List<byte> result = new List<byte>();
        int maxv = (1 << toBits) - 1;
        foreach (byte value in data)
        {
            if ((value >> fromBits) != 0) return null;
            acc = (acc << fromBits) | value;
            bits += fromBits;
            while (bits >= toBits)
            {
                bits -= toBits;
                result.Add((byte)((acc >> bits) & maxv));
            }
        }
        if (bits > 0) { result.Add((byte)((acc << (toBits - bits)) & maxv)); }
        return result.ToArray();
    }

    private static byte[] CreateChecksum(string hrp, byte[] values)
    {
        byte[] hrpExpanded = ExpandHrp(hrp);
        byte[] combined = new byte[hrpExpanded.Length + values.Length + 6];
        Array.Copy(hrpExpanded, 0, combined, 0, hrpExpanded.Length);
        Array.Copy(values, 0, combined, hrpExpanded.Length, values.Length);
        uint mod = Polymod(combined) ^ 1;
        byte[] ret = new byte[6];
        for (int i = 0; i < 6; i++) { ret[i] = (byte)((mod >> (5 * (5 - i))) & 31); }
        return ret;
    }

    private static uint Polymod(byte[] values)
    {
        uint chk = 1;
        foreach (byte v in values)
        {
            uint top = chk >> 25;
            chk = (chk & 0x1ffffff) << 5 ^ v;
            for (int i = 0; i < 5; i++) { if (((top >> i) & 1) != 0) chk ^= Generator[i]; }
        }
        return chk;
    }

    private static readonly uint[] Generator = { 0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3 };

    private static byte[] ExpandHrp(string hrp)
    {
        byte[] result = new byte[2 * hrp.Length + 1];
        for (int i = 0; i < hrp.Length; i++)
        {
            result[i] = (byte)(hrp[i] >> 5);
            result[hrp.Length + 1 + i] = (byte)(hrp[i] & 31);
        }
        result[hrp.Length] = 0;
        return result;
    }

    public static byte[] HexStringToByteArray(string hex)
    {
        if (hex.Length % 2 != 0) throw new ArgumentException("Hex string must have an even length.");
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2) bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }
}