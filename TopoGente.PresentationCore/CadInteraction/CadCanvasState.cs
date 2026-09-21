using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public abstract class CadCanvasState
    {
        protected CadStateMachine StateMachine { get; }
        protected ICadCanvasContext Context { get; }

        protected CadCanvasState(CadStateMachine stateMachine, ICadCanvasContext context)
        {
            StateMachine = stateMachine;
            Context = context;
        }

        public abstract void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null);
        public abstract void OnMouseMove(double modelX, double modelY, KdNode? nearestNode);
        public abstract void OnRightClick();
        
        public virtual void OnKeyDown(CadInteractionKey key)
        {
        }
    }
}
