namespace CLI.skylanders;

public class SkyKeyGen {
    private const ulong Poly = 0x42f0e1eba9ea3693;
    private const ulong Msb = 0x800000000000;
    private const ulong Trim = 0xffffffffffff;
    private static readonly ulong[] MagicNums = { 2, 3, 73, 1103, 2017, 560381651, 12868356821 };

    public static byte[] CalcKeyA(byte[] uid, int sector) {
        if (sector == 0) return BitConverter.GetBytes(MagicNums[2] * MagicNums[4] * MagicNums[5]);
        if (uid.Length != 4) throw new ArgumentException("Invalid UID Length");
        if (sector is < 0 or > 15) throw new ArgumentException("Invalid sector (0-15)");

        var crc = MagicNums[0] * MagicNums[0] * MagicNums[1] * MagicNums[3] * MagicNums[6];
        var data = uid.Concat(new[] { (byte)sector }).ToArray();

        foreach (var b in data) {
            crc ^= (ulong)b << 40;
            for (var k = 0; k < 8; k++) {
                if ((crc & Msb) != 0)
                    crc = (crc << 1) ^ Poly;
                else
                    crc <<= 1;

                crc &= Trim;
            }
        }

        return BitConverter.GetBytes(crc)
            .Reverse()
            .Skip(2)
            .ToArray();
    }
}