﻿using BeatTogether.LiteNetLib.Dispatchers.Abstractions;
using BeatTogether.LiteNetLib.Headers;
using Krypton.Buffers;
using System;
using System.Net;

namespace BeatTogether.LiteNetLib.Dispatchers
{
    public class UnconnectedDispatcher : IMessageDispatcher
    {
        private readonly LiteNetServer _server;

        public UnconnectedDispatcher(
            LiteNetServer server)
        {
            _server = server;
        }

        public void Send(EndPoint endPoint, ReadOnlySpan<byte> message)
        {
            var bufferWriter = new SpanBufferWriter(stackalloc byte[412]);
            new UnconnectedHeader().WriteTo(ref bufferWriter);
            bufferWriter.WriteBytes(message);
            _server.SendAsync(endPoint, bufferWriter);
        }
    }
}
