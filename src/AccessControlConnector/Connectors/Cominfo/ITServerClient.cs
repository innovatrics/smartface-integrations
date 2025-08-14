using System;
using System.Net;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public delegate void ConnectionStateChange(bool connected);
    public delegate void DeviceListCallback(PrtclCmfJson.MsgDevlistResp devList);
    public delegate void DevEventCallback(PrtclCmfJson.MsgDevEvent devEvent);
    public delegate void ActionRespCallback(PrtclCmfJson.MsgActionResp actionResp);


    public interface ITServerClient
    {
        bool IsConnected { get; }

        void Open();
        bool SendMessage(PrtclCmfJson.Header message);
        void Close();
    }
}