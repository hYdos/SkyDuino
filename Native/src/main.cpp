#include <Arduino.h>
#include <SPI.h>
#include <MFRC522.h>
#include <map>
#include <SHA256Gen.h>

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#define RST_PIN 9
#define SS_PIN 10
MFRC522 mfrc522(SS_PIN, RST_PIN);
SHA256Gen hashGen = SHA256Gen();

std::map<byte, void (*)()> functionMap;

// Utilities
uint16_t CalculateCrc(uint8_t *data, size_t dataSize);

void SendResponse(uint8_t *data, size_t dataSize);

// Commands
void nativeVersion();

void resetReader();

void setup() {
    // Setup commands
    functionMap[0] = nativeVersion;
    functionMap[1] = resetReader;

    // Setup for communication
    Serial.begin(115200);
    while (!Serial);
    resetReader();
}

void loop() {
    auto headerSize = 36;
    uint8_t header[headerSize];

    if (Serial.available() >= headerSize) {
        auto bytesRead = Serial.readBytes(header, headerSize);
        if (bytesRead != headerSize) {
            Serial.write(0x02); // 0x02 = bad header length
            return;
        }
        auto dataLength = ((uint32_t) header[0]) | ((uint32_t) header[1] << 8) | ((uint32_t) header[2] << 16) | ((uint32_t) header[3] << 24);
        uint8_t data[dataLength];
        Serial.readBytes(data, dataLength);

        uint8_t hash[32];
        uint16_t hashLength;
        hashGen.make(hash, hashLength, data, dataLength);

        for (int i = 0; i < 32; ++i) {
            auto expectedByte = header[4 + i]; // hash byte in the header
        }

//        if (bytesRead == 36) {
//            auto crc = (uint16_t(header[0]) << 8) | header[1];
//            int32_t dataLength = (header[2] << 24) | (header[3] << 16) | (header[4] << 8) | header[5];
//
//            uint8_t responseData[dataLength];
//
//            auto bytesReadData = Serial.readBytes(responseData, dataLength);
//            if (bytesReadData == dataLength) {
//                auto ourCrc = CalculateCrc(responseData, dataLength);
//                if (ourCrc == crc) {
//                    Serial.write(0xC8); // Let the sender know we received the data correctly
//                    if (functionMap[responseData[0]] != nullptr) functionMap[responseData[0]]();
//                    return; // Exit the loop after processing one set of data
//                } else {
//                    Serial.write(0x01); // 0x01 = bad CRC
//                    Serial.println(ourCrc);
//                }
//            } else {
//                Serial.write(0x02); // 0x02 = bad length
//            }
//        }
    }

    // If no valid data received, continue looping
}

//void loop() {
//    byte buffer[1];
//    Serial.readBytes(buffer, 1);
//
//    if (functionMap[buffer[0]] != nullptr) functionMap[buffer[0]]();
//    else {
//        Serial.println(&"unkn inst " [ buffer[0]]);
//    }
//}

void nativeVersion() {
    uint8_t responseData[] = {0x00, 0x00, 0x01};
    SendResponse(responseData, sizeof(responseData));
}

void resetReader() {
    SPIClass::begin();
    mfrc522.PCD_Init();
}

bool PacketVerifiedCorrectly() {
    if (Serial.available() >= 1) {
        uint8_t buffer = Serial.read();
        return buffer == 0xC8;
    }
    return false;
}

void SendResponse(uint8_t *data, size_t dataSize) {
    uint16_t ourCrc = CalculateCrc(data, dataSize);
    unsigned long startTime = millis();

    while (millis() - startTime < 200) {
        if (PacketVerifiedCorrectly()) {
            uint8_t header[6];
            size_t bytesRead = Serial.readBytes(header, 6);
            if (bytesRead == 6) {
                uint16_t receivedCrc = (uint16_t(header[0]) << 8) | header[1];
                int32_t receivedDataSize = (header[2] << 24) | (header[3] << 16) | (header[4] << 8) | header[5];

                if (ourCrc == receivedCrc && dataSize == receivedDataSize) {
                    Serial.write(0xC8); // Let the sender know we verified the data correctly
                    Serial.write(data, dataSize); // Send the response data
                    break;
                }
            }
        }
    }
}

uint16_t CalculateCrc(uint8_t *data, size_t dataSize) {
    uint16_t crc = 0xFFFF;

    for (size_t i = 0; i < dataSize; i++) {
        crc ^= uint16_t(data[i]);

        for (uint8_t j = 0; j < 8; j++) {
            if (crc & 0x0001) {
                crc = (crc >> 1) ^ 0x8408;
            } else {
                crc >>= 1;
            }
        }
    }

    return crc;
}
