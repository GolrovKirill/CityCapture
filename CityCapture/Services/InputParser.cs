using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityCapture.Models;

namespace CityCapture.Services
{
    public static class InputParser
    {
        public static (Graph graph, int k, int[] capitals) Parse(string filePath)
        {
            var lines = File.ReadAllLines(filePath)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToArray();
            if (lines.Length < 4)
                throw new FormatException("Недостаточно строк во входном файле.");

            // n и m
            var firstTokens = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (firstTokens.Length < 2)
                throw new FormatException("Первая строка должна содержать n и m.");
            int n = int.Parse(firstTokens[0]);
            int m = int.Parse(firstTokens[1]);

            // Рёбра
            var edges = new List<(int, int, int)>();
            for (int i = 1; i <= m; i++)
            {
                var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                int u = int.Parse(parts[0]) - 1;
                int v = int.Parse(parts[1]) - 1;
                int len = int.Parse(parts[2]);
                edges.Add((u, v, len));
            }

            // Количество столиц
            int lineIdx = m + 1;
            if (lineIdx >= lines.Length)
                throw new FormatException("Строка с количеством столиц отсутствует.");
            var kTokens = lines[lineIdx].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (kTokens.Length < 1)
                throw new FormatException("Не указано количество столиц.");
            int k = int.Parse(kTokens[0]);

            // Номера столиц
            lineIdx++;
            if (lineIdx >= lines.Length)
                throw new FormatException("Не указаны номера столиц.");
            var capTokens = lines[lineIdx].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (capTokens.Length < k)
                throw new FormatException("Количество столиц меньше заявленного.");
            int[] capitals = capTokens.Take(k).Select(s => int.Parse(s) - 1).ToArray();

            return (new Graph(n, edges), k, capitals);
        }
    }
}