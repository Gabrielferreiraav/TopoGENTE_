using CommunityToolkit.Mvvm.Messaging;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;
using TopoGente.UI.Messages;

namespace TopoGente.UI.Commands;

/// <summary>
/// Comando reversível para exclusão de uma breakline da malha CDT.
/// Implementa o Padrão Memento: armazena cópias profundas dos vértices de extremidade
/// para mitigar o Paradoxo do Undo Órfão (caso os vértices sejam deletados entre 
/// o Execute e o Undo, o comando os reinsere automaticamente antes de recriar a restrição).
/// </summary>
public class DeleteBreaklineCommand : IUndoableCommand
{
    private readonly Breakline _breakline;
    private readonly ITerrainTriangulator _triangulator;
    private readonly IMessenger _messenger;

    // Memento: cópias profundas dos vértices de extremidade no instante da deleção.
    // TerrainVertex é readonly record struct — a atribuição é uma cópia por valor.
    private readonly TerrainVertex _mementoStart;
    private readonly TerrainVertex _mementoEnd;

    public DeleteBreaklineCommand(
        Breakline breakline,
        TerrainVertex startVertex,
        TerrainVertex endVertex,
        ITerrainTriangulator triangulator,
        IMessenger messenger)
    {
        _breakline = breakline;
        _mementoStart = startVertex;
        _mementoEnd = endVertex;
        _triangulator = triangulator;
        _messenger = messenger;
    }

    public void Execute()
    {
        _triangulator.RemoveConstraint(_breakline);
        _messenger.Send(new TopologyChangedMessage());
    }

    /// <summary>
    /// Desfaz a deleção da breakline.
    /// Antes de reinserir a restrição, verifica se os vértices de extremidade ainda existem
    /// na malha. Se algum tiver sido removido (Paradoxo do Undo Órfão), reinsere-o 
    /// a partir do Memento armazenado para evitar falha do motor Delaunay.
    /// </summary>
    public void Undo()
    {
        // Upsert dos vértices de extremidade via Memento
        // InsertVertex no adaptador faz _domainVertices[id] = vertex (idempotente se já existir)
        _triangulator.InsertVertex(_mementoStart);
        _triangulator.InsertVertex(_mementoEnd);

        _triangulator.AddConstraint(_breakline);
        _messenger.Send(new TopologyChangedMessage());
    }
}
