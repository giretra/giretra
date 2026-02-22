using System.Net;
using System.Net.Sockets;

namespace Giretra.Core.Players.Agents.Remote;

/// <summary>
/// Allocates ephemeral ports by briefly binding to port 0.
/// </summary>
public static class PortAllocator
{
    public static int AllocateFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
