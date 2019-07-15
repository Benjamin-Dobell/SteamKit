﻿using System;
using System.Net;

namespace SteamKit2.Discovery
{
    /// <summary>
    /// Represents the information needed to connect to a CM server
    /// </summary>
    public class ServerRecord
    {
        internal ServerRecord(EndPoint endPoint, ProtocolTypes protocolTypes)
        {
            if (endPoint == null)
            {
                throw new ArgumentNullException(nameof(endPoint));
            }

            EndPoint = endPoint;
            ProtocolTypes = protocolTypes;
        }

        /// <summary>
        /// The endpoint of the server to connect to.
        /// </summary>
        public EndPoint EndPoint { get; }

        /// <summary>
        /// The various protocol types that can be used to communicate with this server.
        /// </summary>
        public ProtocolTypes ProtocolTypes { get; }

        /// <summary>
        /// Gets the host of the associated endpoint. This could be an IP address, or a DNS host name.
        /// </summary>
        /// <returns>The <see cref="IPAddress"/> of the associated endpoint.</returns>
        public string GetHost()
        {
            if (EndPoint is IPEndPoint)
            {
                IPEndPoint ipep = (IPEndPoint) EndPoint;
                return ipep.Address.ToString();
            }
            else if (EndPoint is DnsEndPoint)
            {
                DnsEndPoint dns = (DnsEndPoint) EndPoint;
                return dns.Host;
            }

            throw new InvalidOperationException("Unknown endpoint type.");
        }

        /// <summary>
        /// Gets the port number of the associated endpoint.
        /// </summary>
        /// <returns>The port numer of the associated endpoint.</returns>
        public int GetPort()
        {
            if (EndPoint is IPEndPoint)
            {
                IPEndPoint ipep = (IPEndPoint) EndPoint;
                return ipep.Port;
            }
            else if (EndPoint is DnsEndPoint)
            {
                DnsEndPoint dns = (DnsEndPoint) EndPoint;
                return dns.Port;
            }

            throw new InvalidOperationException("Unreachable code");
        }

        /// <summary>
        /// Creates a server record for a given endpoint.
        /// </summary>
        /// <param name="host">The host to connect to. This can be an IP address or a DNS name.</param>
        /// <param name="port">The port to connect to.</param>
        /// <param name="protocolTypes">The protocol types that this server supports.</param>
        /// <returns></returns>
        public static ServerRecord CreateServer(string host, int port, ProtocolTypes protocolTypes)
        {
            IPAddress address;
            if (IPAddress.TryParse(host, out address))
            {
                return new ServerRecord(new IPEndPoint(address, port), protocolTypes);
            }

            return new ServerRecord(new DnsEndPoint(host, port), protocolTypes);
        }

        /// <summary>
        /// Creates a Socket server given an IP endpoint.
        /// </summary>
        /// <param name="endPoint">The IP address and port of the server.</param>
        /// <returns>A new <see cref="ServerRecord"/> instance</returns>
        public static ServerRecord CreateSocketServer(IPEndPoint endPoint)
        {
            return new ServerRecord(endPoint, ProtocolTypes.Tcp | ProtocolTypes.Udp);
        }

        /// <summary>
        /// Creates a Socket server given an IP endpoint.
        /// </summary>
        /// <param name="address">The IP address and port of the server, as a string.</param>
        /// <param name="serverRecord">A new <see cref="ServerRecord"/>, if the address was able to be parsed. <c>null</c> otherwise.</param>
        /// <returns><c>true</c> if the address was able to be parsed, <c>false</c> otherwise.</returns>
        public static bool TryCreateSocketServer(string address, out ServerRecord serverRecord)
        {
            IPEndPoint endPoint;
            if (!NetHelpers.TryParseIPEndPoint(address, out endPoint))
            {
                serverRecord = default(ServerRecord);
                return false;
            }

            serverRecord = new ServerRecord(endPoint, ProtocolTypes.Tcp | ProtocolTypes.Udp);
            return true;
        }

        /// <summary>
        /// Creates a WebSocket server given an address in the form of "hostname:port".
        /// </summary>
        /// <param name="address">The name and port of the server</param>
        /// <returns>A new <see cref="ServerRecord"/> instance</returns>
        public static ServerRecord CreateWebSocketServer(string address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            EndPoint endPoint;
            const int DefaultPort = 443;

            var indexOfColon = address.IndexOf(':');
            if (indexOfColon >= 0)
            {
                var hostname = address.Substring(0, indexOfColon);
                var portNumber = address.Substring(indexOfColon + 1);

                int port;
                if (!int.TryParse(portNumber, out port))
                {
                    throw new ArgumentException("Port number must be a valid integer value.", nameof(address));
                }

                endPoint = new DnsEndPoint(hostname, port);
            }
            else
            {
                endPoint = new DnsEndPoint(address, DefaultPort);
            }

            return new ServerRecord(endPoint, ProtocolTypes.WebSocket);
        }

        #region Equality and Hashing

        /// <summary>
        /// Determines whether two objects are equal.
        /// </summary>
        /// <param name="left">The object on the left-hand side of the equality operator.</param>
        /// <param name="right">The object on the right-hand side of the equality operator.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public static bool operator ==(ServerRecord left, ServerRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return !ReferenceEquals(left, null) && left.Equals(right);
        }

        /// <summary>
        /// Determines whether two objects are not equal.
        /// </summary>
        /// <param name="left">The object on the left-hand side of the inequality operator.</param>
        /// <param name="right">The object on the right-hand side of the inequality operator.</param>
        /// <returns>true if the specified object is not equal to the current object; otherwise, false.</returns>
        public static bool operator !=(ServerRecord left, ServerRecord right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ServerRecord)
            {
                ServerRecord other = (ServerRecord) obj;
                return EndPoint.Equals(other.EndPoint) && ProtocolTypes == other.ProtocolTypes;
            }

            return false;
        }

        /// <summary>
        /// Hash function
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return EndPoint.GetHashCode() ^ ProtocolTypes.GetHashCode();
        }

        #endregion
    }
}
