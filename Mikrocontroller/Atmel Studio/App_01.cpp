#include "HAL.h"

uint16_t DimRampStepDuration = 1;	// Dauer pro Schritt in 100ms
uint8_t DimRampTarget = 63;			// Ende der Dimmung (0 .. 63)
uint8_t DimStepCount = 1;			// Countdown f�r DimRampSteps
bool EVG_is_on = false;

struct DimPoint {
	HAL_Time Time;					// Start-Uhrzeit
	uint8_t Target;					// Ende der Dimmung (0 .. 100)
	uint8_t Duration;				// Dauer pro Schritt in 100ms, 100 Schitte � 25,5s = 255s = 4h 15 min
};

const DimPoint DimPoints[] = {{{06,10,00}, 100, 179}, \
							  {{10,00,00},  54, 254}, \
							  {{17,00,00}, 100, 254}, \
							  {{21,50,00},   0, 254}};
							  // 100, 179 = bis 06:40:00
							  //  54, 255 = bis 10:19:37
							  // 100, 255 = bis 17:19:37
							  //   0, 255 = bis 22:32:40

void setDimTarget(uint8_t DurationPerStep, uint8_t Target) {
	DimRampStepDuration = DurationPerStep;
	DimRampTarget = Target;
	DimStepCount = 0;
}

struct WhiteBlue {
	uint8_t White;
	uint8_t Blue;
};

WhiteBlue WhiteBluevalues[101] = {{127, 13}, {127, 14}, {127, 15}, {127, 16}, {127, 17}, {127, 18}, {127, 19}, {127, 20}, {127, 21}, {127, 23}, {127, 24}, {127, 26}, {127, 27}, {127, 29}, \
								  {127, 31}, {127, 33}, {127, 35}, {127, 37}, {127, 39}, {127, 42}, {127, 45}, {127, 47}, {127, 50}, {127, 54}, {127, 57}, {127, 61}, {127, 64}, {127, 69}, \
								  {127, 73}, {127, 78}, {127, 83}, {127, 88}, {127, 93}, {127, 99}, {127, 106}, {127, 112}, {127, 119}, {127, 127}, {122, 42}, {117, 37}, {112, 33}, {107, 29}, \
								  {102, 26}, {98, 23}, {93, 20}, {89, 18}, {85, 16}, {81, 14}, {77, 12}, {74, 10}, {70, 8}, {67, 6}, {64, 4}, {61, 2}, {58, 0}, {55, 0}, {53, 0}, {50, 0}, \
								  {48, 0}, {45, 0}, {43, 0}, {41, 0}, {39, 0}, {37, 0}, {35, 0}, {33, 0}, {31, 0}, {30, 0}, {28, 0}, {26, 0}, {25, 0}, {23, 0}, {22, 0}, {21, 0}, {20, 0}, \
								  {18, 0}, {17, 0}, {16, 0}, {15, 0}, {14, 0}, {13, 0}, {12, 0}, {11, 0}, {10, 0}, {9, 0}, {9, 0}, {8, 0}, {7, 0}, {6, 0}, {6, 0}, {5, 0}, {5, 0}, {4, 0}, \
								  {3, 0}, {3, 0}, {2, 0}, {2, 0}, {1, 0}, {1, 0}, {0, 0}, {0, 0}};

void App_tick_100ms() {
	// LED blinken, um Betrieb anzuzeigen
	static bool BlinkOnOff = true;
	static uint8_t BlinkCountdown = 1;
	static uint8_t CurrentDimValue = 63;
	static uint8_t LastDimValue = 63;
	static enum {Up, Down} UpDown = Down;
	static bool ToggleUpDn = false;
	static uint8_t divider = 1;
	if (!(BlinkCountdown--)) {
		BlinkOnOff = !BlinkOnOff;
		if (BlinkOnOff) {				// 100ms ein
			HAL_PWM_SetCh(0, 127);
			BlinkCountdown = 0;
		} else {						// 900ms aus
			HAL_PWM_SetCh(0, 0);
			BlinkCountdown = 8;
		}
	}
	if (HAL_Keys_Get())	{
		if (UpDown == Up) {
			if (CurrentDimValue >= 100) {
				UpDown = Down;
				if (CurrentDimValue > 100) for(;;); // trap cpu
			} else {
				if (divider-- == 0) {
					CurrentDimValue += 1;
					divider = 4;
				}
			}
		} else { // UpDown == Down
			if (CurrentDimValue == 0) {
				UpDown = Up;
			} else {
				if (divider-- == 0) {
					CurrentDimValue -= 1;
					divider = 4;
				}
			}
		}
		ToggleUpDn = true;
	} else {
		if (ToggleUpDn) {
			UpDown = (UpDown == Up) ? Down : Up;		// Toggle Up/Down
			ToggleUpDn = false;
		}
		// Wenn Dimmung l�uft, ggf. Wert ver�ndern
		if (DimRampTarget != CurrentDimValue) {
			if (!(DimStepCount--)) {
				if (CurrentDimValue > DimRampTarget) {
					CurrentDimValue -= 1;
				} else {
					CurrentDimValue += 1;
				}
				DimStepCount = DimRampStepDuration;
			}
		}
	}
	if (LastDimValue != CurrentDimValue) {
 		static uint8_t whiteint; 
		whiteint = WhiteBluevalues[CurrentDimValue].White;
		LastDimValue = CurrentDimValue;
		if (whiteint == 127 && EVG_is_on) {
			EVG_is_on = false;
			HAL_HX2272_SendCode(0, false);
		} else if (whiteint < 127 && !EVG_is_on) {
			EVG_is_on = true;
			HAL_HX2272_SendCode(0, true);
		}
		HAL_PWM_SetCh(2, whiteint);
		HAL_PWM_SetCh(1, WhiteBluevalues[CurrentDimValue].Blue);
		HAL_PWM_SetCh(4, WhiteBluevalues[CurrentDimValue].Blue);
	}
};

void TickSecond() {
	static HAL_Time Time;
	HAL_getTime(&Time);
	static uint8_t i;
	for (i = 0; i < _getlen(DimPoints); i++) {
		if (DimPoints[i].Time.Hour == Time.Hour && \
		    DimPoints[i].Time.Minute == Time.Minute && \
			DimPoints[i].Time.Second == Time.Second) {
				if (DimPoints[i].Duration == 255) {
					HAL_resetTime();
				} else {
					setDimTarget(DimPoints[i].Duration, DimPoints[i].Target);
				}
		}
	}
}

void App_init() {
	//        Adersse "4A" 1 2 3 4 5 A B C D E
	HAL_HX2272_SetCode(0, "F F F L F L F F F F");
	HAL_OnTickSecond = &TickSecond;		// Add event OnTickSecond
};
