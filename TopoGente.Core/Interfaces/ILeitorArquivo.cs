using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Interfaces
{
    public interface ILeitorArquivo
    {
        string NomeFormato { get; }

        List<Estacao> Ler(string[] linhas);

    }
}
