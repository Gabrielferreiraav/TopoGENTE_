using TopoGente.UI.Spatial;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.UI.CadInteraction
{
    public class SelectionState : CadCanvasState
    {
        public SelectionState(CadStateMachine stateMachine, ICadCanvasContext context) 
            : base(stateMachine, context)
        {
        }

        public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            if (nearestNode.HasValue)
            {
                double? z = Context.GetElevation(nearestNode.Value.DomainId);
                Context.NotificarElementoInspecionado(nearestNode.Value.DomainId, nearestNode.Value.X, nearestNode.Value.Y, z, $"Vértice {nearestNode.Value.DomainId}");
            }

            if (Context.EdgeSpatialIndex == null) return;

            Breakline? nearest = Context.EdgeSpatialIndex.FindNearestSegment(modelX, modelY, 0.5);
            
            if (nearest.HasValue)
            {
                var breakline = nearest.Value;

                // Guarda de Integridade Concorrente (Late Binding Validation)
                // Nota Arquitetural: O struct Breakline possui apenas StartVertexId e EndVertexId (8 bytes no total).
                // Ele NÃO contém a cota Z nem a instância do vértice. Portanto, uma mutação altimétrica em outra thread
                // NÃO quebra a igualdade estrutural. Passá-lo por valor é idêntico a passar um Int64 composto,
                // sendo perfeitamente seguro e imune à armadilha de mutação estrutural temida na arquitetura.
                if (!Context.ExisteBreakline(breakline))
                {
                    // Aborta a transição, permanece no SelectionState e notifica o contexto
                    Context.NotificarConflitoSelecao(breakline);
                    return;
                }

                Context.NotificarElementoInspecionado(null, null, null, null, $"Breakline de {breakline.StartVertexId} para {breakline.EndVertexId}");
                StateMachine.ChangeState(new EditingState(StateMachine, Context, breakline));
            }
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            if (Context.EdgeSpatialIndex == null) return;
            Breakline? nearest = Context.EdgeSpatialIndex.FindNearestSegment(modelX, modelY, 0.5);
            if (nearest.HasValue)
            {
                // UI pode ativar efeitos ópticos de hover através do contexto
            }
        }

        public override void OnRightClick()
        {
            // Estado neutro: clique direito não realiza ações transacionais
        }
    }

    public class EditingState : CadCanvasState
    {
        private readonly Breakline _selectedElement;

        public EditingState(CadStateMachine stateMachine, ICadCanvasContext context, Breakline selectedElement) 
            : base(stateMachine, context)
        {
            _selectedElement = selectedElement;
        }

        public override void OnMouseDown(double modelX, double modelY, KdNode? nearestNode = null)
        {
            // Clique em vazio desmarca o elemento selecionado e retorna ao repouso
            StateMachine.ChangeState(new SelectionState(StateMachine, Context));
        }

        public override void OnMouseMove(double modelX, double modelY, KdNode? nearestNode)
        {
            // Lógica para arraste elástico de vértices ou de caixas de manipulação
        }

        public override void OnRightClick()
        {
            // Cancela a seleção ativa e recua para o SelectionState
            StateMachine.ChangeState(new SelectionState(StateMachine, Context));
        }
    }
}
