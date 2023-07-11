using CLI.nfc;
using CLI.skylanders;
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
            
            Console.WriteLine("Found new tag");
            Console.WriteLine($"Card Uid: {BitConverter.ToString(tag.Uid)}");
            
            WriteTag(File.ReadAllBytes("D:/Projects/hYdos/SkylanderWriter/CLI/bin/Debug/net7.0/AD-42-D5-DF.dump"), tag, true);
            // DumpTag(tag);
            return;
        }
    }

    private static void DumpTag(NfcTag tag) {
        var dump = new List<byte>();
        for (byte i = 0; i < 64; i++) {
            var sector = (byte)Math.Floor((decimal)i / 4);
            var isKeyBlock = i % 4 == 3;
            Console.WriteLine($"Reading block {i}");
            var block = tag.ReadBlock(i)[..16];
            dump.AddRange(isKeyBlock ? tag.KeyA[sector].Concat(block[6..10]).Concat(tag.KeyB[sector]).ToArray() : block);
        }

        File.WriteAllBytes($"{BitConverter.ToString(tag.Uid)}.dump", dump.ToArray());
    }

    private static void WriteTag(byte[] dump, NfcTag tag, bool generateKeyA = false) {
        for (byte i = 1; i < 64; i++) { // ignore uid and other info block
            Console.WriteLine($"Writing block {i}");
            var sector = (byte)Math.Floor((decimal)i / 4);
            var blockOffsetInDump = i * 16;
            var blockEnd = blockOffsetInDump + 16;
            var dumpBlock = dump[blockOffsetInDump..blockEnd];
            var isKeyBlock = i % 4 == 3;
            if (isKeyBlock && generateKeyA) dumpBlock = SkyKeyGen.CalcKeyA(tag.Uid, sector).Concat(dumpBlock[6..10]).Concat(NfcTag.SkylanderKeyB).ToArray();

            try {
                tag.WriteBlock(i, dumpBlock);
            }
            catch (AuthenticationException) {
                try {
                    tag.RemoveTimeout();
                    Console.WriteLine("Using Fallback KeyA");
                    tag.WriteBlock(i, dumpBlock, KeyType.KeyA);
                }
                catch (WriteException e) {
                    if (e.Result != 255) throw;
                    Console.WriteLine("Cannot write to read only block :(");
                    continue;
                }
            }
            catch (WriteException) {
                try {
                    tag.RemoveTimeout();
                    Console.WriteLine("Using Fallback KeyA");
                    tag.WriteBlock(i, dumpBlock);
                }
                catch (WriteException e) {
                    if (e.Result != 255) throw;
                    Console.WriteLine("Cannot write to read only block :(");
                    continue;
                }
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