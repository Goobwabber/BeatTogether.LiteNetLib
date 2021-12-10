using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Abstractions
{
    public interface ILiteNetListener
    {
        /// <summary>
        /// New remote peer connected to host, or client connected to remote host
        /// </summary>
        /// <param name="peer">Connected peer object</param>
        void OnPeerConnected(EndPoint peer);

        /// <summary>
        /// Peer disconnected
        /// </summary>
        /// <param name="peer">disconnected peer</param>
        /// <param name="reason">additional info about reason</param>
        /// <param name="additionalData">additional data that can be accessed (only if reason is RemoteConnectionClose)</param>
        void OnPeerDisconnected(EndPoint peer, DisconnectReason reason, ref SpanBufferReader additionalData);

        /// <summary>
        /// Network error (on send or receive)
        /// </summary>
        /// <param name="endPoint">From endPoint (can be null)</param>
        /// <param name="ex">Network exception</param>
        void OnNetworkError(EndPoint endPoint, Exception ex);

        /// <summary>
        /// Received some data
        /// </summary>
        /// <param name="peer">From peer</param>
        /// <param name="reader">DataReader containing all received data</param>
        /// <param name="deliveryMethod">Type of received packet</param>
        void OnNetworkReceive(EndPoint peer, ref SpanBufferReader reader, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Received unconnected message
        /// </summary>
        /// <param name="remoteEndPoint">From address (IP and Port)</param>
        /// <param name="reader">Message data</param>
        /// <param name="messageType">Message type (simple, discovery request or response)</param>
        void OnNetworkReceiveUnconnected(EndPoint remoteEndPoint, ref SpanBufferReader reader, UnconnectedMessageType messageType);

        /// <summary>
        /// Latency information updated
        /// </summary>
        /// <param name="peer">Peer with updated latency</param>
        /// <param name="latency">latency value in milliseconds</param>
        void OnNetworkLatencyUpdate(EndPoint peer, int latency);

        /// <summary>
        /// On peer connection requested
        /// </summary>
        /// <param name="remoteEndPoint">from address (IP and Port)</param>
        /// <param name="additionalData">additional data sent from remote</param>
        bool ShouldAcceptConnectionRequest(EndPoint remoteEndPoint, ref SpanBufferReader additionalData);
    }
}
