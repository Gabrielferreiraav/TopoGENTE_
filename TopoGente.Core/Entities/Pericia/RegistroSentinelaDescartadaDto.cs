namespace TopoGente.Core.Entities.Pericia
{
    /// <summary>
    /// Registro imutável de linha sentinela, resíduo de transmissão ou observação nula descartada pelo motor.
    /// </summary>
    public sealed record RegistroSentinelaDescartadaDto
    {
        public int LinhaArquivo { get; init; }
        public string ConteudoBruto { get; init; } = string.Empty;
        public string MotivoDescarte { get; init; } = string.Empty;
    }
}
