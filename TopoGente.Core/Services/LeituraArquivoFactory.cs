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
        public List<Estacao> ProcessarArquivo(string[] linhasArquivo)
        {
            if (linhasArquivo == null || linhasArquivo.Length == 0)
                throw new ArgumentException("O arquivo fornecido está vazio.");
            string cabecalho = string.Join("\n", linhasArquivo.Take(20));
            foreach (var leitor in _leitorArquivos)
            {
                if (leitor.IdentificarFormato(cabecalho))
                {
                    return leitor.Ler(linhasArquivo);
                }
            }
            throw new NotSupportedException("O formato do arquivo não é suportado por nenhum dos leitores disponíveis.");
        }
    }
}
