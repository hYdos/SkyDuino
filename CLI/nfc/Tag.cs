using CLI.skylanders;
using Newtonsoft.Json;
using SerialApi;
using SerialApi.error;
// _arduino field is set late when cached
#pragma warning disable CS8602

namespace CLI.nfc;

public class NfcTag {
    public static readonly byte[] SkylanderKeyB = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    public static readonly byte[] FactoryKeyAll = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    public readonly byte[] Uid;
    public byte[][] KeyA = new byte[16][];
    public byte[][] KeyB = new byte[16][];
    private SkyDuino? _arduino;

    public static NfcTag Get(byte[] uid, SkyDuino arduino) {
        return File.Exists($"tags/{BitConverter.ToString(uid)}.json") ? ReadFromJsonFile(uid, arduino) : new NfcTag(uid, arduino);
    }
    
    [JsonConstructor]
    internal NfcTag(byte[] uid, byte[][] keyA, byte[][] keyB) {
        Uid = uid;
        KeyA = keyA;
        KeyB = keyB;
        _arduino = null;
    }

    private NfcTag(byte[] uid, SkyDuino arduino) {
        Uid = uid;
        _arduino = arduino;
        FillKeys();
    }

    public byte[] ReadBlock(byte block, bool handleAuthentication = true) {
        if (!handleAuthentication) return _arduino.ReadBlock(block);
        var sector = (byte)Math.Floor((decimal)block / 4);

        try {
            _arduino.AuthenticateSector(KeyA[sector], block, KeyType.KeyA);
        }
        catch (AuthenticationException) {
            RemoveTimeout();
            _arduino.AuthenticateSector(KeyB[sector], block, KeyType.KeyB);
        }

        return _arduino.ReadBlock(block);
    }
    
    public byte[] ReadSector(byte sector) {
        try {
            _arduino.AuthenticateSector(KeyA[sector], (byte)(sector * 4), KeyType.KeyA);
        }
        catch (AuthenticationException) {
            RemoveTimeout();
            _arduino.AuthenticateSector(KeyB[sector], (byte)(sector * 4), KeyType.KeyB);
        }

        return _arduino.ReadSector(sector);
    }

    public void WriteBlock(byte block, byte[] data, KeyType keyType = KeyType.KeyB) {
        if (block == 0) VerifySafeBlock0Overwrite(data);
        var sector = (byte)Math.Floor((decimal)block / 4);
        var key = keyType == KeyType.KeyA ? KeyA[sector] : KeyB[sector];
        _arduino.AuthenticateSector(key, block, keyType);
        _arduino.WriteBlock(block, data);
        if (block % 4 != 3) return;
        KeyA[sector] = data[..6];
        KeyB[sector] = data[10..16];
        WriteToJsonFile();
    }

    private static void VerifySafeBlock0Overwrite(byte[] data) {
        var newUid = data[..4];
        var bcc = data[5];
        var calculatedBcc = newUid[0] ^ newUid[1] ^ newUid[2] ^ newUid[3];
        if (bcc != calculatedBcc) throw new BadBssException();
    }

    public void FillKeys(bool cache = true) {
        for (byte i = 0; i < KeyA.Length; i++) {
            Console.WriteLine($"Finding key A for sector {i}");
            KeyA[i] = GetWorkingKey(i, KeyType.KeyA);
            Console.WriteLine($"Finding key B for sector {i}");
            KeyB[i] = GetWorkingKey(i, KeyType.KeyB);
        }
        
        WriteToJsonFile();
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

    private void WriteToJsonFile() {
        var filePath = $"tags/{BitConverter.ToString(Uid)}.json";
        Directory.CreateDirectory(filePath[..filePath.LastIndexOf('/')]);
        using var writer = new StreamWriter(filePath, false);
        var json = JsonConvert.SerializeObject(this);
        writer.Write(json);
    }

    private static NfcTag ReadFromJsonFile(byte[] uid, SkyDuino arduino) {
        var filePath = $"tags/{BitConverter.ToString(uid)}.json";
        using var reader = new StreamReader(filePath);
        var fileString = reader.ReadToEnd();
        var obj =  JsonConvert.DeserializeObject<NfcTag>(fileString);
        obj!._arduino = arduino;
        return obj;
    }

    public void Authenticate(byte sector, KeyType type) {
        var key = type == KeyType.KeyA ? KeyA[sector] : KeyB[sector];
        _arduino.AuthenticateSector(key, (byte)(sector * 4), type);
    }
}