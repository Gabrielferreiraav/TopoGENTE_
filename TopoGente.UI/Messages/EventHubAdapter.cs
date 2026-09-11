using CommunityToolkit.Mvvm.Messaging;
using TopoGente.UI.Eventing;

namespace TopoGente.UI.Messages
{
    /// <summary>
    /// Adaptador temporário para garantir retrocompatibilidade entre o barramento antigo (IUiEventHub)
    /// e a nova arquitetura do Dashboard baseada em WeakReferenceMessenger.
    /// Impede a quebra de janelas legadas como DiagnosticoErrosWindow.
    /// </summary>
    public class EventHubAdapter
    {
        private readonly IUiEventHub _eventHub;

        public EventHubAdapter(IUiEventHub eventHub)
        {
            _eventHub = eventHub;
            _eventHub.ResultadoAtualizado += OnResultadoAtualizado;
        }

        private void OnResultadoAtualizado(object? sender, ResultadoEventArgs e)
        {
            // Traduz a notificação fortemente acoplada para uma mensagem fraca pub/sub
            WeakReferenceMessenger.Default.Send(new ResultadoMdtMensagem(e.Resultado));
        }

        public void Detach()
        {
            _eventHub.ResultadoAtualizado -= OnResultadoAtualizado;
        }
    }
}
