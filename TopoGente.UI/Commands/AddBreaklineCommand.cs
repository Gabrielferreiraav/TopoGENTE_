using System;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.UI.Commands
{
    public class AddBreaklineCommand : IUndoableCommand
    {
        private readonly ITerrainTriangulator _triangulator;
        private readonly Breakline _breakline;

        public AddBreaklineCommand(ITerrainTriangulator triangulator, Breakline breakline)
        {
            _triangulator = triangulator;
            _breakline = breakline;
        }

        public void Execute()
        {
            _triangulator.AddConstraint(_breakline);
        }

        public void Undo()
        {
            _triangulator.RemoveConstraint(_breakline);
        }
    }
}
