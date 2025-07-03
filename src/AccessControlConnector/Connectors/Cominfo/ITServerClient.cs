using System;
using System.Net;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    public interface ITServerClient
    {
        bool IsConnected { get; }

        void Open();
        bool SendMessage(PrtclCmfJson.Header message);
        void Close();
    }
} 