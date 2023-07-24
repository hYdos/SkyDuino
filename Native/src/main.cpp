#define MFRC522_SPICLOCK (10000000u)    // the MFRC522 can accept upto 10MHz. (Override original 4MHz)

#include <Arduino.h>
#include <SPI.h>
#include <MFRC522.h>
#include <map>
#include "AuthHandler.h"

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

// FIXME: deprecated
void authenticate();

void factoryResetTag();

void fastWriteTag();

void fastReadTag();

void setup() {
    // Setup commands
    functionMap.clear();
    functionMap[0x01] = nativeVersion;
    functionMap[0x02] = resetReader;
    functionMap[0x03] = authenticate; // TODO: replace with command to try all keys and send back status and if any worked the working one.
    functionMap[0x04] = readBlock;
    functionMap[0x05] = writeBlock;
    functionMap[0x06] = isNewTagPresent;
    functionMap[0x07] = selectTag;
    functionMap[0x08] = readUid;
    functionMap[0x09] = readSector;
    functionMap[0x0A] = setTagKeys;
    functionMap[0x0B] = factoryResetTag;
    functionMap[0x0C] = fastWriteTag;
    functionMap[0x0D] = fastReadTag;

    // Setup for communication
    Serial.begin(BAUD_RATE);
    Serial.setTimeout(READ_TIMEOUT);
    SPIClass::begin();
    mfrc522.PCD_Init();
    while (!Serial);
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
    closeBackdoor(mfrc522);
    mfrc522.PICC_HaltA();
    SPIClass::begin();
    mfrc522.PCD_Init();
}

// FIXME: deprecated. make it a utility method in AuthHandler
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
            &mfrc522.uid
    );

    Serial.write((byte) status);
    return;
}

void readUid() {
    Serial.write(mfrc522.uid.uidByte, mfrc522.uid.size);
}

// TO REMOVE
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

    if (!shouldAuthenticate() && !openBackdoor(mfrc522)) {
        Serial.write((byte) 0xA0);
        return;
    }

    auto status = mfrc522.MIFARE_Write(block[0], data, (byte) 16);
    if (!shouldAuthenticate()) closeBackdoor(mfrc522);
    Serial.write((byte) status);
}

void isNewTagPresent() {
    Serial.write((byte) mfrc522.PICC_IsNewCardPresent());
}

void selectTag() {
    Serial.write((byte) mfrc522.PICC_ReadCardSerial());
}

void factoryResetTag() {
    mfrc522.MIFARE_OpenUidBackdoor(false);
    byte block0_buffer[] = {0x01, 0x02, 0x03, 0x04, 0x04, 0x08, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x00};
    byte blank_buffer[] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                           0x00};
    byte key_buffer[] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x80, 0x69, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                         0xFF};

    for (byte i = 0; i < 64; ++i) {
        if (i == 0) { // Block 0
            auto status = mfrc522.MIFARE_Write((byte) i, block0_buffer, (byte) 16);

            if (status != mfrc522.STATUS_OK) {
                Serial.write((byte) status);
                return;
            }
        } else if (i % 4 == 3) { // Key Block
            auto status = mfrc522.MIFARE_Write((byte) i, key_buffer, (byte) 16);

            if (status != mfrc522.STATUS_OK) {
                Serial.write((byte) status);
                return;
            }
        } else { // Everything else
            auto status = mfrc522.MIFARE_Write((byte) i, blank_buffer, (byte) 16);

            if (status != mfrc522.STATUS_OK) {
                Serial.write((byte) status);
                return;
            }
        }
    }

    Serial.write((byte) 0);
}

void fastWriteTag() {
    uint8_t data[64][16];
    for (auto &i: data)Serial.readBytes(i, 16);
    mfrc522.MIFARE_OpenUidBackdoor(false);

    for (byte i = 0; i < 64; ++i) {
        auto status = mfrc522.MIFARE_Write((byte) i, data[i], (byte) 16);

        if (status != mfrc522.STATUS_OK) {
            Serial.write((byte) status);
            return;
        }
    }

    Serial.write((byte) 0);
}

void fastReadTag() {
    uint8_t data[64][16];
    mfrc522.MIFARE_OpenUidBackdoor(false);
    uint8_t size = 18;

    for (byte i = 0; i < 64; ++i) {
        uint8_t block[18];
        auto status = mfrc522.MIFARE_Read((byte) i, block, &size);

        if (status != mfrc522.STATUS_OK) {
            Serial.write((byte) status);
            return;
        }

        // Prob not the best way, but write first 16 bytes into dump
        for (auto j = 0; j < 16; ++j) data[i][j] = block[j];
    }

    Serial.write((byte) 0);
    for (auto &i: data)Serial.write(i, 16);
}
