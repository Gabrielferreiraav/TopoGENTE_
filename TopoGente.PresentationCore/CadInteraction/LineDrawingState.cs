using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class LineDrawingState : CadCanvasState
    {
        private KdNode? _startNode;
        private double _currentMouseX;
        private double _currentMouseY;

        public LineDrawingState(CadStateMachine stateMachine, ICadCanvasContext context, double initialX, double initialY) 
            : base(stateMachine, context)
        {
            _currentMouseX = initialX;
            _currentMouseY = initialY;
        }

        public override void OnMouseDown(double modelX, double modelY)
        {
            // O snapping será interceptado via MouseMove. 
            // Para garantir precisão, assumiremos que OnMouseMove foi disparado imediatamente antes.
        }

        public void SetStartNode(KdNode startNode)
        {
            _startNode = startNode;
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            if (nearestNode.HasValue)
            {
                _currentMouseX = nearestNode.Value.X;
                _currentMouseY = nearestNode.Value.Y;
            }
            else
            {
                _currentMouseX = modelX;
                _currentMouseY = modelY;
            }

            if (_startNode.HasValue)
            {
                RenderRubberBand();
            }
            else if (nearestNode.HasValue)
            {
                // Se ainda não temos um StartNode ancorado e acabamos de encontrar um via Snapping, nós o ancoramos
                SetStartNode(nearestNode.Value);
            }
        }

        public override void OnRightClick()
        {
            // O usuário encerra a operação.
            Context.ClearRubberBand();
            StateMachine.ChangeState(new IdleState(StateMachine, Context));
        }

        public override void OnKeyDown(CadInteractionKey key)
        {
            if (key == CadInteractionKey.Escape)
            {
                Context.ClearRubberBand();
                StateMachine.ChangeState(new IdleState(StateMachine, Context));
            }
        }

        private void RenderRubberBand()
        {
            if (!_startNode.HasValue) return;
            Context.SetRubberBand(_startNode.Value.X, _startNode.Value.Y, _currentMouseX, _currentMouseY);
        }
    }
}
