namespace TopoGente.Core.Entities
{
    public class PontoCoordenada
    {
        public string Nome { get; set; } = string.Empty;

        // Coordenadas Locais ou UTM
        public double X { get; set; } // Leste (E)
        public double Y { get; set; } // Norte (N)
        public double Z { get; set; } // Cota/Elevação

        // Útil para saber se é um ponto de poligonal (fixo) ou irradiado
        public bool EhPontoPoligonal { get; set; }

        // Propriedade para reter a semântica da caderneta de campo
        public string Descricao { get; set; } = string.Empty;

        public double AzimuteChegada { get; set; }

        public string TipoDescricao
        {
            get
            {
                if (EhPontoPoligonal) return "Poligonal (Vante)";
                return "Irradiação";
            }
        }
        public override string ToString()
        {
            return $"{Nome}: X={X:F3}, Y={Y:F3}, Z={Z:F3}";
        }
    }
}