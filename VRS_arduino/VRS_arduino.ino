#include "DualVNH5019MotorShield.h"
#include "TCC_VRS.h"

DualVNH5019MotorShield Shield_Control;
/*
   Pin Map

   Comunicacao Serial:
   0, 1

   Shield:
   2, 4, 6, 7, 8, 9, 10,  12
   A0, A1

   Potenciometros:
   A4 (Mede M1), A5 (Mede M2)

  Botao verde (Home):
  3 e GND 
  
  Botao preto (Start):
  11 e GND 
*/

void command_check(String cmd_cmp);

motors_state state;
motors motor[2];
struct _motors motor_zero = {
  .homming_point = 0,
  .actual_point = 0,
  .set_point = 0,
  .brake = 0
};
unsigned int data_counter;
unsigned int acceleration_counter;
long acceleration_filter;
unsigned int soft_raise;
unsigned int safety_end;

unsigned long timer_count;
unsigned long home_debounce;
unsigned long start_debounce;

struct {
  unsigned char home_position: 1;
  unsigned char start_position: 1;
} flag;

struct {
  unsigned char home_button: 1;
  unsigned char start_button: 1;
} edge;

void setup() {
  Serial.begin(115200);
  while (!(Serial.available()));
  Shield_Control.init();
  state = DISCONNECTED;

  motor[0] = motor_zero;
  motor[0].brake = BRAKE_M1;
  motor[1] = motor_zero;
  motor[1].brake = BRAKE_M2;


  pinMode(13, OUTPUT);
  pinMode(HOME_BUTTON, INPUT_PULLUP);
  pinMode(START_BUTTON, INPUT_PULLUP);
  timer_count = 0;
  flag.home_position = 0;
  flag.start_position = 0;
  Serial.println("Comeca");
}

void loop() {

  timer_count++;

  //Home
  //detect edge
  if (((!digitalRead(HOME_BUTTON)) != (edge.home_button))){
    edge.home_button = (!digitalRead(HOME_BUTTON));
    
    //Rising edge
    if ((!digitalRead(HOME_BUTTON))){
      if (timer_count > home_debounce){
        home_debounce = timer_count + HOME_DEBOUNCE_VALUE;
        flag.home_position ^= 1;
      }
      
    }
  }

  //Start
  //detect edge
  if (((!digitalRead(START_BUTTON)) != (edge.start_button))){
    edge.start_button = (!digitalRead(START_BUTTON));
    
    //Rising edge
    if ((!digitalRead(START_BUTTON))){
      Serial.println("Apertou");
      if (timer_count > start_debounce){
        start_debounce = timer_count + START_DEBOUNCE_VALUE;
        flag.start_position ^= 1;
      }
      //if (flag.home_position) flag.start_position = 0;
    }
  }
  
  /*if (((!digitalRead(HOME_BUTTON)) != (flag.home_position)) && (!digitalRead(HOME_BUTTON))){
    if (timer_count > home_debounce){
      home_debounce = timer_count + HOME_DEBOUNCE_VALUE;
      flag.home_position = 1 if (!flag.home_position) else 0;
    }
  }
  
  if (((!digitalRead(START_BUTTON)) != (flag.start_position))&& (!digitalRead(START_BUTTON))){
    if ((timer_count > start_debounce) && (flag.home_position == 0)){
      start_debounce = timer_count + START_DEBOUNCE_VALUE;
      flag.start_position = (!digitalRead(11));
    }
    if (flag.home_position) flag.start_position = 0;
  }*/

  motor[0].actual_point = ((motor[0].actual_point * (N_MEDIA - 1)) + analogRead(SENSOR_1)) / N_MEDIA;
  motor[1].actual_point = ((motor[1].actual_point * (N_MEDIA - 1)) + analogRead(SENSOR_2)) / N_MEDIA;

  //Shield_Control.setBrakes(motor[0].brake, motor[1].brake);
  Shield_Control.setSpeeds(motor[0].speed_setted, motor[1].speed_setted);

  switch (state) {
    case DISCONNECTED: {
        String cmd;
        digitalWrite(13,LOW);

        motor[0].speed_setted = (motor[0].speed_setted * (ACC_LIMIT - 1)) / ACC_LIMIT;
        motor[1].speed_setted = (motor[1].speed_setted * (ACC_LIMIT - 1)) / ACC_LIMIT;

        if (Serial.available()) {
          //Serial.println("ARDUINO-DISCONNECTED");
          cmd = Serial.readStringUntil('\n');
        }

        if (cmd == "ReadyPc") {
          Serial.println("ReadyAr");
          data_counter = 0;
          motor[0].homming_point = 0;
          motor[1].homming_point = 0;
          cmd = "";
        }
        if (cmd == "GetData") {
          Serial.print("SendData(");
          Serial.print(motor[0].actual_point);
          Serial.print(',');
          Serial.print(motor[1].actual_point);
          Serial.println(')');
          data_counter++;
          motor[0].homming_point = ((motor[0].homming_point * (data_counter - 1)) + motor[0].actual_point) / data_counter;
          motor[1].homming_point = ((motor[1].homming_point * (data_counter - 1)) + motor[1].actual_point) / data_counter;
          cmd = "";
        }
        if (cmd == "DataSetup") {
          Serial.println("ArduinoSetup");
          cmd = "";
        }
        if (cmd.substring(0, cmd.indexOf('(')) == "Controlling") {
          state = CONTROLLING;
          Serial.println("CONTROLLING");
          cmd = "";
        }
        break;
      }
    case CONTROLLING: {
        String cmd;
        digitalWrite(13,LOW);

        if (Serial.available()) {
          //Serial.println("ARDUINO-CONTROLLING");
          if (flag.home_position){
            Serial.println("Homming");
          } else if (flag.start_position){
            Serial.println("Start");
          } else {
            Serial.println("Normal");
          }
          cmd = Serial.readStringUntil('\n');
          safety_end = 0;
        } else {
          safety_end++;
          if (safety_end > 2048)
            state = DISCONNECTED;
        }

        if (cmd == "ReadyPc") {
          state = DISCONNECTED;
          cmd = "";
        }
        if (cmd == "TesteIno") {
          Serial.println("InoCtrl");
          cmd = "";
        }
        if (cmd.substring(0, cmd.indexOf('(')) == "Controlling") {
          motor[0].set_point = cmd.substring(cmd.indexOf('(') + 1, cmd.indexOf(',')).toInt();
          motor[1].set_point = cmd.substring(cmd.indexOf(',') + 1, cmd.indexOf(')')).toInt();
          cmd = "";
          Serial.print("SetpointSetted(");
          Serial.print(motor[0].actual_point);
          Serial.print(',');
          Serial.print(motor[1].actual_point);
          Serial.println(')');
          acceleration_counter = 0;
        }
        if (cmd.substring(0, cmd.indexOf('(')) == "sethome") {
          motor[0].homming_point = cmd.substring(cmd.indexOf('(') + 1, cmd.indexOf(',')).toInt();
          motor[1].homming_point = cmd.substring(cmd.indexOf(',') + 1, cmd.indexOf(')')).toInt();
          cmd = "";
          Serial.print("HomeSetted(");
          Serial.print(motor[0].homming_point);
          Serial.print(',');
          Serial.print(motor[1].homming_point);
          Serial.println(')');
        }
        if (cmd.substring(0, cmd.indexOf('(')) == "setbrake") {
          motor[0].brake = cmd.substring(cmd.indexOf('(') + 1, cmd.indexOf(',')).toInt();
          motor[1].brake = cmd.substring(cmd.indexOf(',') + 1, cmd.indexOf(')')).toInt();
          cmd = "";
          Serial.print("BrakeSetted(");
          Serial.print(motor[0].brake);
          Serial.print(',');
          Serial.print(motor[1].brake);
          Serial.println(')');
          Shield_Control.setBrakes(motor[0].brake, motor[1].brake);
        }

        motor[0].speed_setted = ((motor[0].set_point - motor[0].actual_point)*1.2);
        motor[1].speed_setted = ((motor[1].set_point - motor[1].actual_point)*1.2);

        if ((motor[0].speed_setted < DEAD_ZONE) && (motor[0].speed_setted > -DEAD_ZONE)) {
          motor[0].speed_setted = 0;
        }
        if ((motor[1].speed_setted < DEAD_ZONE) && (motor[1].speed_setted > -DEAD_ZONE)) {
          motor[1].speed_setted = 0;
        }
        if (motor[0].speed_setted >= DEAD_ZONE && motor[0].speed_setted < ZERO_VOLTAGE) {
          motor[0].speed_setted = ZERO_VOLTAGE;
        }else if (motor[0].speed_setted <= -DEAD_ZONE && motor[0].speed_setted > -ZERO_VOLTAGE) {
          motor[0].speed_setted = -ZERO_VOLTAGE;
        }
        if (motor[1].speed_setted >= DEAD_ZONE && motor[1].speed_setted < ZERO_VOLTAGE) {
          motor[1].speed_setted = ZERO_VOLTAGE;
        }else if (motor[1].speed_setted <= -DEAD_ZONE && motor[1].speed_setted > -ZERO_VOLTAGE) {
          motor[1].speed_setted = -ZERO_VOLTAGE;
        }
        
        /*if (((motor[1].set_point - motor[1].actual_point) < 20) && ((motor[1].set_point - motor[1].actual_point)>-20)){
          acceleration_filter = ((motor[1].speed_setted*(ACC_LIMIT_2-1)));
        } else {
          acceleration_filter = ((motor[1].speed_setted*(ACC_LIMIT_2-1)) + (motor[1].set_point - motor[1].actual_point));
        }
        if (((acceleration_filter%ACC_LIMIT_2)>0) && (motor[1].set_point - motor[1].actual_point)>motor[1].speed_setted){
          soft_raise++;
          if (soft_raise>ACC_LIMIT_2){
            motor[1].speed_setted ++;
            soft_raise = 0;
          }
          //motor[1].speed_setted = (acceleration_filter/ACC_LIMIT_2);
        } else {
          motor[1].speed_setted = (acceleration_filter/ACC_LIMIT_2);
        }*/
      }
  }
}

