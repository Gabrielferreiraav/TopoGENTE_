using System;
using System.Collections.Generic;
using System.Text;

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

    }
}
