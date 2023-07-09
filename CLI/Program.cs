using SerialApi;

namespace CLI;

internal static class Program {

    public static void Main(string[] args) {
        var controller = new SkyDuino();
        Console.WriteLine("Native Version " + controller.GetNativeVersion());
        Console.WriteLine("Waiting for tag");

        while (true) {
            if (!controller.IsNewTagPresent()) continue;
            if (!controller.SelectTag()) continue;
            
            Console.WriteLine("Found new tag");
            
            // Test Retry abuse
            TestRetryAbuse(controller);
        }
    }

    private static void TestRetryAbuse(SkyDuino controller) {
        for (var i = 0; i < 10; i++) {
            Console.WriteLine($"Key attempt {i}");
            try {
                controller.ResetRc522();
                if (!controller.IsNewTagPresent()) continue;
                if (!controller.SelectTag()) continue;
                controller.AuthenticateSector(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 5, KeyType.KeyA);
            }
            catch (AuthenticationException e) {
                Console.WriteLine(e.Message);
            }
        }
    }
}