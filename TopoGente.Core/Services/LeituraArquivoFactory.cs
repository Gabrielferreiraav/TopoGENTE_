using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Entities;
using TopoGente.Core.Services.Leitores;

namespace TopoGente.Core.Services
{
    public class LeituraArquivoFactory
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

        public ResultadoLeituraArquivo ProcessarArquivoComResultado(FormatoArquivoEntrada formato, string[] linhasArquivo)
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

            var estacoes = leitor.Ler(linhasArquivo);

            var avisos = new List<string>();

            if (leitor is LeitorLandXml landXml)
            {
                avisos.AddRange(landXml.UltimosAvisos);
            }

            return new ResultadoLeituraArquivo
            {
                Estacoes = estacoes,
                Avisos = avisos
            };
        }
        public List<Estacao> ProcessarArquivo(FormatoArquivoEntrada formato,string[] linhasArquivo)
        => ProcessarArquivoComResultado(formato, linhasArquivo).Estacoes;
    }
}
