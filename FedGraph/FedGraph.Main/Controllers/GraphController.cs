﻿namespace FedGraph.Main
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Cryptography.X509Certificates;

    [Route("api/[controller]")]
    [ApiController]
    public class GraphController : ControllerBase
    {
        IHttpClientFactory clientFactory;
        public GraphController(IHttpClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }
        // Возвращает пару [общее количество вершин, количество смежных вершин]
        [HttpGet("vertexes")]
        public JsonResult GetVertexesNum()
        {
            List<int> arr = new List<int>();
            arr.Add(Application.graph.getVertexesNum());
            arr.Add(Application.graph.getAdjVertexes().Count());
            return new JsonResult(arr);
        }
        // Проверяем, есть ли в графе вершина с таким id
        [HttpGet("contains/{id}")]
        public JsonResult GetContainsVertex(int id)
        {
            if (Application.graph.containsVertex(id))
            {
                Console.WriteLine($"Server contains vertex: {id}");
                return new JsonResult(true);
            }
            Console.WriteLine($"Server don't contains vertex: {id}");
            return new JsonResult(false);
        }
        // Запускаем алгоритм Дейкстры с вершины с таким id
        [HttpGet("search/start/{id}")]
        public void GetStartSearch(int id)
        {
            if (id != null)
            {
                Application.graph.setStartVertexId(id);
                var client = clientFactory.CreateClient();
                Application.graph.dijksra(client);
            }
        }
        // Возвращает кратчайший путь до вершины с id
        [HttpGet("search/end/{id}")]
        public JsonResult GetEndSearch(int id)
        {
            if (Application.graph.containsVertex(id))
            {
                Application.graph.setEndVertexId(id);
                List<Path> path = Application.graph.getShortestPath();
                return new JsonResult(path);
            }
            return new JsonResult(null);
        }
        [HttpGet("search/progress")]
        public JsonResult GetSearchInProgress()
        {
            return new JsonResult(Application.graph.isInProgress());
        }
        [HttpGet("reset")]
        public void GetResetGraph()
        {
            Application.graph.reset();
        }
        // Запускает алгориртм Дейкстры с граничащей вершины вместе с Path, полученным с другого сервера 
        [HttpPost("search/dijkstra")]
        public JsonResult PostTransitionToAdjVertex([FromBody] Path path)
        {   
            /*
            if (path.prev != null)
                Console.WriteLine($"Recieved path: {path.vertex.id} len: {path.min_length} prev: {path.prev.vertex.id}");
            else
                Console.WriteLine($"Recieved path: {path.vertex.id} len: {path.min_length} prev: null");*/
            var client = clientFactory.CreateClient();
            Application.graph.dijksra(client, path);
            return new JsonResult(true);
        }
    }
}
