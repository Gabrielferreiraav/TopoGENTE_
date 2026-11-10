namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Papel funcional e topológico desempenhado por um nó ou ponto no levantamento.
    /// </summary>
    public enum PapelTopologicoEnum
    {
        /// <summary>Vértice inicial com coordenadas conhecidas e visada de ré de amarração.</summary>
        EstacaoPartida,

        /// <summary>Vértice fixo ou referencial de orientação angular.</summary>
        ReReferencia,

        /// <summary>Vértice intermediário de apoio na poligonal.</summary>
        VerticeIntermediario,

        /// <summary>Estação ou visada que encerra a poligonal fechada ou amarra na chegada de poligonal enquadrada.</summary>
        FechamentoAnel,

        /// <summary>Ponto irradiado repetido/revisado auditado como controle pericial de qualidade.</summary>
        PontoHomologoCheck,

        /// <summary>Ponto irradiado cadastral de detalhe do terreno.</summary>
        IrradiacaoCadastral,

        /// <summary>Linha de resíduo de formatação ou leitura nula descartada pelo motor.</summary>
        SentinelaDescartada
    }
}
