using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Interfaces
{
    public interface ILeitorArquivo
    {
        string NomeFormato { get; }

        bool IdentificarFormato(string cabecalhoArquivo);

        List<Estacao> Ler(string[] linhas);

    }
}
