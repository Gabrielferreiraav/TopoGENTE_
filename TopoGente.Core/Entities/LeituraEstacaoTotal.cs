namespace TopoGente.Core.Entities
{
    public class LeituraEstacaoTotal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Dados de Identificação
        public string EstacaoOcupada { get; set; } = string.Empty;
        public string PontoVisado { get; set; } = string.Empty;

        // Estamos assumindo que a UI converterá GMS para Decimal antes de salvar aqui,
        // ou faremos isso na entrada.
        public double AnguloHorizontal { get; set; }
        public double AnguloVertical { get; set; }
        public double DistanciaInclinada { get; set; }

        // Dados do Aparelho
        public double AlturaInstrumento { get; set; } // Hi
        public double AlturaPrisma { get; set; }      // Hp

        // Metadados
        public DateTime DataLeitura { get; set; } = DateTime.Now;
        public string Observacao { get; set; } = string.Empty;
        public TipoLeitura Tipo { get; set; } = TipoLeitura.Irradiacao;
        public bool EhLeituraDePoligonal => Tipo == TipoLeitura.Poligonal;
    }
}