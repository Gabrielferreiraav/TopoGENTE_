using System;
using TopoGente.Core.Entities;

namespace TopoGente.Core.Strategies
{
    public sealed class CompensacaoStrategyFactory
    {
        public ICompensacaoPoligonalStrategy Criar(TipoCenarioPoligonal tipoCenario)
            => tipoCenario switch
            {
                TipoCenarioPoligonal.Enquadrada => new CompensacaoEnquadradaStrategy(),
                TipoCenarioPoligonal.Fechada => new CompensacaoFechadaStrategy(),
                _ => throw new NotSupportedException($"Cenário '{tipoCenario}' não suportado.")
            };
    }
}