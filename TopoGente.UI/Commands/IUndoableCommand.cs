namespace TopoGente.UI.Commands;

public interface IUndoableCommand
{
    void Execute();
    void Undo();
}
