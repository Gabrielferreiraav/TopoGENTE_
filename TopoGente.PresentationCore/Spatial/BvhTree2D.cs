using System;
using System.Collections.Generic;
using System.Linq;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.UI.Spatial;

public class BvhTree2D
{
    private readonly FlatBvhNode[] _nodes;
    private readonly SegmentData[] _segments;

    private readonly struct SegmentData
    {
        public readonly Breakline Breakline;
        public readonly double P1X, P1Y, P2X, P2Y;
        public readonly double MinX, MinY, MaxX, MaxY;
        
        public SegmentData(Breakline breakline, double p1x, double p1y, double p2x, double p2y)
        {
            Breakline = breakline;
            P1X = p1x; P1Y = p1y; P2X = p2x; P2Y = p2y;
            MinX = Math.Min(p1x, p2x);
            MinY = Math.Min(p1y, p2y);
            MaxX = Math.Max(p1x, p2x);
            MaxY = Math.Max(p1y, p2y);
        }
    }

    public BvhTree2D(IEnumerable<Breakline> breaklines, IDictionary<int, TerrainVertex> vertexLookup)
    {
        var validSegments = new List<SegmentData>();
        foreach (var b in breaklines)
        {
            if (vertexLookup.TryGetValue(b.StartVertexId, out var v1) && 
                vertexLookup.TryGetValue(b.EndVertexId, out var v2))
            {
                validSegments.Add(new SegmentData(b, v1.X, v1.Y, v2.X, v2.Y));
            }
        }
        
        int n = validSegments.Count;
        if (n == 0)
        {
            _nodes = Array.Empty<FlatBvhNode>();
            _segments = Array.Empty<SegmentData>();
            return;
        }

        _segments = validSegments.ToArray();
        _nodes = new FlatBvhNode[2 * n]; // Max possible nodes
        
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;

        int nextNodeIdx = 0;
        BuildRecursive(indices, 0, n - 1, ref nextNodeIdx);
        
        // Shrink array if needed (rare, usually exactly 2N-1)
        if (nextNodeIdx < _nodes.Length)
        {
            Array.Resize(ref _nodes, nextNodeIdx);
        }
    }

    private int BuildRecursive(int[] indices, int start, int end, ref int nodeIdx)
    {
        int currentIndex = nodeIdx++;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        for (int i = start; i <= end; i++)
        {
            var seg = _segments[indices[i]];
            if (seg.MinX < minX) minX = seg.MinX;
            if (seg.MinY < minY) minY = seg.MinY;
            if (seg.MaxX > maxX) maxX = seg.MaxX;
            if (seg.MaxY > maxY) maxY = seg.MaxY;
        }

        if (start == end)
        {
            // Leaf node
            _nodes[currentIndex] = new FlatBvhNode(minX, minY, maxX, maxY, indices[start], -1);
            return currentIndex;
        }

        // Internal node: split by longest axis
        double extentX = maxX - minX;
        double extentY = maxY - minY;

        int axis = extentX > extentY ? 0 : 1;

        Array.Sort(indices, start, end - start + 1, Comparer<int>.Create((a, b) =>
        {
            double centerA = axis == 0 ? (_segments[a].MinX + _segments[a].MaxX) / 2 : (_segments[a].MinY + _segments[a].MaxY) / 2;
            double centerB = axis == 0 ? (_segments[b].MinX + _segments[b].MaxX) / 2 : (_segments[b].MinY + _segments[b].MaxY) / 2;
            return centerA.CompareTo(centerB);
        }));

        int mid = start + (end - start) / 2;
        int leftChild = BuildRecursive(indices, start, mid, ref nodeIdx);
        int rightChild = BuildRecursive(indices, mid + 1, end, ref nodeIdx);

        _nodes[currentIndex] = new FlatBvhNode(minX, minY, maxX, maxY, leftChild, rightChild);
        return currentIndex;
    }

    public Breakline? FindNearestSegment(double x, double y, double tolerance)
    {
        if (_nodes.Length == 0) return null;

        double minSqDist = tolerance * tolerance;
        int bestSegmentIdx = -1;

        Stack<int> stack = new Stack<int>();
        stack.Push(0);

        while (stack.Count > 0)
        {
            int nodeIdx = stack.Pop();
            ref var node = ref _nodes[nodeIdx];

            if (!node.Intersects(x, y, tolerance)) continue;

            if (node.IsLeaf)
            {
                var seg = _segments[node.LeftChildOrSegmentId];
                double sqDist = PointToSegmentDistanceSquared(x, y, seg.P1X, seg.P1Y, seg.P2X, seg.P2Y);
                if (sqDist <= minSqDist)
                {
                    minSqDist = sqDist;
                    bestSegmentIdx = node.LeftChildOrSegmentId;
                }
            }
            else
            {
                stack.Push(node.LeftChildOrSegmentId);
                stack.Push(node.RightChildOffset);
            }
        }

        if (bestSegmentIdx != -1)
        {
            return _segments[bestSegmentIdx].Breakline;
        }

        return null;
    }

    private static double PointToSegmentDistanceSquared(double ptX, double ptY, double p1X, double p1Y, double p2X, double p2Y)
    {
        double dx = p2X - p1X;
        double dy = p2Y - p1Y;
        
        if (dx == 0 && dy == 0)
        {
            dx = ptX - p1X;
            dy = ptY - p1Y;
            return dx * dx + dy * dy;
        }

        double t = ((ptX - p1X) * dx + (ptY - p1Y) * dy) / (dx * dx + dy * dy);
        
        if (t < 0)
        {
            dx = ptX - p1X;
            dy = ptY - p1Y;
        }
        else if (t > 1)
        {
            dx = ptX - p2X;
            dy = ptY - p2Y;
        }
        else
        {
            double projX = p1X + t * dx;
            double projY = p1Y + t * dy;
            dx = ptX - projX;
            dy = ptY - projY;
        }

        return dx * dx + dy * dy;
    }
}
