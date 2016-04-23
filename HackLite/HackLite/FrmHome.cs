using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PacketDotNet;
using SharpPcap;

namespace HackLite
{
    public partial class FrmHome : Form
    {
            CaptureDeviceList devices;   //List of devices for this computer
        public static ICaptureDevice device; //The device we will be using
        public static string stringPackets = ""; //Data that is captured
        static int numPackets = 0;

        //public static string rawPacketData = "";
        //ip, mac, time it will expire
        //public static List<Tuple<string, string, DateTime>> ipList = new List<Tuple<string, string, DateTime>>();
        //public static List<string> possibleAddresses = new List<string>();
        private static IPTables ipLists;
        private static Settings settings = new Settings();
        private static bool DHCPisActive = false;
        public static int UDP = 0;
        public static int TCP = 0;
        //the address of the local box
        public static string localIp;
        private static string lastPacket = "";
        //the mac of the local box
        public static PhysicalAddress localMAC;
        private PcapAddress Address;



        //**********Default constructor
        public FrmHome()
        {
            InitializeComponent();

            {
                InitializeComponent();
                devices = CaptureDeviceList.Instance;
                if (devices == null || devices.Count < 1)
                {
                    MessageBox.Show("Error, no Capture Devices Found");
                    Application.Exit();
                }
                foreach (ICaptureDevice dev in devices)
                {
                    comboBox1.Items.Add(dev.Description);
                }
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
           if (comboBox1.Text == "Arp Cache Poisoning")
            {

            } 
           if (comboBox1.Text == "DOS")
            {

            }

        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            {
                //possibleAddresses = IPTables.gennerateIPRange(localIp, localMAC.ToString());
                ipLists = new IPTables(localIp, settings.subnet);
                var list = ipLists.GetAvalible();
                List<Task<string>> arpTasks = new List<Task<string>>();
                foreach (var address in list)
                {
                    arpTasks.Add(ARPAsync(address));
                }
            }
            catch (NullReferenceException nul)
            {
                MessageBox.Show("NIC must be selected and started");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
