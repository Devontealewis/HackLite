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
            //MAC
            //first 8 are destination
            //second 8 are source
            //udp = 11 on 24th byte
            //tcp = 06 on 24th byte
            //IP
            //sorce=27-30
            //destination = 31-34

            //0806 arp
            //
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
            //Console.WriteLine();
            /*
                capturedData += Environment.NewLine
                    + "Source IP: "+sourceIp + Environment.NewLine
                    + "Destination MAC: " + destinationMac + Environment.NewLine
                    + "Source MAC: " + sourceMac + Environment.NewLine
                    + "EtherType: " + type + Environment.NewLine;
            */
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




        private static string selectedIp;



        public void sendPacket(string bytesToSend)
        {
            //must be Hex
            bytesToSend = bytesToSend ?? "";
            //convert to byte array
            string[] bytes = bytesToSend.Split(new string[] { " ", "\n", "\r\n", "\t" }, StringSplitOptions.RemoveEmptyEntries);

            byte[] packet = new byte[bytes.Length];
            int i = 0;
            foreach (string s in bytes)
            {
                packet[i] = Convert.ToByte(s, 16);
                i++;
            }
            try
            {
                FrmHome.device.SendPacket(packet);
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }
        }
        Task<PingReply> PingAsync(string address)
        {
            var tcs = new TaskCompletionSource<PingReply>();
            Ping ping = new Ping();
            ping.PingCompleted += (obj, sender) =>
            {
                if (sender.Reply.Status == IPStatus.Success)
                    dataGridView1.Rows.Add(sender.Reply.Address);
                tcs.SetResult(sender.Reply);
            };
            ping.SendAsync(address, new object());
            return tcs.Task;
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

        private void pingStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                //possibleAddresses = IPTables.gennerateIPRange(localIp, localMAC.ToString());
                ipLists = new ScanTable(localIp, settings.subnet);
                var list = ipLists.GetAvalible();
                List<Task<PingReply>> pingTasks = new List<Task<PingReply>>();
                foreach (var address in list)
                {
                    pingTasks.Add(PingAsync(address));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
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
            }
            catch (NullReferenceException nul)
            {
                MessageBox.Show("NIC must be selected and started");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            updateTable();
        }



        private void FRMCapture_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (device != null && device.Started)
                device.Close();
        }

        private void tabView_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (device == null || !device.Started)
            {
                e.Cancel = true;
                MessageBox.Show("Select NIC First and Start");
            }
        }



    }
}