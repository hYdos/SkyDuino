#ifndef NATIVE_AUTHHANDLER_H
#define NATIVE_AUTHHANDLER_H
#include <stdint.h>
#include <Arduino.h>

struct TagKeys {
    uint8_t KeyA[16][6];
    uint8_t KeyB[16][6];
};

void setKeys(struct TagKeys keys);

uint8_t (*getKeyA(byte sector))[6];

uint8_t (*getKeyB(byte sector))[6];

#endif //NATIVE_AUTHHANDLER_H
