using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WebSocket4Net;

namespace SteamKit2
{
    partial class WebSocketConnection : IConnection
    {
        class WebSocketContext : IDisposable
        {
            public WebSocketContext(WebSocketConnection connection, EndPoint endPoint)
            {
                this.connection = connection;
                EndPoint = endPoint;
                cts = new CancellationTokenSource();
                hostAndPort = GetHostAndPort(endPoint);

                var uri = new Uri(FormattableString.Invariant($"wss://{hostAndPort}/cmsocket/"));

                socket = new WebSocket(uri.ToString());
                socket.DataReceived += OnDataReceived;
                socket.MessageReceived += OnMessageReceived;
                socket.Opened += OnOpenHandler;
                socket.Closed += OnCloseHandler;
                socket.Error += OnError;
            }

            readonly WebSocketConnection connection;
            readonly CancellationTokenSource cts;
            readonly WebSocket socket;
            readonly string hostAndPort;
            int disposed;


            public EndPoint EndPoint { get; }

            public void Start(TimeSpan connectionTimeout)
            {
                socket.Open();
            }
            private void OnOpenHandler(object sender, EventArgs e)
            {
                OpenWriteCompletedEventArgs eventArgs = (OpenWriteCompletedEventArgs) e;

                if (eventArgs.Error != null)
                {
                    Console.Error.WriteLine("Failed to open web socket: " + eventArgs.Error);
                }
                else
                {
                    connection.Connected?.Invoke(connection, EventArgs.Empty);
                }
            }

            private void OnDataReceived(object sender, DataReceivedEventArgs e)
            {
                Console.WriteLine("OnMessageHandler | " + e.Data);
                connection.NetMsgReceived?.Invoke(connection, new NetMsgEventArgs(e.Data, EndPoint));
            }

            private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
            {
                Console.WriteLine("OnMessageHandler | " + e.Message);
                connection.NetMsgReceived?.Invoke(connection, new NetMsgEventArgs(System.Text.Encoding.UTF8.GetBytes(e.Message), EndPoint));
            }

            private void OnCloseHandler(object sender, EventArgs e)
            {
                ClosedEventArgs closedEventArgs = (ClosedEventArgs) e;
                Console.WriteLine("OnCloseHandler | code: " + closedEventArgs.Code + " reason: " + closedEventArgs.Reason);
                connection.DisconnectCore(userInitiated: false, specificContext: this);
            }

            private void OnError(object sender, EventArgs e)
            {
                ErrorEventArgs errorEventArgs = (ErrorEventArgs) e;
                Console.Error.WriteLine(errorEventArgs.GetException());
            }

            public void Send(byte[] data)
            {
                socket.Send(data, 0, data.Length);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 1)
                {
                    return;
                }

                cts.Cancel();
                cts.Dispose();

                socket.Dispose();
            }

            static string GetHostAndPort(EndPoint endPoint)
            {
                if (endPoint is IPEndPoint)
                {
                    IPEndPoint ipep = (IPEndPoint) endPoint;

                    switch (ipep.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            return FormattableString.Invariant($"{ipep.Address}:{ipep.Port}");

                        case AddressFamily.InterNetworkV6:
                            // RFC 2732
                            return FormattableString.Invariant($"[{ipep.ToString()}]:{ipep.Port}");
                    }

                }
                else if (endPoint is DnsEndPoint)
                {
                    DnsEndPoint dns = (DnsEndPoint) endPoint;
                    return FormattableString.Invariant($"{dns.Host}:{dns.Port}");
                }

                throw new InvalidOperationException("Unsupported endpoint type.");
            }
        }
    }
}
