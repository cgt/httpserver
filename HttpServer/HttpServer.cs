using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HttpServer
{
    class HttpServer
    {
        public async Task StartAsync(IPAddress addr = null, int port = 8080)
        {
            addr = addr ?? IPAddress.Any;

            var listener = new TcpListener(addr, port);
            listener.Start();

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClient(client); // ignore warning, let it run async
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var r = new StreamReader(stream))
                using (var w = new StreamWriter(stream))
                {
                    var request = await ReadHttpRequestAsync(r);
                    Console.WriteLine("Method: {0} :: URI: {1} :: Version: {2}", request.Method, request.RequestUri, request.Version);

                    await w.WriteAsync("HTTP/1.0 200 OK\r\n");

                    if (request.Headers.Count > 0)
                    {
                        var body = new StringBuilder();
                        body.Append("Headers:\r\n");
                        foreach (var hdr in request.Headers)
                        {
                            body.Append($"{hdr.Key}:\t{hdr.Value}\r\n");
                        }

                        await w.WriteAsync("Content-Type: text/plain\r\n");
                        await w.WriteAsync($"Content-Length: {body.Length}\r\n");
                        await w.WriteAsync("\r\n");
                        await w.WriteAsync(body.ToString());
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }

                    await w.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClientHandler exception: {ex.Message}");
            }
        }

        private async Task<HttpRequest> ReadHttpRequestAsync(TextReader r)
        {
            var reqline = await r.ReadLineAsync();
            if (string.IsNullOrEmpty(reqline))
            {
                throw new InvalidDataException("no data");
            }
            var split = reqline.Split(); // example: ["GET", "/index.html", "HTTP/1.0"]
            if (split.Length != 3)
            {
                throw new InvalidDataException();
            }

            if (!Enum.TryParse(split[0], out HttpMethod method))
            {
                throw new InvalidDataException($"unknown HTTP method: {split[0]}");
            }

            var req = new HttpRequest
            {
                Method = method,
                RequestUri = split[1],
                Version = split[2],
            };

            // Read headers
            for (string line = await r.ReadLineAsync(); !string.IsNullOrWhiteSpace(line); line = await r.ReadLineAsync())
            {
                var separator = line.IndexOf(':');
                var name = line.Substring(0, separator);
                var value = line.Substring(separator + 1);
                req.Headers.Add(name, value);
            }

            return req;
        }
    }
}
