using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO.Ports;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;
using System.Threading;

namespace EAR
{
    public partial class Form1 : Form
    {
        private string[] m_old_serialPortNames;
        private bool m_SerialPortOpened = false;

        private string m_mode1_cfgFilePath = Environment.CurrentDirectory + @"\" + "config_mode1.ini";
        private string m_mode2_cfgFilePath = Environment.CurrentDirectory + @"\" + "config_mode2.ini";
        private string m_mode3_cfgFilePath = Environment.CurrentDirectory + @"\" + "config_mode3.ini";
        private string m_commPara_cfgFilePath = Environment.CurrentDirectory + @"\" + "comm_para.ini";
        private List<PARAMETER> m_mode1_list = new List<PARAMETER>();
        private List<PARAMETER> m_mode2_list = new List<PARAMETER>();
        private List<PARAMETER> m_mode3_list = new List<PARAMETER>();
        private List<CHECK_STATE> m_SerialChecked_List = new List<CHECK_STATE>();
        private COMM_PARA m_commPara = new COMM_PARA();
        private List<byte> m_buffer = new List<byte>();

        private Int32 m_combox_prev_selectIndex = 0; //用来记录模式选择的前一个index

        private const int HEAD = 0;
        private const int LEN = 1;
        private const int CMDTYPE = 2;
        private const int FRAME_ID = 3;


        private int m_rtcInfo_record_numbers;  //记录收到的rtc信息条数
        //private int m_prev_pack_No=0;
        private int m_total_frames = 0;  //总帧数
        private int m_requestNo = 1;
        private List<RTC_INFO> m_rtc_data_list = new List<RTC_INFO>();
        private bool m_b_saveRtcFile = true;
        private TimeSpan m_duration ;
        private float m_sampling_rate ;

        //private Form_Sensor m_Fm_sensor;
        //private List<SENSOR_DATA> m_honeywellData_list = new List<SENSOR_DATA>();
        //private DataTable m_table = new DataTable();
        public delegate void ShowChartHandler(List<SENSOR_DATA> data_buffer);   //定义委托
        //public event ShowChartHandler ShowChart;

        private List<SENSOR_DATA> m_HWData_list = new List<SENSOR_DATA>();
        private List<SENSOR_DATA> m_HW_buffer = new List<SENSOR_DATA>();  //解决线程问题
        private int m_recv_cnt = 0;
        private bool b_stop_geting_HW_data = false;
        private bool b_start_HW_data = false;
        //private Thread m_thread_show_chart;
        //private DataTable table1;
        private int HONEYWELL_STRC_DATA_SIZE = 30; //#define HONEYWELL_STRC_DATA_SIZE 15  根据下位机而来的

        private DateTime m_tm_start;
        //private DateTime m_tm_end;

        public struct RTC_INFO
        {
            public byte RTC_CODE;
            public byte RTC_RESERVED;
            public byte RTC_YEAR;
            public byte RTC_MONTH;
            public byte RTC_DAY;
            public byte RTC_HOUR;
            public byte RTC_MIN;
            public byte RTC_SEC;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private struct COMM_PARA
        {
            public byte EXHALATION_THRESHOLD;
            public byte WAIT_BEFORE_START;
        }

        public class SENSOR_DATA
        {
            //public byte YEAR;
            //public byte MONTH;
            //public byte DAY;
            //public byte HOUR;
            //public byte MIN;
            //public byte SECOND;
            public byte DATA_0;
            public byte DATA_1;
        }


        private struct CHECK_STATE
        {
            public byte PWM_SERIAL;
            public byte CHECKED;
        }


        private struct PARAMETER
        {
            public byte MODE_SELECTED;        //模式选择
            
            public byte PWM_SERIAL_SELECTED;         //PWM_SERIAL 例如11(PWM1-SERIAL1)，32(PWM3-SERIAL2)
            public byte ENABLE;               //1-开启
            public byte FREQUENCE;
            public byte DUTY_CYCLE;
            public byte PERIOD;
            public byte NUM_OF_CYCLES;
            public byte WAIT_BETWEEN;
            public byte WAIT_AFTER;
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            //屏蔽线程检查
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;

            //双缓冲，解决大量数据刷新界面的问题
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);

            InitApp();

            //if (m_thread_show_chart != null)
            //{
            //    m_thread_show_chart = new Thread(new ParameterizedThreadStart(show_chart));
            //    m_thread_show_chart.Start(m_HW_buffer);
            //}
            ////m_thread_show_chart = new Thread(new ParameterizedThreadStart(show_chart));

        }

        private void SetPWMDefaultParameter()
        {
            #region
            ////可以使用二维数组，后续再改
            //TextBox[,] arr_pwm1 = new TextBox[,]{
            //    {this.textBox_freq_PWM1_serial1,this.textBox_dutyCycle_PWM1_serial1,this.textBox_period_PWM1_serial1,this.textBox_numberOfCycles_PWM1_serial1,this.textBox_waitBetween_PWM1_serial1,this.textBox_waitAfter_PWM1_serial1},
            //};

            //checkBox全部为true
            #region
            this.checkBox_PWM1_serial1.Checked = true;
            this.checkBox_PWM1_serial2.Checked = true;
            this.checkBox_PWM1_serial3.Checked = true;
            this.checkBox_PWM1_serial4.Checked = true;
            this.checkBox_PWM1_serial5.Checked = true;
            this.checkBox_PWM1_serial6.Checked = true;
            this.checkBox_PWM2_serial1.Checked = true;
            this.checkBox_PWM2_serial2.Checked = true;
            this.checkBox_PWM2_serial3.Checked = true;
            this.checkBox_PWM2_serial4.Checked = true;
            this.checkBox_PWM2_serial5.Checked = true;
            this.checkBox_PWM2_serial6.Checked = true;
            this.checkBox_PWM3_serial1.Checked = true;
            this.checkBox_PWM3_serial2.Checked = true;
            this.checkBox_PWM3_serial3.Checked = true;
            this.checkBox_PWM3_serial4.Checked = true;
            this.checkBox_PWM3_serial5.Checked = true;
            this.checkBox_PWM3_serial6.Checked = true;
            #endregion

            //频率默认1Hz，范围为：[1,255Hz]
            #region
            this.textBox_freq_PWM1_serial1.Text = "1";
            this.textBox_freq_PWM1_serial2.Text = "1";
            this.textBox_freq_PWM1_serial3.Text = "1";
            this.textBox_freq_PWM1_serial4.Text = "1";
            this.textBox_freq_PWM1_serial5.Text = "1";
            this.textBox_freq_PWM1_serial6.Text = "1";
            this.textBox_freq_PWM2_serial1.Text = "1";
            this.textBox_freq_PWM2_serial2.Text = "1";
            this.textBox_freq_PWM2_serial3.Text = "1";
            this.textBox_freq_PWM2_serial4.Text = "1";
            this.textBox_freq_PWM2_serial5.Text = "1";
            this.textBox_freq_PWM2_serial6.Text = "1";
            this.textBox_freq_PWM3_serial1.Text = "1";
            this.textBox_freq_PWM3_serial2.Text = "1";
            this.textBox_freq_PWM3_serial3.Text = "1";
            this.textBox_freq_PWM3_serial4.Text = "1";
            this.textBox_freq_PWM3_serial5.Text = "1";
            this.textBox_freq_PWM3_serial6.Text = "1";

            this.textBox_freq_PWM1_serial1.Enabled = true;
            this.textBox_freq_PWM1_serial2.Enabled = true;
            this.textBox_freq_PWM1_serial3.Enabled = true;
            this.textBox_freq_PWM1_serial4.Enabled = true;
            this.textBox_freq_PWM1_serial5.Enabled = true;
            this.textBox_freq_PWM1_serial6.Enabled = true;
            this.textBox_freq_PWM2_serial1.Enabled = true;
            this.textBox_freq_PWM2_serial2.Enabled = true;
            this.textBox_freq_PWM2_serial3.Enabled = true;
            this.textBox_freq_PWM2_serial4.Enabled = true;
            this.textBox_freq_PWM2_serial5.Enabled = true;
            this.textBox_freq_PWM2_serial6.Enabled = true;
            this.textBox_freq_PWM3_serial1.Enabled = true;
            this.textBox_freq_PWM3_serial2.Enabled = true;
            this.textBox_freq_PWM3_serial3.Enabled = true;
            this.textBox_freq_PWM3_serial4.Enabled = true;
            this.textBox_freq_PWM3_serial5.Enabled = true;
            this.textBox_freq_PWM3_serial6.Enabled = true;
            #endregion

            //dutyCycle默认为10%，范围为：[10%,90%]
            #region
            this.textBox_dutyCycle_PWM1_serial1.Text = "10";
            this.textBox_dutyCycle_PWM1_serial2.Text = "10";
            this.textBox_dutyCycle_PWM1_serial3.Text = "10";
            this.textBox_dutyCycle_PWM1_serial4.Text = "10";
            this.textBox_dutyCycle_PWM1_serial5.Text = "10";
            this.textBox_dutyCycle_PWM1_serial6.Text = "10";
            this.textBox_dutyCycle_PWM2_serial1.Text = "10";
            this.textBox_dutyCycle_PWM2_serial2.Text = "10";
            this.textBox_dutyCycle_PWM2_serial3.Text = "10";
            this.textBox_dutyCycle_PWM2_serial4.Text = "10";
            this.textBox_dutyCycle_PWM2_serial5.Text = "10";
            this.textBox_dutyCycle_PWM2_serial6.Text = "10";
            this.textBox_dutyCycle_PWM3_serial1.Text = "10";
            this.textBox_dutyCycle_PWM3_serial2.Text = "10";
            this.textBox_dutyCycle_PWM3_serial3.Text = "10";
            this.textBox_dutyCycle_PWM3_serial4.Text = "10";
            this.textBox_dutyCycle_PWM3_serial5.Text = "10";
            this.textBox_dutyCycle_PWM3_serial6.Text = "10";

            this.textBox_dutyCycle_PWM1_serial1.Enabled = true;
            this.textBox_dutyCycle_PWM1_serial2.Enabled = true;
            this.textBox_dutyCycle_PWM1_serial3.Enabled = true;
            this.textBox_dutyCycle_PWM1_serial4.Enabled = true;
            this.textBox_dutyCycle_PWM1_serial5.Enabled = true;
            this.textBox_dutyCycle_PWM1_serial6.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial1.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial2.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial3.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial4.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial5.Enabled = true;
            this.textBox_dutyCycle_PWM2_serial6.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial1.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial2.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial3.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial4.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial5.Enabled = true;
            this.textBox_dutyCycle_PWM3_serial6.Enabled = true;
            #endregion

            //period默认为1，范围为：[1,255s]
            #region
            this.textBox_period_PWM1_serial1.Text = "1";
            this.textBox_period_PWM1_serial2.Text = "1";
            this.textBox_period_PWM1_serial3.Text = "1";
            this.textBox_period_PWM1_serial4.Text = "1";
            this.textBox_period_PWM1_serial5.Text = "1";
            this.textBox_period_PWM1_serial6.Text = "1";
            this.textBox_period_PWM2_serial1.Text = "1";
            this.textBox_period_PWM2_serial2.Text = "1";
            this.textBox_period_PWM2_serial3.Text = "1";
            this.textBox_period_PWM2_serial4.Text = "1";
            this.textBox_period_PWM2_serial5.Text = "1";
            this.textBox_period_PWM2_serial6.Text = "1";
            this.textBox_period_PWM3_serial1.Text = "1";
            this.textBox_period_PWM3_serial2.Text = "1";
            this.textBox_period_PWM3_serial3.Text = "1";
            this.textBox_period_PWM3_serial4.Text = "1";
            this.textBox_period_PWM3_serial5.Text = "1";
            this.textBox_period_PWM3_serial6.Text = "1";

            this.textBox_period_PWM1_serial1.Enabled = true;
            this.textBox_period_PWM1_serial2.Enabled = true;
            this.textBox_period_PWM1_serial3.Enabled = true;
            this.textBox_period_PWM1_serial4.Enabled = true;
            this.textBox_period_PWM1_serial5.Enabled = true;
            this.textBox_period_PWM1_serial6.Enabled = true;
            this.textBox_period_PWM2_serial1.Enabled = true;
            this.textBox_period_PWM2_serial2.Enabled = true;
            this.textBox_period_PWM2_serial3.Enabled = true;
            this.textBox_period_PWM2_serial4.Enabled = true;
            this.textBox_period_PWM2_serial5.Enabled = true;
            this.textBox_period_PWM2_serial6.Enabled = true;
            this.textBox_period_PWM3_serial1.Enabled = true;
            this.textBox_period_PWM3_serial2.Enabled = true;
            this.textBox_period_PWM3_serial3.Enabled = true;
            this.textBox_period_PWM3_serial4.Enabled = true;
            this.textBox_period_PWM3_serial5.Enabled = true;
            this.textBox_period_PWM3_serial6.Enabled = true;
            #endregion

            //Number of Cycle 默认为1，范围为：[1,250]
            #region
            this.textBox_numberOfCycles_PWM1_serial1.Text = "1";
            this.textBox_numberOfCycles_PWM1_serial2.Text = "1";
            this.textBox_numberOfCycles_PWM1_serial3.Text = "1";
            this.textBox_numberOfCycles_PWM1_serial4.Text = "1";
            this.textBox_numberOfCycles_PWM1_serial5.Text = "1";
            this.textBox_numberOfCycles_PWM1_serial6.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial1.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial2.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial3.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial4.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial5.Text = "1";
            this.textBox_numberOfCycles_PWM2_serial6.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial1.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial2.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial3.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial4.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial5.Text = "1";
            this.textBox_numberOfCycles_PWM3_serial6.Text = "1";

            this.textBox_numberOfCycles_PWM1_serial1.Enabled = true;
            this.textBox_numberOfCycles_PWM1_serial2.Enabled = true;
            this.textBox_numberOfCycles_PWM1_serial3.Enabled = true;
            this.textBox_numberOfCycles_PWM1_serial4.Enabled = true;
            this.textBox_numberOfCycles_PWM1_serial5.Enabled = true;
            this.textBox_numberOfCycles_PWM1_serial6.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial1.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial2.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial3.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial4.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial5.Enabled = true;
            this.textBox_numberOfCycles_PWM2_serial6.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial1.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial2.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial3.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial4.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial5.Enabled = true;
            this.textBox_numberOfCycles_PWM3_serial6.Enabled = true;
            #endregion

            //Wait Between 默认为0，范围为：[0,255s]
            #region
            this.textBox_waitBetween_PWM1_serial1.Text = "0";
            this.textBox_waitBetween_PWM1_serial2.Text = "0";
            this.textBox_waitBetween_PWM1_serial3.Text = "0";
            this.textBox_waitBetween_PWM1_serial4.Text = "0";
            this.textBox_waitBetween_PWM1_serial5.Text = "0";
            this.textBox_waitBetween_PWM1_serial6.Text = "0";
            this.textBox_waitBetween_PWM2_serial1.Text = "0";
            this.textBox_waitBetween_PWM2_serial2.Text = "0";
            this.textBox_waitBetween_PWM2_serial3.Text = "0";
            this.textBox_waitBetween_PWM2_serial4.Text = "0";
            this.textBox_waitBetween_PWM2_serial5.Text = "0";
            this.textBox_waitBetween_PWM2_serial6.Text = "0";
            this.textBox_waitBetween_PWM3_serial1.Text = "0";
            this.textBox_waitBetween_PWM3_serial2.Text = "0";
            this.textBox_waitBetween_PWM3_serial3.Text = "0";
            this.textBox_waitBetween_PWM3_serial4.Text = "0";
            this.textBox_waitBetween_PWM3_serial5.Text = "0";
            this.textBox_waitBetween_PWM3_serial6.Text = "0";

            this.textBox_waitBetween_PWM1_serial1.Enabled = true;
            this.textBox_waitBetween_PWM1_serial2.Enabled = true;
            this.textBox_waitBetween_PWM1_serial3.Enabled = true;
            this.textBox_waitBetween_PWM1_serial4.Enabled = true;
            this.textBox_waitBetween_PWM1_serial5.Enabled = true;
            this.textBox_waitBetween_PWM1_serial6.Enabled = true;
            this.textBox_waitBetween_PWM2_serial1.Enabled = true;
            this.textBox_waitBetween_PWM2_serial2.Enabled = true;
            this.textBox_waitBetween_PWM2_serial3.Enabled = true;
            this.textBox_waitBetween_PWM2_serial4.Enabled = true;
            this.textBox_waitBetween_PWM2_serial5.Enabled = true;
            this.textBox_waitBetween_PWM2_serial6.Enabled = true;
            this.textBox_waitBetween_PWM3_serial1.Enabled = true;
            this.textBox_waitBetween_PWM3_serial2.Enabled = true;
            this.textBox_waitBetween_PWM3_serial3.Enabled = true;
            this.textBox_waitBetween_PWM3_serial4.Enabled = true;
            this.textBox_waitBetween_PWM3_serial5.Enabled = true;
            this.textBox_waitBetween_PWM3_serial6.Enabled = true;
            #endregion

            //Wait After 默认为0，范围为：[0,255s]
            #region
            this.textBox_waitAfter_PWM1_serial1.Text = "0";
            this.textBox_waitAfter_PWM1_serial2.Text = "0";
            this.textBox_waitAfter_PWM1_serial3.Text = "0";
            this.textBox_waitAfter_PWM1_serial4.Text = "0";
            this.textBox_waitAfter_PWM1_serial5.Text = "0";
            this.textBox_waitAfter_PWM1_serial6.Text = "0";
            this.textBox_waitAfter_PWM2_serial1.Text = "0";
            this.textBox_waitAfter_PWM2_serial2.Text = "0";
            this.textBox_waitAfter_PWM2_serial3.Text = "0";
            this.textBox_waitAfter_PWM2_serial4.Text = "0";
            this.textBox_waitAfter_PWM2_serial5.Text = "0";
            this.textBox_waitAfter_PWM2_serial6.Text = "0";
            this.textBox_waitAfter_PWM3_serial1.Text = "0";
            this.textBox_waitAfter_PWM3_serial2.Text = "0";
            this.textBox_waitAfter_PWM3_serial3.Text = "0";
            this.textBox_waitAfter_PWM3_serial4.Text = "0";
            this.textBox_waitAfter_PWM3_serial5.Text = "0";
            this.textBox_waitAfter_PWM3_serial6.Text = "0";

            this.textBox_waitAfter_PWM1_serial1.Enabled = true;
            this.textBox_waitAfter_PWM1_serial2.Enabled = true;
            this.textBox_waitAfter_PWM1_serial3.Enabled = true;
            this.textBox_waitAfter_PWM1_serial4.Enabled = true;
            this.textBox_waitAfter_PWM1_serial5.Enabled = true;
            this.textBox_waitAfter_PWM1_serial6.Enabled = true;
            this.textBox_waitAfter_PWM2_serial1.Enabled = true;
            this.textBox_waitAfter_PWM2_serial2.Enabled = true;
            this.textBox_waitAfter_PWM2_serial3.Enabled = true;
            this.textBox_waitAfter_PWM2_serial4.Enabled = true;
            this.textBox_waitAfter_PWM2_serial5.Enabled = true;
            this.textBox_waitAfter_PWM2_serial6.Enabled = true;
            this.textBox_waitAfter_PWM3_serial1.Enabled = true;
            this.textBox_waitAfter_PWM3_serial2.Enabled = true;
            this.textBox_waitAfter_PWM3_serial3.Enabled = true;
            this.textBox_waitAfter_PWM3_serial4.Enabled = true;
            this.textBox_waitAfter_PWM3_serial5.Enabled = true;
            this.textBox_waitAfter_PWM3_serial6.Enabled = true;
            #endregion

            #endregion
        }

        private void ReadCommPara2Struct()
        {
            FileStream fs = new FileStream(m_commPara_cfgFilePath, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.ASCII);

            byte[] buffer=new byte[2];
            br.Read(buffer, 0, 2);
            
            m_commPara.EXHALATION_THRESHOLD = buffer[0];
            m_commPara.WAIT_BEFORE_START = buffer[1];
            this.textBox_exhalationThreshold.Text = Convert.ToString(buffer[0]/16);
            this.textBox_exhalation_threshold_decimal_fraction.Text = Convert.ToString(buffer[0] % 16);
            this.textBox_waitBeforeStart.Text = Convert.ToString(buffer[1]);

            br.Close();
            fs.Close();
        }

        private void WriteCommPara2File()
        {
            FileStream fs = new FileStream(m_commPara_cfgFilePath, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII);

            bw.Write(m_commPara.EXHALATION_THRESHOLD);
            bw.Write(m_commPara.WAIT_BEFORE_START);

            bw.Close();
            fs.Close();
        }

        private void InitCommParameter()
        {
            if(File.Exists(m_commPara_cfgFilePath))
            {
                ReadCommPara2Struct();
            }
            else
            {
                textBox_exhalationThreshold.Text = "2";
                textBox_exhalation_threshold_decimal_fraction.Text = "0";

                textBox_waitBeforeStart.Text = "1";
                m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(5);
                m_commPara.WAIT_BEFORE_START = Convert.ToByte(3);
            }
        }

        private void InitModeSelect()
        {
            #region
            string[] modes = new string[] { "Mode1", "Mode2", "Mode3" };
            this.comboBox_modeSelect.Items.AddRange(modes);
            this.comboBox_modeSelect.SelectedIndex = 0;
            #endregion
        }

        private void InitSerialPort()
        {
            #region
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length != 0)
            {
                //Array.Sort(ports);
                Array.Sort(ports, (a, b) => Convert.ToInt32(((string)a).Substring(3)).CompareTo(Convert.ToInt32(((string)b).Substring(3))));
                m_old_serialPortNames = ports;
                this.comboBox_portName.Items.AddRange(ports);
                this.comboBox_portName.SelectedIndex = 0;
            }

            this.comboBox_baud.Text = "115200";
            this.comboBox_dataBits.Text = "8";
            this.comboBox_stopBit.Text = "one";
            this.comboBox_parity.Text = "none";
            #endregion
        }

        private void InitParameterList(int mode)
        {
            //if (m_mode1_list.Count != 0)
            //{
            //    m_mode1_list.Clear();
            //}
            //if (m_mode2_list.Count != 0)
            //{
            //    m_mode2_list.Clear();
            //}
            //if (m_mode3_list.Count != 0)
            //{
            //    m_mode3_list.Clear();
            //}

            if(mode==1)
            {
                #region
                PARAMETER pwm1_s1 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x11, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s2 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x12, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s3 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x13, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s4 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x14, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s5 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x15, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s6 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x16, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                
                m_mode1_list.Add(pwm1_s1);
                m_mode1_list.Add(pwm1_s2);
                m_mode1_list.Add(pwm1_s3);
                m_mode1_list.Add(pwm1_s4);
                m_mode1_list.Add(pwm1_s5);
                m_mode1_list.Add(pwm1_s6);

                PARAMETER pwm2_s1 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x21, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s2 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x22, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s3 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x23, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s4 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x24, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s5 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x25, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s6 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x26, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                
                m_mode1_list.Add(pwm2_s1);
                m_mode1_list.Add(pwm2_s2);
                m_mode1_list.Add(pwm2_s3);
                m_mode1_list.Add(pwm2_s4);
                m_mode1_list.Add(pwm2_s5);
                m_mode1_list.Add(pwm2_s6);

                PARAMETER pwm3_s1 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x31, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s2 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x32, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s3 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x33, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s4 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x34, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s5 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x35, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s6 = new PARAMETER() { MODE_SELECTED = 0x00, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x36, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode1_list.Add(pwm3_s1);
                m_mode1_list.Add(pwm3_s2);
                m_mode1_list.Add(pwm3_s3);
                m_mode1_list.Add(pwm3_s4);
                m_mode1_list.Add(pwm3_s5);
                m_mode1_list.Add(pwm3_s6);
                #endregion
                //MessageBox.Show("m_mode1_list:" + m_mode1_list.Count.ToString());
            }
            else if(mode==2)
            {
                #region
                PARAMETER pwm1_s1 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x11, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s2 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x12, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s3 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x13, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s4 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x14, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s5 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x15, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s6 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x16, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode2_list.Add(pwm1_s1);
                m_mode2_list.Add(pwm1_s2);
                m_mode2_list.Add(pwm1_s3);
                m_mode2_list.Add(pwm1_s4);
                m_mode2_list.Add(pwm1_s5);
                m_mode2_list.Add(pwm1_s6);

                PARAMETER pwm2_s1 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x21, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s2 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x22, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s3 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x23, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s4 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x24, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s5 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x25, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s6 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x26, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode2_list.Add(pwm2_s1);
                m_mode2_list.Add(pwm2_s2);
                m_mode2_list.Add(pwm2_s3);
                m_mode2_list.Add(pwm2_s4);
                m_mode2_list.Add(pwm2_s5);
                m_mode2_list.Add(pwm2_s6);

                PARAMETER pwm3_s1 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x31, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s2 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x32, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s3 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x33, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s4 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x34, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s5 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x35, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s6 = new PARAMETER() { MODE_SELECTED = 0x01, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x36, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode2_list.Add(pwm3_s1);
                m_mode2_list.Add(pwm3_s2);
                m_mode2_list.Add(pwm3_s3);
                m_mode2_list.Add(pwm3_s4);
                m_mode2_list.Add(pwm3_s5);
                m_mode2_list.Add(pwm3_s6);
                #endregion
                //MessageBox.Show("m_mode2_list:" + m_mode2_list.Count.ToString());
               
            }
            else if(mode==3)
            {
                #region
                PARAMETER pwm1_s1 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x11, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s2 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x12, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s3 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x13, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s4 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x14, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s5 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x15, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm1_s6 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x16, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode3_list.Add(pwm1_s1);
                m_mode3_list.Add(pwm1_s2);
                m_mode3_list.Add(pwm1_s3);
                m_mode3_list.Add(pwm1_s4);
                m_mode3_list.Add(pwm1_s5);
                m_mode3_list.Add(pwm1_s6);

                PARAMETER pwm2_s1 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x21, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s2 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x22, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s3 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x23, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s4 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x24, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s5 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x25, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm2_s6 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x26, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode3_list.Add(pwm2_s1);
                m_mode3_list.Add(pwm2_s2);
                m_mode3_list.Add(pwm2_s3);
                m_mode3_list.Add(pwm2_s4);
                m_mode3_list.Add(pwm2_s5);
                m_mode3_list.Add(pwm2_s6);

                PARAMETER pwm3_s1 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x31, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s2 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x32, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s3 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x33, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s4 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x34, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s5 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x35, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };
                PARAMETER pwm3_s6 = new PARAMETER() { MODE_SELECTED = 0x02, ENABLE = 0x01, PWM_SERIAL_SELECTED = 0x36, FREQUENCE = 0x01, DUTY_CYCLE = 0x0A, PERIOD = 0x01, NUM_OF_CYCLES = 0x01, WAIT_BETWEEN = 0x00, WAIT_AFTER = 0x00 };

                m_mode3_list.Add(pwm3_s1);
                m_mode3_list.Add(pwm3_s2);
                m_mode3_list.Add(pwm3_s3);
                m_mode3_list.Add(pwm3_s4);
                m_mode3_list.Add(pwm3_s5);
                m_mode3_list.Add(pwm3_s6);
                #endregion
                //MessageBox.Show("m_mode3_list:" + m_mode3_list.Count.ToString());
            }
            else
            {
                //do nothing
            }
            //MessageBox.Show(m_mode1_list.Count.ToString() + " "+m_mode2_list.Count.ToString()+" " + m_mode3_list.Count.ToString());
        }

        private void  SetPWMParameterFromList(int mode)
        {
            
            List<PARAMETER> list = null;
            if(mode==1)
            {
                list = m_mode1_list;
            }
            else if(mode==2)
            {
                list = m_mode2_list;
            }
            else if(mode==3)
            {
                list = m_mode3_list;
            }
            else
            {
                //do nothing
            }
            lock (list)
            {
                foreach (var para in list)
                {
                    switch (para.PWM_SERIAL_SELECTED)
                    {
                        #region
                        case 0x11:
                            #region
                            this.checkBox_PWM1_serial1.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial1.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial1.Enabled = true;
                                this.textBox_period_PWM1_serial1.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial1.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial1.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial1.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial1.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial1.Enabled = false;
                                this.textBox_period_PWM1_serial1.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial1.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial1.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial1.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial1.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial1.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial1.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial1.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial1.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial1.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x12:
                            #region
                            this.checkBox_PWM1_serial2.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial2.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial2.Enabled = true;
                                this.textBox_period_PWM1_serial2.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial2.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial2.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial2.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial2.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial2.Enabled = false;
                                this.textBox_period_PWM1_serial2.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial2.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial2.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial2.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial2.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial2.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial2.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial2.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial2.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial2.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x13:
                            #region
                            this.checkBox_PWM1_serial3.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial3.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial3.Enabled = true;
                                this.textBox_period_PWM1_serial3.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial3.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial3.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial3.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial3.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial3.Enabled = false;
                                this.textBox_period_PWM1_serial3.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial3.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial3.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial3.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial3.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial3.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial3.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial3.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial3.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial3.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x14:
                            #region
                            this.checkBox_PWM1_serial4.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial4.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial4.Enabled = true;
                                this.textBox_period_PWM1_serial4.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial4.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial4.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial4.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial4.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial4.Enabled = false;
                                this.textBox_period_PWM1_serial4.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial4.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial4.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial4.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial4.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial4.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial4.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial4.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial4.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial4.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x15:
                            #region
                            this.checkBox_PWM1_serial5.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial5.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial5.Enabled = true;
                                this.textBox_period_PWM1_serial5.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial5.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial5.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial5.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial5.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial5.Enabled = false;
                                this.textBox_period_PWM1_serial5.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial5.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial5.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial5.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial5.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial5.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial5.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial5.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial5.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial5.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x16:
                            #region
                            this.checkBox_PWM1_serial6.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM1_serial6.Enabled = true;
                                this.textBox_dutyCycle_PWM1_serial6.Enabled = true;
                                this.textBox_period_PWM1_serial6.Enabled = true;
                                this.textBox_numberOfCycles_PWM1_serial6.Enabled = true;
                                this.textBox_waitBetween_PWM1_serial6.Enabled = true;
                                this.textBox_waitAfter_PWM1_serial6.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM1_serial6.Enabled = false;
                                this.textBox_dutyCycle_PWM1_serial6.Enabled = false;
                                this.textBox_period_PWM1_serial6.Enabled = false;
                                this.textBox_numberOfCycles_PWM1_serial6.Enabled = false;
                                this.textBox_waitBetween_PWM1_serial6.Enabled = false;
                                this.textBox_waitAfter_PWM1_serial6.Enabled = false;
                            }
                            this.textBox_freq_PWM1_serial6.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM1_serial6.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM1_serial6.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM1_serial6.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM1_serial6.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM1_serial6.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x21:
                            #region
                            this.checkBox_PWM2_serial1.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial1.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial1.Enabled = true;
                                this.textBox_period_PWM2_serial1.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial1.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial1.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial1.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial1.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial1.Enabled = false;
                                this.textBox_period_PWM2_serial1.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial1.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial1.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial1.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial1.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial1.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial1.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial1.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial1.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial1.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x22:
                            #region
                            this.checkBox_PWM2_serial2.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial2.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial2.Enabled = true;
                                this.textBox_period_PWM2_serial2.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial2.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial2.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial2.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial2.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial2.Enabled = false;
                                this.textBox_period_PWM2_serial2.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial2.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial2.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial2.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial2.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial2.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial2.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial2.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial2.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial2.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x23:
                            #region
                            this.checkBox_PWM2_serial3.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial3.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial3.Enabled = true;
                                this.textBox_period_PWM2_serial3.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial3.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial3.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial3.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial3.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial3.Enabled = false;
                                this.textBox_period_PWM2_serial3.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial3.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial3.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial3.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial3.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial3.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial3.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial3.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial3.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial3.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x24:
                            #region
                            this.checkBox_PWM2_serial4.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial4.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial4.Enabled = true;
                                this.textBox_period_PWM2_serial4.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial4.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial4.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial4.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial4.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial4.Enabled = false;
                                this.textBox_period_PWM2_serial4.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial4.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial4.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial4.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial4.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial4.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial4.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial4.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial4.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial4.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x25:
                            #region
                            this.checkBox_PWM2_serial5.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial5.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial5.Enabled = true;
                                this.textBox_period_PWM2_serial5.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial5.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial5.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial5.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial5.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial5.Enabled = false;
                                this.textBox_period_PWM2_serial5.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial5.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial5.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial5.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial5.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial5.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial5.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial5.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial5.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial5.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x26:
                            #region
                            this.checkBox_PWM2_serial6.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM2_serial6.Enabled = true;
                                this.textBox_dutyCycle_PWM2_serial6.Enabled = true;
                                this.textBox_period_PWM2_serial6.Enabled = true;
                                this.textBox_numberOfCycles_PWM2_serial6.Enabled = true;
                                this.textBox_waitBetween_PWM2_serial6.Enabled = true;
                                this.textBox_waitAfter_PWM2_serial6.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM2_serial6.Enabled = false;
                                this.textBox_dutyCycle_PWM2_serial6.Enabled = false;
                                this.textBox_period_PWM2_serial6.Enabled = false;
                                this.textBox_numberOfCycles_PWM2_serial6.Enabled = false;
                                this.textBox_waitBetween_PWM2_serial6.Enabled = false;
                                this.textBox_waitAfter_PWM2_serial6.Enabled = false;
                            }
                            this.textBox_freq_PWM2_serial6.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM2_serial6.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM2_serial6.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM2_serial6.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM2_serial6.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM2_serial6.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x31:
                            #region
                            this.checkBox_PWM3_serial1.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial1.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial1.Enabled = true;
                                this.textBox_period_PWM3_serial1.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial1.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial1.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial1.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial1.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial1.Enabled = false;
                                this.textBox_period_PWM3_serial1.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial1.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial1.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial1.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial1.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial1.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial1.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial1.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial1.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial1.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x32:
                            #region
                            this.checkBox_PWM3_serial2.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial2.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial2.Enabled = true;
                                this.textBox_period_PWM3_serial2.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial2.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial2.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial2.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial2.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial2.Enabled = false;
                                this.textBox_period_PWM3_serial2.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial2.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial2.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial2.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial2.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial2.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial2.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial2.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial2.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial2.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x33:
                            #region
                            this.checkBox_PWM3_serial3.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial3.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial3.Enabled = true;
                                this.textBox_period_PWM3_serial3.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial3.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial3.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial3.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial3.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial3.Enabled = false;
                                this.textBox_period_PWM3_serial3.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial3.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial3.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial3.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial3.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial3.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial3.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial3.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial3.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial3.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x34:
                            #region
                            this.checkBox_PWM3_serial4.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial4.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial4.Enabled = true;
                                this.textBox_period_PWM3_serial4.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial4.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial4.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial4.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial4.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial4.Enabled = false;
                                this.textBox_period_PWM3_serial4.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial4.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial4.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial4.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial4.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial4.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial4.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial4.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial4.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial4.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x35:
                            #region
                            this.checkBox_PWM3_serial5.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial5.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial5.Enabled = true;
                                this.textBox_period_PWM3_serial5.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial5.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial5.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial5.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial5.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial5.Enabled = false;
                                this.textBox_period_PWM3_serial5.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial5.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial5.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial5.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial5.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial5.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial5.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial5.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial5.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial5.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        case 0x36:
                            #region
                            this.checkBox_PWM3_serial6.Checked = Convert.ToBoolean(para.ENABLE);
                            if (para.ENABLE == 0x01)
                            {
                                this.textBox_freq_PWM3_serial6.Enabled = true;
                                this.textBox_dutyCycle_PWM3_serial6.Enabled = true;
                                this.textBox_period_PWM3_serial6.Enabled = true;
                                this.textBox_numberOfCycles_PWM3_serial6.Enabled = true;
                                this.textBox_waitBetween_PWM3_serial6.Enabled = true;
                                this.textBox_waitAfter_PWM3_serial6.Enabled = true;
                            }
                            else
                            {
                                this.textBox_freq_PWM3_serial6.Enabled = false;
                                this.textBox_dutyCycle_PWM3_serial6.Enabled = false;
                                this.textBox_period_PWM3_serial6.Enabled = false;
                                this.textBox_numberOfCycles_PWM3_serial6.Enabled = false;
                                this.textBox_waitBetween_PWM3_serial6.Enabled = false;
                                this.textBox_waitAfter_PWM3_serial6.Enabled = false;
                            }
                            this.textBox_freq_PWM3_serial6.Text = Convert.ToInt32(para.FREQUENCE).ToString();
                            this.textBox_dutyCycle_PWM3_serial6.Text = Convert.ToInt32(para.DUTY_CYCLE).ToString();
                            this.textBox_period_PWM3_serial6.Text = Convert.ToInt32(para.PERIOD).ToString();
                            this.textBox_numberOfCycles_PWM3_serial6.Text = Convert.ToInt32(para.NUM_OF_CYCLES).ToString();
                            this.textBox_waitBetween_PWM3_serial6.Text = Convert.ToInt32(para.WAIT_BETWEEN).ToString();
                            this.textBox_waitAfter_PWM3_serial6.Text = Convert.ToInt32(para.WAIT_AFTER).ToString();
                            break;
                            #endregion
                        default:
                            break;
                        #endregion
                    }
                }
            }

            
        }

        private void ReadCfgFile2List(string path)
        {
            #region
            List<PARAMETER> list = new List<PARAMETER>();
            if (path == m_mode1_cfgFilePath)
            {
                m_mode1_list.Clear();
                list = m_mode1_list;
            }
            if (path == m_mode2_cfgFilePath)
            {
                m_mode2_list.Clear();
                list = m_mode2_list;
            }
            if (path == m_mode3_cfgFilePath)
            {
                m_mode3_list.Clear();
                list = m_mode3_list;
            }

            #endregion

            #region
            FileStream fs = new FileStream(path, FileMode.Open);
            BinaryReader br = new BinaryReader(fs, Encoding.ASCII);
            
            byte[] bt = new byte[8];
            while (br.Read(bt, 0, 8) > 0)
            {
                PARAMETER para = new PARAMETER();
                
                para.PWM_SERIAL_SELECTED = bt[0];
                para.ENABLE = bt[1];
                para.FREQUENCE = bt[2];
                para.DUTY_CYCLE = bt[3];
                para.PERIOD = bt[4];
                para.NUM_OF_CYCLES = bt[5];
                para.WAIT_BETWEEN = bt[6];
                para.WAIT_AFTER = bt[7];
                list.Add(para);
            }
            br.Close();
            fs.Close();
            #endregion
        }

        private void InitPWMSet()
        {
            #region
            ////模式三
            //#region
            //if (File.Exists(m_mode3_cfgFilePath))
            //{
            //    ReadCfgFile2List(m_mode3_cfgFilePath);
            //    SetPWMParameterFromList(3);
            //}
            //else
            //{
            //    //SetPWMDefaultParameter();
            //    InitParameterList(3);
            //}
            //#endregion

            ////模式二
            //#region
            //if (File.Exists(m_mode2_cfgFilePath))
            //{
            //    ReadCfgFile2List(m_mode2_cfgFilePath);
            //    SetPWMParameterFromList(2);
            //}
            //else
            //{
            //    //SetPWMDefaultParameter();
            //    InitParameterList(2);
            //}
            //#endregion

            ////模式一
            //#region
            //if (File.Exists(m_mode1_cfgFilePath))
            //{
            //    //读取文件，将数据放入链表
            //    ReadCfgFile2List(m_mode1_cfgFilePath);

            //    //将链表中的数据赋值到PWM的参数上
            //    SetPWMParameterFromList(1);
            //}
            //else
            //{
            //    InitParameterList(1);
            //    SetPWMDefaultParameter();
            //}
            #endregion

            if (File.Exists(m_mode1_cfgFilePath)&&File.Exists(m_mode2_cfgFilePath)&&File.Exists(m_mode3_cfgFilePath))
            {
                ReadCfgFile2List(m_mode1_cfgFilePath);
                ReadCfgFile2List(m_mode2_cfgFilePath);
                ReadCfgFile2List(m_mode3_cfgFilePath);

                SetPWMParameterFromList(1);
            }
            else
            {
                InitParameterList(1);
                InitParameterList(2);
                InitParameterList(3);
                SetPWMDefaultParameter();
            }  
        }

        private void GetSystemDateTime()
        {
            textBox_DateTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void InitApp()
        {
            //不需要save current page,隐藏该button
            this.button_saveParameter.Visible = false;


            //初始化参数设置
            InitModeSelect();

            //初始化公共参数
            InitCommParameter();

            //初始化串口设置
            InitSerialPort();

            //初始化PWM1,PWM2,PWM3
            InitPWMSet();

            //加载图片
            LoadPicture();

            //获取系统时间
            GetSystemDateTime();

            //初始化noNeed checkbox
            checkBox_no_need_log.Checked = true;

            ////初始化table
            //m_table.Columns.Add("Item", typeof(int));
            //m_table.Columns.Add("数据", typeof(float));

            ////初始化get honeywell data button
            //this.button_start.Enabled = true;
            //this.button_save.Enabled = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string[] names = SerialPort.GetPortNames();

            if (names.Length == 0)
            {
                return;
            }
            if (m_old_serialPortNames==null)
            {
                return;
            }
            //Array.Sort(names);
            Array.Sort(names, (a, b) => Convert.ToInt32(((string)a).Substring(3)).CompareTo(Convert.ToInt32(((string)b).Substring(3))));
            int nCount = 0;
            if (names.Length == m_old_serialPortNames.Length)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == m_old_serialPortNames[i])
                    {
                        nCount++;
                    }
                }
                if (nCount == names.Length)  //如果每个都相同
                {
                    return;
                }
                else
                {
                    m_old_serialPortNames = names;  //如果不匹配，将新的值赋给旧的值
                }
            }
            else
            {
                m_old_serialPortNames = names;
            }

            this.comboBox_portName.Items.Clear();

            Array.Sort(names, (a, b) => Convert.ToInt32(((string)a).Substring(3)).CompareTo(Convert.ToInt32(((string)b).Substring(3))));

            this.comboBox_portName.Items.AddRange(names);
            this.comboBox_portName.SelectedIndex = 0; 
        }

        private void checkBox_PWM1_serial1_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();


            if(this.checkBox_PWM1_serial1.Checked==false)
            {
                this.textBox_freq_PWM1_serial1.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial1.Enabled = false;
                this.textBox_period_PWM1_serial1.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial1.Enabled = false;
                this.textBox_waitBetween_PWM1_serial1.Enabled = false;
                this.textBox_waitAfter_PWM1_serial1.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial1.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial1.Enabled = true;
                this.textBox_period_PWM1_serial1.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial1.Enabled = true;
                this.textBox_waitBetween_PWM1_serial1.Enabled = true;
                this.textBox_waitAfter_PWM1_serial1.Enabled = true;
            }

            //if (!File.Exists(m_mode1_cfgFilePath))
            //{
            //    this.textBox_freq_PWM1_serial1.Enabled = true;
            //    this.textBox_dutyCycle_PWM1_serial1.Enabled = true;
            //    this.textBox_period_PWM1_serial1.Enabled = true;
            //    this.textBox_numberOfCycles_PWM1_serial1.Enabled = true;
            //    this.textBox_waitBetween_PWM1_serial1.Enabled = true;
            //    this.textBox_waitAfter_PWM1_serial1.Enabled = true;
            //}
        }

        private void checkBox_PWM1_serial2_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM1_serial2.Checked == false)
            {
                this.textBox_freq_PWM1_serial2.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial2.Enabled = false;
                this.textBox_period_PWM1_serial2.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial2.Enabled = false;
                this.textBox_waitBetween_PWM1_serial2.Enabled = false;
                this.textBox_waitAfter_PWM1_serial2.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial2.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial2.Enabled = true;
                this.textBox_period_PWM1_serial2.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial2.Enabled = true;
                this.textBox_waitBetween_PWM1_serial2.Enabled = true;
                this.textBox_waitAfter_PWM1_serial2.Enabled = true;
            }
        }

        private void checkBox_PWM1_serial3_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM1_serial3.Checked == false)
            {
                this.textBox_freq_PWM1_serial3.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial3.Enabled = false;
                this.textBox_period_PWM1_serial3.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial3.Enabled = false;
                this.textBox_waitBetween_PWM1_serial3.Enabled = false;
                this.textBox_waitAfter_PWM1_serial3.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial3.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial3.Enabled = true;
                this.textBox_period_PWM1_serial3.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial3.Enabled = true;
                this.textBox_waitBetween_PWM1_serial3.Enabled = true;
                this.textBox_waitAfter_PWM1_serial3.Enabled = true;
            }
        }

        private void checkBox_PWM1_serial4_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM1_serial4.Checked == false)
            {
                this.textBox_freq_PWM1_serial4.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial4.Enabled = false;
                this.textBox_period_PWM1_serial4.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial4.Enabled = false;
                this.textBox_waitBetween_PWM1_serial4.Enabled = false;
                this.textBox_waitAfter_PWM1_serial4.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial4.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial4.Enabled = true;
                this.textBox_period_PWM1_serial4.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial4.Enabled = true;
                this.textBox_waitBetween_PWM1_serial4.Enabled = true;
                this.textBox_waitAfter_PWM1_serial4.Enabled = true;
            }
        }

        private void checkBox_PWM1_serial5_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM1_serial5.Checked == false)
            {
                this.textBox_freq_PWM1_serial5.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial5.Enabled = false;
                this.textBox_period_PWM1_serial5.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial5.Enabled = false;
                this.textBox_waitBetween_PWM1_serial5.Enabled = false;
                this.textBox_waitAfter_PWM1_serial5.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial5.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial5.Enabled = true;
                this.textBox_period_PWM1_serial5.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial5.Enabled = true;
                this.textBox_waitBetween_PWM1_serial5.Enabled = true;
                this.textBox_waitAfter_PWM1_serial5.Enabled = true;
            }
        }

        private void checkBox_PWM1_serial6_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM1_serial6.Checked == false)
            {
                this.textBox_freq_PWM1_serial6.Enabled = false;
                this.textBox_dutyCycle_PWM1_serial6.Enabled = false;
                this.textBox_period_PWM1_serial6.Enabled = false;
                this.textBox_numberOfCycles_PWM1_serial6.Enabled = false;
                this.textBox_waitBetween_PWM1_serial6.Enabled = false;
                this.textBox_waitAfter_PWM1_serial6.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM1_serial6.Enabled = true;
                this.textBox_dutyCycle_PWM1_serial6.Enabled = true;
                this.textBox_period_PWM1_serial6.Enabled = true;
                this.textBox_numberOfCycles_PWM1_serial6.Enabled = true;
                this.textBox_waitBetween_PWM1_serial6.Enabled = true;
                this.textBox_waitAfter_PWM1_serial6.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial1_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial1.Checked == false)
            {
                this.textBox_freq_PWM2_serial1.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial1.Enabled = false;
                this.textBox_period_PWM2_serial1.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial1.Enabled = false;
                this.textBox_waitBetween_PWM2_serial1.Enabled = false;
                this.textBox_waitAfter_PWM2_serial1.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial1.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial1.Enabled = true;
                this.textBox_period_PWM2_serial1.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial1.Enabled = true;
                this.textBox_waitBetween_PWM2_serial1.Enabled = true;
                this.textBox_waitAfter_PWM2_serial1.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial2_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial2.Checked == false)
            {
                this.textBox_freq_PWM2_serial2.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial2.Enabled = false;
                this.textBox_period_PWM2_serial2.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial2.Enabled = false;
                this.textBox_waitBetween_PWM2_serial2.Enabled = false;
                this.textBox_waitAfter_PWM2_serial2.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial2.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial2.Enabled = true;
                this.textBox_period_PWM2_serial2.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial2.Enabled = true;
                this.textBox_waitBetween_PWM2_serial2.Enabled = true;
                this.textBox_waitAfter_PWM2_serial2.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial3_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial3.Checked == false)
            {
                this.textBox_freq_PWM2_serial3.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial3.Enabled = false;
                this.textBox_period_PWM2_serial3.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial3.Enabled = false;
                this.textBox_waitBetween_PWM2_serial3.Enabled = false;
                this.textBox_waitAfter_PWM2_serial3.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial3.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial3.Enabled = true;
                this.textBox_period_PWM2_serial3.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial3.Enabled = true;
                this.textBox_waitBetween_PWM2_serial3.Enabled = true;
                this.textBox_waitAfter_PWM2_serial3.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial4_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial4.Checked == false)
            {
                this.textBox_freq_PWM2_serial4.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial4.Enabled = false;
                this.textBox_period_PWM2_serial4.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial4.Enabled = false;
                this.textBox_waitBetween_PWM2_serial4.Enabled = false;
                this.textBox_waitAfter_PWM2_serial4.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial4.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial4.Enabled = true;
                this.textBox_period_PWM2_serial4.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial4.Enabled = true;
                this.textBox_waitBetween_PWM2_serial4.Enabled = true;
                this.textBox_waitAfter_PWM2_serial4.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial5_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial5.Checked == false)
            {
                this.textBox_freq_PWM2_serial5.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial5.Enabled = false;
                this.textBox_period_PWM2_serial5.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial5.Enabled = false;
                this.textBox_waitBetween_PWM2_serial5.Enabled = false;
                this.textBox_waitAfter_PWM2_serial5.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial5.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial5.Enabled = true;
                this.textBox_period_PWM2_serial5.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial5.Enabled = true;
                this.textBox_waitBetween_PWM2_serial5.Enabled = true;
                this.textBox_waitAfter_PWM2_serial5.Enabled = true;
            }
        }

        private void checkBox_PWM2_serial6_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM2_serial6.Checked == false)
            {
                this.textBox_freq_PWM2_serial6.Enabled = false;
                this.textBox_dutyCycle_PWM2_serial6.Enabled = false;
                this.textBox_period_PWM2_serial6.Enabled = false;
                this.textBox_numberOfCycles_PWM2_serial6.Enabled = false;
                this.textBox_waitBetween_PWM2_serial6.Enabled = false;
                this.textBox_waitAfter_PWM2_serial6.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM2_serial6.Enabled = true;
                this.textBox_dutyCycle_PWM2_serial6.Enabled = true;
                this.textBox_period_PWM2_serial6.Enabled = true;
                this.textBox_numberOfCycles_PWM2_serial6.Enabled = true;
                this.textBox_waitBetween_PWM2_serial6.Enabled = true;
                this.textBox_waitAfter_PWM2_serial6.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial1_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial1.Checked == false)
            {
                this.textBox_freq_PWM3_serial1.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial1.Enabled = false;
                this.textBox_period_PWM3_serial1.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial1.Enabled = false;
                this.textBox_waitBetween_PWM3_serial1.Enabled = false;
                this.textBox_waitAfter_PWM3_serial1.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial1.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial1.Enabled = true;
                this.textBox_period_PWM3_serial1.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial1.Enabled = true;
                this.textBox_waitBetween_PWM3_serial1.Enabled = true;
                this.textBox_waitAfter_PWM3_serial1.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial2_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial2.Checked == false)
            {
                this.textBox_freq_PWM3_serial2.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial2.Enabled = false;
                this.textBox_period_PWM3_serial2.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial2.Enabled = false;
                this.textBox_waitBetween_PWM3_serial2.Enabled = false;
                this.textBox_waitAfter_PWM3_serial2.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial2.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial2.Enabled = true;
                this.textBox_period_PWM3_serial2.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial2.Enabled = true;
                this.textBox_waitBetween_PWM3_serial2.Enabled = true;
                this.textBox_waitAfter_PWM3_serial2.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial3_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial3.Checked == false)
            {
                this.textBox_freq_PWM3_serial3.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial3.Enabled = false;
                this.textBox_period_PWM3_serial3.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial3.Enabled = false;
                this.textBox_waitBetween_PWM3_serial3.Enabled = false;
                this.textBox_waitAfter_PWM3_serial3.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial3.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial3.Enabled = true;
                this.textBox_period_PWM3_serial3.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial3.Enabled = true;
                this.textBox_waitBetween_PWM3_serial3.Enabled = true;
                this.textBox_waitAfter_PWM3_serial3.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial4_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial4.Checked == false)
            {
                this.textBox_freq_PWM3_serial4.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial4.Enabled = false;
                this.textBox_period_PWM3_serial4.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial4.Enabled = false;
                this.textBox_waitBetween_PWM3_serial4.Enabled = false;
                this.textBox_waitAfter_PWM3_serial4.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial4.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial4.Enabled = true;
                this.textBox_period_PWM3_serial4.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial4.Enabled = true;
                this.textBox_waitBetween_PWM3_serial4.Enabled = true;
                this.textBox_waitAfter_PWM3_serial4.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial5_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial5.Checked == false)
            {
                this.textBox_freq_PWM3_serial5.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial5.Enabled = false;
                this.textBox_period_PWM3_serial5.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial5.Enabled = false;
                this.textBox_waitBetween_PWM3_serial5.Enabled = false;
                this.textBox_waitAfter_PWM3_serial5.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial5.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial5.Enabled = true;
                this.textBox_period_PWM3_serial5.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial5.Enabled = true;
                this.textBox_waitBetween_PWM3_serial5.Enabled = true;
                this.textBox_waitAfter_PWM3_serial5.Enabled = true;
            }
        }

        private void checkBox_PWM3_serial6_Click(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                return;
            }
            GetCurrentPWMParameter2List();

            if (this.checkBox_PWM3_serial6.Checked == false)
            {
                this.textBox_freq_PWM3_serial6.Enabled = false;
                this.textBox_dutyCycle_PWM3_serial6.Enabled = false;
                this.textBox_period_PWM3_serial6.Enabled = false;
                this.textBox_numberOfCycles_PWM3_serial6.Enabled = false;
                this.textBox_waitBetween_PWM3_serial6.Enabled = false;
                this.textBox_waitAfter_PWM3_serial6.Enabled = false;
            }
            else
            {
                this.textBox_freq_PWM3_serial6.Enabled = true;
                this.textBox_dutyCycle_PWM3_serial6.Enabled = true;
                this.textBox_period_PWM3_serial6.Enabled = true;
                this.textBox_numberOfCycles_PWM3_serial6.Enabled = true;
                this.textBox_waitBetween_PWM3_serial6.Enabled = true;
                this.textBox_waitAfter_PWM3_serial6.Enabled = true;
            }
        }

        private void LoadPicture()
        {
            if (!m_SerialPortOpened)
            {
                this.pictureBox1.Load(Environment.CurrentDirectory + @"\" + "red.bmp");
            }
            else
            {
                this.pictureBox1.Load(Environment.CurrentDirectory + @"\" + "green.bmp");
            }
        }

        private void button_openSerialPort_Click(object sender, EventArgs e)
        {
            m_SerialPortOpened = !m_SerialPortOpened;
            
            if (m_SerialPortOpened)
            {
                try
                {
                    this.serialPort1.Open();
                }
                catch (Exception ex)
                {
                    m_SerialPortOpened = false;
                    MessageBox.Show(ex.Message);
                    return;
                }
                this.button_openSerialPort.Text = "Close";

                m_SerialPortOpened = true;
                this.comboBox_portName.Enabled = false;
                LoadPicture();

                //2019.3.26
                send_stop_getting_HW_data();
                m_HWData_list.Clear();
                m_buffer.Clear();
                label_HW.Text = "0";

                
            }
            else
            {
                this.button_openSerialPort.Text = "Connect";
                this.serialPort1.Close();
                m_SerialPortOpened = false;
                LoadPicture();
                this.comboBox_portName.Enabled = true;

            }
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void GetCheckedSerial2List()
        { 
            if(m_SerialChecked_List.Count!=0)
            {
                m_SerialChecked_List.Clear();
            }

            #region
            CHECK_STATE checkState1=new CHECK_STATE();
            checkState1.PWM_SERIAL=0x11;
            if (this.checkBox_PWM1_serial1.Checked==true)
            {
                checkState1.CHECKED=Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState1);
            }
            else
            {
                checkState1.CHECKED=Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState1);
            }


            CHECK_STATE checkState2 = new CHECK_STATE();
            checkState2.PWM_SERIAL = 0x12;
            if (this.checkBox_PWM1_serial2.Checked == true)
            {
                checkState2.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState2);
            }
            else
            {
                checkState2.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState2);
            }

            CHECK_STATE checkState3 = new CHECK_STATE();
            checkState3.PWM_SERIAL = 0x13;
            if (this.checkBox_PWM1_serial3.Checked == true)
            {
                checkState3.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState3);
            }
            else
            {
                checkState3.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState3);
            }

            CHECK_STATE checkState4 = new CHECK_STATE();
            checkState4.PWM_SERIAL = 0x14;
            if (this.checkBox_PWM1_serial4.Checked == true)
            {
                checkState4.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState4);
            }
            else
            {
                checkState4.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState4);
            }

            CHECK_STATE checkState5 = new CHECK_STATE();
            checkState5.PWM_SERIAL = 0x15;
            if (this.checkBox_PWM1_serial5.Checked == true)
            {
                checkState5.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState5);
            }
            else
            {
                checkState5.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState5);
            }

            CHECK_STATE checkState6 = new CHECK_STATE();
            checkState6.PWM_SERIAL = 0x16;
            if (this.checkBox_PWM1_serial6.Checked == true)
            {
                checkState6.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState6);
            }
            else
            {
                checkState6.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState6);
            }
            #endregion

            #region
            CHECK_STATE checkState7 = new CHECK_STATE();
            checkState7.PWM_SERIAL = 0x21;
            if (this.checkBox_PWM2_serial1.Checked == true)
            {

                checkState7.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState7);
            }
            else
            {
                checkState7.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState7);
            }


            CHECK_STATE checkState8 = new CHECK_STATE();
            checkState8.PWM_SERIAL = 0x22;
            if (this.checkBox_PWM2_serial2.Checked == true)
            {
                checkState8.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState8);
            }
            else
            {
                checkState8.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState8);
            }

            CHECK_STATE checkState9 = new CHECK_STATE();
            checkState9.PWM_SERIAL = 0x23;
            if (this.checkBox_PWM2_serial3.Checked == true)
            {
                checkState9.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState9);
            }
            else
            {
                checkState9.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState9);
            }

            CHECK_STATE checkState10 = new CHECK_STATE();
            checkState10.PWM_SERIAL = 0x24;
            if (this.checkBox_PWM2_serial4.Checked == true)
            {
                checkState10.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState10);
            }
            else
            {
                checkState10.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState10);
            }

            CHECK_STATE checkState11 = new CHECK_STATE();
            checkState11.PWM_SERIAL = 0x25;
            if (this.checkBox_PWM2_serial5.Checked == true)
            {
                checkState11.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState11);
            }
            else
            {
                checkState11.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState11);
            }

            CHECK_STATE checkState12 = new CHECK_STATE();
            checkState12.PWM_SERIAL = 0x26;
            if (this.checkBox_PWM2_serial6.Checked == true)
            {
                checkState12.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState12);
            }
            else
            {
                checkState12.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState12);
            }
            #endregion

            #region
            CHECK_STATE checkState13 = new CHECK_STATE();
            checkState13.PWM_SERIAL = 0x31;
            if (this.checkBox_PWM3_serial1.Checked == true)
            {

                checkState13.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState13);
            }
            else
            {
                checkState13.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState13);
            }


            CHECK_STATE checkState14 = new CHECK_STATE();
            checkState14.PWM_SERIAL = 0x32;
            if (this.checkBox_PWM3_serial2.Checked == true)
            {
                checkState14.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState14);
            }
            else
            {
                checkState14.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState14);
            }

            CHECK_STATE checkState15 = new CHECK_STATE();
            checkState15.PWM_SERIAL = 0x33;
            if (this.checkBox_PWM3_serial3.Checked == true)
            {
                checkState15.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState15);
            }
            else
            {
                checkState15.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState15);
            }

            CHECK_STATE checkState16 = new CHECK_STATE();
            checkState16.PWM_SERIAL = 0x34;
            if (this.checkBox_PWM3_serial4.Checked == true)
            {
                checkState16.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState16);
            }
            else
            {
                checkState16.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState16);
            }

            CHECK_STATE checkState17 = new CHECK_STATE();
            checkState17.PWM_SERIAL = 0x35;
            if (this.checkBox_PWM3_serial5.Checked == true)
            {
                checkState17.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState17);
            }
            else
            {
                checkState17.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState17);
            }

            CHECK_STATE checkState18 = new CHECK_STATE();
            checkState18.PWM_SERIAL = 0x36;
            if (this.checkBox_PWM3_serial6.Checked == true)
            {
                checkState18.CHECKED = Convert.ToByte(1);
                m_SerialChecked_List.Add(checkState18);
            }
            else
            {
                checkState18.CHECKED = Convert.ToByte(0);
                m_SerialChecked_List.Add(checkState18);
            }
            #endregion
   
        }

        void GetCurrentPWMParameter2List()
        {
            GetCheckedSerial2List();
            //if (this.comboBox_modeSelect.SelectedIndex == 0 && m_mode1_list.Count != 0)
            if (this.comboBox_modeSelect.Text=="Mode1"&& m_mode1_list.Count != 0)
            {
                m_mode1_list.Clear();
            }
            //if (this.comboBox_modeSelect.SelectedIndex == 1 && m_mode2_list.Count != 0)
            if (this.comboBox_modeSelect.Text == "Mode2" && m_mode2_list.Count != 0)
            {
                m_mode2_list.Clear();
            }
            //if (this.comboBox_modeSelect.SelectedIndex == 2 && m_mode3_list.Count != 0)
            if (this.comboBox_modeSelect.Text == "Mode3" && m_mode3_list.Count != 0)
            {
                m_mode3_list.Clear();
            }
            

            foreach (var check in m_SerialChecked_List)
            {
                PARAMETER para = new PARAMETER();
                para.PWM_SERIAL_SELECTED = check.PWM_SERIAL;
                switch (check.PWM_SERIAL)
                {
                    #region
                    case 0x11:
                        para.ENABLE = this.checkBox_PWM1_serial1.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial1.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial1.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial1.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial1.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial1.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial1.Text);
                        break;
                    case 0x12:
                        para.ENABLE = this.checkBox_PWM1_serial2.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial2.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial2.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial2.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial2.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial2.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial2.Text);
                        break;
                    case 0x13:
                        para.ENABLE = this.checkBox_PWM1_serial3.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial3.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial3.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial3.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial3.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial3.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial3.Text);
                        break;
                    case 0x14:
                        para.ENABLE = this.checkBox_PWM1_serial4.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial4.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial4.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial4.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial4.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial4.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial4.Text);
                        break;
                    case 0x15:
                        para.ENABLE = this.checkBox_PWM1_serial5.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial5.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial5.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial5.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial5.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial5.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial5.Text);
                        break;
                    case 0x16:
                        para.ENABLE = this.checkBox_PWM1_serial6.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM1_serial6.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM1_serial6.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM1_serial6.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM1_serial6.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM1_serial6.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM1_serial6.Text);
                        break;
                    case 0x21:
                        para.ENABLE = this.checkBox_PWM2_serial1.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial1.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial1.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial1.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial1.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial1.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial1.Text);
                        break;
                    case 0x22:
                        para.ENABLE = this.checkBox_PWM2_serial2.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial2.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial2.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial2.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial2.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial2.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial2.Text);
                        break;
                    case 0x23:
                        para.ENABLE = this.checkBox_PWM2_serial3.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial3.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial3.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial3.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial3.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial3.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial3.Text);
                        break;
                    case 0x24:
                        para.ENABLE = this.checkBox_PWM2_serial4.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial4.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial4.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial4.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial4.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial4.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial4.Text);
                        break;
                    case 0x25:
                        para.ENABLE = this.checkBox_PWM2_serial5.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial5.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial5.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial5.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial5.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial5.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial5.Text);
                        break;
                    case 0x26:
                        para.ENABLE = this.checkBox_PWM2_serial6.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM2_serial6.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM2_serial6.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM2_serial6.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM2_serial6.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM2_serial6.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM2_serial6.Text);
                        break;
                    case 0x31:
                        para.ENABLE = this.checkBox_PWM3_serial1.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial1.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial1.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial1.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial1.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial1.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial1.Text);
                        break;
                    case 0x32:
                        para.ENABLE = this.checkBox_PWM3_serial2.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial2.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial2.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial2.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial2.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial2.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial2.Text);
                        break;
                    case 0x33:
                        para.ENABLE = this.checkBox_PWM3_serial3.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial3.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial3.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial3.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial3.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial3.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial3.Text);
                        break;
                    case 0x34:
                        para.ENABLE = this.checkBox_PWM3_serial4.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial4.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial4.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial4.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial4.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial4.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial4.Text);
                        break;
                    case 0x35:
                        para.ENABLE = this.checkBox_PWM3_serial5.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial5.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial5.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial5.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial5.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial5.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial5.Text);
                        break;
                    case 0x36:
                        para.ENABLE = this.checkBox_PWM3_serial6.Checked ? Convert.ToByte(1) : Convert.ToByte(0);
                        para.FREQUENCE = Convert.ToByte(this.textBox_freq_PWM3_serial6.Text);
                        para.DUTY_CYCLE = Convert.ToByte(this.textBox_dutyCycle_PWM3_serial6.Text);
                        para.PERIOD = Convert.ToByte(this.textBox_period_PWM3_serial6.Text);
                        para.NUM_OF_CYCLES = Convert.ToByte(this.textBox_numberOfCycles_PWM3_serial6.Text);
                        para.WAIT_BETWEEN = Convert.ToByte(this.textBox_waitBetween_PWM3_serial6.Text);
                        para.WAIT_AFTER = Convert.ToByte(this.textBox_waitAfter_PWM3_serial6.Text);
                        break;
                    default:
                        break;
                    #endregion
                }
                //if (this.comboBox_modeSelect.SelectedIndex == 0)
                if (this.comboBox_modeSelect.Text == "Mode1" )
                {
                    para.MODE_SELECTED = 0x00;
                    m_mode1_list.Add(para);
                }
                //else if (this.comboBox_modeSelect.SelectedIndex == 1)
                else if (this.comboBox_modeSelect.Text == "Mode2")
                {
                    para.MODE_SELECTED = 0x01;
                    m_mode2_list.Add(para);
                }
                else if (this.comboBox_modeSelect.Text == "Mode3" )
                {
                    para.MODE_SELECTED = 0x02;
                    m_mode3_list.Add(para);
                }
            }
        }

        private void SaveParameter2File()
        {
            GetCurrentPWMParameter2List();

            FileStream fs = null;
            List<PARAMETER> list = null;
            if (this.comboBox_modeSelect.SelectedIndex == 0)
            {
                fs = new FileStream(m_mode1_cfgFilePath, FileMode.Create);
                list = m_mode1_list;
            }
            else if (this.comboBox_modeSelect.SelectedIndex == 1)
            {
                fs = new FileStream(m_mode2_cfgFilePath, FileMode.Create);
                list = m_mode2_list;
            }
            else if (this.comboBox_modeSelect.SelectedIndex == 2)
            {
                fs = new FileStream(m_mode3_cfgFilePath, FileMode.Create);
                list = m_mode3_list;
            }
            else
            {
                //do nothing
            }
            
            BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII);
            foreach (var parameter in list)
            {
                bw.Write(parameter.PWM_SERIAL_SELECTED);
                bw.Write(parameter.ENABLE);
                bw.Write(parameter.FREQUENCE);
                bw.Write(parameter.DUTY_CYCLE);
                bw.Write(parameter.PERIOD);
                bw.Write(parameter.NUM_OF_CYCLES);
                bw.Write(parameter.WAIT_BETWEEN);
                bw.Write(parameter.WAIT_AFTER);
            }

            bw.Close();
            fs.Close();

            //保存公共参数到cfg文件
            //m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(this.textBox_exhalationThreshold.Text);
            m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                                            +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));
            m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);

            WriteCommPara2File();
        }

        private void button_saveParameter_Click(object sender, EventArgs e)
        {
            //SaveParameter2File();
        }

        private void comboBox_modeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CheckTextBox() == 1 || CheckTextBox() == 2)
            {
                return;
            }
            #region
            if (this.comboBox_modeSelect.SelectedIndex==0)
            {
                #region
                //if (m_mode1_list.Count != 0)
                //{
                //    SetPWMParameterFromList(1);
                //}
                //else
                //{
                //    SetPWMDefaultParameter();
                //}
                #endregion
                SetPWMParameterFromList(1);
            }
            else if(this.comboBox_modeSelect.SelectedIndex==1)
            {
                #region
                //if (m_mode1_list.Count != 0)
                //{
                //    SetPWMParameterFromList(2);
                //}
                //else
                //{
                //    SetPWMDefaultParameter();
                //}
                #endregion
                SetPWMParameterFromList(2);
            }
            else if(this.comboBox_modeSelect.SelectedIndex==2)
            {
                #region
                //if (m_mode1_list.Count != 0)
                //{
                //    SetPWMParameterFromList(3);
                //}
                //else
                //{
                //    SetPWMDefaultParameter();
                //}
                #endregion
                SetPWMParameterFromList(3);
            }
            else
            {
                //do nothing
            }
            #endregion
        }

        private void SendIsPCBRcvFinshed()
        {
            byte[] buffer = new byte[6];
            buffer[HEAD] = 0xFF;  
            buffer[LEN] = 0x04;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x07;

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256); //checksum2
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2); 
        }

        private void SendCommPara2SerialPort()
        {
            //m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(this.textBox_exhalationThreshold.Text);
            m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                                            +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));

            m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);
            byte[] buffer = new byte[8];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x06;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x04;
            buffer[4] = m_commPara.EXHALATION_THRESHOLD;
            buffer[5] = m_commPara.WAIT_BEFORE_START;

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256); //checksum2
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2); 
        }

        private void  SendMode1ParaByPWM(int PWM)
        {
            byte[] buffer = new byte[54];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x34;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = Convert.ToByte(16 * Convert.ToUInt32("1") + PWM);
            #region
            int j = 4;
            for (int i = 6 * (PWM - 1); i < 6 * PWM; i++)
            {
                buffer[j++] = m_mode1_list[i].PWM_SERIAL_SELECTED;
                buffer[j++] = m_mode1_list[i].ENABLE;
                buffer[j++] = m_mode1_list[i].FREQUENCE;
                buffer[j++] = m_mode1_list[i].DUTY_CYCLE;
                buffer[j++] = m_mode1_list[i].PERIOD;
                buffer[j++] = m_mode1_list[i].NUM_OF_CYCLES;
                buffer[j++] = m_mode1_list[i].WAIT_BETWEEN;
                buffer[j++] = m_mode1_list[i].WAIT_AFTER;
               
            }
            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(0x34); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256); //checksum2
            #endregion
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void  SendMode2ParaByPWM(int PWM)
        {
            byte[] buffer = new byte[54];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x34;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = Convert.ToByte(16*Convert.ToUInt32("2") + PWM);
            #region
            int j = 4;
            for (int i = 6 * (PWM - 1); i < 6 * PWM; i++)
            {
                buffer[j++] = m_mode2_list[i].PWM_SERIAL_SELECTED;
                buffer[j++] = m_mode2_list[i].ENABLE;
                buffer[j++] = m_mode2_list[i].FREQUENCE;
                buffer[j++] = m_mode2_list[i].DUTY_CYCLE;
                buffer[j++] = m_mode2_list[i].PERIOD;
                buffer[j++] = m_mode2_list[i].NUM_OF_CYCLES;
                buffer[j++] = m_mode2_list[i].WAIT_BETWEEN;
                buffer[j++] = m_mode2_list[i].WAIT_AFTER;

            }
            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(0x34); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256); //checksum2
            #endregion
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void SendMode3ParaByPWM(int PWM)
        {
            byte[] buffer = new byte[54];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x34;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = Convert.ToByte(16 * Convert.ToUInt32("3") + PWM);
            #region
            int j = 4;
            for (int i = 6 * (PWM - 1); i < 6 * PWM; i++)
            {
                buffer[j++] = m_mode3_list[i].PWM_SERIAL_SELECTED;
                buffer[j++] = m_mode3_list[i].ENABLE;
                buffer[j++] = m_mode3_list[i].FREQUENCE;
                buffer[j++] = m_mode3_list[i].DUTY_CYCLE;
                buffer[j++] = m_mode3_list[i].PERIOD;
                buffer[j++] = m_mode3_list[i].NUM_OF_CYCLES;
                buffer[j++] = m_mode3_list[i].WAIT_BETWEEN;
                buffer[j++] = m_mode3_list[i].WAIT_AFTER;
            }
            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(0x34); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256); //checksum2
            #endregion
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void SendModePWMPara2SerialPort(int mode,int PWM)
        {
            #region
            if (mode==1&&PWM==1)
            {
                SendMode1ParaByPWM(PWM);
            }
            else if(mode==1&&PWM==2)
            {
                SendMode1ParaByPWM(PWM); 
            }
            else if (mode == 1 && PWM == 3)
            {
                SendMode1ParaByPWM(PWM); 
            }
           
            else if (mode == 2 && PWM == 1)
            {
                SendMode2ParaByPWM(PWM);
            }
            else if (mode == 2 && PWM == 2)
            {
                SendMode2ParaByPWM(PWM);
            }
            else if (mode == 2 && PWM == 3)
            {
                SendMode2ParaByPWM(PWM);
            }
           
            else if (mode == 3 && PWM == 1)
            {
                SendMode3ParaByPWM(PWM);
            }
            else if (mode == 3 && PWM == 2)
            {
                SendMode3ParaByPWM(PWM);
            }
            else if (mode == 3 && PWM == 3)
            {
                SendMode3ParaByPWM(PWM);
            }
        
            else
            {
                //do nothing
            }
            #endregion
        }

        //返回0：ok， 1-存在空数据，2-duty cycle数据长度小于1
        int CheckTextBox()
        {
            if (
                //freq
                #region
                this.textBox_freq_PWM1_serial1.Text == "" || 
                this.textBox_freq_PWM1_serial2.Text == "" || 
                this.textBox_freq_PWM1_serial3.Text == "" || 
                this.textBox_freq_PWM1_serial4.Text == "" || 
                this.textBox_freq_PWM1_serial5.Text == "" || 
                this.textBox_freq_PWM1_serial6.Text == "" || 

                this.textBox_freq_PWM2_serial1.Text == "" || 
                this.textBox_freq_PWM2_serial2.Text == "" || 
                this.textBox_freq_PWM2_serial3.Text == "" || 
                this.textBox_freq_PWM2_serial4.Text == "" || 
                this.textBox_freq_PWM2_serial5.Text == "" || 
                this.textBox_freq_PWM2_serial6.Text == "" || 

                this.textBox_freq_PWM3_serial1.Text == "" || 
                this.textBox_freq_PWM3_serial2.Text == "" || 
                this.textBox_freq_PWM3_serial3.Text == "" || 
                this.textBox_freq_PWM3_serial4.Text == "" || 
                this.textBox_freq_PWM3_serial5.Text == "" || 
                this.textBox_freq_PWM3_serial6.Text == "" ||
                #endregion

                //duty cycle
                #region
                this.textBox_dutyCycle_PWM1_serial1.Text == "" || 
                this.textBox_dutyCycle_PWM1_serial2.Text == "" || 
                this.textBox_dutyCycle_PWM1_serial3.Text == "" || 
                this.textBox_dutyCycle_PWM1_serial4.Text == "" || 
                this.textBox_dutyCycle_PWM1_serial5.Text == "" || 
                this.textBox_dutyCycle_PWM1_serial6.Text == "" || 

                this.textBox_dutyCycle_PWM2_serial1.Text == "" || 
                this.textBox_dutyCycle_PWM2_serial2.Text == "" || 
                this.textBox_dutyCycle_PWM2_serial3.Text == "" || 
                this.textBox_dutyCycle_PWM2_serial4.Text == "" || 
                this.textBox_dutyCycle_PWM2_serial5.Text == "" || 
                this.textBox_dutyCycle_PWM2_serial6.Text == "" || 

                this.textBox_dutyCycle_PWM3_serial1.Text == "" || 
                this.textBox_dutyCycle_PWM3_serial2.Text == "" || 
                this.textBox_dutyCycle_PWM3_serial3.Text == "" || 
                this.textBox_dutyCycle_PWM3_serial4.Text == "" || 
                this.textBox_dutyCycle_PWM3_serial5.Text == "" || 
                this.textBox_dutyCycle_PWM3_serial6.Text == "" ||
                #endregion

                //period
                #region
                this.textBox_period_PWM1_serial1.Text == "" || 
                this.textBox_period_PWM1_serial2.Text == "" || 
                this.textBox_period_PWM1_serial3.Text == "" || 
                this.textBox_period_PWM1_serial4.Text == "" || 
                this.textBox_period_PWM1_serial5.Text == "" || 
                this.textBox_period_PWM1_serial6.Text == "" || 

                this.textBox_period_PWM2_serial1.Text == "" || 
                this.textBox_period_PWM2_serial2.Text == "" || 
                this.textBox_period_PWM2_serial3.Text == "" || 
                this.textBox_period_PWM2_serial4.Text == "" || 
                this.textBox_period_PWM2_serial5.Text == "" || 
                this.textBox_period_PWM2_serial6.Text == "" || 

                this.textBox_period_PWM3_serial1.Text == "" || 
                this.textBox_period_PWM3_serial2.Text == "" || 
                this.textBox_period_PWM3_serial3.Text == "" || 
                this.textBox_period_PWM3_serial4.Text == "" || 
                this.textBox_period_PWM3_serial5.Text == "" || 
                this.textBox_period_PWM3_serial6.Text == "" ||
                #endregion

                //number of cycles
                #region
                this.textBox_numberOfCycles_PWM1_serial1.Text == "" || 
                this.textBox_numberOfCycles_PWM1_serial2.Text == "" || 
                this.textBox_numberOfCycles_PWM1_serial3.Text == "" || 
                this.textBox_numberOfCycles_PWM1_serial4.Text == "" || 
                this.textBox_numberOfCycles_PWM1_serial5.Text == "" || 
                this.textBox_numberOfCycles_PWM1_serial6.Text == "" || 

                this.textBox_numberOfCycles_PWM2_serial1.Text == "" || 
                this.textBox_numberOfCycles_PWM2_serial2.Text == "" || 
                this.textBox_numberOfCycles_PWM2_serial3.Text == "" || 
                this.textBox_numberOfCycles_PWM2_serial4.Text == "" || 
                this.textBox_numberOfCycles_PWM2_serial5.Text == "" || 
                this.textBox_numberOfCycles_PWM2_serial6.Text == "" || 

                this.textBox_numberOfCycles_PWM3_serial1.Text == "" || 
                this.textBox_numberOfCycles_PWM3_serial2.Text == "" || 
                this.textBox_numberOfCycles_PWM3_serial3.Text == "" || 
                this.textBox_numberOfCycles_PWM3_serial4.Text == "" || 
                this.textBox_numberOfCycles_PWM3_serial5.Text == "" || 
                this.textBox_numberOfCycles_PWM3_serial6.Text == "" ||
                #endregion

                //wait between
                #region
                this.textBox_waitBetween_PWM1_serial1.Text == "" || 
                this.textBox_waitBetween_PWM1_serial2.Text == "" || 
                this.textBox_waitBetween_PWM1_serial3.Text == "" || 
                this.textBox_waitBetween_PWM1_serial4.Text == "" || 
                this.textBox_waitBetween_PWM1_serial5.Text == "" || 
                this.textBox_waitBetween_PWM1_serial6.Text == "" || 

                this.textBox_waitBetween_PWM2_serial1.Text == "" || 
                this.textBox_waitBetween_PWM2_serial2.Text == "" || 
                this.textBox_waitBetween_PWM2_serial3.Text == "" || 
                this.textBox_waitBetween_PWM2_serial4.Text == "" || 
                this.textBox_waitBetween_PWM2_serial5.Text == "" || 
                this.textBox_waitBetween_PWM2_serial6.Text == "" || 

                this.textBox_waitBetween_PWM3_serial1.Text == "" || 
                this.textBox_waitBetween_PWM3_serial2.Text == "" || 
                this.textBox_waitBetween_PWM3_serial3.Text == "" || 
                this.textBox_waitBetween_PWM3_serial4.Text == "" || 
                this.textBox_waitBetween_PWM3_serial5.Text == "" || 
                this.textBox_waitBetween_PWM3_serial6.Text == "" ||
                #endregion

                //wait after
                #region
                this.textBox_waitAfter_PWM1_serial1.Text == "" || 
                this.textBox_waitAfter_PWM1_serial2.Text == "" || 
                this.textBox_waitAfter_PWM1_serial3.Text == "" || 
                this.textBox_waitAfter_PWM1_serial4.Text == "" || 
                this.textBox_waitAfter_PWM1_serial5.Text == "" || 
                this.textBox_waitAfter_PWM1_serial6.Text == "" || 

                this.textBox_waitAfter_PWM2_serial1.Text == "" || 
                this.textBox_waitAfter_PWM2_serial2.Text == "" || 
                this.textBox_waitAfter_PWM2_serial3.Text == "" || 
                this.textBox_waitAfter_PWM2_serial4.Text == "" || 
                this.textBox_waitAfter_PWM2_serial5.Text == "" || 
                this.textBox_waitAfter_PWM2_serial6.Text == "" || 

                this.textBox_waitAfter_PWM3_serial1.Text == "" || 
                this.textBox_waitAfter_PWM3_serial2.Text == "" || 
                this.textBox_waitAfter_PWM3_serial3.Text == "" || 
                this.textBox_waitAfter_PWM3_serial4.Text == "" || 
                this.textBox_waitAfter_PWM3_serial5.Text == "" || 
                this.textBox_waitAfter_PWM3_serial6.Text == "" ||
                #endregion

                this.textBox_exhalationThreshold.Text==""||this.textBox_waitBeforeStart.Text=="")

            {
                return 1;
            }
            if (//duty cycle
                #region
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial1.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial2.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial3.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial4.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial5.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM1_serial6.Text) < 5 ||

                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial1.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial2.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial3.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial4.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial5.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM2_serial6.Text) < 5 ||

                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial1.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial2.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial3.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial4.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial5.Text) < 5 ||
                Convert.ToInt32(this.textBox_dutyCycle_PWM3_serial6.Text) < 5 
                #endregion
                )
            {
                return 2;
            }
            return 0;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!this.serialPort1.IsOpen)
            {
                MessageBox.Show("Please connect serial port first!");
                return;
            }

            int res = CheckTextBox();
            if (res == 1)
            {
                MessageBox.Show("Not allow emtpy data!");
                return;
            }
            if (res == 2)
            {
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                return;
            }
           
            //MessageBox.Show(this.serialPort1.PortName);
            //this.button_Write.Enabled = false;

            //刷新链表
            GetCurrentPWMParameter2List();

            //发送公共信息到串口
            SendCommPara2SerialPort();
            System.Threading.Thread.Sleep(120);  //阻塞线程150ms，留时间给下位机读取数据

            //根据MODE-PWM发送，一帧一帧的发送
            for (int i = 1; i <= 3;i++ )
            {
                for(int j=1;j<=3;j++)
                {
                    SendModePWMPara2SerialPort(i, j);
                    System.Threading.Thread.Sleep(120);
                }
            }
            //发送询问帧，下位机是否接收完数据
            SendIsPCBRcvFinshed();

            //MessageBox.Show("Write completed!");
            //this.button_Write.Enabled = true;
  
        }
        private void SendQuery1ForParameters()
        {
            //下发获取参数的命令 frameID=0x05
            byte[] buffer = new byte[7];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x05;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x05;   //请求第一帧
            buffer[4] = 0x01; //数据为0x01
            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256);
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void SendQuery2ForParameters()
        {
            //下发获取参数的命令 frameID=0x05
            byte[] buffer = new byte[7];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x05;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x06;   //请求第二帧
            buffer[4] = 0x01; //数据为0x01
            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256);
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void button1_Click(object sender, EventArgs e)
        {

            if (!this.serialPort1.IsOpen)
            {
                MessageBox.Show("Please connect serial port first!");
                return;
            }

           
            //MessageBox.Show(this.serialPort1.PortName);
            //this.button_Read.Enabled = false;
            //下发请求
            SendQuery1ForParameters();

            System.Threading.Thread.Sleep(150);

            SendQuery2ForParameters();
            //if(m_bRcvParamtersCompleted)
            //{
            //    SetPWMParameterFromList(this.comboBox_modeSelect.SelectedIndex+1);
            //    MessageBox.Show("接收数据完成！");
            //    this.button_Read.Enabled = true;
            //}

        }

        private void ParseFrame1()
        {
            m_commPara.EXHALATION_THRESHOLD = m_buffer[4];
            m_commPara.WAIT_BEFORE_START = m_buffer[5];
            this.textBox_exhalationThreshold.Text = Convert.ToString(m_commPara.EXHALATION_THRESHOLD/16);
            this.textBox_exhalation_threshold_decimal_fraction.Text = Convert.ToString(m_commPara.EXHALATION_THRESHOLD % 16); 

            this.textBox_waitBeforeStart.Text = Convert.ToString(m_commPara.WAIT_BEFORE_START);
            if(m_mode1_list.Count!=0)
            {
                m_mode1_list.Clear();
            }

            int j = 2+4;
            for (int i = 0; i < 18; i++)  // Mode1-PWM1,Mode1-PWM2,Mode1-PWM3
            {
                PARAMETER para = new PARAMETER();
                para.MODE_SELECTED = 0x00;
                para.PWM_SERIAL_SELECTED = m_buffer[j++];
                para.ENABLE = m_buffer[j++];
                para.FREQUENCE = m_buffer[j++];
                para.DUTY_CYCLE = m_buffer[j++];
                para.PERIOD = m_buffer[j++];
                para.NUM_OF_CYCLES = m_buffer[j++];
                para.WAIT_BETWEEN = m_buffer[j++];
                para.WAIT_AFTER = m_buffer[j++];
                m_mode1_list.Add(para);
            }

            if (m_mode2_list.Count != 0)
            {
                m_mode2_list.Clear();
            }

            for (int i = 0; i < 12; i++)  // Mode2-PWM1，Mode2-PWM2
            {
                PARAMETER para = new PARAMETER();
                para.MODE_SELECTED = 0x00;
                para.PWM_SERIAL_SELECTED = m_buffer[j++];
                para.ENABLE = m_buffer[j++];
                para.FREQUENCE = m_buffer[j++];
                para.DUTY_CYCLE = m_buffer[j++];
                para.PERIOD = m_buffer[j++];
                para.NUM_OF_CYCLES = m_buffer[j++];
                para.WAIT_BETWEEN = m_buffer[j++];
                para.WAIT_AFTER = m_buffer[j++];
                m_mode2_list.Add(para);
            }
            //MessageBox.Show("收到第1帧");
        }

        private void ParseFrame2()
        {
            //解析frame1时，已经清空了链表，这里不能在清空链表2
            int j = 4;
            for (int i = 0; i < 6; i++)  // Mode2-PWM3
            {
                PARAMETER para = new PARAMETER();
                para.MODE_SELECTED = 0x00;
                para.PWM_SERIAL_SELECTED = m_buffer[j++];
                para.ENABLE = m_buffer[j++];
                para.FREQUENCE = m_buffer[j++];
                para.DUTY_CYCLE = m_buffer[j++];
                para.PERIOD = m_buffer[j++];
                para.NUM_OF_CYCLES = m_buffer[j++];
                para.WAIT_BETWEEN = m_buffer[j++];
                para.WAIT_AFTER = m_buffer[j++];
                m_mode2_list.Add(para);
            }

            if (m_mode3_list.Count != 0)
            {
                m_mode3_list.Clear();
            }

            for (int i = 0; i < 18; i++)  // Mode3-PWM1,Mode3-PWM2,Mode3-PWM3
            {
                PARAMETER para = new PARAMETER();
                para.MODE_SELECTED = 0x00;
                para.PWM_SERIAL_SELECTED = m_buffer[j++];
                para.ENABLE = m_buffer[j++];
                para.FREQUENCE = m_buffer[j++];
                para.DUTY_CYCLE = m_buffer[j++];
                para.PERIOD = m_buffer[j++];
                para.NUM_OF_CYCLES = m_buffer[j++];
                para.WAIT_BETWEEN = m_buffer[j++];
                para.WAIT_AFTER = m_buffer[j++];
                m_mode3_list.Add(para);
            }
            //m_bRcvParamtersCompleted = true;
            //MessageBox.Show("收到第二帧");
            //MessageBox.Show("读取完成");
            SetPWMParameterFromList(this.comboBox_modeSelect.SelectedIndex + 1);
            
            SaveAllParameter2Files();
        }

        private void SaveAllParameter2Files()
        {
            //文件cfg1,cfg2,cfg3
            FileStream fs = new FileStream(m_mode1_cfgFilePath, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII);
            foreach (var parameter in m_mode1_list)
            {
                bw.Write(parameter.PWM_SERIAL_SELECTED);
                bw.Write(parameter.ENABLE);
                bw.Write(parameter.FREQUENCE);
                bw.Write(parameter.DUTY_CYCLE);
                bw.Write(parameter.PERIOD);
                bw.Write(parameter.NUM_OF_CYCLES);
                bw.Write(parameter.WAIT_BETWEEN);
                bw.Write(parameter.WAIT_AFTER);
            }
            bw.Close();
            fs.Close();

            fs = new FileStream(m_mode2_cfgFilePath, FileMode.Create);
            bw = new BinaryWriter(fs, Encoding.ASCII);
            foreach (var parameter in m_mode2_list)
            {
                bw.Write(parameter.PWM_SERIAL_SELECTED);
                bw.Write(parameter.ENABLE);
                bw.Write(parameter.FREQUENCE);
                bw.Write(parameter.DUTY_CYCLE);
                bw.Write(parameter.PERIOD);
                bw.Write(parameter.NUM_OF_CYCLES);
                bw.Write(parameter.WAIT_BETWEEN);
                bw.Write(parameter.WAIT_AFTER);
            }
            bw.Close();
            fs.Close();

            fs = new FileStream(m_mode3_cfgFilePath, FileMode.Create);
            bw = new BinaryWriter(fs, Encoding.ASCII);
            foreach (var parameter in m_mode3_list)
            {
                bw.Write(parameter.PWM_SERIAL_SELECTED);
                bw.Write(parameter.ENABLE);
                bw.Write(parameter.FREQUENCE);
                bw.Write(parameter.DUTY_CYCLE);
                bw.Write(parameter.PERIOD);
                bw.Write(parameter.NUM_OF_CYCLES);
                bw.Write(parameter.WAIT_BETWEEN);
                bw.Write(parameter.WAIT_AFTER);
            }
            bw.Close();
            fs.Close();
            

            //保存公共参数到cfg文件
            m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                              +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));

            m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);

            WriteCommPara2File();
        }
        private void request_rtc_frame_x(ref int number)
        {
            if (m_requestNo > m_total_frames)
                return;

            byte[] buffer = new byte[8];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x06;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x70;   //请求RTC数据包 

            buffer[4] = Convert.ToByte(number / 256);   //请求第x包，x=1,2,3,4....
            buffer[5] = Convert.ToByte(number % 256);

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256);
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }
        private void get_rtc_total_record_numbers()
        {
            m_rtcInfo_record_numbers = m_buffer[4] * 256 * 256 * 256 + m_buffer[5] * 256 * 256 + m_buffer[6] * 256 + m_buffer[7];
            //MessageBox.Show("数据记录条数:" + Convert.ToString(m_rtcInfo_record_numbers));

            System.Threading.Thread.Sleep(50);

            init_rtc_releated_var();

            //通过m_rtcInfo_record_numbers知道一共有多少帧会传上来
            int pageNumbers = m_rtcInfo_record_numbers / 256;  //有pageNumbers页
            int pageRest = m_rtcInfo_record_numbers % 256;      //不足一页的，还剩下pageRest条记录

            m_total_frames += 9 * pageNumbers; //9的含义：每页可以分成8+1次发送, 8个满包(8*30条记录),1个非满包(16条记录)

            //不足一页的
            m_total_frames += pageRest / 30;  //满包
            if (pageRest % 30 != 0)
            {
                m_total_frames++;  //非满包的就只有一个
            }
            m_requestNo = 1;

            request_rtc_frame_x(ref m_requestNo);

            //初始化progressbar
            progressBar1.Maximum = m_rtcInfo_record_numbers;
        }

        private void get_rtc_data()
        {
            int record_size = (m_buffer[LEN] - 2 - 4) / 8;  //有几条记录
            if (record_size <= 0 || (m_buffer[LEN] - 2 - 4) % 8 != 0)
            {
                return;
            }

            int j = 6;
            for (int i = 0; i < record_size; i++)
            {
                RTC_INFO info = new RTC_INFO();

                info.RTC_CODE = m_buffer[j++];
                info.RTC_RESERVED = m_buffer[j++];
                info.RTC_YEAR = m_buffer[j++];
                info.RTC_MONTH = m_buffer[j++];
                info.RTC_DAY = m_buffer[j++];
                info.RTC_HOUR = m_buffer[j++];
                info.RTC_MIN = m_buffer[j++];
                info.RTC_SEC = m_buffer[j++];

                m_rtc_data_list.Add(info);
            }

            //debug
            //MessageBox.Show(Convert.ToString(m_rtc_data_list.Count));
        }

        private void ParseData2Lists()
        {
            //将数据解析挂入到3个链表中
             if(m_buffer[CMDTYPE] != 0x00)
            {
                return;
            }
            //根据帧类型来判断
             switch (m_buffer[FRAME_ID])
             {
                 //新增了一个功能，下位机发送回来的“是否接收参数完成”，在此进行判断
                 case 0x08:
                     if (m_buffer[m_buffer[LEN] - 1] == 0x01)
                     {
                         MessageBox.Show("Write to equipment successful!");
                     }
                     else
                     {
                         MessageBox.Show("Write to equipment failed!");
                     }
                     break;
                 case 0x09:  //如果是参数数据帧1
                     ParseFrame1();
                     break;
                 case 0x0A:  //如果是报警数据帧2
                     ParseFrame2();
                     break;
                case 0x66:
                    if (m_buffer[m_buffer[LEN] - 1] == 0x01)
                    {
                        button_synch.Enabled = false;
                        button_enable_synRTC_function.Text = "ENABLE";
                        MessageBox.Show("Synchronize to equipment successful!");
                    }
                    else
                    {
                        MessageBox.Show("Synchronize to equipment failed!");
                    }
                    break;
                case 0x69:
                    get_rtc_total_record_numbers();
                    break;
                case 0x71:
                    if (m_buffer[4] * 256 + m_buffer[5] == m_requestNo)
                    {
                        get_rtc_data();

                        progressBar1.Value = m_rtc_data_list.Count;
                        if (m_rtc_data_list.Count == m_rtcInfo_record_numbers)
                        {
                            System.Threading.Thread.Sleep(500);

                            MessageBox.Show("Receive RTC data successful!\n\nYou can save it now!");

                            //Thread th = new Thread(new ThreadStart(delegate() { save_rtc_data(); }));
                            //th.TrySetApartmentState(ApartmentState.STA);
                            //th.Start();
                            //th.Join();

                            return;
                        }

                        //System.Threading.Thread.Sleep(10);

                        m_requestNo++;
                        request_rtc_frame_x(ref m_requestNo);
                    }
                    break;
                case 0x77:
                    //Show_honeywell_data_2_chart(m_buffer);
                    if (!b_stop_geting_HW_data)
                    {
                        //m_recv_cnt解决线程问题
                        lock(m_HW_buffer)
                        {
                            if (m_recv_cnt == 1)
                            {
                                m_recv_cnt = 0;
                                m_HW_buffer.Clear();
                                for (int i = 0; i < m_HWData_list.Count; i++)
                                {
                                    SENSOR_DATA data = new SENSOR_DATA();
                                    data.DATA_0 = m_HWData_list[i].DATA_0;
                                    data.DATA_1 = m_HWData_list[i].DATA_1;
                                    m_HW_buffer.Add(data);
                                }
                            }
                            else
                            {
                                m_recv_cnt++;
                                get_HW_data_2_list();
                            }
                        }
                    }
                    break;
                default:
                     break;
             }
        }

        private void get_HW_data_2_list()
        {
            int SIZE = HONEYWELL_STRC_DATA_SIZE;
            int strc_len = 2;

            string str_tmp = "";

            for (int i = 0; i < SIZE; i++)
            {
                //注意strct_data一定不能写在for的外面，不然会出现几个数据相同问题
                SENSOR_DATA strct_data = new SENSOR_DATA();   

                strct_data.DATA_0 = m_buffer[4 + 0 + i * strc_len];
                strct_data.DATA_1 = m_buffer[4 + 1 + i * strc_len];

                str_tmp+= strct_data.DATA_0.ToString()+"."+strct_data.DATA_1.ToString() + " ";

                m_HWData_list.Add(strct_data);
            }

            this.label_HW.Text = m_HWData_list.Count.ToString();
            //this.label_HW.Text = m_HWData_list.Count.ToString()+ " "+ str_tmp;
            //m_str_duration = (DateTime.Now - m_tm_start).ToString("HH:mm:ss");
            m_duration = (DateTime.Now - m_tm_start);
            //m_str_sampling_rate = (m_HWData_list.Count / (DateTime.Now - m_tm_start).TotalSeconds).ToString();
            m_sampling_rate =  ((float)(m_HWData_list.Count)) / (float)(DateTime.Now - m_tm_start).TotalSeconds;
            this.label_HW.Text = m_HWData_list.Count.ToString() + "     Duration: " + m_duration.ToString(@"hh\:mm\:ss")
                + "    Sample rate:"+" "+ String.Format("{0:N2}", m_sampling_rate);
        }

        private void show_chart(object list_data)
        {
            lock (m_HW_buffer)
            {
              //  while (true)
                {
                   // Thread.Sleep(500);
                    List<SENSOR_DATA> list = (List<SENSOR_DATA>)list_data;

                    if (list == null || list.Count == 0)
                    {
                        //continue;
                        return;
                    }

                    DataTable table1 = new DataTable();
                    table1 = new DataTable();
                    table1.Columns.Add("编号", typeof(int));
                    table1.Columns.Add("数据", typeof(float));

                    int LEN = 300;  //显示500个数据
                    float Y_MAX = 100;
                    List<SENSOR_DATA> show_list = new List<SENSOR_DATA>();

                    int index = 0;
                    index = (list.Count < LEN) ? 0 : list.Count - LEN;

                    int i = 0;
                    
                    for (; index < list.Count; index++)
                    {
                        if (m_HW_buffer == null || m_HW_buffer.Count == 0)
                        {
                            return;
                        }
                        double data = (float)(m_HW_buffer[index].DATA_0 * 10 + m_HW_buffer[index].DATA_1) / 10f;
                        data = data > Y_MAX ? Y_MAX : data;
                        table1.Rows.Add(i++, data);
                    }

                    Series series = new Series("S1");
                    ChartArea chartArea = new ChartArea("C1");
                    series.ChartArea = "C1";

                    this.chart1.Series.Clear();
                    this.chart1.ChartAreas.Clear();
                    this.chart1.Titles.Clear();

                    this.chart1.Titles.Add("Pressure(mmHg)");

                    this.chart1.BorderlineColor = Color.Gray;
                    this.chart1.BorderlineWidth = 1;
                    this.chart1.BorderlineDashStyle = ChartDashStyle.Solid;
                    series.BorderWidth = 2;

                    //chartarea中设置是否显示虚线
                    chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.NotSet;
                    chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

                    //series中设置x,y轴坐标类型
                    series.XValueType = ChartValueType.Int32;  //设置X,Y轴的坐标类型
                    series.YValueType = ChartValueType.Int32;

                    //设置图标类型，折线图
                    series.ChartType = SeriesChartType.Line;
                    series.MarkerSize = 1;

                    ////chartArea_honeywellData.AxisX.LabelStyle.Format = "HH:mm\nMM-dd";
                    //chartArea.AxisX.LabelStyle.Format = "%d";
                    chartArea.AxisX.Minimum = 0;
                    chartArea.AxisX.Maximum = LEN;
                    chartArea.AxisX.Interval = 1;
                    ////chartArea_honeywellData.AxisX.Interval = 0.125 / 3 * duration; //调整x轴坐标，特别注意这个！

                    //chartarea中设置y轴显示范围，以及步长
                    chartArea.AxisY.Maximum = Y_MAX;
                    chartArea.AxisY.Minimum = -10;
                    chartArea.AxisY.Interval = 10;

                    this.chart1.Legends.Clear(); //清除chart_workData的legend
                    this.chart1.ChartAreas.Add(chartArea);
                    this.chart1.Series.Add(series);
                    this.chart1.Series[0].Points.DataBind(table1.AsEnumerable(), "编号", "数据", "");

                    table1 = null;

                    //this.chart1.Invalidate();
                }
            }

        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var nPendingRead = this.serialPort1.BytesToRead;
            byte[] tmp = new byte[nPendingRead];
            this.serialPort1.Read(tmp, 0, nPendingRead);

            //m_bRcvParamtersCompleted = false;
            lock (m_buffer)
            {
                m_buffer.AddRange(tmp);
#region
                while (m_buffer.Count >= 4)
                {
                    if (m_buffer[HEAD] == 0xFF) //帧头
                    {
                        int len = Convert.ToInt32(m_buffer[LEN]); // 获取帧长度(不包含checksum1和checksum2)
                        if (m_buffer.Count < len + 2)  //数据没有接收完全，继续接收
                        {
                            break;
                        }
                        int checksum = 256 * Convert.ToInt32(m_buffer[len]) + Convert.ToInt32(m_buffer[len + 1]);
                        int sum = 0;
                        for (int i = 1; i < len; i++) //校验和不包含包头
                        {
                            sum += Convert.ToInt32(m_buffer[i]);
                        }
                        //MessageBox.Show(sum.ToString());
                        if (checksum == sum)
                        {
                            //解析数据，加入到3个链表中
                            ParseData2Lists();
                        }
                        else
                        {
                            //校验之后发现数据不对,清除该帧数据
                            m_buffer.RemoveRange(0, len + 2);
                            continue;
                        }
                        m_buffer.RemoveRange(0, len + 2);
                    }
                    else
                    {
                        m_buffer.RemoveAt(0); //清除帧头
                    }
                }
#endregion
            }
        }

        private void comboBox_portName_SelectedValueChanged(object sender, EventArgs e)
        {
            this.serialPort1.PortName = this.comboBox_portName.Text;
        }

        private void button_export_Click(object sender, EventArgs e)
        {
            int res = CheckTextBox();
            if (res == 1)
            {
                MessageBox.Show("Not allow emtpy data!");
                return;
            }
            if (res == 2)
            {
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                return;
            }

            if(m_mode1_list.Count==0||m_mode2_list.Count==0||m_mode3_list.Count==0)
            {
                MessageBox.Show("No data");
            }
            if(this.folderBrowserDialog1.ShowDialog()==DialogResult.OK)
            {
                var path = this.folderBrowserDialog1.SelectedPath;
                //写配置文件1
                FileStream fs = new FileStream(path + @"\" + "Parameters.csv",FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, Encoding.Default);

                m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                                +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));

                m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);
                sw.WriteLine("Exhalation threshold(mmgH):" + "," + Convert.ToString(m_commPara.EXHALATION_THRESHOLD/16)+"."+Convert.ToString(m_commPara.EXHALATION_THRESHOLD%16));
                sw.WriteLine("Wait before start(Sec):" + "," + Convert.ToString(m_commPara.WAIT_BEFORE_START));

                List<PARAMETER> list=null;
                for (int i = 0; i < 3;i++ )
                {
                    switch(i)
                    {
#region
                        case 0:
                            sw.WriteLine(" ");
                            sw.WriteLine(" " + "," + "MODE1" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," +
                               "MODE1" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + "MODE1");

                            list = m_mode1_list;
                            break;
                        case 1:
                            sw.WriteLine(" ");
                            sw.WriteLine(" " + "," + "MODE2" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," +
                               "MODE2" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + "MODE2");

                            list = m_mode2_list;
                            break;
                        case 2:
                            sw.WriteLine(" ");
                            sw.WriteLine(" " + "," + "MODE3" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," +
                               "MODE3" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + "MODE3");

                            list=m_mode3_list;
                            break;
                        default:
                            break;
#endregion
                    }
                    //准备链表存储数据
#region
                    List<byte> list_enable = new List<byte>();
                    List<byte> list_freq = new List<byte>();
                    List<byte> list_dutyCycle = new List<byte>();
                    List<byte> list_period = new List<byte>();
                    List<byte> list_numOfCycles = new List<byte>();
                    List<byte> list_waitBetween = new List<byte>();
                    List<byte> list_waitAfter = new List<byte>();
#endregion
                    foreach (var para in list)
                    {
#region
                        list_enable.Add(para.ENABLE);
                        list_freq.Add(para.FREQUENCE);
                        list_dutyCycle.Add(para.DUTY_CYCLE);
                        list_period.Add(para.PERIOD);
                        list_numOfCycles.Add(para.NUM_OF_CYCLES);
                        list_waitBetween.Add(para.WAIT_BETWEEN);
                        list_waitAfter.Add(para.WAIT_AFTER);
#endregion
                    }

                    //写入模式1的参数数据
                    sw.WriteLine(" "+","+"PWM1" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," +
                               "PWM2" + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + " " + "," + "PWM3");
                    sw.WriteLine(" "+","+"S1" + "," + "S2" + "," + "S3" + "," + "S4" + "," + "S5" + "," + "S6" + "," + " " + "," +
                                    "S1" + "," + "S2" + "," + "S3" + "," + "S4" + "," + "S5" + "," + "S6" + "," + " " + "," +
                                    "S1" + "," + "S2" + "," + "S3" + "," + "S4" + "," + "S5" + "," + "S6");
                    //分别写入
#region
                    //写enable
#region
                    string tmpStr = "";
                    int nIndex = 0;
                    for (int j = -1; j <= 19;j++ )
                    {
                        if(j==-1)
                        {
                            tmpStr = "Enable (1:true 0:false)"+",";
                            continue;
                        }
                        if(j==6||j==13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_enable[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写freq
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Frequency (1-255 Hz)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_freq[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写duty cycle
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Duty cycle (5%-99%)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_dutyCycle[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写period
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Period (1-255 Sec)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_period[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写Number of cycles
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Number of cycles (1-250)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_numOfCycles[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写wait between
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Wait between (0-255 Sec)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_waitBetween[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion

                    //写wait after
#region
                    tmpStr = "";
                    nIndex = 0;
                    for (int j = -1; j <= 19; j++)
                    {
                        if (j == -1)
                        {
                            tmpStr = "Wait after (0-255 Sec)" + ",";
                            continue;
                        }
                        if (j == 6 || j == 13)
                        {
                            tmpStr += " " + ",";
                            continue;
                        }
                        tmpStr += Convert.ToString(list_waitAfter[nIndex++]) + ",";
                    }
                    sw.WriteLine(tmpStr);
#endregion
#endregion
                }

                sw.Close();
                fs.Close();
                MessageBox.Show("Export \"Parameter.csv\" sucessfully!");
            }
            else
            {

            }

        }

        private void 帮助ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void 软件版本ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form_aboutUs_english fm = new Form_aboutUs_english();
            fm.ShowDialog();
        }

        private void textBox_freq_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            //8-退格键
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8) )
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            ////初始化PWM1,PWM2,PWM3
            //InitPWMSet();
        }

        private void textBox_freq_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_freq_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_freq_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_dutyCycle_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_period_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_period_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM1_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM2_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8) )
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitAfter_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_waitBetween_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial3_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_numberOfCycles_PWM3_serial1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM3_serial6_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_dutyCycle_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }

        }

        private void textBox_dutyCycle_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_dutyCycle_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (((TextBox)sender).Text.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (((TextBox)sender).Text[i] <= '0' && ((TextBox)sender).Text[i] >= '9')
                        return;
                }
                Int32 num = Convert.ToInt32(((TextBox)sender).Text);
                if (num < 5 || num > 100)
                {
                    MessageBox.Show("Out of range,please input again!");
                    ((TextBox)sender).Text = "";
                }
            }
        }

        private void textBox_period_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_period_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_numberOfCycles_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 1 || Convert.ToInt32(((TextBox)sender).Text) > 250)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBetween_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM1_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM2_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial1_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial2_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial3_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial4_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial5_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitAfter_PWM3_serial6_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }


        private void comboBox_modeSelect_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (CheckTextBox() == 1)
            {
                ((ComboBox)sender).SelectedIndex = m_combox_prev_selectIndex;
                MessageBox.Show("Not allow empty data!");
                return;
            }
            if (CheckTextBox() == 2)
            {
                ((ComboBox)sender).SelectedIndex = m_combox_prev_selectIndex;
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                return;
            }
            m_combox_prev_selectIndex = ((ComboBox)sender).SelectedIndex;
            ////MessageBox.Show("改变之前的值:" + ((ComboBox)sender).SelectedIndex.ToString() + "  " + ((ComboBox)sender).Text);
            
            if (m_mode1_list.Count != 0 && m_mode2_list.Count != 0 && m_mode3_list.Count != 0)
            {
                GetCurrentPWMParameter2List();
            }
            
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CheckTextBox() == 0)
            {
                GetCurrentPWMParameter2List();
                SaveAllParameter2Files();
                //MessageBox.Show("save all parameters to files ok");
            }
            if (CheckTextBox() == 1)
            {
                MessageBox.Show("Not allow empty data!");
                e.Cancel = true;
            }
            if(CheckTextBox() == 2)
            {
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                e.Cancel = true;
            }
            
        }

        private void textBox_waitBeforeStart_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 255)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_waitBeforeStart_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        private void textBox_exhalationThreshold_TextChanged(object sender, EventArgs e)
        {
            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0 || ((TextBox)sender).Text == "")
            {
                return;
            }

            if (Convert.ToInt32(((TextBox)sender).Text) < 0 || Convert.ToInt32(((TextBox)sender).Text) > 15)
            {
                MessageBox.Show("Out of range,please input again!");
                ((TextBox)sender).Text = "";
            }
        }

        private void textBox_exhalationThreshold_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            } 
        }

        void SaveAllParameter2ConfigFile()
        {
            if (this.saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
#region
                this.textBox_paraCfgFilePath.Text = this.saveFileDialog1.FileName;
                FileStream fs = new FileStream(this.saveFileDialog1.FileName, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII);

                m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                                 +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));

                m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);
                bw.Write(m_commPara.EXHALATION_THRESHOLD);
                bw.Write(m_commPara.WAIT_BEFORE_START);

                foreach (var parameter in m_mode1_list)
                {
                    bw.Write(parameter.PWM_SERIAL_SELECTED);
                    bw.Write(parameter.ENABLE);
                    bw.Write(parameter.FREQUENCE);
                    bw.Write(parameter.DUTY_CYCLE);
                    bw.Write(parameter.PERIOD);
                    bw.Write(parameter.NUM_OF_CYCLES);
                    bw.Write(parameter.WAIT_BETWEEN);
                    bw.Write(parameter.WAIT_AFTER);
                }

                foreach (var parameter in m_mode2_list)
                {
                    bw.Write(parameter.PWM_SERIAL_SELECTED);
                    bw.Write(parameter.ENABLE);
                    bw.Write(parameter.FREQUENCE);
                    bw.Write(parameter.DUTY_CYCLE);
                    bw.Write(parameter.PERIOD);
                    bw.Write(parameter.NUM_OF_CYCLES);
                    bw.Write(parameter.WAIT_BETWEEN);
                    bw.Write(parameter.WAIT_AFTER);
                }

                foreach (var parameter in m_mode3_list)
                {
                    bw.Write(parameter.PWM_SERIAL_SELECTED);
                    bw.Write(parameter.ENABLE);
                    bw.Write(parameter.FREQUENCE);
                    bw.Write(parameter.DUTY_CYCLE);
                    bw.Write(parameter.PERIOD);
                    bw.Write(parameter.NUM_OF_CYCLES);
                    bw.Write(parameter.WAIT_BETWEEN);
                    bw.Write(parameter.WAIT_AFTER);
                }

                bw.Close();
                fs.Close();
#endregion
            }
           
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            
        }

        private void button_save_Click(object sender, EventArgs e)
        {
            if (CheckTextBox() == 0)
            {
                GetCurrentPWMParameter2List();
                //SaveAllParameter2Files();
                SaveAllParameter2ConfigFile();
                //MessageBox.Show("save all parameters to files ok");
            }
            if (CheckTextBox() == 1)
            {
                MessageBox.Show("Not allow empty data!");
                //e.Cancel = true;
                return;
            }
            if (CheckTextBox() == 2)
            {
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                //e.Cancel = true;
                return;
            }
        }

        private void button_load_Click(object sender, EventArgs e)
        {
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
#region
                this.textBox_paraCfgFilePath.Text = this.openFileDialog1.FileName;
                FileStream fs = new FileStream(this.openFileDialog1.FileName, FileMode.Open);
                BinaryReader br = new BinaryReader(fs, Encoding.ASCII);

                byte[] buffer=new byte[434];
                int len=br.Read(buffer, 0, 434);
                if (len < 434)
                {
                    MessageBox.Show("The file corrupt!");
                    return;
                }

                m_commPara.EXHALATION_THRESHOLD = buffer[0];
                m_commPara.WAIT_BEFORE_START = buffer[1];
                this.textBox_exhalationThreshold.Text = Convert.ToString(m_commPara.EXHALATION_THRESHOLD/16);
                this.textBox_exhalation_threshold_decimal_fraction.Text = Convert.ToString(m_commPara.EXHALATION_THRESHOLD % 16);

                this.textBox_waitBeforeStart.Text = Convert.ToString(m_commPara.WAIT_BEFORE_START);

                if (m_mode1_list.Count != 0 && m_mode2_list.Count != 0 && m_mode3_list.Count != 0)
                {
                    m_mode1_list.Clear();
                    m_mode2_list.Clear();
                    m_mode3_list.Clear();
#region
                    int j = 0;
                    for (int i = 0; i < 18*3; i++)
                    {
                        PARAMETER parameter = new PARAMETER();

                        parameter.PWM_SERIAL_SELECTED = buffer[2 + j++];
                        parameter.ENABLE = buffer[2 + j++];
                        parameter.FREQUENCE = buffer[2 + j++];
                        parameter.DUTY_CYCLE = buffer[2 + j++];
                        parameter.PERIOD = buffer[2 + j++];
                        parameter.NUM_OF_CYCLES = buffer[2 + j++];
                        parameter.WAIT_BETWEEN = buffer[2 + j++];
                        parameter.WAIT_AFTER = buffer[2 + j++];

                        if (i < 18)
                        {
                            m_mode1_list.Add(parameter);
                        }
                        if (i < 36 && i>=18)
                        //if (i < 36)
                        {
                            m_mode2_list.Add(parameter);
                        }
                        if (i < 54 && i>=36)
                        //if (i < 54)
                        {
                            m_mode3_list.Add(parameter);
                        }
                    }
#endregion

                    SetPWMParameterFromList(this.comboBox_modeSelect.SelectedIndex+1);
                }

                br.Close();
                fs.Close();
#endregion
            }
           
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        string ConBverInt2Hex(byte bt)
        {
            string tmp = null;
            Int32 a=Convert.ToInt32(bt) / 16;
            Int32 b = Convert.ToInt32(bt) % 16;
            //tmp += Convert.ToString(a);
            if (a == 10)
            {
                tmp += "A";
            }
            else if (a == 11)
            {
                tmp += "B";
            }
            else if (a == 12)
            {
                tmp += "C";
            }
            else if (a == 13)
            {
                tmp += "D";
            }
            else if (a == 14)
            {
                tmp += "E";
            }
            else if (a == 15)
            {
                tmp += "F";
            }
            else
            {
                tmp += Convert.ToString(a);
            }

            if (b == 10)
            {
                tmp += "A";
            }
            else if (b == 11)
            {
                tmp += "B";
            }
            else if (b == 12)
            {
                tmp += "C";
            }
            else if (b == 13)
            {
                tmp += "D";
            }
            else if (b == 14)
            {
                tmp += "E";
            }
            else if (b == 15)
            {
                tmp += "F";
            }
            else
            {
                tmp += Convert.ToString(b);
            }
            return tmp;
        }

        private void exportTxtFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int res = CheckTextBox();
            if (res == 1)
            {
                MessageBox.Show("Not allow emtpy data!");
                return;
            }
            if (res == 2)
            {
                MessageBox.Show("\"duty cycle\" are not allow below 5%");
                return;
            }

            if (m_mode1_list.Count == 0 || m_mode2_list.Count == 0 || m_mode3_list.Count == 0)
            {
                MessageBox.Show("No data");
            }
            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                var path = this.folderBrowserDialog1.SelectedPath;
                //写配置文件1
                FileStream fs = new FileStream(path + @"\" + "Parameters_EAR.txt", FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, Encoding.Default);

                UInt32 sum = 0;
                //m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(this.textBox_exhalationThreshold.Text);
                m_commPara.EXHALATION_THRESHOLD = Convert.ToByte(Convert.ToInt32(this.textBox_exhalationThreshold.Text)*16
                                                 +Convert.ToInt32(this.textBox_exhalation_threshold_decimal_fraction.Text));

                m_commPara.WAIT_BEFORE_START = Convert.ToByte(this.textBox_waitBeforeStart.Text);
                //sw.WriteLine("Exhalation threshold(mmgH):" + "," + Convert.ToString(m_commPara.EXHALATION_THRESHOLD));
                //sw.WriteLine("Wait before start(Sec):" + "," + Convert.ToString(m_commPara.WAIT_BEFORE_START));

                sum += Convert.ToUInt32(m_commPara.EXHALATION_THRESHOLD);
                sum += Convert.ToUInt32(m_commPara.WAIT_BEFORE_START);

                string str = "";
                str += Convert.ToString(m_commPara.EXHALATION_THRESHOLD)+",//"
                    +Convert.ToString(m_commPara.EXHALATION_THRESHOLD/16)+"."+Convert.ToString(m_commPara.EXHALATION_THRESHOLD%16) 
                    +","+ Convert.ToString(m_commPara.EXHALATION_THRESHOLD / 16)+"*16+" + Convert.ToString(m_commPara.EXHALATION_THRESHOLD % 16)
                    +"="+Convert.ToString(m_commPara.EXHALATION_THRESHOLD) + "\n";

                str += Convert.ToString(m_commPara.WAIT_BEFORE_START)+","+"\n\n//MODE1\n";

                int cnt = 0;
                
                foreach (var para in m_mode1_list)
                {
                    sum += Convert.ToUInt32(para.PWM_SERIAL_SELECTED);
                    sum += Convert.ToUInt32(para.ENABLE);
                    sum += Convert.ToUInt32(para.FREQUENCE);
                    sum += Convert.ToUInt32(para.DUTY_CYCLE);
                    sum += Convert.ToUInt32(para.PERIOD);
                    sum += Convert.ToUInt32(para.NUM_OF_CYCLES);
                    sum += Convert.ToUInt32(para.WAIT_BETWEEN);
                    sum += Convert.ToUInt32(para.WAIT_AFTER);


                    str += "0x" + ConBverInt2Hex(Convert.ToByte(para.PWM_SERIAL_SELECTED)) + "," +
                            Convert.ToString(Convert.ToByte(para.ENABLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.FREQUENCE)) + "," +
                            Convert.ToString(Convert.ToByte(para.DUTY_CYCLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.PERIOD)) + "," +
                            Convert.ToString(Convert.ToByte(para.NUM_OF_CYCLES)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_BETWEEN)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_AFTER)) + "," +"\n";
                    if (cnt == 5 || cnt == 11 || cnt == 17)
                    {
                        str += "\n";
                    }
                    cnt++;
                }
                str += "\n//MODE2\n";

                cnt = 0;
                foreach (var para in m_mode2_list)
                {
                    sum += Convert.ToUInt32(para.PWM_SERIAL_SELECTED);
                    sum += Convert.ToUInt32(para.ENABLE);
                    sum += Convert.ToUInt32(para.FREQUENCE);
                    sum += Convert.ToUInt32(para.DUTY_CYCLE);
                    sum += Convert.ToUInt32(para.PERIOD);
                    sum += Convert.ToUInt32(para.NUM_OF_CYCLES);
                    sum += Convert.ToUInt32(para.WAIT_BETWEEN);
                    sum += Convert.ToUInt32(para.WAIT_AFTER);

                    str += "0x" + ConBverInt2Hex(Convert.ToByte(para.PWM_SERIAL_SELECTED)) + "," +
                            Convert.ToString(Convert.ToByte(para.ENABLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.FREQUENCE)) + "," +
                            Convert.ToString(Convert.ToByte(para.DUTY_CYCLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.PERIOD)) + "," +
                            Convert.ToString(Convert.ToByte(para.NUM_OF_CYCLES)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_BETWEEN)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_AFTER)) + "," + "\n";
                    if (cnt == 5 || cnt == 11 || cnt == 17)
                    {
                        str += "\n";
                    }
                    cnt++;
                }
                str += "\n//MODE3\n";

                cnt = 0;
                foreach (var para in m_mode3_list)
                {
                    sum += Convert.ToUInt32(para.PWM_SERIAL_SELECTED);
                    sum += Convert.ToUInt32(para.ENABLE);
                    sum += Convert.ToUInt32(para.FREQUENCE);
                    sum += Convert.ToUInt32(para.DUTY_CYCLE);
                    sum += Convert.ToUInt32(para.PERIOD);
                    sum += Convert.ToUInt32(para.NUM_OF_CYCLES);
                    sum += Convert.ToUInt32(para.WAIT_BETWEEN);
                    sum += Convert.ToUInt32(para.WAIT_AFTER);

                    str += "0x" + ConBverInt2Hex(Convert.ToByte(para.PWM_SERIAL_SELECTED)) + "," +
                            Convert.ToString(Convert.ToByte(para.ENABLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.FREQUENCE)) + "," +
                            Convert.ToString(Convert.ToByte(para.DUTY_CYCLE)) + "," +
                            Convert.ToString(Convert.ToByte(para.PERIOD)) + "," +
                            Convert.ToString(Convert.ToByte(para.NUM_OF_CYCLES)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_BETWEEN)) + "," +
                            Convert.ToString(Convert.ToByte(para.WAIT_AFTER)) + "," + "\n";
                    if (cnt == 5 || cnt == 11 || cnt == 17)
                    {
                        str += "\n";
                    }
                    cnt++;
                }
                str += "\n//Checksum\n";

                str += "0x" + ConBverInt2Hex(Convert.ToByte (sum / 256)) + ",";
                str += "0x" + ConBverInt2Hex(Convert.ToByte(sum % 256));


                sw.WriteLine(str);
               
                sw.Close();
                fs.Close();
                MessageBox.Show("Export \"Parameters_EAR.txt\" sucessfully!");
            }
            else
            {

            }
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (File.Exists(m_commPara_cfgFilePath))
            {
                File.Delete(m_commPara_cfgFilePath);
            }
            if (File.Exists(m_mode1_cfgFilePath))
            {
                File.Delete(m_mode1_cfgFilePath);
            }
            if (File.Exists(m_mode2_cfgFilePath))
            {
                File.Delete(m_mode2_cfgFilePath);
            }
            if (File.Exists(m_mode3_cfgFilePath))
            {
                File.Delete(m_mode3_cfgFilePath);
            }

            InitCommParameter();
            InitPWMSet();
        }

        private void textBox_exhalation_threshold_decimal_fraction_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || (e.KeyChar == 8))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void button_enable_synRTC_function_Click(object sender, EventArgs e)
        {
            button_synch.Enabled = !button_synch.Enabled;
            if (button_synch.Enabled == true)
            {
                button_enable_synRTC_function.Text = "DISABLE";
                button_synch.Enabled = true;
            }
            else
            {
                button_enable_synRTC_function.Text = "ENABLE";
                button_synch.Enabled = false;
            }
        }

        private void button_synch_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show("Please open serial port first!");
                return;
            }

            DateTime dt = DateTime.Now;
            Byte year1 = Convert.ToByte(dt.Year / 100);
            Byte year2 = Convert.ToByte(dt.Year % 100);
            Byte month = Convert.ToByte(dt.Month);
            Byte day = Convert.ToByte(dt.Day);
            Byte weekDay = Convert.ToByte(dt.DayOfWeek);
            Byte hour = Convert.ToByte(dt.Hour);
            Byte min = Convert.ToByte(dt.Minute);
            Byte sec = Convert.ToByte(dt.Second);


            ////debug
            //Byte year1 = Convert.ToByte(19);
            //Byte year2 = Convert.ToByte(99);
            //Byte month = Convert.ToByte(12);
            //Byte day = Convert.ToByte(31);
            //Byte hour = Convert.ToByte(23);
            //Byte min = Convert.ToByte(59);
            //Byte sec = Convert.ToByte(50);

            byte[] buffer = new byte[14];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 12;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x65;

            buffer[4 + 0] = year1;
            buffer[4 + 1] = year2;
            buffer[4 + 2] = month;
            buffer[4 + 3] = day;
            buffer[4 + 4] = weekDay;
            buffer[4 + 5] = hour;
            buffer[4 + 6] = min;
            buffer[4 + 7] = sec;

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256);
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private void button_get_RTC_info_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)
            {
                MessageBox.Show("Please open serial port first!");
                return;
            }


            byte[] buffer = new byte[6];
            buffer[HEAD] = 0xFF;
            buffer[LEN] = 0x04;
            buffer[CMDTYPE] = 0x01;
            buffer[FRAME_ID] = 0x68;   //请求RTC数据总字节数

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[LEN]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[LEN])] = Convert.ToByte(sum / 256);
            buffer[Convert.ToInt32(buffer[LEN]) + 1] = Convert.ToByte(sum % 256);
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[LEN]) + 2);
        }

        private String code2HEX(byte code)
        {
#region
            String tmp = "";
            switch (code)
            {
                case 0x11:
                    tmp = "0x11";
                    break;
                case 0x12:
                    tmp = "0x12";
                    break;
                case 0x13:
                    tmp = "0x13";
                    break;
                case 0x14:
                    tmp = "0x14";
                    break;
                case 0x15:
                    tmp = "0x15";
                    break;
                case 0x16:
                    tmp = "0x16";
                    break;
                case 0x17:
                    tmp = "0x17";
                    break;
                case 0x18:
                    tmp = "0x18";
                    break;
                case 0x19:
                    tmp = "0x19";
                    break;
                case 0x20:
                    tmp = "0x20"; ;
                    break;
                case 0x21:
                    tmp = "0x21"; ;
                    break;
                case 0x22:
                    tmp = "0x22"; ;
                    break;
                case 0x23:
                    tmp = "0x23"; ;
                    break;
                case 0x24:
                    tmp = "0x24"; ;
                    break;
                case 0x25:
                    tmp = "0x25"; ;
                    break;
                case 0x26:
                    tmp = "0x26"; ;
                    break;
                default:
                    break;
            }
            return tmp;
#endregion
        }

        private String code2str(byte code)
        {
#region
            String tmp = "";
            switch (code)
            {
                case 0x11:
                    tmp = "System power on";
                    break;
                case 0x12:
                    tmp = "One cycle finished";
                    break;
                case 0x13:
                    tmp = "Manual power off";
                    break;
                //case 0x14:
                //    tmp = "Not detect hand";
                    //break;
                case 0x15:
                    tmp = "No power";
                    break;
                case 0x16:
                    tmp = "Over pressure";
                    break;
                case 0x17:
                    tmp = "System_been_triggered";
                    break;
                //case 0x18:
                //    tmp = "Self test fail";
                //    break;
                case 0x19:
                    tmp = "No trigger in 60s";
                    break;
                case 0x20:
                    tmp = "PC synchronize RTC";
                    break;
                case 0x21:
                    tmp = "Switch to mode1";
                    break;
                case 0x22:
                    tmp = "Switch to mode2";
                    break;
                case 0x23:
                    tmp = "Switch to mode3";
                    break;
                case 0x24:
                    tmp = "Current mode is MODE1";
                    break;
                case 0x25:
                    tmp = "Current mode is MODE2";
                    break;
                case 0x26:
                    tmp = "Current mode is MODE3";
                    break;
                default:
                    break;
            }
            return tmp;
#endregion
        }

        private bool export_rtc_info_to_file()
        {
            String str = this.saveFileDialog2.FileName;

            str = str.Insert(str.IndexOf('.'), "");
            FileStream fs1 = null;
            try
            {
                fs1 = new FileStream(str, FileMode.Create);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return false;
            }

            StreamWriter sw1 = new StreamWriter(fs1, Encoding.Default);

            Byte pre_code = 0x00;
            DateTime pre_dt = DateTime.Now;


            sw1.WriteLine("StartTime" + "," + "EndTime" + "," + "Info" + "," + "Duration");
            foreach (var info in m_rtc_data_list)
            {
                if (pre_code == 0x11)
                {
                    if (info.RTC_CODE != 0x20&&info.RTC_CODE != 0x11)
                    { 
                        if (info.RTC_CODE == 0x13|| info.RTC_CODE == 0x15|| info.RTC_CODE == 0x16|| info.RTC_CODE == 0x19)
                        {
                            //可以开始写文件了
                            DateTime startTime = pre_dt;
                            DateTime endTime = Convert.ToDateTime(Convert.ToString(info.RTC_YEAR) + "-"
                                                    + Convert.ToString(info.RTC_MONTH) + "-"
                                                    + Convert.ToString(info.RTC_DAY) + " "
                                                    + Convert.ToString(info.RTC_HOUR) + ":"
                                                    + Convert.ToString(info.RTC_MIN) + ":"
                                                    + Convert.ToString(info.RTC_SEC));
                            TimeSpan tsp = endTime - startTime;
                            String str_duration = tsp.Hours.ToString() + ":" + tsp.Minutes.ToString() + ":" + tsp.Seconds.ToString();
                            sw1.WriteLine(startTime.ToString("yy-MM-dd HH:mm:ss") + "," + endTime.ToString("yy-MM-dd HH:mm:ss")
                                + "," + code2str(info.RTC_CODE) + "," + str_duration);
                        }
                       
                        
                    }
                }

                if (info.RTC_CODE == 0x11)
                {
                    pre_code = info.RTC_CODE;
                    pre_dt = Convert.ToDateTime(Convert.ToString(info.RTC_YEAR) + "-"
                                                + Convert.ToString(info.RTC_MONTH) + "-"
                                                + Convert.ToString(info.RTC_DAY) + " "
                                                + Convert.ToString(info.RTC_HOUR) + ":"
                                                + Convert.ToString(info.RTC_MIN) + ":"
                                                + Convert.ToString(info.RTC_SEC));
                }
            }


            sw1.Close();
            fs1.Close();
            return true;
        }

        private bool export_rtc_log()
        {
            //String fileName = "rtc_data " + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv";
            //FileStream fs = new FileStream(path + @"\" + fileName, FileMode.Create);
            String str = this.saveFileDialog2.FileName;
            str = str.Insert(str.IndexOf('.'), "_log");
            FileStream fs = null;
            try
            {
                fs = new FileStream(str, FileMode.Create);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return false;
            }

            StreamWriter sw = new StreamWriter(fs, Encoding.Default);

            //输出码表对照
            sw.WriteLine("Code" + "," + "Code Info" + "," + "Mark");
            sw.WriteLine("0x11" + "," + "System power on" + "," + "User power on the system");
            sw.WriteLine("0x12" + "," + "One cycle finished" + "," + "System run over all parameters after system been triggered");
            sw.WriteLine("0x13" + "," + "Manual power off" + "," + "User manual power off the system");
            //sw.WriteLine("0x14" + "," + "Not detect hand" + "," + "System auto power off because of not detect hand in 20s");
            sw.WriteLine("0x15" + "," + "No power" + "," + "System auto power off because of no power");
            sw.WriteLine("0x16" + "," + "Over pressure" + "," + "System auto power off because of over pressure");
            sw.WriteLine("0x17" + "," + "System been triggered" + "," + "Treatment start as exhale triggered the system");
           // sw.WriteLine("0x18" + "," + "Self test fail" + "," + "System auto power off because of self test fail");
            sw.WriteLine("0x19" + "," + "No trigger in 60s" + "," + "System auto power off because of no trigger in 60s");
            sw.WriteLine("0x20" + "," + "PC synchronize RTC" + "," + "Press synchronize time button on App");
            //sw.WriteLine("0x21" + "," + "Switch to MODE1" + "," + "User manual switch to MODE1");
            //sw.WriteLine("0x22" + "," + "Switch to MODE2" + "," + "User manual switch to MODE2");
            //sw.WriteLine("0x23" + "," + "Switch to MODE3" + "," + "User manual switch to MODE3");
            //sw.WriteLine("0x24" + "," + "Current mode is 1" + "," + "UCurrent mode is MODE1");
            //sw.WriteLine("0x25" + "," + "Current mode is 2" + "," + "UCurrent mode is MODE2");
            //sw.WriteLine("0x26" + "," + "Current mode is 3" + "," + "UCurrent mode is MODE3");

            sw.WriteLine(" "); //空一行

            sw.WriteLine("DateTime" + "," + "Code" + "," + "Code Info");
            foreach (var info in m_rtc_data_list)
            {
                String str_dateTime = Convert.ToString(info.RTC_YEAR) + "-"
                            + Convert.ToString(info.RTC_MONTH) + "-"
                            + Convert.ToString(info.RTC_DAY) + " "
                            + Convert.ToString(info.RTC_HOUR) + ":"
                            + Convert.ToString(info.RTC_MIN) + ":"
                            + Convert.ToString(info.RTC_SEC);
                //DateTime dt = Convert.ToDateTime(str_dateTime);
                //str_dateTime=dt.ToString("yy/MM/dd HH:mm:ss");
                sw.WriteLine(str_dateTime + "," + code2HEX(info.RTC_CODE) + "," + code2str(info.RTC_CODE));
            }

            sw.Close();
            fs.Close();
            return true;
        }

        private void save_rtc_data()
        {
#region
            if (m_rtc_data_list.Count > 0)
            {
                //没有接收完,不允许操作
                if (m_rtc_data_list.Count != m_rtcInfo_record_numbers)
                {
                    MessageBox.Show("Please save after receive completed!");
                    return;
                }

                if (this.saveFileDialog2.ShowDialog() == DialogResult.OK)
                {
                    //导出用户需要的rtc信息 
                    if (!export_rtc_info_to_file())
                    {
                        return;
                    }

                    if (checkBox_no_need_log.Checked == false)
                    {
                        //根据用户的需要导出详细的数据信息
                        if (!export_rtc_log())
                        {
                            return;
                        }
                    }
                    m_b_saveRtcFile = true;
                    MessageBox.Show("RTC file save successful!");
                }
                else
                {
                    m_b_saveRtcFile = false;
                }
            }
            else
            {
                MessageBox.Show("Please get rtc data first!");
            }
#endregion
        }

        private void init_rtc_releated_var()
        {
            if (m_rtc_data_list != null)
            {
                m_rtc_data_list.Clear();
            }
            m_total_frames = 0; //清除m_total_frames
            progressBar1.Value = 0;
        }

        private void button_save_rtc_data_Click(object sender, EventArgs e)
        {
            save_rtc_data();
            if (m_b_saveRtcFile)
            {
                init_rtc_releated_var();
            }
        }

        private int m_no_recv_cnt = 0;
        private int m_prev_recv_cnt = 0;

        private bool Is_no_reply_from_device()
        {
            bool res = false;
            if (m_no_recv_cnt == 30)
            {
                m_recv_cnt = 0;
                m_prev_recv_cnt = 0;

                m_no_recv_cnt = 0;
                res = true;
            }
            else
            {
                if (m_recv_cnt == m_prev_recv_cnt)
                {
                    m_no_recv_cnt++;
                }
                else
                {
                    m_prev_recv_cnt = m_no_recv_cnt;
                }
            }
            return res;
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            GetSystemDateTime();


            if (b_start_HW_data && Is_no_reply_from_device())
            {
                b_start_HW_data = false;
                button_start.Text = "Start";
                button_Read.Enabled = true;
                button_openSerialPort.Enabled = true;
                MessageBox.Show("No response from device!");
            }

            if (m_HW_buffer != null&&m_HW_buffer.Count>0)
            {
                show_chart(m_HW_buffer);
            }
            

        }

        private void sensorDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //if (!this.serialPort1.IsOpen)
            //{
            //    MessageBox.Show("Please open serial port!");
            //    return;
            //}

            //m_Fm_sensor = new Form_Sensor();
            //m_Fm_sensor.GetHoneywellData += send_query;   //注册调用的函数
            ////m_Fm_sensor.GetHoneywellData += new Form_Sensor.GetHoneyWellDataHandler(send_query);
            ////m_Fm_sensor.ShowDialog();    //模态对话框
            //m_Fm_sensor.Show();        //非模态对话框
        }

        private void send_query(byte[] buffer)
        {
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[1]) + 2);
        }

        private void button_start_Click(object sender, EventArgs e)
        {
            if (!this.serialPort1.IsOpen)
            {
                MessageBox.Show("Please connect serial port!");
                return;
            }

            b_start_HW_data = !b_start_HW_data;

            if (b_start_HW_data)   //开始发送数据
            {
                //选择chart页
                tabControl1.SelectedIndex = 1;

                this.button_start.Text = "Stop";

                //disable 读下位机参数按键
                button_Read.Enabled = false;

                //disable "connect"按键
                button_openSerialPort.Enabled = false;

                //发送,停止获取honeywell data 数据
                send_stop_getting_HW_data();
                Thread.Sleep(500);

                //清空m_buffer
                m_buffer.Clear();

                //清空m_HWData_list
                m_HWData_list.Clear();

                //初始化lable_HW
                this.label_HW.Text = "0";

                //初始化b_stop_geting_HW_data
                b_stop_geting_HW_data = false;

                send_query_for_HW_data();

                //获取开始时间
                m_tm_start = DateTime.Now;
                m_duration = new TimeSpan(0, 0, 0);
            }
            else   //停止发送数据
            {
                button_openSerialPort.Enabled = true;
                button_Read.Enabled = true;

                this.button_start.Text = "Start";
                b_stop_geting_HW_data = true;
                send_stop_getting_HW_data();


                Thread.Sleep(500);
                
            }
        }

        private void send_query_for_HW_data()
        {
            byte[] buffer = new byte[6];
            buffer[0] = 0xFF;  //head
            buffer[1] = 0x04;  //len
            buffer[2] = 0x01;  //cmdtype
            buffer[3] = 0x76; //FRAMID          #define GET_HONEYWELL_DATA	0x76	//上位机发送的请求获取honeywell数据命令

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[1]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[1])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[1]) + 1] = Convert.ToByte(sum % 256); //checksum2
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[1]) + 2);
        }

        private void send_stop_getting_HW_data()
        {
            byte[] buffer = new byte[6];
            buffer[0] = 0xFF;  //head
            buffer[1] = 0x04;  //len
            buffer[2] = 0x01;  //cmdtype
            buffer[3] = 0x78; //FRAMID          #define SOP_GET_HONEYWELL_DATA		0x78	//停止发送数据

            int sum = 0;
            for (int i = 1; i < Convert.ToInt32(buffer[1]); i++)
            {
                sum += buffer[i];
            }
            buffer[Convert.ToInt32(buffer[1])] = Convert.ToByte(sum / 256);   //checksum1
            buffer[Convert.ToInt32(buffer[1]) + 1] = Convert.ToByte(sum % 256); //checksum2
            this.serialPort1.Write(buffer, 0, Convert.ToInt32(buffer[1]) + 2);
        }

        private void button_save_Click_1(object sender, EventArgs e)
        {
            if (b_start_HW_data)
            {
                MessageBox.Show("Please stop receiving data...");
                return;
            }

            b_stop_geting_HW_data = true;
            //this.button_start.Enabled = true;
            //this.button_save.Enabled = false;
            send_stop_getting_HW_data();
            //MessageBox.Show(m_HWData_list.Count.ToString());
            if (save_HW_data_2_file())
            {
                this.label_HW.Text = "0";
            }
        }

        private bool save_HW_data_2_file()
        {
            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                var path = this.folderBrowserDialog1.SelectedPath;

                FileStream fs = null;
                StreamWriter sw = null;
                try
                {
                    fs = new FileStream(path + @"\" + "Pressure_Data.csv", FileMode.Create);
                }
                catch (IOException ex)
                {
                    if (fs != null)
                    {
                        fs.Close();
                    }
                    MessageBox.Show(ex.Message);
                    return false;
                }
                try
                {
                    sw = new StreamWriter(fs, Encoding.Default);
                }
                catch (IOException ex)
                {
                    if (sw != null)
                    {
                        sw.Close();
                    }
                    MessageBox.Show(ex.Message);
                    return false;
                }

                sw.WriteLine("Quntity of Recv Datas:" + "," + m_HWData_list.Count.ToString());
                //sw.WriteLine("Duraton:" + ","+m_duration.ToString(@"hh\:mm\:ss")); 
                sw.WriteLine("Duraton:" + "," + m_duration.ToString());
                sw.WriteLine("Sampling Rate:" + "," + String.Format("{0:N2}",m_sampling_rate));
                sw.WriteLine(" ");
                sw.WriteLine("Index"+","+"Sec,"+"Value(mmHg)");

                float f_sec_step = (float)m_duration.TotalSeconds/(float)m_HWData_list.Count();
                for (int i = 0; i < m_HWData_list.Count; i++)
                {
                    string str = "";
                    str +=i.ToString()+","+ ((i+1) * f_sec_step).ToString() + ","+
                        string.Format("{0:N2}", (m_HWData_list[i].DATA_0 * 10 + m_HWData_list[i].DATA_1) / 10f);
                    sw.WriteLine(str);
                }

                sw.Close();
                fs.Close();
                m_HWData_list.Clear();
                MessageBox.Show("Export \"Pressure_Data.csv\" sucessfully!");
                return true;
            }
            else
            {
                return false;
            }
        }

        private void chart1_SelectionRangeChanged(object sender, CursorEventArgs e)
        {
            MessageBox.Show("wa");
        }

        private void chart1_AxisScrollBarClicked(object sender, ScrollBarEventArgs e)
        {
            MessageBox.Show("dfdfdf");
        }
    }
}
