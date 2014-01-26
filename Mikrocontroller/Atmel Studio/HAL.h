#include <stdint.h>

#define _getlen(a) (sizeof(a) / sizeof(a[0]))
#ifndef UASG01_H_
#define UASG01_H_

// Setzt den Helligkeitswert eines PWM-Kanals auf einen bestimmten Wert
// Channel: Kanal-Nummer (muss vorher mit HAL_HX2272_SetCode initialisiert sein)
// Value: Helligkeitswert zwischen 0..255
void HAL_PWM_SetCh(uint8_t Channel, uint8_t Value);

// Setzt den zu sendenden Code gemäß der DIP-Schalter am Empfänger
// Channel: Kanal-Nummer, muss kleiner als HAL_PWM_Count sein
// CodeStr: Z.B. "F F F L F L F F F F"; L = Low, F = float, H = High
void HAL_HX2272_SetCode(uint8_t Channel, const char *CodeStr);
void HAL_HX2272_SendCode(uint8_t Channel, bool On);
void HAL_HX2272_SendCode(uint8_t Channel, bool On, uint8_t Ticks);

// Gibt den Status aller Tasten (bis zu 20 Bits) zurück.
uint32_t HAL_Keys_Get();

// Wird vom HAL alle 100ms aufgerufen. Achtung: Nur für kurze Routinen
void App_tick_100ms();

// Event, einmal pro Sekunde. Achtung: Nur für kurze Routinen
extern void (*volatile HAL_OnTickSecond)();

// Event, wenn ein Taster gedrückt wurde. Achtung: Nur für kurze Routinen
extern void (*volatile HAL_OnKeyDown)(uint32_t keys, uint32_t keydown);

// Event, einmal pro Minute. Achtung: Nur für kurze Routinen
extern void (*volatile HAL_OnTickMinute)();

// Wird vom HAL einmal beim RESET aufgerufen
void App_init();

struct HAL_Time {
	uint8_t	Hour;
	uint8_t	Minute;
	uint8_t	Second;
};

void HAL_getTime(HAL_Time *CurrentTime);
void HAL_resetTime();
void HAL_HX2272_SendCode(uint8_t DeviceNr, bool On);

#endif /* UASG01_H_ */