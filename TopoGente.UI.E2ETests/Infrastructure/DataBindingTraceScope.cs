using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TopoGente.UI.E2ETests.Infrastructure;

/// <summary>
/// Escopo determinístico (IDisposable) que anexa um TraceListener ao
/// PresentationTraceSources.DataBindingSource e restaura o nível e listeners originais ao finalizar.
/// </summary>
internal sealed class DataBindingTraceScope : IDisposable
{
    private readonly TraceSource _dataBindingSource;
    private readonly SourceLevels _previousLevel;
    private readonly CapturingTraceListener _listener;

    public IReadOnlyList<string> Messages => _listener.Messages;

    public DataBindingTraceScope(SourceLevels level = SourceLevels.Warning)
    {
        PresentationTraceSources.Refresh();
        _dataBindingSource = PresentationTraceSources.DataBindingSource;
        _previousLevel = _dataBindingSource.Switch.Level;
        _listener = new CapturingTraceListener();

        _dataBindingSource.Switch.Level = level;
        _dataBindingSource.Listeners.Add(_listener);
    }

    public void Dispose()
    {
        _dataBindingSource.Listeners.Remove(_listener);
        _dataBindingSource.Switch.Level = _previousLevel;
        _listener.Dispose();
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        public List<string> Messages { get; } = new();

        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Add(message);
            }
        }
    }
}
