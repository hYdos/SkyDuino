using SerialApi;

namespace CLI;

internal static class Program {
    private static readonly byte[] WrittenSkylanderKeyB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    private static readonly byte[] FactoryKeyAll = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

    public static void Main(string[] args) {
        var arduino = new SkyDuino();
        Console.WriteLine("Native Version " + arduino.GetNativeVersion());
        Console.WriteLine("Waiting for tag");

        while (true) {
            if (!arduino.IsNewTagPresent()) continue;
            if (!arduino.SelectTag()) continue;

            Console.WriteLine("Found new tag");
            Console.WriteLine($"Card Uid: {BitConverter.ToString(arduino.GetUid())}");

            arduino.AuthenticateSector(WrittenSkylanderKeyB, 3, KeyType.KeyB);
            Console.WriteLine($"Block 3: {BitConverter.ToString(arduino.ReadBlock(3)[..15])}");
            return;
        }
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
}