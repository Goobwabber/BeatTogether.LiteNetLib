using BeatTogether.LiteNetLib.Abstractions;
using BeatTogether.LiteNetLib.Enums;
using Krypton.Buffers;
using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Tests.Utilities
{
    public class ListenerService : ILiteNetListener
    {
        public event Action<EndPoint, byte[], DeliveryMethod> ReceiveConnectedEvent;
        public event Action<EndPoint, byte[], UnconnectedMessageType> ReceiveUnconnectedEvent;
        public event Action<EndPoint> ConnectedEvent;
        public event Action<EndPoint, DisconnectReason, byte[]> DisconnectedEvent;

        private readonly ILogger _logger;
        private readonly LiteNetReliableDispatcher _reliableDispatcher;

        public ListenerService(
            ILogger<ListenerService> logger,
            LiteNetReliableDispatcher reliableDispatcher)
        {
            _logger = logger;
            _reliableDispatcher = reliableDispatcher;
        }

        public void OnNetworkError(EndPoint endPoint, Exception ex)
        {
            throw new NotImplementedException();
        }

        public void OnNetworkLatencyUpdate(EndPoint peer, int latency)
        {
            throw new NotImplementedException();
        }

        public void OnNetworkReceive(EndPoint peer, ref SpanBufferReader reader, DeliveryMethod deliveryMethod)
        {
            _logger.LogInformation($"Received connected '{deliveryMethod}' message from {peer}");
            ReceiveConnectedEvent?.Invoke(peer, reader.RemainingData.ToArray(), deliveryMethod);
        }

        public void OnNetworkReceiveUnconnected(EndPoint remoteEndPoint, ref SpanBufferReader reader, UnconnectedMessageType messageType)
        {
            _logger.LogInformation($"Received unconnected '{messageType}' message from {remoteEndPoint}");
            ReceiveUnconnectedEvent?.Invoke(remoteEndPoint, reader.RemainingData.ToArray(), messageType);
        }
        
        public void OnPeerConnected(EndPoint peer)
        {
            _logger.LogInformation($"Client connected from {peer}");
            ConnectedEvent?.Invoke(peer);
        }

        public void OnPeerDisconnected(EndPoint peer, DisconnectReason reason, ref SpanBufferReader additionalData)
        {
            _logger.LogInformation($"Client disconnected from {peer}");
            DisconnectedEvent?.Invoke(peer, reason, additionalData.RemainingData.ToArray());
        }

        public bool ShouldAcceptConnectionRequest(EndPoint remoteEndPoint, ref SpanBufferReader additionalData)
        {
            _logger.LogInformation($"Received connection request from {remoteEndPoint}");
            return true;
        }
    }
}
