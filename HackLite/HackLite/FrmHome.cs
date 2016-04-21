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



        //**********Default constructor
        public FrmHome()
        {
            InitializeComponent();

            //Get the list of devices
            devices = CaptureDeviceList.Instance;

            //Make sure that there is at least one device
            if (devices.Count < 1)
            {
                MessageBox.Show("No Capture Devices Found!!!");
                Application.Exit();
            }

            //Add the devices to the combo box
            foreach (ICaptureDevice dev in devices)
            {
                comboBox1.Items.Add(dev.Description);
            }

            //Get the second device and display in combo box
            device = devices[2];
            comboBox1.Text = device.Description;

            //Register our handler function to the 'packet arrival' event
            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(device_OnPacketArrival);

            //Open the device for capturing
            int readTimeoutMilliseconds = 1000;
            device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
        }
        private static void device_OnPacketArrival(object sender, CaptureEventArgs packet)
        {
            //Increment the number of packets captured
            numPackets++;

            //Put the packet number in the capture window
            stringPackets += "Packet Number: " + Convert.ToString(numPackets);
            stringPackets += Environment.NewLine;

            //Array to store our data
            byte[] data = packet.Packet.Data;

            //Keep track of the number of bytes displayed per line
            int byteCounter = 0;


            stringPackets += "Destination MAC Address: ";
            //Parsing the packets
            foreach (byte b in data)
            {
                //Add the byte to our string (in hexidecimal)
                if (byteCounter <= 13) stringPackets += b.ToString("X2") + " ";
                byteCounter++;

                switch (byteCounter)
                {
                    case 6:
                        stringPackets += Environment.NewLine;
                        stringPackets += "Source MAC Address: ";
                        break;
                    case 12:
                        stringPackets += Environment.NewLine;
                        stringPackets += "EtherType: ";
                        break;
                    case 14:
                        if (data[12] == 8)
                        {
                            if (data[13] == 0) stringPackets += "(IP)";
                            if (data[13] == 6) stringPackets += "(ARP)";
                        }
                        break;
                }

            }


            stringPackets += Environment.NewLine + Environment.NewLine;
            byteCounter = 0;
            stringPackets += "Raw Data" + Environment.NewLine;
            //Process each byte in our captured packet
            foreach (byte b in data)
            {
                //Add the byte to our string (in hexidecimal)
                stringPackets += b.ToString("X2") + " ";
                byteCounter++;

                if (byteCounter == 16)
                {
                    byteCounter = 0;
                    stringPackets += Environment.NewLine;
                }

            }
            stringPackets += Environment.NewLine;
            stringPackets += Environment.NewLine;
        } //End device_OnPacketArrival

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
    }
}
