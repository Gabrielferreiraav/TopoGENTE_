using TopoGente.UI.Spatial;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.UI.CadInteraction
{
    /// <summary>
    /// Interface estrita para garantir o Princípio de Segregação de Interfaces (ISP).
    /// Pura abstração matemática: zero dependências de WPF ou primitivas gráficas.
    /// </summary>
    public interface ICadCanvasContext
    {
        KdTree2D? SpatialIndex { get; }
        BvhTree2D? EdgeSpatialIndex { get; }
        
        // Coordenadas lógicas expostas para a View/ViewModel compilar as geometrias reais
        double? RubberBandStartX { get; }
        double? RubberBandStartY { get; }
        double? RubberBandEndX { get; }
        double? RubberBandEndY { get; }

        void SetRubberBand(double? startX, double? startY, double? endX, double? endY);
        
        void ClearRubberBand() => SetRubberBand(null, null, null, null);
        
        void EmitirLinhaVetorizada(int startVertexId, int endVertexId);

        bool ExisteBreakline(Breakline breakline);
        void NotificarConflitoSelecao(Breakline breakline);
    }
}
