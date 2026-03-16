using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;


namespace TopoGente.Core.Interfaces
{
    public interface ILeitorArquivo
    {
        string NomeFormato { get; }

        List<Estacao> Ler(string[] linhas);

    }

    public interface ILeituraArquivoFactory
    {
        ResultadoLeituraArquivo ProcessarArquivoComResultado(FormatoArquivoEntrada formato, string[] linhasArquivo);
        List<Estacao> ProcessarArquivo(FormatoArquivoEntrada formato, string[] linhasArquivo);

    }


    public interface ILevantamentoProcessor
    {
        ResultadoLevantamento Processar(MetadadosCenario metadados, List<LeituraEstacaoTotal> leituras, Dictionary<string, PontoCoordenada> pontosConhecidos);

    }

    public interface IArquivoProjetoService
    {
        void SalvarProjeto(ProjetoTopo projeto, string caminhoArquivo);
        ProjetoTopo CarregarProjeto(string caminhoArquivo);
    }

    public interface IOrganizarCaminhamento
    {
        List<Estacao> UnificarEstacoes(List<Estacao> todasEstacoes);
        List<LeituraEstacaoTotal> OrganizarPorVante(List<Estacao> todasEstacoes, string nomeEstacaoPartida);

    }

    public interface IExportadorDxfService
    {
        void SalvarDxf(List<PontoCoordenada> pontos, string caminhoArquivo);
    }

    public interface IQaCheckService
    {
        RelatorioQA GerarRelatorioQaChecks(
            List<Estacao> estacoesOrganizadas,
            ResultadoLevantamento resultado,
            Dictionary<string, PontoCoordenada> pontosConhecidos,
            double toleranciaDeltaXY = 0.01,
            double toleranciaDeltaZ = 0.02);

    }
}
