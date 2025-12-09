using System;
using System.Collections.Generic;
using TopoGente.Core.Entities; // Ajustado para o nome do seu projeto

namespace TopoGente.Core.Validators
{
    public static class LeituraValidator
    {
        public static ValidationResult Validar(LeituraEstacaoTotal leitura)
        {
            var result = new ValidationResult();

            // Validação de Identificação
            if (string.IsNullOrWhiteSpace(leitura.EstacaoOcupada))
                result.AddError("A estação ocupada deve ser informada.");

            if (string.IsNullOrWhiteSpace(leitura.PontoVisado))
                result.AddError("O ponto visado deve ser informado.");

            if (leitura.EstacaoOcupada == leitura.PontoVisado)
                result.AddError("A estação e o ponto visado não podem ser os mesmos.");

            // Validação de Ângulos
            if (leitura.AnguloHorizontal < 0 || leitura.AnguloHorizontal >= 360)
                result.AddError("O ângulo horizontal deve estar entre 0° e 360°.");

            if (leitura.AnguloVertical < 0 || leitura.AnguloVertical >= 360)
                result.AddError("O ângulo vertical deve estar entre 0° e 360°.");

            // Validação de Distâncias
            if (leitura.DistanciaInclinada <= 0)
                result.AddError("A distância inclinada deve ser maior que zero.");

            // Validação de Alturas
            if (leitura.AlturaInstrumento < 0 || leitura.AlturaInstrumento > 2.5)
                result.AddError("A altura do instrumento parece incorreta.");

            if (leitura.AlturaPrisma < 0 || leitura.AlturaPrisma > 5.0)
                result.AddError("A altura do prisma parece incorreta.");

            return result;
        }
    }
}