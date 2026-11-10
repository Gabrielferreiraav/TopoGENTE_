using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Core.Interfaces
{
    public interface ILeitorArquivo
    {
        string NomeFormato { get; }
        List<Estacao> Ler(IEnumerable<string> linhas);
        List<Estacao> Ler(IEnumerable<string> linhas, ConfiguracaoImportacao config) => Ler(linhas);
        IReadOnlyDictionary<string, PontoCoordenada> UltimosPontosConhecidos { get; }
        IReadOnlyList<string> UltimosAvisos => Array.Empty<string>();
        IReadOnlyList<PontoCoordenada> UltimosPontosImportados => Array.Empty<PontoCoordenada>();
        IReadOnlyList<RegistroSentinelaDescartadaDto> UltimasSentinelasDescartadas => Array.Empty<RegistroSentinelaDescartadaDto>();
    }

    public interface ILeituraArquivoFactory
    {
        ResultadoLeituraArquivo ProcessarArquivoComResultado(FormatoArquivoEntrada formato, string[] linhasArquivo, ConfiguracaoImportacao? config = null);
        List<Estacao> ProcessarArquivo(FormatoArquivoEntrada formato, string[] linhasArquivo, ConfiguracaoImportacao? config = null);
    }

    public interface ILevantamentoProcessor
    {
        ResultadoLevantamento Processar(List<SequenciaPoligonal> sequencias, List<Estacao> todasEstacoes, Dictionary<string, PontoCoordenada> pontosConhecidos);
        ResultadoLevantamento GerarEsbocoBruto(List<SequenciaPoligonal> sequencias, List<Estacao> todasEstacoes);
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
        TrilhaTopologicaDto ConstruirTrilhaTopologica(
            List<Estacao> estacoesBrutas,
            List<Estacao> estacoesUnificadas,
            MetadadosCenario? metadados,
            IReadOnlyDictionary<string, PontoCoordenada>? pontosConhecidos,
            IReadOnlyList<RegistroSentinelaDescartadaDto>? sentinelas);
    }

    public interface IExportadorDxfService
    {
        void SalvarDxf(DxfExportPayload payload, string caminhoArquivo);
    }

    public interface IExportarTxtService
    {
        void ExportarCoordenadasGestor(ResultadoLevantamento resultado, string caminhoArquivo);
        void ExportarMemoriaCalculo(ResultadoLevantamento resultado, string caminhoArquivo);
    }

    public interface IQaCheckService
    {
        RelatorioQA GerarRelatorioQaChecks(
            List<Estacao> estacoesOrganizadas,
            ResultadoLevantamento resultado,
            Dictionary<string, PontoCoordenada> pontosConhecidos,
            double toleranciaDeltaXY = 0.01,
            double toleranciaDeltaZ = 0.02);

        RelatorioQA GerarRelatorioQaChecks(
            List<Estacao> estacoesOrganizadas,
            ResultadoLevantamento resultado,
            Dictionary<string, PontoCoordenada> pontosConhecidos,
            ParametrosTolerancia tolerancias)
            => GerarRelatorioQaChecks(estacoesOrganizadas, resultado, pontosConhecidos, tolerancias.ToleranciaCheckDeltaXY, tolerancias.ToleranciaCheckDeltaZ);
    }

    public interface IFormatadorMemorialPericialService
    {
        string FormatarCapitulo1TrilhaTopologica(TrilhaTopologicaDto trilha);
        string FormatarCapitulo2MemorialLances(MemorialLancesDto lances);
        string FormatarCapitulo3Ajustamento(MemoriaAjustamentoDto ajustamento);
        string FormatarMemorialCompleto(MemorialRastreabilidadePericial memorial);
    }

    public interface ITopografiaVisitor
    {
        void VisitarEstacao(Estacao estacao);
        void VisitarLeitura(LeituraEstacaoTotal leitura);
    }

    public interface IGrafoElement
    {
        void Accept(ITopografiaVisitor visitor);
    }

    public interface ITopografiaIterator
    {
        void First();
        void Next();
        bool IsDone();
        Estacao CurrentItem();
    }

    public interface IClassificadorGrafo 
    {
        void ClassificarArestasGrafo(List<Estacao> todasEstacoes, MetadadosCenario metadados);
    }
}
