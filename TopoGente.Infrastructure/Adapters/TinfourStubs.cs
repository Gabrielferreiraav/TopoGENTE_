using System.Collections.Generic;

// NOTA TÉCNICA DO ARQUITETO:
// Stub do Tinfour.NET para compilação estática do módulo desacoplado.
// Substituído no ambiente produtivo pelo empacotamento real (NuGet).
namespace Tinfour.Core
{
    public class Vertex 
    { 
        public double X { get;} public double Y { get;} public double Z { get;} public int Index { get;} 
        public Vertex(double x, double y, double z, int id) { X=x; Y=y; Z=z; Index=id; }
    }
    public interface IConstraint { }
    public class LinearConstraint : IConstraint 
    { 
        public LinearConstraint(Vertex v1, Vertex v2) { } 
    }
    public class IncrementalTin 
    { 
        public void Add(List<Vertex> list) {} 
        public void AddConstraints(List<IConstraint> c, bool restoreDelaunay) {}
        public List<(int V0, int V1, int V2)> GetTriangles() => new List<(int, int, int)>();
        public double GetMinimumElevation() => 0.0;
        public double GetMaximumElevation() => 1000.0;
    }
}
namespace Tinfour.Interpolation
{
    public class NaturalNeighborInterpolator 
    { 
        public NaturalNeighborInterpolator(Core.IncrementalTin tin) {}
        public double Interpolate(double x, double y, object state) => 125.5; 
    }
}
namespace Tinfour.Contour
{
    public class Contour
    {
        public double Z { get; set; }
        public List<Core.Vertex> GetCoordinates() => new List<Core.Vertex>();
    }
    public class ContourBuilderForTin
    {
        public ContourBuilderForTin(Core.IncrementalTin tin, object interpolator, double[] zLevels, bool b) {}
        public List<Contour> GetContours() => new List<Contour>();
    }
}
