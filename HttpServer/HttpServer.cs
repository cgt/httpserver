﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpServer
{
    class HttpServer
    {
        private readonly string _rootPath;

        public HttpServer(string rootPath)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        }

        private int _clientCount;

        public async Task StartAsync(IPAddress addr = null, int port = 8080)
        {
            addr = addr ?? IPAddress.Any;

            var shutdownRequested = new TaskCompletionSource<bool>();
            var shutdownListener = new ShutdownListener();
            shutdownListener.ShutdownRequested += (s, e) => shutdownRequested.SetResult(true);
            _ = shutdownListener.StartAsync()
                .ContinueWith(t => Console.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);

            var listener = new TcpListener(addr, port);
            listener.Start();

            while (true)
            {
                var clientTask = listener.AcceptTcpClientAsync();
                var task = await Task.WhenAny(clientTask, shutdownRequested.Task);
                if (task == clientTask)
                {
                    _ = HandleClient(clientTask.Result) // assign to _ to suppress warning about not awaiting task
                    .ContinueWith(t => Console.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
                }
                else if (task == shutdownRequested.Task)
                {
                    listener.Stop(); // Stop accepting new clients

                    if (_clientCount < 0) throw new InvalidOperationException($"{nameof(_clientCount)} less than 0");

                    while (_clientCount > 0)
                    {
                        await Task.Delay(100);
                    }

                    if (_clientCount < 0) throw new InvalidOperationException($"{nameof(_clientCount)} less than 0");

                    Console.WriteLine("Graceful shutdown");
                    return;
                }
                else
                {
                    throw new InvalidOperationException();
                }
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
                Interlocked.Increment(ref _clientCount);

                using (client)
                using (var stream = client.GetStream())
                using (var r = new StreamReader(stream))
                using (var w = new StreamWriter(stream))
                {
                    var request = await ReadHttpRequestAsync(r);
                    Console.WriteLine("Method: {0} :: URI: {1} :: Version: {2}", request.Method, request.RequestUri, request.Version);

                    var filePath = Path.Combine(_rootPath, request.RequestUri.TrimStart('/')); // DANGEROUS
                    Console.WriteLine(filePath);
                    try
                    {
                        using (var f = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            await w.WriteAsync("HTTP/1.0 200 OK\r\n");
                            await w.WriteAsync("Content-Type: text/plain\r\n");
                            await w.WriteAsync($"Content-Length: {f.Length}\r\n");
                            await w.WriteAsync("\r\n");
                            await w.FlushAsync();
                            await f.CopyToAsync(stream);
                            await stream.FlushAsync();
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        await WriteSimpleResponse(w, 404, "Not Found");
                    }
                    catch
                    {
                        await WriteSimpleResponse(w, 500, "Internal Server Error");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClientHandler exception: {ex.Message}");
            }
            finally
            {
                Interlocked.Decrement(ref _clientCount);
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

        private async Task WriteSimpleResponse(StreamWriter w, int status, string msg)
        {
            if (w == null)
            {
                throw new ArgumentNullException(nameof(w));
            }
            if (msg == null)
            {
                throw new ArgumentNullException(nameof(msg));
            }

            var body = $"{status} {msg}\r\n";
            await w.WriteAsync($"HTTP/1.0 {status} {msg}\r\n");
            await w.WriteAsync("Content-Type: text/plain\r\n");
            await w.WriteAsync($"Content-Length: {body.Length}\r\n");
            await w.WriteAsync("\r\n");
            await w.WriteAsync(body);
            await w.WriteAsync("\r\n");
            await w.FlushAsync();
        }
    }
}
