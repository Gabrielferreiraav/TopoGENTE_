using System;
using CommunityToolkit.Mvvm.Messaging;
using TopoGENTE.Domain.Ports;
using TopoGENTE.Domain.ValueObjects;
using TopoGente.UI.Messages;

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
            WeakReferenceMessenger.Default.Send(new TopologyChangedMessage());
        }

        public void Undo()
        {
            _triangulator.RemoveConstraint(_breakline);
            WeakReferenceMessenger.Default.Send(new TopologyChangedMessage());
        }
    }
}
