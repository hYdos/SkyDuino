// See https://aka.ms/new-console-template for more information

using SerialApi;

var controller = new NfcController();
Console.WriteLine("Nfc Controller Ready");
const string correctResult = "0.1.0";

controller.SendDataExpectResult(new byte[] { 0 }, stream => {
    var major = stream.ReadByte();
    var minor = stream.ReadByte();
    var patch = stream.ReadByte();
    Console.WriteLine($"Version {major}.{minor}.{patch}");
});