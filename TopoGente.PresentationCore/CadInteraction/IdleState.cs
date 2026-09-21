using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction
{
    public class IdleState : CadCanvasState
    {
        public IdleState(CadStateMachine stateMachine, ICadCanvasContext context) 
            : base(stateMachine, context)
        {
        }

        public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            if (nearestNode.HasValue)
            {
                if (!Context.PodeTraçarBreaklines)
                {
                    Context.NotificarAviso("Para traçar linhas obrigatórias (breaklines), primeiro compense a poligonal para gerar o MDT base.");
                    return;
                }

                StateMachine.ChangeState(new LineDrawingState(StateMachine, Context, nearestNode.Value));
            }
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            if (nearestNode.HasValue)
            {
                Context.SetSnapMarker(nearestNode.Value.X, nearestNode.Value.Y);
            }
            else
            {
                Context.ClearSnapMarker();
            }
        }

        public override void OnRightClick()
        {
            // Nenhuma ação (nenhum elástico ativo)
        }
    }
}
