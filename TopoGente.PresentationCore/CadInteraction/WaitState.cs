using TopoGente.UI.Spatial;

namespace TopoGente.UI.CadInteraction;

/// <summary>
/// Estado temporário da máquina de estados CAD que bloqueia todas as interações
/// do operador enquanto o motor de triangulação executa um recálculo global em background.
/// Todos os eventos de mouse e teclado são engolidos silenciosamente (no-op).
/// A CadStateMachine transiciona para este estado quando IsTopologyRebuilding = true
/// e retorna ao IdleState quando o recálculo termina.
/// </summary>
public class WaitState : CadCanvasState
{
    public WaitState(CadStateMachine stateMachine, ICadCanvasContext context)
        : base(stateMachine, context)
    {
    }

    public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
    {
        // No-op: bloqueio ativo durante recálculo
    }

    public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
    {
        // No-op: bloqueio ativo durante recálculo
    }

    public override void OnRightClick()
    {
        // No-op: bloqueio ativo durante recálculo
    }
}
