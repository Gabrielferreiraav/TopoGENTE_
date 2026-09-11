using CommunityToolkit.Mvvm.Messaging;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;
using TopoGente.UI.Messages;

namespace TopoGente.UI.Commands;

/// <summary>
/// Comando reversível para inserção de um vértice isolado de projeto na malha CDT.
/// Armazena o TerrainVertex completo como Memento para permitir Undo idempotente.
/// </summary>
public class InsertVertexCommand : IUndoableCommand
{
    private readonly TerrainVertex _vertex;
    private readonly ITerrainTriangulator _triangulator;
    private readonly IMessenger _messenger;

    public InsertVertexCommand(
        TerrainVertex vertex,
        ITerrainTriangulator triangulator,
        IMessenger messenger)
    {
        _vertex = vertex;
        _triangulator = triangulator;
        _messenger = messenger;
    }

    public void Execute()
    {
        _triangulator.InsertVertex(_vertex);
        _messenger.Send(new TopologyChangedMessage());
    }

    public void Undo()
    {
        _triangulator.RemoveVertex(_vertex.Id);
        _messenger.Send(new TopologyChangedMessage());
    }
}
