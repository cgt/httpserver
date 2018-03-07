using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HttpServer
{
    class ShutdownListener
    {
        public async Task StartAsync()
        {
            var listener = new TcpListener(IPAddress.Loopback, 8081);
            listener.Start();
            var client = await listener.AcceptTcpClientAsync();
            client.Close();
            listener.Stop();
            ShutdownRequested?.Invoke(this, new EventArgs());
        }

        public event EventHandler ShutdownRequested;
    }
}
