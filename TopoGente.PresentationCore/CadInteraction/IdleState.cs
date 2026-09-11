using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class IdleState : CadCanvasState
    {
        public IdleState(CadStateMachine stateMachine, ICadCanvasContext context) 
            : base(stateMachine, context)
        {
        }

        public override void OnMouseDown(double modelX, double modelY)
        {
            // Transição para o estado de desenho aguardando o primeiro nó, ou capturando imediatamente se houver atração.
            StateMachine.ChangeState(new LineDrawingState(StateMachine, Context, modelX, modelY));
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            // Em repouso, apenas atualiza cursor ou ignora
        }

        public override void OnRightClick()
        {
            // Nenhuma ação (nenhum elástico ativo)
        }
    }
}
