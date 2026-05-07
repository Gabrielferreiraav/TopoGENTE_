using TopoGente.Core.Interfaces;

namespace TopoGente.Core.Entities
{
    public class LeituraEstacaoTotal : IGrafoElement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string? SetupId { get; set; }
        public DateTime? TimeStamp { get; set; }

        public string EstacaoOcupada { get; set; } = string.Empty;
        public string PontoVisado { get; set; } = string.Empty;
        public double AnguloHorizontal { get; set; }
        public double AnguloVertical { get; set; }
        public double DistanciaInclinada { get; set; }

        public double AlturaInstrumento { get; set; }
        public double AlturaPrisma { get; set; }

        public string Observacao { get; set; } = string.Empty;
        public TipoLeitura Tipo { get; set; } = TipoLeitura.Irradiacao;
        public bool EhLeituraDePoligonal => Tipo == TipoLeitura.Poligonal;
        public string? Purpose { get; set; }

        public int OrdemArquivo { get; set; }

        public void Accept(ITopografiaVisitor visitor)
        {
            visitor.VisitarLeitura(this);
        }
    }
}