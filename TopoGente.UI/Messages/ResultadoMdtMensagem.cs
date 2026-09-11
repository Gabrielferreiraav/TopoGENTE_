using CommunityToolkit.Mvvm.Messaging.Messages;
using TopoGente.Core.Entities;

namespace TopoGente.UI.Messages
{
    /// <summary>
    /// Mensagem fortemente tipada para desacoplar a notificação de recálculo topológico 
    /// do barramento síncrono IUiEventHub.
    /// </summary>
    public class ResultadoMdtMensagem : ValueChangedMessage<ResultadoLevantamento>
    {
        public ResultadoMdtMensagem(ResultadoLevantamento resultado) : base(resultado)
        {
        }
    }
}
