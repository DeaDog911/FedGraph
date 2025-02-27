﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FedGraph.Client
{
    public class Vertex
    {
        public int id { get; set; }
        public string info { get; set; }
        public List<int> adj_vertices { get; set; }
    }
    public class Path
    {
        public Vertex vertex { get; set; }
        public int min_length { get; set; }
        public Path prev { get; set; }
    }
    class Server
    {
        public int id { get; set;}
        public string address { get; set;}
    }
    class Config
    {
        public List<Server> servers { get; set;}
    }
    class Parsing
    {
        public static Config parse(string filename="config.json")
        {
            Config config;
            using (StreamReader file = File.OpenText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                config = (Config)serializer.Deserialize(file, typeof(Config));
            }
            return config;
        }
    }
}
