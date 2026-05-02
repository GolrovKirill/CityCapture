using System;
using System.Collections.Generic;
using System.Linq;
using CityCapture.Models;

namespace CityCapture.Services
{
    public static class CaptureEngine
    {
        public static (List<CaptureStep> history, List<HashSet<int>> stateCities) Run(Graph graph, int k, int[] capitals)
        {
            int n = graph.VertexCount;
            var adj = graph.BuildAdjacencyList();

            int[] owner = new int[n];
            for (int i = 0; i < k; i++)
                owner[capitals[i]] = i + 1;

            var unoccupied = new HashSet<int>(Enumerable.Range(0, n).Except(capitals));
            var history = new List<CaptureStep> { new CaptureStep(owner) };

            var stateCities = new HashSet<int>[k];
            for (int s = 0; s < k; s++)
            {
                stateCities[s] = new HashSet<int> { capitals[s] };
            }

            bool progress = true;
            while (unoccupied.Count > 0 && progress)
            {
                progress = false;
                for (int s = 0; s < k; s++)
                {
                    if (unoccupied.Count == 0) break;

                    int bestCity = -1;
                    int bestDist = int.MaxValue;

                    // Поиск ближайшего незанятого соседа территории государства
                    foreach (int city in stateCities[s])
                    {
                        foreach (var (to, len) in adj[city])
                        {
                            if (unoccupied.Contains(to) && len < bestDist)
                            {
                                bestDist = len;
                                bestCity = to;
                            }
                            else if (unoccupied.Contains(to) && len == bestDist && to < bestCity)
                            {
                                bestCity = to;
                            }
                        }
                    }

                    if (bestCity != -1)
                    {
                        owner[bestCity] = s + 1;
                        stateCities[s].Add(bestCity);
                        unoccupied.Remove(bestCity);
                        progress = true;
                        history.Add(new CaptureStep(owner));
                    }
                }
            }

            return (history, stateCities.ToList());
        }
    }
}