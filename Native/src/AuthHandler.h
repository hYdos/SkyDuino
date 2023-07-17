#ifndef NATIVE_AUTHHANDLER_H
#define NATIVE_AUTHHANDLER_H

#include <stdint.h>
#include <Arduino.h>

typedef struct {
    bool isMagicTag;
    uint8_t KeyA[16][6];
    uint8_t KeyB[16][6];
} TagKeys;

/**
 * Sets the keys to be used when reading or writing tags
 */
void setTagKeys();

/**
 * Turns off authentication. Speeds up writing on magic cards, and allows to write anywhere on the tag unrestricted
 */
void disableAuthentication();

/**
 * Re-enables authentication. Should be done after writing is done
 */
void enableAuthentication();

void setKeys(TagKeys keys);

uint8_t (*getKeyA(byte sector))[6];

uint8_t (*getKeyB(byte sector))[6];

#endif //NATIVE_AUTHHANDLER_H
