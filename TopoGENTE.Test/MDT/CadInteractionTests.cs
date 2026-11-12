using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using TopoGente.UI.CadInteraction;
using TopoGente.UI.Spatial;

namespace TopoGente.Test.MDT;

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

    public double? SnapMarkerX { get; private set; }
    public double? SnapMarkerY { get; private set; }

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

    public void SetSnapMarker(double? x, double? y)
    {
        SnapMarkerX = x;
        SnapMarkerY = y;
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

    public double? GetElevation(int vertexId) => vertexId * 10.0; // dummy mock

    public void SetMeasurementBand(double? startX, double? startY, double? endX, double? endY)
    {
        SetRubberBand(startX, startY, endX, endY);

    }

    public void ClearMeasurementBand() => SetMeasurementBand(null, null, null, null);

    public double UltimoDH { get; private set; }
    public double UltimoDI { get; private set; }
    public double UltimoDZ { get; private set; }
    public double UltimaInclinacao { get; private set; }
    public double UltimoAzimute { get; private set; }
    public string UltimaInspecaoDescricao { get; private set; } = "";

    public void NotificarMedicao(double dh, double di, double dz, double inclinacao, double azimute)
    {
        UltimoDH = dh; UltimoDI = di; UltimoDZ = dz; UltimaInclinacao = inclinacao; UltimoAzimute = azimute;
    }

    public void LimparMedicao() { }

    public void NotificarElementoInspecionado(int? verticeId, double? x, double? y, double? z, string? descricao)
    {
        UltimaInspecaoDescricao = descricao ?? "";
    }

    public void LimparInspecao() { }

    public Dictionary<int, string> NomesVerticesMock { get; } = new();
    public string? ObterIdentificadorVertice(int vertexId) =>
        NomesVerticesMock.TryGetValue(vertexId, out var nome) ? nome : vertexId.ToString();

    public bool PodeTraçarBreaklines { get; set; } = true;
    public string? UltimoAvisoNotificado { get; private set; }
    public void NotificarAviso(string mensagem) => UltimoAvisoNotificado = mensagem;
}

public class CadInteractionTests
{
    [Fact]
    public void Cenário1_Validação_Ciclo_De_Vida_Polimorfico()
    {
        var context = new StubCadCanvasContext();
        var stateMachine = new CadStateMachine(context);

        var nodeA = new KdNode(100.0, 200.0, 1);
        stateMachine.ChangeState(new IdleState(stateMachine, context));
        stateMachine.HandleMouseDown(100.0, 200.0, nodeA);
        stateMachine.HandleMouseMove(150.0, 250.0, null);

        context.RubberBandStartX.Should().BeApproximately(100.0, 1e-4);
        context.RubberBandStartY.Should().BeApproximately(200.0, 1e-4);
        context.RubberBandEndX.Should().BeApproximately(150.0, 1e-4);
        context.RubberBandEndY.Should().BeApproximately(250.0, 1e-4);
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

        stateMachine.ChangeState(new IdleState(stateMachine, context));
        var node = new KdNode(0.0, 0.0, 2);
        stateMachine.HandleMouseDown(0.0, 0.0, node);
        stateMachine.HandleMouseMove(50.0, 50.0, null);
        
        Assert.NotNull(context.RubberBandStartX);

        stateMachine.HandleKeyDown(CadInteractionKey.Escape);

        Assert.Null(context.RubberBandStartX);
        Assert.Null(context.SnapMarkerX);
    }

    [Fact]
    public void Cenário4_Devem_Tratar_Abrupto_Descarte_De_Breakline_Durante_Transicao_De_Estado()
    {
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
        
        stateMachine.ChangeState(new SelectionState(stateMachine, context));
        Assert.IsType<SelectionState>(stateMachine.CurrentState);

        context.DeveSimularExclusaoConcorrente = true;
        
        stateMachine.HandleMouseDown(5.0, 5.0);
        
        Assert.Equal(1, context.ConflitosNotificados);
        Assert.IsType<SelectionState>(stateMachine.CurrentState);
    }

    [Fact]
    public void Cenário5_Dois_Cliques_Devem_Emitir_Linha_Vetorizada()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);
        sm.ChangeState(new IdleState(sm, context));

        var noA = new KdNode(100.0, 200.0, 1);
        sm.HandleMouseDown(100.0, 200.0, noA);

        var noB = new KdNode(300.0, 400.0, 2);
        sm.HandleMouseMove(300.0, 400.0, noB);
        sm.HandleMouseDown(300.0, 400.0, noB);

        Assert.Equal(1, context.Emitidas);
        Assert.Equal((1, 2), context.UltimaEmissao);
        context.SnapMarkerX.Should().BeApproximately(300.0, 1e-4);
    }

    [Fact]
    public void Cenário6_Encadeamento_Contínuo_E_Término_Com_Escape()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);
        sm.ChangeState(new IdleState(sm, context));

        var noA = new KdNode(10.0, 10.0, 1);
        sm.HandleMouseDown(10.0, 10.0, noA);

        var noB = new KdNode(20.0, 20.0, 2);
        sm.HandleMouseMove(20.0, 20.0, noB);
        sm.HandleMouseDown(20.0, 20.0, noB);
        Assert.Equal(1, context.Emitidas);
        Assert.Equal((1, 2), context.UltimaEmissao);

        var noC = new KdNode(30.0, 30.0, 3);
        sm.HandleMouseMove(30.0, 30.0, noC);
        sm.HandleMouseDown(30.0, 30.0, noC);
        Assert.Equal(2, context.Emitidas);
        Assert.Equal((2, 3), context.UltimaEmissao);

        sm.HandleKeyDown(CadInteractionKey.Escape);
        Assert.Null(context.RubberBandStartX);
        Assert.IsType<IdleState>(sm.CurrentState);
    }

    [Fact]
    public void Cenário7_Validacao_Matematica_MeasurementState()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);
        sm.SetToolMode(CadToolMode.Medicao);
        
        var noA = new KdNode(0.0, 0.0, 1); // Z mock = 10
        sm.HandleMouseDown(0.0, 0.0, noA);
        
        var noB = new KdNode(3.0, 4.0, 2); // Z mock = 20
        sm.HandleMouseMove(3.0, 4.0, noB);
        
        context.UltimoDH.Should().BeApproximately(5.0, 0.001); // sqrt(3^2 + 4^2) = 5
        context.UltimoDZ.Should().BeApproximately(10.0, 0.001); // 20 - 10 = 10
        context.UltimoDI.Should().BeApproximately(11.180, 0.001); // sqrt(5^2 + 10^2) = 11.180
        context.UltimaInclinacao.Should().BeApproximately(200.0, 0.001); // 10 / 5 = 200%
        context.UltimoAzimute.Should().BeApproximately(36.870, 0.001); // atan2(3,4) = 36.87
    }

    [Fact]
    public void Cenário8_Alternancia_Reativa_ToolMode()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);
        
        sm.SetToolMode(CadToolMode.Medicao);
        Assert.IsType<MeasurementState>(sm.CurrentState);
        
        sm.SetToolMode(CadToolMode.Breakline);
        Assert.IsType<IdleState>(sm.CurrentState);
        
        sm.SetToolMode(CadToolMode.Inspecao);
        Assert.IsType<SelectionState>(sm.CurrentState);
    }

    [Fact]
    public void Cenário9_Inspecao_De_Elemento()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);
        sm.SetToolMode(CadToolMode.Inspecao);
        
        var noA = new KdNode(1.0, 1.0, 99);
        sm.HandleMouseDown(1.0, 1.0, noA);
        
        Assert.Contains("Vértice 99", context.UltimaInspecaoDescricao);
    }

    [Fact]
    public void Cenário10_Tentativa_De_Traçar_Breakline_Sem_Mdt_Deve_Bloquear_E_Notificar_Aviso()
    {
        var context = new StubCadCanvasContext { PodeTraçarBreaklines = false };
        var sm = new CadStateMachine(context, CadToolMode.Breakline);
        var noA = new KdNode(1.0, 2.0, 10);

        sm.HandleMouseDown(1.0, 2.0, noA);

        Assert.IsType<IdleState>(sm.CurrentState);
        Assert.NotNull(context.UltimoAvisoNotificado);
        Assert.Contains("compense a poligonal", context.UltimoAvisoNotificado);
    }

    [Fact]
    public void Cenário11_Estado_Inicial_Da_Maquina_Deve_Ser_Inspecao()
    {
        var context = new StubCadCanvasContext();
        var sm = new CadStateMachine(context);

        Assert.IsType<SelectionState>(sm.CurrentState);
    }

    [Fact]
    public void Cenário12_Inspecao_Vertice_Com_Nome_Topografico_Deve_Conter_Nome_Ponto()
    {
        var context = new StubCadCanvasContext();
        context.NomesVerticesMock[1] = "P1";
        var sm = new CadStateMachine(context);
        sm.SetToolMode(CadToolMode.Inspecao);

        var noP1 = new KdNode(722003.929, 7702667.278, 1);
        sm.HandleMouseDown(722003.929, 7702667.278, noP1);

        Assert.Contains("Ponto P1", context.UltimaInspecaoDescricao);
    }

    [Fact]
    public void Cenário13_Inspecao_Breakline_Com_Nomes_Topograficos_Deve_Identificar_Lances()
    {
        var context = new StubCadCanvasContext();
        context.NomesVerticesMock[0] = "E1";
        context.NomesVerticesMock[1] = "P1";

        var breaklines = new List<TopoGENTE.Domain.ValueObjects.Breakline>
        {
            new TopoGENTE.Domain.ValueObjects.Breakline(0, 1)
        };
        var vertexLookup = new Dictionary<int, TopoGENTE.Domain.ValueObjects.TerrainVertex>
        {
            [0] = new TopoGENTE.Domain.ValueObjects.TerrainVertex(722020.712, 7702635.787, 651.934, 0),
            [1] = new TopoGENTE.Domain.ValueObjects.TerrainVertex(722003.929, 7702667.278, 651.799, 1)
        };
        context.EdgeSpatialIndex = new BvhTree2D(breaklines, vertexLookup);

        var sm = new CadStateMachine(context);
        sm.SetToolMode(CadToolMode.Inspecao);

        // Clica próximo ao ponto médio do segmento E1->P1
        double midX = (722020.712 + 722003.929) / 2.0;
        double midY = (7702635.787 + 7702667.278) / 2.0;
        sm.HandleMouseDown(midX, midY, null);

        Assert.Contains("Breakline de E1 para P1", context.UltimaInspecaoDescricao);
    }
}
