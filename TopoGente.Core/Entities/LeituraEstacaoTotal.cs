using TopoGente.Core.Interfaces;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TopoGente.Infrastructure")]
[assembly: InternalsVisibleTo("TopoGENTE.Test")]

namespace TopoGente.Core.Entities
{
    public class LeituraEstacaoTotal : IGrafoElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string? SetupId { get; set; }
        public DateTime? TimeStamp { get; set; }

        public string EstacaoOcupada { get; init; } = string.Empty;
        public string PontoVisado { get; init; } = string.Empty;
        public double AnguloHorizontal { get; init; }
        public double AnguloVertical { get; init; }
        public double DistanciaInclinada { get; init; }

        public double AlturaInstrumento { get; init; }
        public double AlturaPrisma { get; init; }

        public string Observacao { get; init; } = string.Empty;
        public TipoLeitura Tipo { get; internal set; } = TipoLeitura.Irradiacao;
        public bool EhLeituraDePoligonal => Tipo == TipoLeitura.Poligonal;
        public string? Purpose { get; internal set; }

        public int OrdemArquivo { get; init; }

        public void Accept(ITopografiaVisitor visitor)
        {
            visitor.VisitarLeitura(this);
        }
    }
}