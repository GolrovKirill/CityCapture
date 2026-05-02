using System;
using System.Collections.Generic;
using System.Drawing;
using CityCapture.Models;

namespace CityCapture.Services
{
    public static class GraphLayouter
    {
        public static PointF[] ComputeLayout(Graph graph, Size area)
        {
            int n = graph.VertexCount;
            var adj = graph.BuildAdjacencyList(); // игнорируем длины
            var positions = new PointF[n];
            var rnd = new Random(42);
            float w = area.Width;
            float h = area.Height;

            for (int i = 0; i < n; i++)
                positions[i] = new PointF(rnd.Next(50, (int)w - 50), rnd.Next(50, (int)h - 50));

            float idealLen = 80f;
            float repulsion = 5000f;
            float attraction = 0.01f;
            int iterations = 150;

            for (int it = 0; it < iterations; it++)
            {
                var disp = new PointF[n];
                // отталкивание
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        float dx = positions[i].X - positions[j].X;
                        float dy = positions[i].Y - positions[j].Y;
                        float distSq = dx * dx + dy * dy;
                        if (distSq < 1) distSq = 1;
                        float force = repulsion / distSq;
                        float sqrtDist = (float)Math.Sqrt(distSq);
                        float nx = dx / sqrtDist;
                        float ny = dy / sqrtDist;
                        disp[i].X += nx * force;
                        disp[i].Y += ny * force;
                        disp[j].X -= nx * force;
                        disp[j].Y -= ny * force;
                    }
                }
                // притяжение по рёбрам
                foreach (var (u, v, _) in graph.Edges)
                {
                    float dx = positions[u].X - positions[v].X;
                    float dy = positions[u].Y - positions[v].Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 1) dist = 1;
                    float force = (dist - idealLen) * attraction;
                    float nx = dx / dist;
                    float ny = dy / dist;
                    disp[u].X -= nx * force;
                    disp[u].Y -= ny * force;
                    disp[v].X += nx * force;
                    disp[v].Y += ny * force;
                }
                // обновление позиций
                for (int i = 0; i < n; i++)
                {
                    positions[i].X += disp[i].X;
                    positions[i].Y += disp[i].Y;
                    positions[i].X = Math.Max(20, Math.Min(w - 20, positions[i].X));
                    positions[i].Y = Math.Max(20, Math.Min(h - 20, positions[i].Y));
                }
            }
            return positions;
        }
    }
}