using System.CommandLine;
using System.Diagnostics;
using CLI.nfc;
using SerialApi;
using SerialApi.error;

namespace CLI;

internal static class Program {
    private static SkyDuino? _arduino;

    private static void Setup(bool selectTag = true) {
        Console.WriteLine("Setting up... Now is a good time to put your Tag on the reader");
        _arduino = new SkyDuino();
        var version = _arduino.GetNativeVersion();
        if (version != "0.2.1") throw new Exception($"Native Version mismatch. Expected Arduino running 0.2.1 but got {version}");
        Console.WriteLine("Native Version " + _arduino.GetNativeVersion());

        if (selectTag) {
            if (!_arduino.IsNewTagPresent()) Console.WriteLine("Tag was not present. put your tag on the reader first before running anything");
            if (!_arduino.SelectTag()) Console.WriteLine("Tag was not present. put your tag on the reader first before running anything");
        }
    }

    public static int Main(string[] args) {
        var rootCommand = new RootCommand("CLI For using the SkyDuino");

        // Shared Argument Options
        var unlockBlock0Option = new Option<bool>(
            name: "--unlock-block-0",
            description: "Enables writing to block0. Useful for full dumps and if you have a \"Magic\" tag",
            getDefaultValue: () => false);

        unlockBlock0Option.AddAlias("-ub0");

        // Dump Argument Options
        var outputArgument = new Option<string?>(
            name: "--output",
            description: "The output file path",
            getDefaultValue: () => null);

        outputArgument.AddAlias("-o");

        var dumpCommand = new Command("dump", "Dump's the current tag to a file") {
            outputArgument,
            unlockBlock0Option
        };

        // Write Argument Options
        var generateSkylanderKeysOption = new Option<bool>(
            name: "--gen-sky-keys",
            description: "Generates the keys needed to be recognised as a Skylander",
            getDefaultValue: () => false);

        unlockBlock0Option.AddAlias("-ub0");

        var ignoreFailuresOption = new Option<bool>(
            name: "--ignore-failures",
            description: "Just like my father",
            getDefaultValue: () => false);

        ignoreFailuresOption.AddAlias("-f");

        var disableSafetyOption = new Option<bool>(
            name: "--no-protection",
            description: "Turns off all pre-emptive safety. DO NOT TURN THIS ON",
            getDefaultValue: () => false);

        disableSafetyOption.AddAlias("-np");

        var inputArgument = new Argument<string>(
            name: "--input",
            description: "The dump to write to the tag");

        var write = new Command("write", "Writes a dump file to a tag") {
            inputArgument,
            unlockBlock0Option,
            disableSafetyOption,
            generateSkylanderKeysOption,
            ignoreFailuresOption
        };

        // Reset Command Options
        var reset = new Command("reset", "Does its best to reset the tag to factory defaults. Only works on Magic tags");

        dumpCommand.SetHandler(DumpTag, outputArgument, unlockBlock0Option);
        write.SetHandler(WriteTag, inputArgument, unlockBlock0Option, disableSafetyOption, generateSkylanderKeysOption, ignoreFailuresOption);
        reset.SetHandler(ResetTag);
        rootCommand.AddCommand(dumpCommand);
        rootCommand.AddCommand(write);
        rootCommand.AddCommand(reset);
        return rootCommand.InvokeAsync(args).Result;
    }

    private static void ResetTag() {
        Setup(false);
        
        Console.WriteLine("This will write to ANY tag nearby the reader until the program is closed. Make sure you know what you are doing");
        // not in SkyDuino.cs just in case
        var result = _arduino!.SendDataExpectResult(new[] { (byte) Functions.FactoryResetTag }, 1)[0];
        if (result != 0 && result != 67) Console.WriteLine(new WriteException(result).Message);
        else if (result == 67) Console.WriteLine("Failed to open backdoor. card prob not there");
        Console.WriteLine("Reset Done");
    }

    private static void DumpTag(string? output, bool canWriteBlock0) {
        Setup();
        var tag = NfcTag.Get(_arduino!.GetUid(), _arduino, canWriteBlock0);
        tag.SetActive(canWriteBlock0);
        Console.WriteLine($"Card Uid: {BitConverter.ToString(tag.Uid)}");
        output ??= $"{BitConverter.ToString(tag.Uid)}.dump";
        var stopwatch = new Stopwatch();

        stopwatch.Start();
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

        File.WriteAllBytes(output, dump.ToArray());
        stopwatch.Stop();
        Console.WriteLine($"Dump took {stopwatch.ElapsedMilliseconds}ms");
    }

    // TODO: do this sector by sector
    private static void WriteTag(string inputDump, bool writeBlock0, bool disableSafety, bool genSkyKeys, bool ignoreFails) {
        Setup();
        var tag = NfcTag.Get(_arduino!.GetUid(), _arduino, writeBlock0);
        tag.SetActive(writeBlock0);
        Console.WriteLine($"Card Uid: {BitConverter.ToString(tag.Uid)}");
        var startOffset = (byte)(writeBlock0 ? 0 : 1);
        var dump = File.ReadAllBytes(inputDump);
        var stopwatch = new Stopwatch();

        stopwatch.Start();
        for (var i = startOffset; i < 64; i++) {
            // ignore uid and other info block
            Console.WriteLine($"Writing block {i}");
            var sector = (byte)Math.Floor((decimal)i / 4);
            var blockOffsetInDump = i * 16;
            var blockEnd = blockOffsetInDump + 16;
            var dumpBlock = dump[blockOffsetInDump..blockEnd];
            var isKeyBlock = i % 4 == 3;
            if (isKeyBlock && genSkyKeys) dumpBlock = SkyKeyGen.CalcKeyA(tag.Uid, sector).Concat(dumpBlock[6..10]).Concat(NfcTag.SkylanderKeyB).ToArray();

            // Step 1: Write
            if (writeBlock0) {
                tag.WriteBlock(i, dumpBlock, disableSafety, KeyType.KeyA);
            }
            else {
                try {
                    Console.WriteLine("Trying write with KeyB");
                    tag.WriteBlock(i, dumpBlock, disableSafety);
                }
                catch (Exception) {
                    try {
                        Console.WriteLine("Trying write with KeyA");
                        tag.WriteBlock(i, dumpBlock, disableSafety, KeyType.KeyA);
                    }
                    catch (Exception exception) {
                        if (exception is AuthenticationException) Console.WriteLine("We cant write to a read only block :(");
                        if (!ignoreFails) throw;
                    }
                }
            }

            // Step 2: Verify
            Console.WriteLine($"Verifying block {i}");
            var block = tag.ReadBlock(i)[..16];
            if (!isKeyBlock && !dumpBlock.SequenceEqual(block)) {
                Console.WriteLine("Write Fail :(");
                Console.WriteLine("Expected: [{0}]", BitConverter.ToString(dumpBlock).Replace("-", " "));
                Console.WriteLine("Received: [{0}]", BitConverter.ToString(block).Replace("-", " "));
                Console.WriteLine("Write Encountered an error and has been stopped.");
                return;
            }

            Console.WriteLine(isKeyBlock ? "Just hope your Key block is ok" : "Write Success!");
        }

        stopwatch.Stop();
        Console.WriteLine($"Write took {stopwatch.ElapsedMilliseconds}ms");
    }
}