namespace SerialApi.error; 

public class WriteException : Exception {

    public readonly int Result;
    public readonly string Reason;

    public WriteException(int result) : base($"MIFARE_Write() failed. Reason: {GetReason(result)}") {
        Result = result;
        Reason = GetReason(result);
    }

    private static string GetReason(int result) {
        return result switch {
            0 => "OK",
            1 => "ERROR",
            2 => "COLLISION",
            3 => "TIMEOUT", // Too many attempts. Reset reader
            4 => "NO_ROOM",
            5 => "INTERNAL_ERROR",
            6 => "INVALID",
            7 => "CRC_WRONG",
            0xFF => "MIFARE_NAK", // Not authenticated
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };
    }
}