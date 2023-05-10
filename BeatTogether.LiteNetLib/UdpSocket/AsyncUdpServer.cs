using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncUdp
{
    public class AsyncUdpServer : IDisposable
    {
        public string Address { get; private set; }
        public int Port => IPEndpoint.Port;
        public IPEndPoint IPEndpoint { get; private set; }
        public EndPoint Endpoint { get; private set; }

        private byte[] _RecvBuffer;
        private int _MaxPacketRecvSize;
        private int _ConcurrentRecv;

        private CancellationTokenSource ServerStopped;
        public bool IsStarted { get; private set; }
        Socket _Socket;
        EndPoint _receiveEndpoint;


        /// <summary>
        /// Async Udp server
        /// </summary>
        /// <remarks>
        /// If receiveAsync is set to true, then the server will start recieving the next packet while the current one is still being handled
        /// </remarks>
        public AsyncUdpServer(IPEndPoint endpoint, int concurrentRecv, int BufferSize)
        {
            Address = endpoint.Address.ToString();
            Endpoint = endpoint;
            IPEndpoint = endpoint;
            _ConcurrentRecv = concurrentRecv;
            _MaxPacketRecvSize = BufferSize;
            _RecvBuffer = GC.AllocateArray<byte>(BufferSize * concurrentRecv, pinned: true);
        }

        /// <summary>
        /// Create a new socket object
        /// </summary>
        /// <remarks>
        /// Method may be override if you need to prepare some specific socket object in your implementation.
        /// </remarks>
        /// <returns>Socket object</returns>
        protected virtual Socket CreateSocket()
        {
            return new Socket(Endpoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        }

        /// <summary>
        /// Start the server (synchronous)
        /// </summary>
        /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
        public virtual bool Start()
        {
            ServerStopped = new();
            if (IsStarted)
                return false;
            // Setup event args

            // Create a new server socket
            _Socket = CreateSocket();

            // Update the server socket disposed flag
            IsSocketDisposed = false;

            // Apply the option: dual mode (this option must be applied before recieving)
            if (_Socket.AddressFamily == AddressFamily.InterNetworkV6)
                _Socket.DualMode = true;

            // Bind the server socket to the endpoint
            _Socket.Bind(Endpoint);
            // Refresh the endpoint property based on the actual endpoint created
            Endpoint = _Socket.LocalEndPoint!;

            // Call the server starting handler
            OnStarting();

            // Prepare receive endpoint
            _receiveEndpoint = new IPEndPoint((Endpoint.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0);

            // Update the started flag
            IsStarted = true;

            StartReceiveAsync();
            // Call the server started handler
            OnStarted();

            return true;
        }

        /// <summary>
        /// Stop the server (synchronous)
        /// </summary>
        /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
        public virtual bool Stop()
        {
            if (!IsStarted)
                return false;

            // Call the server stopping handler
            OnStopping();
            // Update the started flag
            IsStarted = false;
            ServerStopped.Cancel();
            try
            {
                // Close the server socket
                _Socket.Close();

                // Dispose the server socket
                _Socket.Dispose();

                // Update the server socket disposed flag
                IsSocketDisposed = true;
            }
            catch (ObjectDisposedException) { }

            // Call the server stopped handler
            OnStopped();

            return true;
        }

        private void StartReceiveAsync()
        {
            // Try to receive datagram
            for (int i = 0; i < _ConcurrentRecv; i++) //TODO Allow LNL to increase/decrease the amount of concurrent recievers after server start
            {
                int IndexStart = i;
                Task.Run(() => Recieve(IndexStart));
            }
        }

        private async void Recieve(int BufferId)
        {
            EndPoint RecvEndpoint = _receiveEndpoint;
            Memory<byte> RecvBuffer = _RecvBuffer.AsMemory().Slice(BufferId * _MaxPacketRecvSize, _MaxPacketRecvSize);
            SocketReceiveFromResult recvResult;
            while (IsStarted)
            {
                try
                {
                    recvResult = await _Socket.ReceiveFromAsync(RecvBuffer, SocketFlags.None, RecvEndpoint, ServerStopped.Token);
                }
                catch (SocketException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception) { return; }

                var recvPacket = RecvBuffer[..recvResult.ReceivedBytes];
                OnReceived(recvResult.RemoteEndPoint, recvPacket);
            }
        }

        /// <summary>
        /// Send datagram to the given endpoint (asynchronous)
        /// Recommended you manage max sends at once if you are not calling this synchronously to avoid a stack overflow exeption from the socket
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        public async virtual Task SendAsync(EndPoint endpoint, Memory<byte> buffer)
        {
            try
            {
                await _Socket.SendToAsync(buffer, SocketFlags.None, endpoint, ServerStopped.Token);
            }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception) { return; }
        }

        /// <summary>
        /// Send datagram to the given endpoint (Synchronous)
        /// </summary>
        /// <param name="endpoint">Endpoint to send</param>
        /// <param name="buffer">Datagram buffer to send</param>
        public virtual void SendSerial(EndPoint endpoint, Span<byte> buffer)
        {
            try
            {
                _Socket.SendTo(buffer, endpoint);
            }
            catch (SocketException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception) { return; }
        }

        #region Datagram handlers / Override-able methords

        /// <summary>
        /// Handle server starting notification
        /// </summary>
        protected virtual void OnStarting() { }
        /// <summary>
        /// Handle server started notification
        /// </summary>
        protected virtual void OnStarted() { }
        /// <summary>
        /// Handle server stopping notification
        /// </summary>
        protected virtual void OnStopping() { }
        /// <summary>
        /// Handle server stopped notification
        /// </summary>
        protected virtual void OnStopped() { }

        /// <summary>
        /// Handle datagram received notification
        /// </summary>
        /// <param name="endpoint">Received endpoint</param>
        /// <param name="buffer">Received datagram buffer</param>
        /// <remarks>
        /// Notification is called when another datagram was received from some endpoint
        /// </remarks>
        protected virtual void OnReceived(EndPoint endpoint, Memory<byte> buffer) { }

        #endregion

        #region IDisposable implementation

        /// <summary>
        /// Disposed flag
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Server socket disposed flag
        /// </summary>
        public bool IsSocketDisposed { get; private set; } = true;

        // Implement IDisposable.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposingManagedResources)
        {
            // The idea here is that Dispose(Boolean) knows whether it is
            // being called to do explicit cleanup (the Boolean is true)
            // versus being called due to a garbage collection (the Boolean
            // is false). This distinction is useful because, when being
            // disposed explicitly, the Dispose(Boolean) method can safely
            // execute code using reference type fields that refer to other
            // objects knowing for sure that these other objects have not been
            // finalized or disposed of yet. When the Boolean is false,
            // the Dispose(Boolean) method should not execute code that
            // refer to reference type fields because those objects may
            // have already been finalized."

            if (!IsDisposed)
            {
                if (disposingManagedResources)
                {
                    // Dispose managed resources here...
                    Stop();
                }

                // Dispose unmanaged resources here...

                // Set large fields to null here...

                // Mark as disposed.
                IsDisposed = true;
            }
        }

        #endregion
    }
}