using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public class RelatorioQA
    {
        public DateTime GeradoEm { get; set; } = DateTime.Now;

        public double ToleranciaCheckDeltaXY { get; set; } = 0.01;
        public double ToleranciaCheckDeltaZ { get; set; } = 0.02;

        public List<EventoQACheck> Checks { get; set; } = new();
    }

    public class EventoQACheck
    {
        // Chave lógica (setupID + targetPoint + timeStamp (se existir))
        public string SetupId { get; set; } = string.Empty;
        public string TargetPoint { get; set; } = string.Empty;
        public DateTime? TimeStamp { get; set; }

        public string EstacaoOcupada { get; set; } = string.Empty;

        public double? DeltaXY { get; set; }
        public double? DeltaZ { get; set; }

        public bool ExcedeuDeltaXY { get; set; }
        public bool ExcedeuDeltaZ { get; set; }

        public string Mensagem { get; set; } = string.Empty;
    }
}