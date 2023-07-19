namespace SerialApi; 

public enum Functions: byte {
    NativeVersion = 0x01,
    ResetReader = 0x02,
    Authenticate = 0x03,
    ReadBlock = 0x04,
    WriteBlock = 0x05,
    IsNewTagPresent = 0x06,
    SelectTag = 0x07,
    ReadUid = 0x08,
    ReadSector = 0x09,
    SetTagKeys = 0x0A,
    FactoryResetTag = 0x0B
}