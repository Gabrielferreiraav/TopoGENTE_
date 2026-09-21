using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    /// <summary>
    /// Estado de desenho de linhas (breaklines) com encadeamento contínuo.
    /// Padrão topoGRAPH / AutoCAD: após consolidar (A, B), B torna-se a nova âncora.
    /// O operador encerra a cadeia com ESC ou Botão Direito.
    /// </summary>
    public class LineDrawingState : CadCanvasState
    {
        private KdNode _startNode;
        private KdNode? _hoveredNode;
        private double _currentMouseX;
        private double _currentMouseY;

        public LineDrawingState(CadStateMachine stateMachine, ICadCanvasContext context, KdNode startNode) 
            : base(stateMachine, context)
        {
            _startNode = startNode;
        }

        public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            if (nearestNode.HasValue && nearestNode.Value.DomainId != _startNode.DomainId)
            {
                Context.EmitirLinhaVetorizada(_startNode.DomainId, nearestNode.Value.DomainId);
                _startNode = nearestNode.Value;
                RenderRubberBand();
            }
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            _hoveredNode = nearestNode;

            if (nearestNode.HasValue)
            {
                _currentMouseX = nearestNode.Value.X;
                _currentMouseY = nearestNode.Value.Y;
                Context.SetSnapMarker(nearestNode.Value.X, nearestNode.Value.Y);
            }
            else
            {
                _currentMouseX = modelX;
                _currentMouseY = modelY;
                Context.ClearSnapMarker();
            }

            RenderRubberBand();
        }

        public override void OnRightClick()
        {
            Context.ClearRubberBand();
            Context.ClearSnapMarker();
            StateMachine.ChangeState(new IdleState(StateMachine, Context));
        }

        public override void OnKeyDown(CadInteractionKey key)
        {
            if (key == CadInteractionKey.Escape)
            {
                Context.ClearRubberBand();
                Context.ClearSnapMarker();
                StateMachine.ChangeState(new IdleState(StateMachine, Context));
            }
        }

        private void RenderRubberBand()
        {
            Context.SetRubberBand(_startNode.X, _startNode.Y, _currentMouseX, _currentMouseY);
        }
    }
}

