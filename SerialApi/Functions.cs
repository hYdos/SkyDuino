namespace SerialApi; 

public enum Functions: byte {
    NativeVersion = 0x10,
    ResetReader = 0x20,
    Authenticate = 0x30,
    ReadBlock = 0x40,
    WriteBlock = 0x50,
    IsNewTagPresent = 0x60,
    SelectTag = 0x70,
    ReadUid = 0x80,
    ReadSector = 0x90
}