using System;
using System.Net;
using System.Threading;

namespace SteamKit2
{
    partial class WebSocketConnection : IConnection
    {
        WebSocketContext currentContext;

        public event EventHandler<NetMsgEventArgs> NetMsgReceived;

        public event EventHandler Connected;

        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public EndPoint CurrentEndPoint { get; set; }
        public ProtocolTypes ProtocolTypes
        {
            get { return ProtocolTypes.WebSocket; }
        }

        public void Connect(EndPoint endPoint, int timeout = 5000)
        {
            var newContext = new WebSocketContext(this, endPoint);
            var oldContext = Interlocked.Exchange(ref currentContext, newContext);
            if (oldContext != null)
            {
                DebugLog.WriteLine(nameof(WebSocketConnection), "Attempted to connect while already connected. Closing old connection...");
                oldContext.Dispose();
                Disconnected?.Invoke(this, new DisconnectedEventArgs(false));
            }

            CurrentEndPoint = newContext.EndPoint;
            newContext.Start(TimeSpan.FromMilliseconds(timeout));
        }

        public void Disconnect(bool userInitiated)
        {
            DisconnectCore(userInitiated, specificContext: null);
        }

        public IPAddress GetLocalIP()
        {
            return IPAddress.None;
        }

        public void Send(byte[] data)
        {
            currentContext?.Send(data);
        }

        void DisconnectCore(bool userInitiated, WebSocketContext specificContext)
        {
            var oldContext = Interlocked.Exchange(ref currentContext, null);
            if (oldContext != null && (specificContext == null || oldContext == specificContext))
            {
                oldContext.Dispose();

                Disconnected?.Invoke(this, new DisconnectedEventArgs(userInitiated));
                CurrentEndPoint = null;
            }
            else
            {
                specificContext?.Dispose();
            }
        }
    }
}
