using System;

namespace TopoGente.UI.ViewModels
{
    public class EstacaoCaminhamentoItemViewModel
    {
        public string NomeEstacao { get; init; } = string.Empty;
        public string NomeRe { get; init; } = string.Empty;
        public double AnguloHorizontalVante { get; init; }
        public string AnguloHorizontalVanteFormatado => AnguloHorizontalVante.ToString("F4");
        public string NomeVante { get; init; } = string.Empty;
        public double DistanciaVante { get; init; }
    }
}
