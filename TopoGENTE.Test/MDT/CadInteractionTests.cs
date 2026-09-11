using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TopoGente.UI.CadInteraction;
using TopoGente.UI.Spatial;

namespace TopoGENTE.Test.UI.CadInteraction;

// Stub manual de ICadCanvasContext
// Implementado para maximizar inlining via Native AOT.
// Sem uso de Reflection ou proxies de Mocking frameworks.
public sealed class StubCadCanvasContext : ICadCanvasContext
{
    public KdTree2D? SpatialIndex { get; set; }
    public BvhTree2D? EdgeSpatialIndex { get; set; }

    public double? RubberBandStartX { get; private set; }
    public double? RubberBandStartY { get; private set; }
    public double? RubberBandEndX { get; private set; }
    public double? RubberBandEndY { get; private set; }

    public int Emitidas { get; private set; }
    public (int start, int end) UltimaEmissao { get; private set; }

    public bool DeveSimularExclusaoConcorrente { get; set; } = false;
    public int ConflitosNotificados { get; private set; }

    public void SetRubberBand(double? startX, double? startY, double? endX, double? endY)
    {
        RubberBandStartX = startX;
        RubberBandStartY = startY;
        RubberBandEndX = endX;
        RubberBandEndY = endY;
    }

    public void EmitirLinhaVetorizada(int startVertexId, int endVertexId)
    {
        Emitidas++;
        UltimaEmissao = (startVertexId, endVertexId);
    }

    public bool ExisteBreakline(TopoGENTE.Domain.ValueObjects.Breakline breakline)
    {
        if (DeveSimularExclusaoConcorrente) return false;
        return true;
    }

    public void NotificarConflitoSelecao(TopoGENTE.Domain.ValueObjects.Breakline breakline)
    {
        ConflitosNotificados++;
    }
}

public class CadInteractionTests
{
    [Fact]
    public void Cenário1_Validação_Ciclo_De_Vida_Polimorfico()
    {
        var context = new StubCadCanvasContext();
        var stateMachine = new CadStateMachine(context);

        stateMachine.HandleMouseDown(100.0, 200.0);
        // Simula snapping com nó encontrado
        stateMachine.HandleMouseMove(100.0, 200.0, new KdNode(100.0, 200.0, 1)); 
        // Movimenta para o próximo alvo
        stateMachine.HandleMouseMove(150.0, 250.0, null);

        Assert.Equal(100.0, context.RubberBandStartX);
        Assert.Equal(200.0, context.RubberBandStartY);
        Assert.Equal(150.0, context.RubberBandEndX);
        Assert.Equal(250.0, context.RubberBandEndY);
    }

    [Fact]
    public async Task Cenário2_Prova_Matemática_Backpressure_WaitState()
    {
        var context = new StubCadCanvasContext();
        var stateMachine = new CadStateMachine(context);
        
        stateMachine.ChangeState(new WaitState(stateMachine, context));

        int eventosProcessados = 0;

        Action simularClique = () => 
        {
            stateMachine.HandleMouseDown(50.0, 50.0);
            Interlocked.Increment(ref eventosProcessados);
        };

        int N = 1000;
        var tarefas = new Task[N];
        
        for (int i = 0; i < N; i++)
        {
            tarefas[i] = Task.Run(simularClique);
        }

        await Task.WhenAll(tarefas);

        Assert.Equal(1000, eventosProcessados);
        Assert.Null(context.RubberBandStartX);
        Assert.Null(context.RubberBandStartY);
        Assert.Equal(0, context.Emitidas);
    }

    [Fact]
    public void Cenário3_LineDrawingState_Deve_Ser_Abortado_Via_CadInteractionKey()
    {
        var context = new StubCadCanvasContext();
        var stateMachine = new CadStateMachine(context);

        stateMachine.HandleMouseDown(0.0, 0.0);
        stateMachine.HandleMouseMove(0.0, 0.0, new KdNode(0.0, 0.0, 2));
        stateMachine.HandleMouseMove(50.0, 50.0, null);
        
        Assert.NotNull(context.RubberBandStartX);

        // Act: Envia comando de Escape usando o enumerador agnóstico de domínio
        stateMachine.HandleKeyDown(CadInteractionKey.Escape);

        // Assert: O elástico deve ser limpo e a máquina volta para o IdleState.
        // Se ela ficasse presa, um novo clique não atualizaria a origem.
        Assert.Null(context.RubberBandStartX);
    }

    [Fact]
    public void Cenário4_Devem_Tratar_Abrupto_Descarte_De_Breakline_Durante_Transicao_De_Estado()
    {
        // Arrange
        var context = new StubCadCanvasContext();
        
        var v1 = new TopoGENTE.Domain.ValueObjects.TerrainVertex(0, 0, 0, 1);
        var v2 = new TopoGENTE.Domain.ValueObjects.TerrainVertex(10, 10, 0, 2);
        var breakline = new TopoGENTE.Domain.ValueObjects.Breakline(1, 2);
        
        var breaklines = new System.Collections.Generic.List<TopoGENTE.Domain.ValueObjects.Breakline> { breakline };
        var vertexLookup = new System.Collections.Generic.Dictionary<int, TopoGENTE.Domain.ValueObjects.TerrainVertex>
        {
            { 1, v1 },
            { 2, v2 }
        };
        
        context.EdgeSpatialIndex = new BvhTree2D(breaklines, vertexLookup);
        var stateMachine = new CadStateMachine(context);
        
        // Inicia em SelectionState
        stateMachine.ChangeState(new SelectionState(stateMachine, context));
        Assert.IsType<SelectionState>(stateMachine.CurrentState);

        // Act - Simular exclusão de breakline em background
        context.DeveSimularExclusaoConcorrente = true;
        
        // Tenta clicar exatamente na breakline (x=5, y=5)
        stateMachine.HandleMouseDown(5.0, 5.0);
        
        // Assert
        Assert.Equal(1, context.ConflitosNotificados);
        Assert.IsType<SelectionState>(stateMachine.CurrentState);
    }
}
