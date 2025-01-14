﻿using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace FedGraph.Main
{
    public class Graph
    {
        private int vertexesNum;
        private int[,] matrix; // Матрица смежности
        private List<int> visited; // Посещённые вершины
        private Dictionary<int, Path> pathes; // Словарь key: id, value: Path - путь до вершины
        private List<Vertex> adj_vertexes; // граничащие вершины
        private List<Vertex> vertexes;

        private List<CServer> servers; // Список из сереверов

        private int startVertexId;
        private int endVertexId;

        private bool inProgress;
        private int[] vertexesIds; // проиндексированные айдишники вершин

        public Graph(Config config)
        {
            this.vertexesNum = config.vertexes.Count();
            this.matrix = new int[vertexesNum, vertexesNum];
            this.visited = new List<int>();
            this.pathes = new Dictionary<int, Path>();
            this.adj_vertexes = new List<Vertex>();
            this.vertexes = config.vertexes;
            this.startVertexId = -1;
            this.endVertexId = -1;
            this.vertexesIds = new int[vertexesNum];
            this.servers = config.servers;
            Console.WriteLine("vertexesNum: " + vertexesNum);
                
            for (int i = 0; i < vertexesNum; i++)
            {
                if (config.vertexes[i].adj_vertices.Count() > 0) 
                {
                    // Заполняем список граничащих вершин
                    adj_vertexes.Add(config.vertexes[i]);
                }
                // Заполняем массив id вершин, нужный для более простого доступа к ним
                vertexesIds[i] = config.vertexes[i].id;
            }
            // Создаём матрицу смежности по спискам смежности из конфига
            fillMatrix(config);
        }
        public int getEdgeWeight(int v1, int v2)
        {
            return this.matrix[v1, v2];
        }

        public List<Vertex> getAdjVertexes()
        {
            return adj_vertexes;
        }

        public void setStartVertexId(int id)
        {
            startVertexId = id;
        }

        public void setEndVertexId(int id)
        {
            endVertexId = id;
        }

        public bool containsVertex(int id)
        {
            if (vertexesIds.Contains(id)) return true;
            return false;
        }
        private Vertex? getVertexWithId(int id)
        {
            Vertex vertex = null;
            foreach (Vertex v in vertexes)
            {
                if (v.id == id)
                        { vertex = v; }
            }
            return vertex;

        }
        // Заполняет матрицу смежности из конфига
        private void fillMatrix(Config config)
        {
            for (int i = 0; i < vertexesNum; i++)
            {
                for (int j = 0; j < vertexesNum; j++)
                {
                    if (i != j)
                        matrix[i, j] = -1;
                    else
                        matrix[i,j] = 0;
                }
            }
            for (int i = 0; i < vertexesNum; i++)
            {
                for (int j = 0; j < config.adj_list[i].edges.Count(); j++)
                {
                    int v_id = getVertexNum(config.adj_list[i].edges[j].id);
                    matrix[i, v_id] = config.adj_list[i].edges[j].weight;
                }
            }
        }

        private int getVertexIdWithMinLength()
        {
            int minLength = int.MaxValue;
            int id = -1;
            foreach (KeyValuePair<int, Path> p in pathes)
            {
                try
                {
                    if (p.Value.getMinLength() < minLength
                        && !visited.Contains(p.Key))
                    {
                        minLength = p.Value.getMinLength();
                        id = p.Key;
                    }
                }
                catch (System.NullReferenceException e) { }
            }
            return id; 
        }
        // Получить номер вершины
        private int getVertexNum(int id)
        {
            int mId;
            for (mId = 0; mId < vertexesNum; mId++)
                if (vertexesIds[mId] == id)
                    break;
            return mId;
        }
        // Получить количество вершин
        public int getVertexesNum()
        {
            return this.vertexesNum;
        }

        public async void dijksra(HttpClient client, Path recievedPath=null)
        {
            inProgress = true; // Нужна для отслеживания окончания работы алгоритма
            int startId = -1;
            Path startPath;
            // Если путь начинаетя с граничащей, но не начальной вершины
            if (recievedPath != null)
            {
                startId = recievedPath.vertex.id;
                startPath = recievedPath;
            }
            // Если поиск начинается с начальной вершины
            else
            {
                startId = startVertexId;
                startPath = new Path(getVertexWithId(startId), 0, null);
            }
            Console.WriteLine($"Start {startId}");
            // Если вершина проходится во второй раз (для граничащих вершин)
            if (pathes.ContainsKey(startId))
            {
                try
                {
                    // Если путь до граничащей вершины изменился при получении пути с другого графа, то массив помеченных вершин очищается,
                    // чтобы алгоритм сработал ещё раз и перезаписал пути для вершин с уменьшенной длиной
                    if (pathes[startId].getMinLength() > recievedPath.getMinLength())
                    {
                        pathes[startId] = recievedPath;
                        visited.Clear();
                    }
                }
                catch (System.NullReferenceException e) { }
                //путь до граничащей вершины изменился, поэтому нужно пересмотреть все смежные с ней вершины
            }
            else
            {
                // Если алгоритм начинается с этой вершины впервые 
                pathes.Add(startId, startPath);
            }

            if (startPath.prev != null)
                Console.WriteLine($"startPath: id: {startPath.vertex.id}, len: {startPath.min_length}, prev: {startPath.prev.vertex.id}");
            else
                Console.WriteLine($"startPath: id: {startPath.vertex.id}, len: {startPath.min_length}, prev: null");

            // Алгоритм выполняется на подографе до тех пора, пока все вершины не будут помечены
            while (visited.Count() != vertexesNum)
            {
                // Получаем вершину с минимальным путём до неё из непоесещённых
                int vertexId = getVertexIdWithMinLength();
                // Алгоритм завершается, если больше нет путей
                if (vertexId == -1) { break; }
                //помечаем вершину как посещенную
                visited.Add(vertexId);
                                      
                // Ищем порядковый номер айдишника вершины
                int mId = getVertexNum(vertexId);
                int weight;
                // Проходимся по всем смежным вершинам по матрице смежности и создаём либо обновляем path для таких вершин
                for (int i = 0; i < vertexesNum; i++)
                {
                    if (i != mId)
                    {
                        weight = matrix[mId, i];
                        if (weight > -1)
                        {
                            // реальный id - id из конфига
                            int vertexRealId = vertexesIds[i];
                            Path prevVertexPath = pathes[vertexId];
                            int prevVertexLength = prevVertexPath.getMinLength();
                            // Содание path для вершины, если она не была ещё пройдена
                            if (!pathes.ContainsKey(vertexRealId))
                            {
                                Path path = new Path(vertexes[i], weight + prevVertexLength, prevVertexPath);
                                pathes.Add(vertexRealId, path);
                            }
                            // Изменение пути для вершины, если новый путь оказался короче
                            else
                            {
                                Path path = pathes[vertexRealId];
                                if (path.getMinLength() > weight + prevVertexLength)
                                {
                                    path.setPrevious(prevVertexPath, weight);
                                }
                            }
                        }
                    }
                }
                /*
                Console.WriteLine("PATHES: ");
                try
                {
                    foreach (KeyValuePair<int, Path> pair in pathes)
                    {
                        if (pair.Value.prev != null)
                            Console.WriteLine($"VertexId: {pair.Key} - Path: id: {pair.Value.vertex.id} minlen: {pair.Value.min_length} prev: {pair.Value.prev.vertex.id}");
                        else
                            Console.WriteLine($"VertexId: {pair.Key} - Path: id: {pair.Value.vertex.id} minlen: {pair.Value.min_length} prev: null");
                    }
                }
                catch (System.InvalidOperationException excep) { }
                */
                // Если вершина граничащая и при этом не является начальной, потому что в этом случае распараллеливаение производит клиент
                if (isAdjVertex(vertexId) && vertexId != startVertexId)
                {
                    foreach (CServer s in servers)
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, s.address + $"/api/graph/contains/{vertexId}");
                        var response = await client.SendAsync(request);
                        // Если подграф на другом сервере содержит эту гранчащую вершину
                        if (await response.Content.ReadAsStringAsync() == "true")
                        {
                            // Отправляем на сервер сериализованный Path для граничащей вершины
                            JsonContent content = JsonContent.Create(pathes[vertexId]);
                            Console.WriteLine($"Sent path: {pathes[vertexId].vertex.id} len: {pathes[vertexId].min_length} prev: {pathes[vertexId].prev}");
                            await client.PostAsync(s.address + "/api/graph/search/dijkstra", content);
                        }
                    }
                }
            }
            /*
            Console.WriteLine("End");
            Console.WriteLine("ENDPATHES: ");
            try
            {
                foreach (KeyValuePair<int, Path> pair in pathes)
                {
                    if (pair.Value.prev != null)
                        Console.WriteLine($"VertexId: {pair.Key} - Path: id: {pair.Value.vertex.id} minlen: {pair.Value.min_length} prev: {pair.Value.prev.vertex.id}");
                    else
                        Console.WriteLine($"VertexId: {pair.Key} - Path: id: {pair.Value.vertex.id} minlen: {pair.Value.min_length} prev: null");
                }
            }
            catch (System.InvalidOperationException excep) { }
            */
            inProgress = false;
        }
        public bool isInProgress()
        {
            return inProgress;
        }
        public bool isAdjVertex(int vertexId)
        {
            foreach (Vertex v in adj_vertexes) {
               if (v.id == vertexId) { return true; }
            }
            return false;
        }
        // Возвращает кратчайший путь к вершине, которая указана как конечная
        public List<Path> getShortestPath()
        {
            List<Path> restoredPath = new List<Path>();
            if (endVertexId != -1)
            {
                // Восстанавливаем путь для вершины
                restoredPath = restorePath(pathes[endVertexId]);
                /*// Выводим на экран
                for (int i = restoredPath.Count() - 1; i >= 0; i--)
                {
                    Console.Write(restoredPath[i].vertex.id + " ");
                }
                Console.Write(": " + restoredPath[0].min_length);
                Console.WriteLine();*/
                return restoredPath;
            }
            return restoredPath;
        }
        // Создаем список из объектов пути до вершины.
        private List<Path> restorePath (Path path)
        {
            List<Path> entirePath = new List<Path>();
            while (path != null)
            {
                entirePath.Add(path);
                path = path.prev;
            }
            return entirePath;
        }

        public void reset()
        {
            Console.WriteLine("RESET");
            startVertexId = -1;
            inProgress = false;
            endVertexId = -1;
            pathes.Clear();
            visited.Clear();
        }

        //debug
#if DEBUG
        public void printMatrix()
        {
            for (int i = 0; i < vertexesNum; i++)
            {
                for (int j = 0; j < vertexesNum; j++)
                {
                    Console.Write(matrix[i,j]);
                    Console.Write(" ");
                }
                Console.WriteLine();
            }
        }
#endif
    }
}
