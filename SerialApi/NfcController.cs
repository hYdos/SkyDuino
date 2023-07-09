using System.IO.Ports;
using System.Security.Cryptography;

namespace SerialApi;

public class NfcController {

    private readonly SerialPort _serialPort;

    public NfcController() {
        _serialPort = new SerialPort();
        _serialPort.PortName = "COM4";
        _serialPort.BaudRate = 2000000;
        _serialPort.ReadTimeout = 3000;
        _serialPort.WriteTimeout = 3000;
        _serialPort.Open();
    }

    public void SendDataExpectResult(byte[] data, Action<MemoryStream> dataReader, int timeout = 200) {
        while (true) {
            SendRawData(data);
            Thread.Sleep(timeout); // give that AtMega328p a chance
            var result = PacketVerifiedCorrectly();
            if (result != 0xC8) {
                Console.WriteLine("Transaction failed. got result " + result);
                if (result == 0x01) Console.WriteLine(_serialPort.ReadLine());
                continue;
            }

            // Prepare to read actual response
            TryAcceptResponse(dataReader, timeout);

            break;
        }
    }

    private void TryAcceptResponse(Action<MemoryStream> dataReader, int timeout) {
        while (true) {
            var header = new byte[6];
            _serialPort.Read(header, 0, 6);
            var crc = BitConverter.ToUInt16(header.AsSpan()[..1]);
            var dataLength = BitConverter.ToInt32(header.AsSpan()[2..5]);
            var responseData = new byte[dataLength];
            var ourCrc = CalculateCrc(responseData);
            var sha256 = CalculateSha256(responseData);
            if (ourCrc == crc) {
                _serialPort.Write(new byte[] {
                    0xC8
                }, 0, 1); // let the microcontroller know we are ok

                using var stream = new MemoryStream(responseData);
                dataReader(stream);
            }
            else {
                Thread.Sleep(timeout * 2); // Maybe some more time if you really need it...
                continue;
            }

            break;
        }
    }

    private int PacketVerifiedCorrectly() {
        var buffer = new byte[1];
        _serialPort.Read(buffer, 0, 1);
        return buffer[0];
    }

    private void SendRawData(byte[] data) {
        var hash = CalculateSha256(data);
        var packetHeader = BitConverter.GetBytes(data.Length).Concat(hash).ToArray();
        var fullPacket = packetHeader.Concat(data).ToArray();

        _serialPort.Write(fullPacket, 0, fullPacket.Length);
    }


    private static byte[] CalculateSha256(byte[] rawData) {
        return SHA256.HashData(rawData);
    }

    private static ushort CalculateCrc(IEnumerable<byte> data) {
        ushort crc = 0xFFFF;

        foreach (var b in data) {
            crc ^= (ushort)(b << 8);

            for (var j = 0; j < 8; j++) {
                if ((crc & 0x8000) != 0) {
                    crc = (ushort)((crc << 1) ^ 0x1021);
                }
                else {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }
}