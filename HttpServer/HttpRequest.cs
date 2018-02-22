using System.Collections.Generic;

namespace HttpServer
{
    class HttpRequest
    {
        public HttpMethod Method { get; set; }
        public string RequestUri { get; set; }

        public string Version { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
    }
}