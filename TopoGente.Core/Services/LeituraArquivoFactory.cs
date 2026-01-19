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
                new LeitorCsvPadrao()
            };
        }
        public List<Estacao> ProcessarArquivo(FormatoArquivoEntrada formato,string[] linhasArquivo)
        {
            if (linhasArquivo == null || linhasArquivo.Length == 0)
                throw new ArgumentException("O arquivo fornecido está vazio.");


            ILeitorArquivo? leitor = formato switch
            {
                FormatoArquivoEntrada.Fbk => _leitorArquivos.OfType<LeitorFbk>().FirstOrDefault(),
                FormatoArquivoEntrada.CsvPadrao => _leitorArquivos.OfType<LeitorCsvPadrao>().FirstOrDefault(),
                _ => null
            };
            return leitor.Ler(linhasArquivo);
        }
    }
}
