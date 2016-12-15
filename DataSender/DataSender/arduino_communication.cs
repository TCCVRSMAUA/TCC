using System;
using System.IO.Ports;
using System.Timers;

namespace DataSender
{
    public enum _state
    {
        DISCONNECTED,
        INITIALIZING,
        CONTROLLING,
        HOMMING_LOCK
    }

    public enum arduino_command
    {
        CONTROLLING,
        SETHOME,
        SETBRAKE
    }

    public enum command_types
    {
        HOME,
        START,
        NORMAL
    }

    class arduino_communication
    {
        private enum _state_init
        {
            NOT_READY,
            GETTING_DATA,
            DATA_READY
        }
        private enum _state_ctrl
        {
            CONTROLLING,
            SETHOME,
            SETBRAKE
        }
        public struct motor
        {
            public long position;
            public long setpoint;
            public long brake;
            public long home;
            public long start;
        }

        Timer arduino_admin;
        private ulong admin_clock;

        private SerialPort arduino_serial;
        public _state state;
        private _state_init state_init;
        private _state_ctrl state_ctrl;

        private bool command_valid;
        private String command_checker;
        private ulong command_timeout;

        private long[] values_received;
        private long[] values_transmit;

        private uint calibrate_counter;
        public motor[] motors { get; private set; }
        public command_types command_type { get; private set; }

        private bool homming_calibration;
        private bool turn_off_thread;

        public arduino_communication()
        {
            arduino_admin = new Timer { Interval = 1 };
            arduino_admin.Elapsed += arduino_admin_func;
            arduino_admin.Start();

            arduino_serial = new SerialPort();
            //arduino_serial.DataReceived += new SerialDataReceivedEventHandler(serial_data_received);
            command_valid = false;
            values_received = new long[2];
            values_transmit = new long[2];
            motors = new motor[2];
            calibrate_counter = 0;
        }
        public bool connect(String port_name)
        {
            try
            {
                arduino_serial.PortName = port_name;
                arduino_serial.BaudRate = 115200;
                arduino_serial.Handshake = Handshake.RequestToSend;
                arduino_serial.DtrEnable = true;
                arduino_serial.RtsEnable = true;
                arduino_serial.Open();
                //arduino_serial = new SerialPort(port_name, 9600);//, Parity.None, 8, StopBits.One);
            }
            catch
            {
                return false;
            }

            state = _state.INITIALIZING;
            state_init = _state_init.NOT_READY;
            state_ctrl = _state_ctrl.CONTROLLING;
            return true;
        }
        public void disconnect()
        {
            arduino_serial.DiscardInBuffer();
            arduino_serial.DiscardOutBuffer();
            arduino_serial.Close();
            state = _state.DISCONNECTED;
        }
        public String[] available_ports()
        {
            return SerialPort.GetPortNames();
        }
        public void command(arduino_command cmd, long[] values)
        {
            if (values.Length != 2) throw new ArgumentException("Value invalid length");
            switch (cmd)
            {
                case arduino_command.CONTROLLING:
                    if (homming_calibration)
                    {
                        motors[0].setpoint = values[0] + motors[0].home;
                        motors[1].setpoint = values[1] + motors[1].home;
                    }
                    else
                    {
                        motors[0].setpoint = values[0];
                        motors[1].setpoint = values[1];
                    }
                    if (motors[0].setpoint < 0) motors[0].setpoint = 0;
                    else if (motors[0].setpoint > 1023) motors[0].setpoint = 1023;
                    if (motors[1].setpoint < 0) motors[1].setpoint = 0;
                    else if (motors[1].setpoint > 1023) motors[1].setpoint = 1023;
                    break;
                case arduino_command.SETBRAKE:
                    motors[0].brake = values[0];
                    motors[1].brake = values[1];
                    state_ctrl = _state_ctrl.SETBRAKE;
                    break;
                case arduino_command.SETHOME:
                    motors[0].home = values[0];
                    motors[1].home = values[1];
                    state_ctrl = _state_ctrl.SETHOME;
                    break;
            }
        }
        public void set_homming_calibration(bool on_off)
        {
            homming_calibration = on_off;
        }

        private void arduino_admin_func(Object source, ElapsedEventArgs e)
        {
            if (arduino_serial != null)
                if (!arduino_serial.IsOpen)
                    state = _state.DISCONNECTED;
            admin_clock++;
            if (arduino_serial.IsOpen)
                if (arduino_serial.BytesToRead > 0)
                    this.serial_data_received(null, null);
            switch (state)
            {
                case _state.DISCONNECTED:
                    /*if (arduino_serial.IsOpen)
                    {
                        state = _state.INITIALIZING;
                        return;
                    }*/
                    /*if (!comboBox1.InvokeRequired)
                    {
                        String[] ports = SerialPort.GetPortNames();
                        if (this.actual_ports.Length == 255)
                        {
                            foreach (String port in ports)
                                comboBox1.Items.Add(port);
                            comboBox1.Refresh();
                        }
                        else if (!this.actual_ports.SequenceEqual(ports))
                        {
                            comboBox1.Items.Clear();
                            foreach (String port in ports)
                                comboBox1.Items.Add(port);
                            comboBox1.Refresh();
                        }
                        this.actual_ports = ports;
                    }*/
                    break;
                case _state.INITIALIZING:
                    switch (state_init)
                    {
                        case _state_init.NOT_READY:
                            if (command_valid)
                            {
                                state_init++;
                                command_valid = false;
                                command_timeout = 0;
                                command_checker = "";
                                return;
                            }
                            if (command_checker == "" || admin_clock > command_timeout)
                            {
                                //Console.WriteLine("ReadyPc");
                                arduino_serial.WriteLine("ReadyPc");
                                command_checker = "ReadyAr";
                                command_timeout = admin_clock + 10;
                                calibrate_counter = 0;
                            }
                            break;
                        case _state_init.GETTING_DATA:
                            if (command_valid)
                            {
                                calibrate_counter++;
                                //motors[0].position = ((motors[0].position * (calibrate_counter - 1)) + values_received[0])/ calibrate_counter;
                                //motors[1].position = ((motors[1].position * (calibrate_counter - 1)) + values_received[1]) / calibrate_counter;
                                motors[0].position = values_received[0];
                                motors[1].position = values_received[1];
                                if (calibrate_counter > 128)
                                {
                                    motors[0].setpoint = motors[0].position;
                                    motors[1].setpoint = motors[1].position;
                                    state_init++;
                                }
                                command_valid = false;
                                command_timeout = 0;
                                command_checker = "";
                            }
                            if (command_checker == "" || admin_clock > command_timeout)
                            {
                                arduino_serial.WriteLine("GetData");
                                command_checker = "SendData";
                                command_timeout = admin_clock + 10;
                            }
                            break;
                        case _state_init.DATA_READY:
                            if (command_valid)
                            {
                                state++;
                                command_valid = false;
                                command_timeout = 0;
                                command_checker = "";
                                return;
                            }
                            if (command_checker == "" || admin_clock > command_timeout)
                            {
                                arduino_serial.WriteLine("DataSetup");
                                command_checker = "ArduinoSetup";
                                command_timeout = admin_clock + 10;
                            }
                            break;
                    }
                    break;
                case _state.CONTROLLING:
                    if (command_valid)
                    {
                        if (command_checker.Equals("SetpointSetted"))
                        {
                            motors[0].position = values_received[0];
                            motors[1].position = values_received[1];
                        }
                        else if (command_checker.Equals("BrakeSetted"))
                        {
                            if (motors[0].brake != values_received[0] || motors[1].brake != values_received[1]);
                        }
                        else if (command_checker.Equals("HomeSetted"))
                        {
                            if (motors[0].home != values_received[0] || motors[1].home != values_received[1]);
                        }
                        command_valid = false;
                        command_timeout = 0;
                        command_checker = "";
                        return;
                    }
                    if (command_checker == "" || admin_clock > command_timeout)
                        switch (state_ctrl)
                        {
                            case _state_ctrl.CONTROLLING:
                                arduino_serial.WriteLine(String.Format("Controlling({0},{1})", motors[0].setpoint, motors[1].setpoint));
                                command_checker = "SetpointSetted";
                                break;
                            case _state_ctrl.SETBRAKE:
                                arduino_serial.WriteLine(String.Format("setbrake({0},{1})", motors[0].brake, motors[1].brake));
                                command_checker = "BrakeSetted";
                                state_ctrl = _state_ctrl.CONTROLLING;
                                break;
                            case _state_ctrl.SETHOME:
                                arduino_serial.WriteLine(String.Format("sethome({0},{1})", motors[0].home, motors[1].home));
                                command_checker = "HomeSetted";
                                state_ctrl = _state_ctrl.CONTROLLING;
                                break;
                        }
                    break;
                case _state.HOMMING_LOCK:
                    if (command_checker == "" || admin_clock > command_timeout)
                    {
                        arduino_serial.WriteLine("TesteIno");
                        command_checker = "InoCtrl";
                        command_timeout += admin_clock + 10;
                    }
                    break;
            }
        }

        private void serial_data_received(object sender, SerialDataReceivedEventArgs e)
        {
            String command_feedback;
            try
            {
                command_feedback = arduino_serial.ReadLine();
                //Console.WriteLine(command_feedback);
            }
            catch
            {
                return;
            }
            if (command_checker == null) return;
            if (!command_feedback.Contains(command_checker))
            {
                //throw new InvalidOperationException("Invalid command feedback");
            }
            else
            {
                command_valid = true;
                if (command_feedback.IndexOf('(') > -1 && command_feedback.IndexOf(',') > -1 && command_feedback.IndexOf(')') > -1)
                {
                    int startchar = command_feedback.IndexOf('(') + 1;
                    int numlength = (command_feedback.IndexOf(',') - startchar);
                    values_received[0] = Convert.ToInt64(command_feedback.Substring(startchar, numlength));
                    startchar = command_feedback.IndexOf(',') + 1;
                    numlength = (command_feedback.IndexOf(')') - startchar);
                    values_received[1] = Convert.ToInt64(command_feedback.Substring(startchar, numlength));
                }
                if (command_checker == "InoCtrl")
                    state = _state.CONTROLLING;
            }

            if (command_feedback.Contains("Homming"))
            {
                command_type = command_types.HOME;
            } else if (command_feedback.Contains("Start"))
            {
                command_type = command_types.START;
            } else if (command_feedback.Contains("Normal"))
            {
                command_type = command_types.NORMAL;
            }
            //if(command_feedback.Contains("Apertou"))
                //Console.WriteLine("Received Data: "+ command_feedback);
        }
    }
}
