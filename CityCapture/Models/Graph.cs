using System;
using System.Collections.Generic;

namespace CityCapture.Models
{
    public class Graph
    {
        public int VertexCount { get; }
        public List<(int u, int v, int length)> Edges { get; }

        // Матрица кратчайших расстояний (заполняется по требованию)
        public int[,] ShortestDistances { get; private set; }

        public Graph(int vertexCount, List<(int, int, int)> edges)
        {
            VertexCount = vertexCount;
            Edges = edges ?? new List<(int, int, int)>();
        }

        // Возвращает списки смежности (сосед, длина)
        public List<(int to, int len)>[] BuildAdjacencyList()
        {
            var adj = new List<(int, int)>[VertexCount];
            for (int i = 0; i < VertexCount; i++)
                adj[i] = new List<(int, int)>();
            foreach (var (u, v, len) in Edges)
            {
                adj[u].Add((v, len));
                adj[v].Add((u, len));
            }
            return adj;
        }

        // Вычисление кратчайших расстояний между всеми парами вершин
        public void ComputeAllPairsShortestPaths()
        {
            int n = VertexCount;
            int[,] dist = new int[n, n];
            int INF = int.MaxValue / 3;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    dist[i, j] = (i == j) ? 0 : INF;

            foreach (var (u, v, len) in Edges)
            {
                if (len < dist[u, v])
                {
                    dist[u, v] = len;
                    dist[v, u] = len;
                }
            }

            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i, k] + dist[k, j] < dist[i, j])
                            dist[i, j] = dist[i, k] + dist[k, j];

            ShortestDistances = dist;
        }
    }
}