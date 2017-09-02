﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using PKHeX.Core;
using System.Text;
using System.Windows.Forms;

namespace serveLegality
{
    public partial class serveLegalityGUI : Form
    {
        private const int PKSIZE = 232;
        private const String HEADER = "PKSMOTA";
        private const int GAME_LEN = 1;
        private const int GEN6 = 6;
        private const int GEN7 = 7;
        private const string MGDatabasePath = "mgdb";
        public serveLegalityGUI()
        {
            InitializeComponent();
        }

        public static void DisplayUsage()
        {
            Console.WriteLine("\nUsage: serveLegality IPADDRESS [verbose]");
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }

        public static PKM GetPKMFromPayload(byte[] inputBuffer)
        {
            byte[] pkmData = new byte[PKSIZE];
            Array.Copy(inputBuffer, HEADER.Length + GAME_LEN, pkmData, 0, 232);
            PKM pk;

            byte version = inputBuffer[HEADER.Length];
            switch (version)
            {
                case GEN6:
                    pk = new PK6(pkmData);
                    break;
                case GEN7:
                    pk = new PK7(pkmData);
                    break;
                default:
                    pk = new PK7();
                    break;
            }

            return pk;
        }

        public static void PrintLegality(byte[] inputBuffer, bool verbose)
        {
            GameInfo.GameStrings gs = GameInfo.GetStrings("en");
            PKM pk = GetPKMFromPayload(inputBuffer);
            LegalityAnalysis la = new LegalityAnalysis(pk);
            Console.WriteLine("===================================================================");
            Console.WriteLine("Received: " + gs.specieslist[pk.Species] + "\n" + la.Report(verbose));
            Console.WriteLine("===================================================================");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            MessageBox.Show("Toggle Verbose and non verbose legalities");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            IPAddress PKSMAddress;
            bool verbose = checkBox1.Checked;
            if (!IPAddress.TryParse(textBox1.Text, out PKSMAddress))
            {
                DisplayUsage();
                return;
            }

            Console.WriteLine("Loading mgdb in memory...");
            Legal.RefreshMGDB(MGDatabasePath);

            byte[] inputBuffer = new byte[HEADER.Length + GAME_LEN + PKSIZE];
            byte[] outputBuffer = new byte[HEADER.Length + GAME_LEN + PKSIZE];

            IPAddress serverAddress = IPAddress.Parse(GetLocalIPAddress());
            if (textBox2.Text != "") if(!IPAddress.TryParse(textBox2.Text, out serverAddress)) return;
            TcpListener listener = new TcpListener(serverAddress, 9000);

            Console.WriteLine("\nserveLegality is running on " + serverAddress + "... Press CTRL+C to exit.");
            Console.WriteLine("Waiting for a connection from PKSM (running on address " + PKSMAddress + ")...");

            while (true)
            {
                try
                {
                    listener.Start();
                    Socket inputSocket = listener.AcceptSocket();
                    inputSocket.Receive(inputBuffer);
                    inputSocket.Close();
                    listener.Stop();

                    PrintLegality(inputBuffer, verbose);
                    PKM pk = GetPKMFromPayload(inputBuffer);
                    ServeLegality.AutoLegality al = new ServeLegality.AutoLegality();
                    PKM legal = al.LoadShowdownSetModded_PKSM(pk);

                    Array.Copy(Encoding.ASCII.GetBytes(HEADER), 0, outputBuffer, 0, HEADER.Length);
                    Array.Copy(legal.Data, 0, outputBuffer, 7, PKSIZE);
                    TcpClient client = new TcpClient();
                    client.Connect(PKSMAddress, 9000);
                    Stream stream = client.GetStream();
                    stream.Write(outputBuffer, 0, outputBuffer.Length);
                    client.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error" + ex.StackTrace);
                }
            }
        }
    }
}