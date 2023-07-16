#include "AuthHandler.h"

struct TagKeys tagKeys;

void setKeys(struct TagKeys keys) {
    tagKeys = keys;
}

uint8_t (*getKeyA(byte sector))[6] {
    return &tagKeys.KeyA[sector];
}

uint8_t (*getKeyB(byte sector))[6] {
    return &tagKeys.KeyB[sector];
}
