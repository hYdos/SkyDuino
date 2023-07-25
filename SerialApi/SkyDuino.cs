using System.IO.Ports;
using SerialApi.error;

namespace SerialApi;

public class SkyDuino {


    private readonly SerialPort _serialPort;

    public SkyDuino() {
        Console.WriteLine($"{string.Join(", ", SerialPort.GetPortNames())}");
        Console.WriteLine("Connecting to Arduino");
        _serialPort = new SerialPort();
        _serialPort.PortName = SerialPort.GetPortNames()[0];
        _serialPort.BaudRate = 57600;
        _serialPort.ReadTimeout = 4000;
        _serialPort.WriteTimeout = 1000;
        _serialPort.Open();

        _serialPort.DtrEnable = true;
        Thread.Sleep(100);
        _serialPort.DtrEnable = false;
        Console.WriteLine("Waiting for Arduino...");

        // Read possibly junk data because we know it might be bad
        try {
            while (true) {
                var line = _serialPort.ReadLine();
                if (!line.Equals("==================================\r")) continue;
                Console.WriteLine("Verified Serial connection is okay");
                break;
            }
        }
        catch (TimeoutException) {
            Console.WriteLine("Hello? Arduino??? (Failed to setup reliable serial with arduino)");
            throw;
        }

        _serialPort.ReadTimeout = 2000;
        Console.WriteLine("Arduino Ready");
    }

    public string GetNativeVersion() {
        using var stream = new MemoryStream(SendDataExpectResult(new[] { (byte)Functions.NativeVersion }, 3));
        var major = stream.ReadByte();
        var minor = stream.ReadByte();
        var patch = stream.ReadByte();
        return $"{major}.{minor}.{patch}";
    }

    public byte[] GetUid() {
        return SendDataExpectResult(new[] { (byte)Functions.ReadUid }, 4, 200); // can be > 4 bytes so wait a bit longer just in case
    }

    public void ResetRc522() {
        SendData(new[] { (byte)Functions.ResetReader });
    }

    public bool IsNewTagPresent() {
        using var stream = new MemoryStream(SendDataExpectResult(new[] { (byte)Functions.IsNewTagPresent }, 1));
        return stream.ReadByte() == 1;
    }

    public bool SelectTag() {
        using var stream = new MemoryStream(SendDataExpectResult(new[] { (byte)Functions.SelectTag }, 1));
        return stream.ReadByte() == 1;
    }

    public void AuthenticateSector(byte[] key, byte block, KeyType keyType) {
        var data = new[] { (byte)Functions.Authenticate }.Concat(key.Concat(new[] { block, (byte)(keyType == KeyType.KeyA ? 0x00 : 0x01) })).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        var result = stream.ReadByte();

        if (result != 0) throw new AuthenticationException(result, keyType);
    }
    
    /**
     * @deprecated :(
     */
    public byte[] ReadBlock(byte block) {
        var data = new[] { (byte)Functions.ReadBlock }.Concat(new[] { block }).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 18 + 1));
        var result = stream.ReadByte();

        if (result != 0) throw new ReadException(result);
        var blockData = new byte[18];
        stream.Read(blockData, 0, blockData.Length);
        return blockData;
    }

    public byte[] ReadSector(byte sector) {
        var data = new[] { (byte)Functions.ReadSector }.Concat(new[] { sector }).ToArray();
        using var statusInfo = new MemoryStream(SendDataExpectResult(data, 1));
        var result = statusInfo.ReadByte();
        if (result != 0) throw new ReadException(result);
        return WaitForData(18 * 4);
    }

    public void WriteBlock(byte block, byte[] blockData) {
        if (blockData.Length != 16) throw new Exception("Bad block data. Needs to be a length of 16");
        var data = new[] { (byte)Functions.WriteBlock }.Concat(new[] { block }).Concat(blockData).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        var result = stream.ReadByte();

        if (result != 0) throw new WriteException(result);
    }
    
    public void WriteFullFast(byte[] dump) {
        if (dump.Length != 1024) throw new Exception("Bad dump size. Needs to be a length of 1024");
        var data = new[] { (byte)Functions.FastWrite }.Concat(dump).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        
        var result = stream.ReadByte();
        if (result != 0) throw new WriteException(result);
    }
    
    public byte[] ReadFullFast() {
        var data = new[] { (byte)Functions.FastRead }.ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        
        var result = stream.ReadByte();
        if (result != 0) throw new ReadException(result);
        return WaitForData(1024);
    }

    public void SetKeys(bool isMagic, byte[][] keyA, byte[][] keyB) {
        var data = new byte[1 + 16 * 6 * 2];
        data[0] = (byte) (isMagic ? 1 : 0);
        // TODO: the rest lol
        SendData(new[] { (byte)Functions.SetTagKeys }.Concat(data).ToArray());
    }

    public void SendData(byte[] data, int timeout = 500) {
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
    }

    public byte[] WaitForData(int dataLength, int timeout = 100) {
        while (_serialPort.BytesToRead < dataLength) Thread.Sleep(timeout);
        var readData = new byte[dataLength];
        _serialPort.Read(readData, 0, readData.Length);
        return readData;
    }

    public byte[] SendDataExpectResult(byte[] data, int expectedMinDataLength, int timeout = 100) {
        if (_serialPort.BytesToRead != 0) throw new Exception("Left bytes unused :(");
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
        return WaitForData(expectedMinDataLength, timeout);
    }
}