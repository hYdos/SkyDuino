using SerialApi;

namespace CLI;

internal static class Program {

    public static void Main(string[] args) {
        var controller = new NfcController();
        Console.WriteLine("Native Version " + controller.GetNativeVersion());
        Console.WriteLine("Waiting for tag");

        while (true) {
            if (!controller.IsNewTagPresent()) continue;
            if (!controller.SelectTag()) continue;
            
            Console.WriteLine("Found new tag");
            controller.AuthenticateSector(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 3, KeyType.KeyB);
        }
    }
}