using CLI.skylanders;
using Newtonsoft.Json;
using SerialApi;
using SerialApi.error;

namespace CLI.nfc;

public class NfcTag {
    private static readonly byte[] SkylanderKeyB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    private static readonly byte[] FactoryKeyAll = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    public readonly byte[][] KeyA = new byte[16][];
    public readonly byte[][] KeyB = new byte[16][];
    public readonly byte[] Uid;
    private readonly SkyDuino _arduino;

    public static NfcTag Get(byte[] uid, SkyDuino arduino) {
        var possibleCachedFile = $"tags/{BitConverter.ToString(uid)}.json";
        return File.Exists(possibleCachedFile) ? ReadFromJsonFile(possibleCachedFile)! : new NfcTag(uid, arduino);
    }
    
    private NfcTag(byte[] uid, SkyDuino arduino) {
        Uid = uid;
        _arduino = arduino;
    }

    public byte[] ReadBlock(byte block, KeyType keyType = KeyType.KeyA) {
        var sector = (byte)Math.Floor((decimal)block / 4);
        var key = keyType == KeyType.KeyA ? KeyA[sector] : KeyB[sector];
        _arduino.AuthenticateSector(key, block, keyType);
        return _arduino.ReadBlock(block);
    }

    public void WriteBlock(byte block, byte[] data, KeyType keyType = KeyType.KeyB) {
        var sector = (byte)Math.Floor((decimal)block / 4);
        var key = keyType == KeyType.KeyA ? KeyA[sector] : KeyB[sector];
        _arduino.AuthenticateSector(key, block, keyType);
        _arduino.WriteBlock(block, data);
    }

    public void FillKeys(bool cache = true) {
        for (byte i = 0; i < KeyA.Length; i++) {
            Console.WriteLine($"Finding key A for sector {i}");
            KeyA[i] = GetWorkingKey(i, KeyType.KeyA);
            Console.WriteLine($"Finding key B for sector {i}");
            KeyB[i] = GetWorkingKey(i, KeyType.KeyB);
        }
        
        WriteToJsonFile($"tags/{BitConverter.ToString(Uid)}.json");
    }

    /**
     * If this fails the tag is bricked probably or not a blank, skylander, or partial skylander tag
     */
    private byte[] GetWorkingKey(byte sector, KeyType type) {
        if (type == KeyType.KeyA) {
            var gennedKeyA = SkyKeyGen.CalcKeyA(Uid, sector);
            if (TryKey(_arduino, FactoryKeyAll, sector, KeyType.KeyA)) return FactoryKeyAll;
            if (TryKey(_arduino, gennedKeyA, sector, KeyType.KeyA)) return gennedKeyA;
        }
        else {
            if (TryKey(_arduino, FactoryKeyAll, sector, KeyType.KeyB)) return FactoryKeyAll;
            if (TryKey(_arduino, SkylanderKeyB, sector, KeyType.KeyB)) return SkylanderKeyB;
        }

        throw new Exception($"No working keys known for {Uid}. Your tag is probably bricked :(");
    }

    public void RemoveTimeout() {
        _arduino.ResetRc522();
        if (!_arduino.IsNewTagPresent()) throw new Exception("Tag Was Moved!");
        if (!_arduino.SelectTag()) throw new Exception("Tag Was Moved!");
    }

    private bool TryKey(SkyDuino arduino, byte[] key, byte sector, KeyType type) {
        try {
            arduino.AuthenticateSector(key, (byte)(sector * 4), type);
            return true;
        }
        catch (AuthenticationException) {
            RemoveTimeout();
            return false;
        }
    }

    private void WriteToJsonFile(string filePath) {
        Directory.CreateDirectory(filePath[..filePath.LastIndexOf('/')]);
        using var writer = new StreamWriter(filePath, false);
        var json = JsonConvert.SerializeObject(this);
        writer.Write(json);
    }

    private static NfcTag? ReadFromJsonFile(string filePath) {
        using var reader = new StreamReader(filePath);
        var fileString = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<NfcTag>(fileString);
    }
}