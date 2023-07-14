using System.CommandLine;
using System.Diagnostics;
using CLI.nfc;
using CLI.skylanders;
using SerialApi;
using SerialApi.error;

namespace CLI;

internal static class Program {
    
    public static int Main(string[] args) {
        // var rootCommand = new RootCommand("Sample app for System.CommandLine");
        //
        // var outputOption = new Option<string?>(
        //     name: "--output",
        //     description: "The output file path",
        //     getDefaultValue: () => null);
        //
        // var dumpFileOption = new Option<string?>(
        //     name: "--dump",
        //     description: "The dump file to write to the card",
        //     getDefaultValue: () => null);
        //
        // var disableSafety = new Option<bool>(
        //     name: "--i-live-on-the-edge",
        //     description: "Turns off all pre-emptive safety. DO NOT TURN THIS ON",
        //     getDefaultValue: () => false);
        //
        // var dumpCommand = new Command("dump", "Dump's the current tag to ") {
        //     outputOption
        // };
        //
        // dumpCommand.SetHandler(CmdDumpTag);
        //
        // rootCommand.AddCommand(dumpCommand);
        // return rootCommand.InvokeAsync(args).Result;

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
            
            // WriteTag(File.ReadAllBytes("D:/Projects/hYdos/SkylanderWriter/CLI/bin/Debug/net7.0/AD-42-D5-DF.dump"), tag, true);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            DumpTag(tag);
            stopwatch.Stop();
            Console.WriteLine($"Dump took {stopwatch.ElapsedMilliseconds}ms");
            return 0;
        }
    }

    private static void DumpTag(NfcTag tag) {
        var dump = new List<byte>();
        for (byte sector = 0; sector < 16; sector++) {
            Console.WriteLine($"Reading sector {sector}");
            var sectorData = tag.ReadSector(sector);
            
            for (byte block = 0; block < 4; block++) {
                var blockStart = 18 * block;
                var blockEnd = blockStart + 16;
                dump.AddRange(sectorData[blockStart..blockEnd]);
            }
        }

        File.WriteAllBytes($"{BitConverter.ToString(tag.Uid)}.dump", dump.ToArray());
    }

    private static void WriteTag(byte[] dump, NfcTag tag, bool generateKeyA = false) {
        for (byte i = 1; i < 64; i++) {
            // ignore uid and other info block
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
}