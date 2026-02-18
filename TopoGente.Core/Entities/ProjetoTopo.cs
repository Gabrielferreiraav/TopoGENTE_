using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public class ProjetoTopo
    {
        public string Versao { get; set; } = "1.0";
        public DateTime DataSalvamento { get; set; } = DateTime.Now;

        public double StartX { get; set; }
        public double StartY { get; set; }
        public double StartZ { get; set; }
        public double StartAzimute { get; set; }
        public List<Estacao> Estacoes { get; set; } = new List<Estacao>();

        public RelatorioQA? RelatorioQA { get; set; }

        /// <summary>Metadados do cenário escolhido pelo usuário.</summary>
        public MetadadosCenario? Metadados { get; set; }
    }
}
