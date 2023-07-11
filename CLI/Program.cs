using CLI.nfc;
using SerialApi;
using SerialApi.error;

namespace CLI;

internal static class Program {

    public static void Main(string[] args) {
        var arduino = new SkyDuino();
        Console.WriteLine("Native Version " + arduino.GetNativeVersion());
        Console.WriteLine("Waiting for tag");

        while (true) {
            // TODO: instead do safety check to make sure user is ready so nfc tag doesnt move
            if (!arduino.IsNewTagPresent()) continue;
            if (!arduino.SelectTag()) continue;
            var tag = NfcTag.Get(arduino.GetUid(), arduino);
            tag.FillKeys();

            Console.WriteLine("Found new tag");
            Console.WriteLine($"Card Uid: {BitConverter.ToString(tag.Uid)}");

            WriteTag(File.ReadAllBytes("C:/Users/hydos/Downloads/0D_3C_D5_DF_hydos_scarlet_ninjini"), tag);
            return;
        }
    }

    private static void DumpTag(NfcTag tag) {
        var dump = new List<byte>();
        for (byte i = 0; i < 64; i++) {
            Console.WriteLine($"Reading block {i}");
            var block = tag.ReadBlock(i)[..16];
            dump.AddRange(block);
        }

        File.WriteAllBytes($"{BitConverter.ToString(tag.Uid)}.dump", dump.ToArray());
    }

    private static void WriteTag(byte[] dump, NfcTag tag, bool generateKeyA = false) {
        for (byte i = 1; i < 64; i++) {
            Console.WriteLine($"Writing block {i}");
            var sector = (byte)Math.Floor((decimal)i / 4);
            var blockOffsetInDump = i * 16;
            var blockEnd = blockOffsetInDump + 16;
            var dumpBlock = dump[blockOffsetInDump..blockEnd];
            var isKeyBlock = i % 4 == 3;
            if (isKeyBlock && generateKeyA) dumpBlock = tag.KeyA[sector].Concat(dumpBlock[6..]).ToArray();

            try {
                tag.WriteBlock(i, dump[blockOffsetInDump..blockEnd]);
            }
            catch (AuthenticationException) {
                tag.RemoveTimeout();
                Console.WriteLine("Using Fallback KeyA");
                tag.WriteBlock(i, dump[blockOffsetInDump..blockEnd], KeyType.KeyA);
            }

            if (isKeyBlock) {
                Console.WriteLine("Skipping Key Block Verification");
                continue;
            }

            Console.WriteLine($"Verifying block {i}");
            var block = tag.ReadBlock(i)[..16];
            if (!dumpBlock.SequenceEqual(block)) {
                Console.WriteLine("Write Fail :(");
                Console.WriteLine("Expected: [{0}]", BitConverter.ToString(dumpBlock).Replace("-", " "));
                Console.WriteLine("Received: [{0}]", BitConverter.ToString(block).Replace("-", " "));
            }
            else Console.WriteLine("Write Success!");
        }

        File.WriteAllBytes($"{BitConverter.ToString(tag.Uid)}.dump", dump.ToArray());
    }

    private static void TestRetryAbuse(SkyDuino arduino) {
        for (var i = 0; i < 16; i++) {
            try {
                arduino.AuthenticateSector(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, (byte)(i * 4 + 1), KeyType.KeyB);
                Console.WriteLine("authenticated");
            }
            catch (AuthenticationException e) {
                arduino.ResetRc522();
                if (!arduino.IsNewTagPresent()) continue;
                if (!arduino.SelectTag()) continue;
                Console.WriteLine(e.Message);
            }
        }
    }

    private static string ReadAccessBits(byte[] bytes) {
        // Convert the bytes to binary strings
        var binary1 = Convert.ToString(bytes[0], 2).PadLeft(8, '0');
        var binary2 = Convert.ToString(bytes[1], 2).PadLeft(8, '0');
        var binary3 = Convert.ToString(bytes[2], 2).PadLeft(8, '0');

        // Concatenate the binary strings
        var concatenatedBits = binary1 + binary2 + binary3;

        // Extract the access bits for each block
        var block1AccessBits = concatenatedBits.Substring(0, 8);
        var block2AccessBits = concatenatedBits.Substring(8, 8);
        var block3AccessBits = concatenatedBits.Substring(16, 8);

        // Combine the access bits for all blocks
        var accessBits = block1AccessBits + " " + block2AccessBits + " " + block3AccessBits;

        return accessBits;
    }
}