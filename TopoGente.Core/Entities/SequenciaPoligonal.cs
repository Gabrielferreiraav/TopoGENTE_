using System;
using System.Collections.Generic;

namespace TopoGente.Core.Entities
{
    public class SequenciaPoligonal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Nome { get; set; } = string.Empty; // Ex: "Poligonal Principal", "Ramal Secundário 1"
        
        /// <summary>
        /// Flag que indica se esta é a espinha dorsal da malha (1ª Ordem/Base) 
        /// ou um ramal derivado (Secundária/Auxiliar).
        /// </summary>
        public bool EhPrincipal { get; set; } = false;

        /// <summary>
        /// Nome do nó de ancoragem na Poligonal Principal de onde esta secundária se origina.
        /// Requerido para Poligonais Secundárias.
        /// </summary>
        public string? EstacaoAncoragemNome { get; set; }

        /// <summary>
        /// Metadados individuais de partida, orientação, azimute e chegada específicos deste caminhamento.
        /// </summary>
        public MetadadosCenario Metadados { get; set; } = new();

        /// <summary>
        /// Lista ordenada das estações que compõem este caminhamento específico.
        /// </summary>
        public List<string> Estacoes { get; set; } = new();
    }
}
