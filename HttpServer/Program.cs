﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HttpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new HttpServer(@"C:\Users\Chris\Documents\public");
            server.StartAsync().Wait();
        }
    }
}
