#define MFRC522_SPICLOCK (10000000u)    // the MFRC522 can accept upto 10MHz. (Override original 4MHz)

#include <Arduino.h>
#include <SPI.h>
#include <MFRC522.h>
#include <map>
#include <AuthHandler.h>

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"

#define BAUD_RATE 115200
#define READ_TIMEOUT 20
#define VERSION_MAJOR 0x00
#define VERSION_MINOR 0x02
#define VERSION_PATCH 0x01

MFRC522 mfrc522(10, 9);
std::map<byte, void (*)()> functionMap;

// Commands
void nativeVersion();

void resetReader();

void readBlock();

void writeBlock();

void isNewTagPresent();

void selectTag();

void readUid();

void readSector();

void setup() {
    // Setup commands
    functionMap.clear();
    functionMap[0x01] = nativeVersion;
    functionMap[0x02] = resetReader;
    functionMap[0x04] = readBlock;
    functionMap[0x05] = writeBlock;
    functionMap[0x06] = isNewTagPresent;
    functionMap[0x07] = selectTag;
    functionMap[0x08] = readUid;
    functionMap[0x09] = readSector;
    functionMap[0x0A] = setTagKeys;

    // Setup for communication
    Serial.begin(BAUD_RATE);
    Serial.setTimeout(READ_TIMEOUT);
    while (!Serial);
    resetReader();
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
    uint8_t responseData[] = {VERSION_MAJOR, VERSION_MINOR, VERSION_PATCH};
    Serial.write(responseData, sizeof(responseData));
}

void resetReader() {
    SPIClass::begin();
    mfrc522.PCD_Init();
}

// FIXME: deprecated. make it a utility method in AuthHandler
//void authenticate() {
//    uint8_t keyBytes[6];
//    Serial.readBytes(keyBytes, 6);
//    uint8_t block[1];
//    Serial.readBytes(block, 1);
//    uint8_t keyType[1];
//    Serial.readBytes(keyType, 1);
//    auto cmd = keyType[0] == 0 ? MFRC522::PICC_CMD_MF_AUTH_KEY_A : MFRC522::PICC_CMD_MF_AUTH_KEY_B;
//
//    auto status = mfrc522.PCD_Authenticate(
//            cmd,
//            block[0],
//            reinterpret_cast<MFRC522::MIFARE_Key *>(keyBytes),
//            &mfrc522.uid
//    );
//
//    Serial.write((byte) status);
//}

void readUid() {
    Serial.write(mfrc522.uid.uidByte, mfrc522.uid.size);
}

void readBlock() {
    uint8_t block[1];
    Serial.readBytes(block, 1);

    uint8_t buffer[18];
    uint8_t size = 18;
    auto status = mfrc522.MIFARE_Read(block[0], buffer, &size);
    Serial.write((byte) status);
    Serial.write(buffer, 18);
}

void readSector() {
    uint8_t sector[1];
    Serial.readBytes(sector, 1);

    uint8_t buffer[18 * 4];
    uint8_t size = 18;
    for (byte i = 0; i < 4; ++i) {
        auto status = mfrc522.MIFARE_Read((sector[0] * 4) + i, buffer + (18 * i), &size);

        if (status != MFRC522::STATUS_OK) {
            Serial.write(i);
            Serial.write(status);
            return;
        }
    }

    Serial.write((byte) MFRC522::STATUS_OK); // Status is ok for all sectors
    Serial.write(buffer, 18 * 4);
}

void writeBlock() {
    uint8_t block[1];
    Serial.readBytes(block, 1);
    uint8_t data[16];
    Serial.readBytes(data, 16);

    if (block[0] == 0) {
        // Stop encrypted traffic so we can send raw bytes
        mfrc522.PCD_StopCrypto1();

        // Activate UID backdoor
        if (!mfrc522.MIFARE_OpenUidBackdoor(false)) {
            Serial.write((byte) 0xBB);
            return;
        }
    }

    auto status = mfrc522.MIFARE_Write(block[0], data, (byte) 16);
    Serial.write((byte) status);

    if (block[0] == 0) {
        // Wake the card up again
        byte atqa_answer[2];
        byte atqa_size = 2;
        mfrc522.PICC_WakeupA(atqa_answer, &atqa_size);
    }
}

void isNewTagPresent() {
    Serial.write((byte) mfrc522.PICC_IsNewCardPresent());
}

void selectTag() {
    Serial.write((byte) mfrc522.PICC_ReadCardSerial());
}
