#include <Arduino.h>
#include <SPI.h>
#include <MFRC522.h>
#include <map>

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
MFRC522 mfrc522(10, 9);
std::map<byte, void (*)()> functionMap;

// Commands
void nativeVersion();

void resetReader();

void authenticate();

void readBlock();

void writeBlock();

void isNewTagPresent();

void selectTag();

void setup() {
    // Setup commands
    functionMap.clear();
    functionMap[0x10] = nativeVersion;
    functionMap[0x20] = resetReader;
    functionMap[0x30] = authenticate;
    functionMap[0x40] = readBlock;
    functionMap[0x50] = writeBlock;
    functionMap[0x60] = isNewTagPresent;
    functionMap[0x70] = selectTag;

    // Setup for communication
    Serial.begin(921600);
    Serial.setTimeout(200);
    while (!Serial);
    resetReader();
    delay(2000);
    Serial.println("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
    Serial.println("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");
    Serial.println("CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC");
    Serial.println("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
    Serial.println("EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE");
    Serial.println("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
    Serial.println("==================================");
}

void loop() {
    byte buffer[1];
    buffer[0] = 0x00;
    Serial.readBytes(buffer, 1);
    if (functionMap[buffer[0]] != nullptr) functionMap[buffer[0]]();
}

void nativeVersion() {
    uint8_t responseData[] = {0x00, 0x01, 0x00};
    Serial.write(responseData, sizeof(responseData));
}

void resetReader() {
    SPIClass::begin();
    mfrc522.PCD_Init();
}

void authenticate() {
    uint8_t keyBytes[6];
    Serial.readBytes(keyBytes, 6);
    uint8_t block[1];
    Serial.readBytes(block, 1);
    uint8_t keyType[1];
    Serial.readBytes(keyType, 1);
    auto cmd = keyType[0] == 0 ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B;

    auto status = mfrc522.PCD_Authenticate(
            cmd,
            block[0],
            reinterpret_cast<MFRC522::MIFARE_Key *>(keyBytes),
            &(mfrc522.uid)
    );

    Serial.write((byte) status);
}

void readBlock() {

}

void writeBlock() {

}

void isNewTagPresent() {
    Serial.write((byte) mfrc522.PICC_IsNewCardPresent());
}

void selectTag() {
    Serial.write((byte) mfrc522.PICC_ReadCardSerial());
}
