namespace CLI.nfc;

public class Sector {
    public readonly byte[][] Blocks = {
        new byte[16],
        new byte[16],
        new byte[16],
        new byte[16]
    };

    public byte[] GetKeyA() {
        var key = new byte[6];
        for (var i = 0; i < key.Length; i++) {
            key[i] = Blocks[3][i]; // block 3 first 6 bytes are keyA second 6 are blockB
        }

        return key;
    }

    public byte[] GetKeyB() {
        var key = new byte[6];
        for (var i = 0; i < key.Length; i++) {
            key[i] = Blocks[3][5 + i]; // block 3 first 6 bytes are keyA second 6 are blockB
        }

        return key;
    }
}