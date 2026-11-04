using TopoGente.Core.Entities;

namespace TopoGente.UI.Services
{
    public interface IDialogService
    {
        string? SelecionarArquivoAbertura(string filtro, string titulo);
        string? SelecionarArquivoSalvamento(string filtro, string titulo, string nomePadrao, string extensaoPadrao);
        ConfiguracaoImportacao? ConfigurarImportacao(string caminhoArquivo, string[] linhas, FormatoArquivoEntrada formato) => null;
    }

    public interface IMessageService
    {
        void MostrarErro(string mensagem, string titulo);
        void MostrarAviso(string mensagem, string titulo);
        void MostrarSucesso(string mensagem, string titulo);
    }

    public interface IFileService
    {
        string[] LerLinhas(string caminho);
    }
}
