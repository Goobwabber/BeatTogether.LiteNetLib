using BeatTogether.LNLTest.Utilities;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BeatTogether.LNLTest;

public class ClientConnection : IDisposable
{
    private NetManager _clientManager;
    private ILogger<ClientConnection> _logger;
    private byte[] _garbage; 
    
    public bool IsConnected => _clientManager.ConnectedPeersCount == 1;

    public ClientConnection(ServiceProvider provider, byte[] garbage)
    {
        _logger = provider.GetRequiredService<ILogger<ClientConnection>>();
        _garbage = garbage;
        
        var listener = new EventBasedNetListener();
        _clientManager = new NetManager(listener);
        _clientManager.Start();
        
        listener.NetworkErrorEvent += ((point, error) =>
        {
            _logger.LogInformation("Network error event: " + error);
        });
        listener.PeerConnectedEvent += ((peer) => _logger.LogInformation("Connected to peer"));
        listener.PeerDisconnectedEvent += ((peer, info) =>
        {
            _logger.LogInformation("Disconnected from to peer: ", info.SocketErrorCode);
        });
    }

    public void Connect()
    {
        _clientManager.Connect("127.0.0.1", TestServer._Port, "");
    }

    public void Disconnect()
    {
        _clientManager.DisconnectAll();
    }

    public void PollEvents()
    {
        _clientManager.PollEvents();
    }
    

    public void SendGarbage(DeliveryMethod deliveryMethod)
    {
        _clientManager.SendToAll(_garbage, deliveryMethod);
    }

    public void Dispose()
    {
        Disconnect();
        _clientManager.Stop();
    }
}