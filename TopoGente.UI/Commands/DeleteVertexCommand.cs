using CommunityToolkit.Mvvm.Messaging;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;
using TopoGente.UI.Messages;

namespace TopoGente.UI.Commands;

/// <summary>
/// Comando reversível para exclusão de um vértice livre da malha CDT.
/// O adaptador lançará TopologicalInvarianceException se o vértice ancorar breaklines ativas
/// (Block Delete), impedindo a execução do comando antes mesmo de empilhá-lo no Undo stack.
/// Armazena o TerrainVertex completo como Memento para permitir restauração no Undo.
/// </summary>
public class DeleteVertexCommand : IUndoableCommand
{
    private readonly TerrainVertex _mementoVertex;
    private readonly ITerrainTriangulator _triangulator;
    private readonly IMessenger _messenger;

    public DeleteVertexCommand(
        TerrainVertex vertex,
        ITerrainTriangulator triangulator,
        IMessenger messenger)
    {
        _mementoVertex = vertex;
        _triangulator = triangulator;
        _messenger = messenger;
    }

    /// <summary>
    /// Remove o vértice da malha. Se o vértice ancorar breaklines ativas,
    /// o adaptador lançará TopologicalInvarianceException e este comando
    /// NÃO será empilhado no _undoStack (o chamador deve tratar a exceção).
    /// </summary>
    public void Execute()
    {
        _triangulator.RemoveVertex(_mementoVertex.Id);
        _messenger.Send(new TopologyChangedMessage());
    }

    /// <summary>
    /// Restaura o vértice na malha a partir do Memento armazenado.
    /// </summary>
    public void Undo()
    {
        _triangulator.InsertVertex(_mementoVertex);
        _messenger.Send(new TopologyChangedMessage());
    }
}
