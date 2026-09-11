using System.Runtime.InteropServices;

namespace TopoGente.UI.Spatial;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct FlatBvhNode
{
    public readonly double MinX;
    public readonly double MinY;
    public readonly double MaxX;
    public readonly double MaxY;

    // Se o nó for folha, armazena o ID do segmento; se for nó interno, o offset do filho esquerdo.
    public readonly int LeftChildOrSegmentId;
    
    // Se for -1, indica que o nó é uma folha. Caso contrário, armazena o índice do filho direito.
    public readonly int RightChildOffset;

    public FlatBvhNode(double minX, double minY, double maxX, double maxY, int leftChildOrSegmentId, int rightChildOffset)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        LeftChildOrSegmentId = leftChildOrSegmentId;
        RightChildOffset = rightChildOffset;
    }

    public bool Intersects(double x, double y, double tolerance)
    {
        return x + tolerance >= MinX && x - tolerance <= MaxX &&
               y + tolerance >= MinY && y - tolerance <= MaxY;
    }

    public bool IsLeaf => RightChildOffset == -1;
}
