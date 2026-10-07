using System.Net;
using System.Net.Sockets;

namespace Vagalume.Postgres.Embedded;

/// <summary>
/// <see cref="IFreePortFinder"/> that asks the operating system for an ephemeral loopback port.
/// </summary>
public sealed class LoopbackPortFinder : IFreePortFinder
{
    public int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
