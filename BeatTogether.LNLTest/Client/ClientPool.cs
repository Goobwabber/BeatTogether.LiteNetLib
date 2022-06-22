using BeatTogether.LNLTest.Utilities;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;

namespace BeatTogether.LNLTest;

public class ClientPool
{
    private ServiceProvider _provider;
    
    private List<ClientConnection> _connections = new();
    private byte[] _garbage = PacketWriter.GetBullshitToSend();

    public bool IsAllSettled => _connections.Find(conn => !conn.IsConnected) == null;

    public ClientPool(ServiceProvider provider)
    {
        _provider = provider;
    }

    public void PollEvents()
    {
        foreach (var connection in _connections)
        {
            connection.PollEvents();
        }
    }

    public void Connect()
    {
        var conn = new ClientConnection(_provider, _garbage);
        conn.Connect();
        _connections.Add(conn);
    }

    public bool DisconnectOne()
    {
        if (_connections.Count == 0)
        {
            return false;
        }
        
        ClientConnection connection = _connections[0];
        _connections.Remove(connection);
        
        connection.Disconnect();

        return true;
    }

    public void DisconnectAll()
    {
        foreach (var connection in _connections)
        {
            connection.Disconnect();
        }
        
        _connections.Clear();
    }

    public void SendGarbage(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        foreach (var connection in _connections)
        {
            connection.SendGarbage(deliveryMethod);
        }
    }
}