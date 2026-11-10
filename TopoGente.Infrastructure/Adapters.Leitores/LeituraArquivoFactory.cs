using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Infrastructure.Adapters.Leitores
{
    public class LeituraArquivoFactory : ILeituraArquivoFactory
    {
        private readonly List<ILeitorArquivo> _leitorArquivos;

        public LeituraArquivoFactory()
        {
            _leitorArquivos = new List<ILeitorArquivo>
            {
                new LeitorFbk(),
                new LeitorCsvPadrao(),
                new LeitorLandXml()
            };
        }

        public ResultadoLeituraArquivo ProcessarArquivoComResultado(
            FormatoArquivoEntrada formato,
            string[] linhasArquivo,
            ConfiguracaoImportacao? config = null)
        {
            if (linhasArquivo == null || linhasArquivo.Length == 0)
            {
                throw new ArgumentException("O arquivo fornecido está vazio");
            }

            ILeitorArquivo? leitor = formato switch
            {
                FormatoArquivoEntrada.Fbk => _leitorArquivos.OfType<LeitorFbk>().FirstOrDefault(),
                FormatoArquivoEntrada.CsvPadrao => _leitorArquivos.OfType<LeitorCsvPadrao>().FirstOrDefault(),
                FormatoArquivoEntrada.LandXml => _leitorArquivos.OfType<LeitorLandXml>().FirstOrDefault(),
                _ => null
            };

            if (leitor == null)
                throw new NotSupportedException($"O formato de arquivo '{formato}' não é suportado.");

            var estacoes = config != null
                ? leitor.Ler(linhasArquivo, config)
                : leitor.Ler(linhasArquivo);

            var avisos = new List<string>(leitor.UltimosAvisos);

            return new ResultadoLeituraArquivo
            {
                Estacoes = estacoes,
                Avisos = avisos,
                PontosConhecidosGlobais = leitor.UltimosPontosConhecidos,
                PontosImportados = leitor.UltimosPontosImportados.Count > 0 ? leitor.UltimosPontosImportados : null,
                SentinelasDescartadas = leitor.UltimasSentinelasDescartadas
            };
        }

        public List<Estacao> ProcessarArquivo(
            FormatoArquivoEntrada formato,
            string[] linhasArquivo,
            ConfiguracaoImportacao? config = null)
            => ProcessarArquivoComResultado(formato, linhasArquivo, config).Estacoes;
    }
}
