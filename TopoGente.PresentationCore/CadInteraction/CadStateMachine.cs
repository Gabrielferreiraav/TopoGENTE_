using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class CadStateMachine
    {
        public CadCanvasState CurrentState { get; private set; }

        public CadStateMachine(ICadCanvasContext context)
        {
            CurrentState = new IdleState(this, context);
        }

        public void ChangeState(CadCanvasState newState)
        {
            CurrentState = newState;
        }

        public void HandleMouseDown(double modelX, double modelY)
        {
            CurrentState.OnMouseDown(modelX, modelY);
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
    }
}
