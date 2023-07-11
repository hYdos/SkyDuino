namespace SerialApi.error;

public class AuthenticationException : Exception {

    public readonly int Result;
    public readonly KeyType? KeyType;

    public AuthenticationException(int result, KeyType keyType) : base($"PCD_Authenticate() on {keyType} failed. Reason: {GetReason(result)}") {
        Result = result;
        KeyType = keyType;
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