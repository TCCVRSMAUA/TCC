using System;
using System.Windows.Forms;
using Ets2SdkClient;
using System.Linq;

namespace DataSender
{

    /*public enum arduino_state
    {
        DISCONNECTED,
        INITIALIZING,
        CONTROLLING,
        HOMMING_LOCK
    }

    public enum arduino_state_init
    {
        NOT_READY,
        GETTING_DATA,
        DATA_READY
    }*/

    public partial class Form1 : Form
    {
        private enum _command_request
        {
            SETPOINT,
            SETHOME,
            SETBRAKE
        }

        public Ets2SdkTelemetry Telemetry;
        private arduino_communication arduino;

        private System.Timers.Timer gui_admin;
        private String[] actual_ports;

        private _command_request command_request;

        private float pitch_rotation;
        //private float roll_rotation;

        private ulong current_time;

        private int motors_offset;

        public Form1()
        {
            InitializeComponent();
            foreach (Control ctrl in this.Controls)
                ctrl.Enabled = false;
            comboBox1.Enabled = true;
            button1.Enabled = true;
            trackBar4.Enabled = true;

            arduino = new arduino_communication();
            Telemetry = new Ets2SdkTelemetry();
            Telemetry.Data += Telemetry_Data;

            Telemetry.JobFinished += TelemetryOnJobFinished;
            Telemetry.JobStarted += TelemetryOnJobStarted;

            if (Telemetry.Error != null)
            {
                lbGeneral.Text =
                    "General info:\r\nFailed to open memory map " + Telemetry.Map +
                        " - on some systems you need to run the client (this app) with elevated permissions, because e.g. you're running Steam/ETS2 with elevated permissions as well. .NET reported the following Exception:\r\n" +
                        Telemetry.Error.Message + "\r\n\r\nStacktrace:\r\n" + Telemetry.Error.StackTrace;
            }

            this.actual_ports = new string[255];
            button1.Click += connect_arduino;
            button2.Click += sethome_request;
            button3.Click += setbrake_request;
            button5.Click += setstart_request;
            trackBar4.ValueChanged += change_control;

            gui_admin = new System.Timers.Timer { Interval = 5 };
            gui_admin.Elapsed += gui_admin_func;
            gui_admin.Start();

            current_time = 0;

            trackBar1.ValueChanged += new EventHandler(double_control);
            checkBox1.Click += new EventHandler(double_control_activated);

            this.FormClosing += Form1_FormClosing;
        }

        private void TelemetryOnJobFinished(object sender, EventArgs args)
        {
            //MessageBox.Show("Job finished, or at least unloaded nearby cargo destination.");
        }
        private void TelemetryOnJobStarted(object sender, EventArgs e)
        {
            //MessageBox.Show("Just started job OR loaded game with active.");
        }
        private void Telemetry_Data(Ets2Telemetry data, bool updated)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new TelemetryData(Telemetry_Data), new object[2] { data, updated });
                    return;
                }

                /*var dataToIno = data.Physics;
                var PitchRotation = data.Physics.RotationY;
                var RollRotation = data.Physics.RotationZ;*/

                pitch_rotation = data.Physics.RotationY;
                //roll_rotation = data.Physics.RotationZ;

                /*
                 * Heading:     valor-> (0.0 .. 1.0)        representa-> (0 .. 360)
                 * Pitching:    valor-> (-0.25 .. 0.25)     representa-> (-90 .. 90)
                 * Roll:        valor-> (-0.5 .. 0.5)       representa-> (-180 .. 180)
                 */

                /*label2.Text = String.Format("{0:N1}", dataToIno.RotationX * 360.0);
                label3.Text = String.Format("{0:N1}", dataToIno.RotationY * 360.0);
                label5.Text = String.Format("{0:N1}", dataToIno.RotationZ * 360.0);*/

                /*
                _serialPort.WriteLine("RotationX:");
                _serialPort.WriteLine(String.Format("{0:N0}", dataToIno.RotationX));
                _serialPort.WriteLine("RotationY:");
                _serialPort.WriteLine(String.Format("{0:N0}", dataToIno.RotationY));
                _serialPort.WriteLine("RotationZ:");
                _serialPort.WriteLine(String.Format("{0:N0}", dataToIno.RotationZ));
                 */

            }
            catch { }
        }

        private void gui_admin_func(Object source, System.Timers.ElapsedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new System.Timers.ElapsedEventHandler(gui_admin_func), new object[2] { source, e });
                    return;
                }
                catch (ObjectDisposedException exception_catched)
                {
                    Console.WriteLine(exception_catched.Message);
                    gui_admin.Stop();
                    return;
                }
            }
            label7.Text = trackBar1.Value.ToString();
            label8.Text = trackBar2.Value.ToString();
            label9.Text = trackBar3.Value.ToString();
            textBox1.Text = arduino.motors[0].position.ToString();
            textBox2.Text = arduino.motors[1].position.ToString();
            textBox3.Text = (pitch_rotation * 360).ToString();
            textBox4.Text = arduino.motors[0].setpoint.ToString();
            textBox5.Text = arduino.motors[1].setpoint.ToString();
            switch (arduino.state)
            {
                case _state.DISCONNECTED:
                    this.Text = "DISCONNECTED";
                    String[] ports = arduino.available_ports();
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
                    break;
                case _state.INITIALIZING:
                    this.Text = "INITIALIZING";
                    trackBar1.Value = Convert.ToInt32(arduino.motors[0].position);
                    trackBar2.Value = Convert.ToInt32(arduino.motors[1].position);
                    break;
                case _state.CONTROLLING:
                    //this.Text = "CONTROLLING";

                    current_time += 5;
                    chart1.Series["Setpoint1"].Points.AddXY(Convert.ToDouble(current_time) / 1000, arduino.motors[0].setpoint);
                    chart1.Series["Position1"].Points.AddXY(Convert.ToDouble(current_time) / 1000, arduino.motors[0].position);
                    chart1.Series["Setpoint2"].Points.AddXY(Convert.ToDouble(current_time) / 1000, arduino.motors[1].setpoint);
                    chart1.Series["Position2"].Points.AddXY(Convert.ToDouble(current_time) / 1000, arduino.motors[1].position);
                    chart1.ChartAreas[0].AxisX.LabelStyle.Enabled = false;
                    chart1.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
                    chart1.ChartAreas[0].AxisX.Maximum = Math.Round(Convert.ToDouble(current_time) / 1000, 2);
                    chart1.ChartAreas[0].AxisX.Minimum = Math.Round((Convert.ToDouble(current_time) / 1000) - 5.0, 2);
                    chart1.ChartAreas[0].AxisX.Interval = 1;
                    chart1.ChartAreas[0].AxisY.Maximum = 1024;
                    chart1.ChartAreas[0].AxisY.Minimum = 0;
                    chart1.ChartAreas[0].AxisY.Interval = 1024 / 16;

                    switch (command_request)
                    {
                        case _command_request.SETPOINT:
                            {
                                arduino.set_homming_calibration(trackBar4.Value == 1);
                                long[] setpoints;
                                if (trackBar4.Value == 1)
                                {
                                    if (arduino.command_type == command_types.HOME)
                                    {
                                        this.Text = "CONTROLLING - HOME";
                                        setpoints = new long[] { arduino.motors[0].home, arduino.motors[1].home };
                                    }
                                    else if (arduino.command_type == command_types.START)
                                    {
                                        this.Text = "CONTROLLING - START";
                                        setpoints = new long[] { arduino.motors[0].start, arduino.motors[1].start };
                                    }
                                    else{
                                        this.Text = "CONTROLLING - NORMAL";
                                        setpoints = new long[] { convert_to_arduino(pitch_rotation), convert_to_arduino(pitch_rotation) };
                                    }
                                }
                                else
                                {
                                    if (arduino.command_type == command_types.HOME)
                                    {
                                        this.Text = "CONTROLLING - HOME";
                                        setpoints = new long[] { arduino.motors[0].home, arduino.motors[1].home };
                                    }
                                    else if (arduino.command_type == command_types.START)
                                    {
                                        this.Text = "CONTROLLING - START";
                                        setpoints = new long[] { arduino.motors[0].start, arduino.motors[1].start };
                                    }
                                    else
                                    {
                                        this.Text = "CONTROLLING - NORMAL";
                                        setpoints = new long[] { trackBar1.Value, trackBar2.Value };
                                    }
                                }
                                    
                                
                                arduino.command(arduino_command.CONTROLLING, setpoints);
                            }
                            break;
                        case _command_request.SETHOME:
                            {
                                long[] sethomepoints = { trackBar1.Value, trackBar2.Value };
                                arduino.command(arduino_command.SETHOME, sethomepoints);
                                command_request = _command_request.SETPOINT;
                            }
                            break;
                        case _command_request.SETBRAKE:
                            {
                                long[] setbrakevalues = { trackBar3.Value, trackBar3.Value };
                                arduino.command(arduino_command.SETBRAKE, setbrakevalues);
                                command_request = _command_request.SETPOINT;
                            }
                            break;
                    }
                    break;
                case _state.HOMMING_LOCK:
                    this.Text = "HOMMING_LOCK";
                    break;
            }
        }

        private void connect_arduino(Object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex > -1)
            {
                bool connected = arduino.connect(comboBox1.SelectedItem.ToString());
                if (connected)
                {
                    button1.Text = "Disconnect";
                    button1.Click -= this.connect_arduino;
                    button1.Click += this.disconnect_arduino;

                    foreach (Control ctrl in this.Controls)
                        ctrl.Enabled = true;
                    comboBox1.Enabled = false;
                    trackBar4.Enabled = false;
                }
            }
        }
        private void disconnect_arduino(Object sender, EventArgs e)
        {
            button1.Text = "Connect";
            button1.Click -= this.disconnect_arduino;
            button1.Click += this.connect_arduino;
            arduino.disconnect();
            foreach (Control ctrl in this.Controls)
                ctrl.Enabled = false;
            comboBox1.Enabled = true;
            button1.Enabled = true;
            trackBar4.Enabled = true;
        }

        private void change_control(Object sender, EventArgs e)
        {
            if (trackBar4.Value == 0)
            {
                tabControl1.TabPages.Remove(tabPage2);
                tabControl1.TabPages.Insert(0, tabPage1);
                tabControl1.SelectedTab = tabPage1;
            }
            else
            {
                tabControl1.TabPages.Remove(tabPage1);
                tabControl1.TabPages.Insert(0, tabPage2);
                tabControl1.SelectedTab = tabPage2;
            }
            arduino.state = _state.INITIALIZING;
        }

        private void sethome_request(Object sender, EventArgs e)
        {
            command_request = _command_request.SETHOME;
        }
        private void setstart_request(Object sender, EventArgs e)
        {
            arduino.motors[0].start = trackBar1.Value;
            arduino.motors[1].start = trackBar2.Value;
        }
        private void setbrake_request(Object sender, EventArgs e)
        {
            command_request = _command_request.SETBRAKE;
        }

        private void double_control(Object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                int second_motor = trackBar1.Value + motors_offset;
                if (second_motor > -1 && second_motor < 1024)
                {
                    trackBar2.Value = second_motor;
                } else if (second_motor < 0){
                    trackBar1.Value = (-motors_offset);
                } else if (second_motor > 1023)
                {
                    trackBar1.Value = 1023 - motors_offset;
                }
            }
        }
        private void double_control_activated(Object sender, EventArgs e)
        {
            trackBar2.Enabled = !checkBox1.Checked;
            if (checkBox1.Checked)
                motors_offset = trackBar2.Value - trackBar1.Value;
        }

        private long convert_to_arduino(float angle_from_game)
        {
            double to_degrees = angle_from_game*360.0;
            to_degrees = Math.Round(to_degrees, 1);
            if (to_degrees > 5) to_degrees = 5;
            if (to_degrees < -5) to_degrees = -5;
            if (to_degrees >= 0)
                return Convert.ToInt64(50*to_degrees);
            else
                return Convert.ToInt64(30*to_degrees);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            System.Collections.Generic.List<System.Windows.Forms.DataVisualization.Charting.DataPoint> setpoint1_graph = 
                chart1.Series["Setpoint1"].Points.ToList<System.Windows.Forms.DataVisualization.Charting.DataPoint>();
            System.Collections.Generic.List<System.Windows.Forms.DataVisualization.Charting.DataPoint> position1_graph =
                chart1.Series["Position1"].Points.ToList<System.Windows.Forms.DataVisualization.Charting.DataPoint>();
            System.Collections.Generic.List<System.Windows.Forms.DataVisualization.Charting.DataPoint> setpoint2_graph =
                chart1.Series["Setpoint2"].Points.ToList<System.Windows.Forms.DataVisualization.Charting.DataPoint>();
            System.Collections.Generic.List<System.Windows.Forms.DataVisualization.Charting.DataPoint> position2_graph =
                chart1.Series["Position2"].Points.ToList<System.Windows.Forms.DataVisualization.Charting.DataPoint>();
            Clipboard.Clear();
            String[] data_to_clipboard = new String[setpoint1_graph.Count+1];
            data_to_clipboard[0] = "Tempo(s)\tSetpoint1\tPosition1\tSetpoint2\tPosition2";
            for (int i = 0; i < setpoint1_graph.Count; i++)
            {
                data_to_clipboard[i + 1] = String.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                    setpoint1_graph[i].XValue, setpoint1_graph[i].YValues[0],
                    position1_graph[i].YValues[0],
                    setpoint2_graph[i].YValues[0],
                    position2_graph[i].YValues[0]);
            }
            Clipboard.SetText(String.Join("\n", data_to_clipboard));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            gui_admin.Stop();
            gui_admin.Dispose();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                trackBar1.Value = Convert.ToInt32(textBox6.Text);
                trackBar2.Value = Convert.ToInt32(textBox7.Text);
            }
            catch (Exception except)
            {
                MessageBox.Show("Apenas Números: " + except.ToString());
            }
        }
    }
}
