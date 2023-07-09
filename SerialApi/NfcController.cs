using System.IO.Ports;

namespace SerialApi;

public class NfcController {

    private readonly SerialPort _serialPort;

    public NfcController() {
        _serialPort = new SerialPort();
        _serialPort.PortName = "COM4";
        _serialPort.BaudRate = 115200;
        _serialPort.ReadTimeout = 3000;
        _serialPort.WriteTimeout = 1000;
        _serialPort.Open();

        // Toggle the DTR signal to simulate a reset
        _serialPort.DtrEnable = true;
        Thread.Sleep(100);
        _serialPort.DtrEnable = false;
        Thread.Sleep(3000); // 1 second more than the arduino to let it be ready after the reset

        // Read possibly junk data because we know it might be bad
        try {
            while (true) {
                Console.WriteLine(_serialPort.ReadLine().Replace("\r", ""));
            }
        }
        catch (TimeoutException) {
        }

        Console.WriteLine("Ready");
    }

    public string GetNativeVersion() {
        using var stream = new MemoryStream(SendDataExpectResult(new byte[] { 0x10 }, 3));
        var major = stream.ReadByte();
        var minor = stream.ReadByte();
        var patch = stream.ReadByte();
        return $"{major}.{minor}.{patch}";
    }

    public bool IsNewTagPresent() {
        using var stream = new MemoryStream(SendDataExpectResult(new byte[] { 0x60 }, 1, 100));
        return stream.ReadByte() == 1;
    }

    public bool SelectTag() {
        using var stream = new MemoryStream(SendDataExpectResult(new byte[] { 0x70 }, 1, 100));
        return stream.ReadByte() == 1;
    }

    public void AuthenticateSector(byte[] key, byte block, KeyType keyType) {
        var data = new byte[] { 0x30 }.Concat(key.Concat(new[] { block, (byte) (keyType == KeyType.KeyA ? 0x00 : 0x01) })).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        var result = stream.ReadByte();

        if (result != 0) throw new AuthenticationException(result, keyType);
    }
    
    public void SendData(byte[] data, int timeout = 500) {
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
    }

    public byte[] SendDataExpectResult(byte[] data, int expectedMinDataLength, int timeout = 500) {
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
        while (_serialPort.BytesToRead < expectedMinDataLength) Thread.Sleep(timeout);
        var readData = new byte[_serialPort.BytesToRead];
        _serialPort.Read(readData, 0, readData.Length);
        return readData;
    }
}