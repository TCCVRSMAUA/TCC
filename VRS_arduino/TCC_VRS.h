
#define SENSOR_1          A4
#define SENSOR_2          A5

#define SET_HOMMING_BTN   3
#define RESET_HOMMING_BTN 11

#define N_MEDIA           64

#define BRAKE_M1          400
#define BRAKE_M2          400

#define ZERO_VOLTAGE      80
#define DEAD_ZONE         30

#define ACC_LIMIT         32
#define ACC_LIMIT_2       5

#define HOME_BUTTON       3 
#define START_BUTTON      5 

#define HOME_DEBOUNCE_VALUE     500
#define START_DEBOUNCE_VALUE    500

typedef enum {
  DISCONNECTED,
  CONNECTED,
  HOMMING_POSITION,
  CONTROLLING
} motors_state;

typedef struct _motors{
  long homming_point;
  long actual_point;
  long set_point;
  long brake;
  long speed_setted;
} motors;

