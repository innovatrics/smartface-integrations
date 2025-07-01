using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// using System.Windows.Forms;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public class TServerClient
    {

        public delegate void ConnectionStateChange(bool connected);
        public delegate void DeviceListCallback(PrtclCmfJson.MsgDevlistResp devList);
        public delegate void DevEventCallback(PrtclCmfJson.MsgDevEvent devEvent);
        public delegate void ActionRespCallback(PrtclCmfJson.MsgActionResp actionResp);

        TcpClient tcpClient;
        const int buffer_size = 65535;
        byte[] rcv_data = new byte[buffer_size];
        //private Stopwatch inter_clock;
        //private long lastCommunTime; // ms

        public event ConnectionStateChange onConnectionState;
        public event DeviceListCallback onDeviceList;
        public event DevEventCallback onDevEvent;
        public event ActionRespCallback onActionResp;
        public IPAddress ip { get; private set; }
        public int port { get; set; }

        public float Version { get; private set; }

        public bool IsConnected => (tcpClient != null) && tcpClient.Connected;

        public TServerClient(IPAddress ipAddr, int iPort)
        {
            //inter_clock = new Stopwatch();
            //inter_clock.Start();
            //lastCommunTime = 0;

            ip = ipAddr;
            port = iPort;
        }

        public void Open()
        {
            //lastCommunTime = inter_clock.ElapsedMilliseconds;
            Version = 0;
            tcpClient = new TcpClient();
            tcpClient.Connect(ip, port);
            if (tcpClient.Connected)
            {
                StartReading(tcpClient.GetStream());
                string LocalHostName = Dns.GetHostName();
                SendMessage(new PrtclCmfJson.MsgLogin("Demo:", LocalHostName));
            }
            else
                onConnectionState?.Invoke(false);
        }

        public void StartReading(NetworkStream stream)
        {
            Task.Run(() =>
            {
                ByteArray array = new ByteArray();
                while (IsConnected)
                {
                    int length = stream.Read(rcv_data, 0, rcv_data.Length);
                    if (length == 0)
                        throw new Exception("Connection was closed.");
                    array.Append(rcv_data, length);
                    int i = array.IndexOfNewLine();
                    while (array.Length > 0)
                    {
                        if (i >= 0)
                        {
                            byte[] packet = array.SubArray(i + 2);
                            string message = Encoding.UTF8.GetString(packet);
                            PrtclCmfJson.Header header = JsonConvert.DeserializeObject<PrtclCmfJson.Header>(message);
                            if (header != null)
                            {
                                switch (header.cmd)
                                {
                                    case PrtclCmfJson.Command.login_resp:
                                        PrtclCmfJson.MsgLoginResp loginResp = JsonConvert.DeserializeObject<PrtclCmfJson.MsgLoginResp>(message);
                                        Version = loginResp.version;
                                        onConnectionState?.Invoke(true);
                                        break;
                                    case PrtclCmfJson.Command.ping:
                                        SendMessage(new PrtclCmfJson.Header(PrtclCmfJson.Command.ping_resp));
                                        break;
                                    case PrtclCmfJson.Command.device_list_resp:
                                        PrtclCmfJson.MsgDevlistResp devlistresp = JsonConvert.DeserializeObject<PrtclCmfJson.MsgDevlistResp>(message);
                                        onDeviceList?.Invoke(devlistresp);
                                        break;
                                    case PrtclCmfJson.Command.dev_event:
                                        PrtclCmfJson.MsgDevEvent devEvent = JsonConvert.DeserializeObject<PrtclCmfJson.MsgDevEvent>(message);
                                        onDevEvent?.Invoke(devEvent);
                                        break;
                                    case PrtclCmfJson.Command.action_resp:
                                        PrtclCmfJson.MsgActionResp actionResp = JsonConvert.DeserializeObject<PrtclCmfJson.MsgActionResp>(message);
                                        onActionResp?.Invoke(actionResp);
                                        break;
                                }
                            }
                            i = array.IndexOfNewLine();
                        }
                        else
                            array.Clear();
                    }
                }
            });
        }

        public bool SendMessage(PrtclCmfJson.Header message)
        {
            try
            {
                if (tcpClient.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(message, new JsonSerializerSettings()
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        }) + "\r\n");
                    return tcpClient.Client.Send(data) == data.Length;
                }
            }
            catch
            {
                Close();
            }
            return false;
        }

        public void Close()
        {
            if (IsConnected)
            {
                tcpClient.Close();
                onConnectionState?.Invoke(false);
            }
        }
    }
}
