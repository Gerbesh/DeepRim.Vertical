using System;
using System.Collections.Generic;

namespace DeepRim.Vertical.VerticalUtilities;

// Engine-free rules shared by the executors and conservation tests.
public static class UtilityMath
{
    public sealed class TickLedger<T>
    {
        private int tick = int.MinValue;
        private readonly HashSet<T> claimed = new();
        private void Advance(int value) { if (tick != value) { tick = value; claimed.Clear(); } }
        public bool Contains(int value, T item) { Advance(value); return claimed.Contains(item); }
        public bool Claim(int value, T item) { Advance(value); return claimed.Add(item); }
    }
    public static float HeatTransfer(float source, float target, int sourceCells, int targetCells, float budget)
    {
        if (sourceCells <= 0 || targetCells <= 0 || budget <= 0f
            || float.IsNaN(source) || float.IsNaN(target) || float.IsNaN(budget)
            || float.IsInfinity(source) || float.IsInfinity(target) || float.IsInfinity(budget)) return 0f;
        var equilibrium = (source - target) / (1f / sourceCells + 1f / targetCells);
        return Math.Sign(equilibrium) * Math.Min(Math.Abs(equilibrium), budget);
    }

    public readonly struct ColumnNode
    {
        public readonly UtilityKind Kind;
        public readonly bool Enabled;
        public ColumnNode(UtilityKind kind, bool enabled) { Kind = kind; Enabled = enabled; }
    }
    public static int? FindIntakeLevel(int outletLevel, int? selected, Func<int, ColumnNode?> lookup)
    {
        if (outletLevel == int.MinValue) return null;
        for (var level = outletLevel - 1; ; level--)
        {
            var node = lookup(level);
            if (!node.HasValue || !node.Value.Enabled) return null;
            if (node.Value.Kind == UtilityKind.ThermalIntake && (!selected.HasValue || selected == level)) return level;
            if (level == int.MinValue) return null;
        }
    }

    public static List<List<T>> Groups<T>(IEnumerable<T> nodes, IEnumerable<Tuple<T, T>> edges)
    {
        var adjacency = new Dictionary<T, HashSet<T>>();
        foreach (var node in nodes) if (!adjacency.ContainsKey(node)) adjacency[node] = new HashSet<T>();
        foreach (var edge in edges)
        {
            if (!adjacency.ContainsKey(edge.Item1) || !adjacency.ContainsKey(edge.Item2)) continue;
            adjacency[edge.Item1].Add(edge.Item2);
            adjacency[edge.Item2].Add(edge.Item1);
        }
        var result = new List<List<T>>();
        var visited = new HashSet<T>();
        foreach (var node in adjacency.Keys)
        {
            if (!visited.Add(node)) continue;
            var group = new List<T>();
            var queue = new Queue<T>();
            queue.Enqueue(node);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                group.Add(current);
                foreach (var next in adjacency[current]) if (visited.Add(next)) queue.Enqueue(next);
            }
            result.Add(group);
        }
        return result;
    }
}
