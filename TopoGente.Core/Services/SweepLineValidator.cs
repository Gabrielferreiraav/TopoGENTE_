using System;
using System.Collections.Generic;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.Core.Services
{
    public static class SweepLineValidator
    {
        private class Point
        {
            public double X { get; set; }
            public double Y { get; set; }
            public int Id { get; set; }
        }

        private class Segment
        {
            public Point P1 { get; set; } = null!;
            public Point P2 { get; set; } = null!;
            public int Id { get; set; }
            public Breakline Original { get; set; }
        }

        private class Event : IComparable<Event>
        {
            public double X { get; set; }
            public double Y { get; set; }
            public int Type { get; set; }
            public Segment Segment { get; set; } = null!;

            public int CompareTo(Event? other)
            {
                if (other == null) return 1;
                int c = X.CompareTo(other.X);
                if (c == 0)
                {
                    c = Y.CompareTo(other.Y);
                    if (c == 0) return Type.CompareTo(other.Type);
                }
                return c;
            }
        }

        public static void ValidarCruzamentos(ReadOnlySpan<TerrainVertex> vertices, ReadOnlySpan<Breakline> breaklines)
        {
            if (breaklines.Length < 2) return;

            var events = new List<Event>(breaklines.Length * 2);

            for (int i = 0; i < breaklines.Length; i++)
            {
                var bl = breaklines[i];
                var v1 = vertices[bl.StartVertexId];
                var v2 = vertices[bl.EndVertexId];

                var p1 = new Point { X = v1.X, Y = v1.Y, Id = bl.StartVertexId };
                var p2 = new Point { X = v2.X, Y = v2.Y, Id = bl.EndVertexId };

                if (p1.X > p2.X || (p1.X == p2.X && p1.Y > p2.Y))
                {
                    (p1, p2) = (p2, p1);
                }

                var seg = new Segment { P1 = p1, P2 = p2, Id = i, Original = bl };
                
                events.Add(new Event { X = p1.X, Y = p1.Y, Type = 0, Segment = seg });
                events.Add(new Event { X = p2.X, Y = p2.Y, Type = 1, Segment = seg });
            }

            events.Sort();

            var activeSegments = new List<Segment>();

            foreach (var ev in events)
            {
                var seg = ev.Segment;

                if (ev.Type == 0)
                {
                    foreach (var active in activeSegments)
                    {
                        CheckIntersection(seg, active);
                    }
                    activeSegments.Add(seg);
                }
                else
                {
                    activeSegments.Remove(seg);
                }
            }
        }

        private static void CheckIntersection(Segment? s1, Segment? s2)
        {
            if (s1 == null || s2 == null) return;
            
            if (s1.P1.Id == s2.P1.Id || s1.P1.Id == s2.P2.Id ||
                s1.P2.Id == s2.P1.Id || s1.P2.Id == s2.P2.Id)
                return;

            double d = (s1.P1.X - s1.P2.X) * (s2.P1.Y - s2.P2.Y) - (s1.P1.Y - s1.P2.Y) * (s2.P1.X - s2.P2.X);
            
            // Mitigação de instabilidade numérica (IEEE 754): épsilon ajustado para 1e-6
            if (Math.Abs(d) < 1e-6) return; // Paralelos ou colineares
            
            double t = ((s1.P1.X - s2.P1.X) * (s2.P1.Y - s2.P2.Y) - (s1.P1.Y - s2.P1.Y) * (s2.P1.X - s2.P2.X)) / d;
            double u = ((s1.P1.X - s2.P1.X) * (s1.P1.Y - s1.P2.Y) - (s1.P1.Y - s2.P1.Y) * (s1.P1.X - s1.P2.X)) / d;

            if (t > 0 && t < 1 && u > 0 && u < 1)
            {
                throw new BreaklineConflictException(
                    $"Conflito geometrico de linhas de quebra detectado. " +
                    $"Segmento A (Vertices {s1.Original.StartVertexId}-{s1.Original.EndVertexId}) intercepta " +
                    $"Segmento B (Vertices {s2.Original.StartVertexId}-{s2.Original.EndVertexId}) fora de um vertice comum."
                );
            }
        }
    }
}
