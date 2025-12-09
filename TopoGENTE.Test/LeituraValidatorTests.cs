using TopoGente.Core.Entities;
using TopoGente.Core.Validators;
using Xunit;

namespace TopoGente.Test
{
    public class LeituraValidatorTests
    {
        [Fact]
        public void LeituraValida_DeveRetornarSucesso()
        {
            var leitura = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "E1",
                PontoVisado = "P1",
                AnguloHorizontal = 45.5,
                AnguloVertical = 90.0,
                DistanciaInclinada = 120.5,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.6
            };

            var resultado = LeituraValidator.Validar(leitura);

            Assert.True(resultado.IsValid);
            Assert.Empty(resultado.Errors);
        }

        [Fact]
        public void LeituraComDistanciaNegativa_DeveFalhar()
        {
            var leitura = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "E1",
                PontoVisado = "P1",
                DistanciaInclinada = -10.0, // Erro aqui
                AnguloHorizontal = 100,
                AnguloVertical = 90,
                AlturaInstrumento = 1.5,
                AlturaPrisma = 1.6
            };

            var resultado = LeituraValidator.Validar(leitura);

            Assert.False(resultado.IsValid);
            Assert.Contains("A distância inclinada deve ser maior que zero.", resultado.Errors);
        }
    }
}