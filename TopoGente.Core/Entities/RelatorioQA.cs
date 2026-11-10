using System;
using System.Collections.Generic;
using TopoGente.Core.Entities.Pericia;

namespace TopoGente.Core.Entities
{
    public class RelatorioQA
    {
        public DateTime GeradoEm { get; set; } = DateTime.Now;

        public double ToleranciaCheckDeltaXY { get; set; } = 0.01;
        public double ToleranciaCheckDeltaZ { get; set; } = 0.02;

        public List<EventoQACheck> Checks { get; set; } = new();

        /// <summary>
        /// Trilha pericial completa da auditoria topológica, lances geométricos e vetor de ajustamento.
        /// </summary>
        public MemorialRastreabilidadePericial? MemorialRastreabilidade { get; set; }

        public double ErroAngular { get; set; }
        public double ErroLinear { get; set; }
        public double ErroAltimetrico { get; set; }

        public ParametrosTolerancia ToleranciasAplicadas { get; set; } = ParametrosTolerancia.Padrao;

        public double ToleranciaAngularAdmissivel { get; set; }
        public double ToleranciaAngularSegundos { get; set; }
        public double ToleranciaLinearAdmissivel { get; set; }
        public double ToleranciaLinearAbsolutaMetros { get; set; }
        public double ToleranciaLinearRelativaMetros { get; set; }
        public double PrecisaoLinearObtidaDenominador { get; set; }
        public double ToleranciaAltimetricaAdmissivel { get; set; }

        public bool AngularConforme { get; set; } = true;
        public bool AprovadoRelativo { get; set; } = true;
        public bool AprovadoAbsoluto { get; set; } = true;
        public bool AprovadoLinearGeral { get; set; } = true;

        /// <summary>Alias para AprovadoLinearGeral para compatibilidade com código existente.</summary>
        public bool LinearConforme
        {
            get => AprovadoLinearGeral;
            set => AprovadoLinearGeral = value;
        }

        public ModoAvaliacaoLinearEnum ModoAvaliacaoLinearUtilizado { get; set; } = ModoAvaliacaoLinearEnum.AmbosRigorosoNBR;
        public bool AltimetricoConforme { get; set; } = true;

        public bool TodosChecksConformes =>
            (Checks == null || Checks.TrueForAll(c => !c.ExcedeuDeltaXY && !c.ExcedeuDeltaZ)) &&
            AngularConforme &&
            AprovadoLinearGeral &&
            AltimetricoConforme;
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
