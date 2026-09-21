using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class CadStateMachine
    {
        public CadCanvasState CurrentState { get; private set; }
        private readonly ICadCanvasContext _context;

        public CadStateMachine(ICadCanvasContext context, CadToolMode initialMode = CadToolMode.Inspecao)
        {
            _context = context;
            CurrentState = new SelectionState(this, context);
            if (initialMode != CadToolMode.Inspecao)
            {
                SetToolMode(initialMode);
            }
        }

        public void ChangeState(CadCanvasState newState)
        {
            CurrentState = newState;
        }

        public void HandleMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            CurrentState.OnMouseDown(modelX, modelY, nearestNode);
        }

        public void HandleMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            CurrentState.OnMouseMove(modelX, modelY, nearestNode);
        }

        public void HandleRightClick()
        {
            CurrentState.OnRightClick();
        }

        public void HandleKeyDown(CadInteractionKey key)
        {
            CurrentState.OnKeyDown(key);
        }

        public void SetToolMode(CadToolMode mode)
        {
            if (CurrentState is MeasurementState && mode != CadToolMode.Medicao)
            {
                CurrentState.OnKeyDown(CadInteractionKey.Escape);
            }
            if (CurrentState is LineDrawingState && mode != CadToolMode.Breakline)
            {
                CurrentState.OnKeyDown(CadInteractionKey.Escape);
            }

            switch (mode)
            {
                case CadToolMode.Breakline:
                    ChangeState(new IdleState(this, _context));
                    break;
                case CadToolMode.Medicao:
                    ChangeState(new MeasurementState(this, _context));
                    break;
                case CadToolMode.Inspecao:
                default:
                    ChangeState(new SelectionState(this, _context));
                    break;
            }
        }
    }
}
