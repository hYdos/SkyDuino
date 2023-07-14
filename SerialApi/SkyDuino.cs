using System.IO.Ports;
using SerialApi.error;

namespace SerialApi;

public class SkyDuino {

    private readonly SerialPort _serialPort;

    public SkyDuino() {
        Console.WriteLine("Connecting to Arduino");
        _serialPort = new SerialPort();
        _serialPort.PortName = SerialPort.GetPortNames()[0];
        _serialPort.BaudRate = 2000000;
        _serialPort.ReadTimeout = 200;
        _serialPort.WriteTimeout = 1000;
        _serialPort.Open();

        // Toggle the DTR signal to simulate a reset
        _serialPort.DtrEnable = true;
        Thread.Sleep(100);
        _serialPort.DtrEnable = false;
        Console.WriteLine("Waiting for Arduino...");
        Thread.Sleep(4000); // Arduino Uno has some serious reset time

        // Read possibly junk data because we know it might be bad
        try {
            while (true) {
                _serialPort.ReadLine();
            }
        }
        catch (TimeoutException) {
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

    public byte[] ReadBlock(byte block) {
        var data = new[] { (byte)Functions.ReadBlock }.Concat(new[] { block }).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 17));
        var result = stream.ReadByte();

        if (result != 0) throw new ReadException(result);
        var blockData = new byte[18];
        stream.Read(blockData, 0, blockData.Length);
        return blockData;
    }
    
    public byte[] ReadSector(byte sector) {
        var data = new[] { (byte)Functions.ReadSector }.Concat(new[] { sector }).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 18 * 4 + 1));
        var result = stream.ReadByte();

        if (result != 0) throw new ReadException(result);
        var sectorData = new byte[18 * 4];
        stream.Read(sectorData, 0, sectorData.Length);
        return sectorData;
    }

    public void WriteBlock(byte block, byte[] blockData) {
        if (blockData.Length != 16) throw new Exception("Bad block data. Needs to be a length of 16");
        var data = new[] { (byte)Functions.WriteBlock }.Concat(new[] { block }).Concat(blockData).ToArray();
        using var stream = new MemoryStream(SendDataExpectResult(data, 1));
        var result = stream.ReadByte();

        if (result != 0) throw new WriteException(result);
    }

    public void SendData(byte[] data, int timeout = 500) {
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
    }
    
    public byte[] SendDataExpectResult(byte[] data, int expectedMinDataLength, int timeout = 100) {
        _serialPort.Write(data, 0, data.Length);
        Thread.Sleep(timeout);
        while (_serialPort.BytesToRead < expectedMinDataLength) Thread.Sleep(timeout);
        var readData = new byte[_serialPort.BytesToRead];
        _serialPort.Read(readData, 0, readData.Length);
        if (_serialPort.BytesToRead != 0) throw new Exception("Left bytes unused :(");
        return readData;
    }
}