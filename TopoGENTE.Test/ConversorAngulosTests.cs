using Xunit;
using TopoGente.Core.Utilities;

namespace TopoGente.Tests
{
    public class ConversorAngulosTests
    {
        [Fact]
        public void GMS_Para_Decimal_DeveFuncionar()
        {
            // 30 graus e 30 minutos é EXATAMENTE 30.5 graus
            double resultado = ConversorAngulos.ParaDecimal(30, 30, 0);
            Assert.Equal(30.5, resultado);
        }

        [Fact]
        public void Decimal_Para_GMS_DeveFuncionar()
        {
            // 45.25 graus deve ser 45° 15' 00"
            var (g, m, s) = ConversorAngulos.ParaGMS(45.25);

            Assert.Equal(45, g);
            Assert.Equal(15, m);
            Assert.Equal(0, s);
        }

        [Fact]
        public void Deve_Arredondar_Segundos_Corretamente()
        {
            // Um caso chato de dízima periódica
            // 10° 00' 00.001"
            double valor = 10.0 + (0.001 / 3600.0);
            var (g, m, s) = ConversorAngulos.ParaGMS(valor);

            Assert.Equal(10, g);
            Assert.Equal(0, m);
            Assert.Equal(0.001, s);
        }
    }
}