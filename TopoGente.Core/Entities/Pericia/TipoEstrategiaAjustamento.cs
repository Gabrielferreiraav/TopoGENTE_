namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Identifica a metodologia matemática aplicada para o cálculo ou compensação da poligonal.
    /// </summary>
    public enum TipoEstrategiaAjustamento
    {
        /// <summary>Nenhum ajustamento aplicado. Coordenadas brutas obtidas puramente por transporte analítico (poligonal aberta, levantamento reprovado na NBR 13.133 ou pré-cálculo).</summary>
        SemAjustamento,

        /// <summary>Compensação proporcional clássica de Bowditch para poligonais fechadas.</summary>
        Bowditch,

        /// <summary>Compensação proporcional de Bowditch para poligonais enquadradas entre dois marcos conhecidos.</summary>
        PoligonalEnquadrada,

        /// <summary>Transporte analítico puro sem compensação proporcional, confrontado com vértice de controle geodésico.</summary>
        VerificacaoLinhaBase
    }
}
