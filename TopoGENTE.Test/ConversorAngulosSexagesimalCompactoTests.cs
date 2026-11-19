using System;
using FluentAssertions;
using TopoGente.Core.Utilities;
using Xunit;

namespace TopoGENTE.Test
{
    public class ConversorAngulosSexagesimalCompactoTests
    {
        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(90.0000, 90.0)]
        [InlineData(180.3000, 180.5)]
        [InlineData(45.1530, 45.258333)]
        [InlineData(359.5959, 359.999722)]
        public void DeFormatoCompacto_ValoresValidos_ConverteComPrecisaoAnalitica(double gggmmss, double grausDecimaisEsperado)
        {
            double resultado = ConversorAngulos.DeFormatoCompacto(gggmmss);

            resultado.Should().BeApproximately(grausDecimaisEsperado, 1e-4);
        }

        [Theory]
        [InlineData(100.6000, "Minutos inválidos (60 >= 60)")]
        [InlineData(154.8777, "Minutos inválidos (87 >= 60)")]
        [InlineData(45.5960, "Segundos inválidos")]
        [InlineData(12.3099, "Segundos inválidos (99")]
        public void DeFormatoCompacto_MinutosOuSegundosInvalidos_LancaArgumentOutOfRangeException(double gggmmss, string trechoErroEsperado)
        {
            Action act = () => ConversorAngulos.DeFormatoCompacto(gggmmss);

            act.Should().Throw<ArgumentOutOfRangeException>()
               .WithMessage($"*{trechoErroEsperado}*");
        }

        [Theory]
        [InlineData(154.3045, true, 154.5125, null)]
        [InlineData(154.8777, false, 0.0, "Minutos inválidos (87 >= 60)")]
        [InlineData(12.5959, true, 12.999722, null)]
        [InlineData(12.5965, false, 0.0, "Segundos inválidos")]
        public void TryDeFormatoCompacto_ValidaCorretamente(double gggmmss, bool esperadoValido, double grausEsperado, string? erroEsperado)
        {
            bool sucesso = ConversorAngulos.TryDeFormatoCompacto(gggmmss, out double grausDecimais, out string? erro);

            sucesso.Should().Be(esperadoValido);
            if (esperadoValido)
            {
                grausDecimais.Should().BeApproximately(grausEsperado, 1e-4);
                erro.Should().BeNull();
            }
            else
            {
                erro.Should().NotBeNull();
                erro.Should().Contain(erroEsperado);
            }
        }

        [Theory]
        [InlineData("154.3045", true, "154° 30' 45.00\"", 154.5125)]
        [InlineData("154,3045", true, "154° 30' 45.00\"", 154.5125)]
        [InlineData("0", true, "0° 00' 00.00\"", 0.0)]
        [InlineData("", true, "0° 00' 00.00\"", 0.0)]
        [InlineData("   ", true, "0° 00' 00.00\"", 0.0)]
        [InlineData("154.8777", false, "", 0.0)]
        [InlineData("invalido", false, "", 0.0)]
        public void TryDeFormatoCompactoTexto_ProcessaEntradasDeUsuarioComDidatica(string entrada, bool esperadoValido, string formatadoEsperado, double grausEsperado)
        {
            bool sucesso = ConversorAngulos.TryDeFormatoCompactoTexto(entrada, out double grausDecimais, out string formatado, out string? erro);

            sucesso.Should().Be(esperadoValido);
            if (esperadoValido)
            {
                formatado.Should().Be(formatadoEsperado);
                grausDecimais.Should().BeApproximately(grausEsperado, 1e-4);
                erro.Should().BeNull();
            }
            else
            {
                erro.Should().NotBeNull();
            }
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(90.0, 90.0)]
        [InlineData(180.5, 180.30)]
        [InlineData(45.258333, 45.1530)]
        [InlineData(154.5125, 154.3045)]
        public void ParaFormatoCompacto_GrausDecimais_ConverteParaGggMmssSemPerda(double grausDecimais, double compactoEsperado)
        {
            double compacto = ConversorAngulos.ParaFormatoCompacto(grausDecimais);

            compacto.Should().BeApproximately(compactoEsperado, 1e-4);
        }

        [Fact]
        public void AzimuteCalculadoPorCoordenadas_RoundTripViaParaFormatoCompacto_PreservaDirecaoExata()
        {
            // Coordenadas simuladas de Estação e Ré
            double xEst = 1000.0, yEst = 1000.0;
            double xRe = 1500.0, yRe = 1250.0;

            // Azimute analítico em graus decimais
            double azDecimal = GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(xEst, yEst, xRe, yRe);

            // Converter para o formato compacto GGG.MMSS para exibição na UI
            double azCompacto = ConversorAngulos.ParaFormatoCompacto(azDecimal);

            // Tentar reinterpretar o valor exibido na UI
            bool parseOk = ConversorAngulos.TryDeFormatoCompacto(azCompacto, out double azReconvertido, out string? erro);

            parseOk.Should().BeTrue(because: erro);
            azReconvertido.Should().BeApproximately(azDecimal, 1e-4, "o roundtrip não pode corromper os minutos ou segundos");
        }
    }
}
