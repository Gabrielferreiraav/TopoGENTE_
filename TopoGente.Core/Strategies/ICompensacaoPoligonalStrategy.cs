using TopoGente.Core.Entities;

namespace TopoGente.Core.Strategies
{
    public interface ICompensacaoPoligonalStrategy
    {
        ResultadoCompensacaoDTO Compensar(CompensacaoPoligonalInputDTO entrada);
    }
}