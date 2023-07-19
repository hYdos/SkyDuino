#ifndef NATIVE_AUTHHANDLER_H
#define NATIVE_AUTHHANDLER_H

#include <stdint.h>
#include <Arduino.h>
#include <MFRC522.h>

typedef struct {
    bool isMagicTag;
    uint8_t KeyA[16][6];
    uint8_t KeyB[16][6];
} TagKeys;

bool shouldAuthenticate();

/**
 * Sets the keys to be used when reading or writing tags
 */
void setTagKeys();

/**
 * Takes keys and tries them all until one works
*/
void getWorkingKey();

bool openBackdoor(MFRC522 mfrc522);

void closeBackdoor(MFRC522 mfrc522);

void setKeys(TagKeys keys);

uint8_t (*getKeyA(byte sector))[6];

uint8_t (*getKeyB(byte sector))[6];

#endif //NATIVE_AUTHHANDLER_H
