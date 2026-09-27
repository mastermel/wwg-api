using System.Net;
using System.Net.Sockets;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.IntegrationTests;

// The healthy path (exit 0 against a running container) is checked on the built image; TestServer
// doesn't listen on a real port for the command to call.
public sealed class HealthCheckCommandTests
{
    [Fact]
    public async Task Run_NothingListening_ReturnsUnhealthy()
    {
        // A port that was free a moment ago: nothing is listening on it.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var exitCode = await HealthCheckCommand.RunAsync(new Uri($"http://localhost:{port}"));

        Assert.Equal(1, exitCode);
    }
}
