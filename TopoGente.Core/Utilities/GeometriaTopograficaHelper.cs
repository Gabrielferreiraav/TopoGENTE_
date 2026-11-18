using System;
using TopoGente.Core.Entities;
using TopoGENTE.Domain.Exceptions;

namespace TopoGente.Core.Utilities
{
    public static class GeometriaTopograficaHelper
    {
        public const double ToleranciaZenitalGraus = 0.001;

        public static void ValidarParametrosZenitais(double distanciaInclinada, double anguloVerticalGraus)
        {
            if (distanciaInclinada < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(distanciaInclinada), "A distância inclinada não pode ser negativa.");
            }

            if (double.IsNaN(anguloVerticalGraus) || double.IsInfinity(anguloVerticalGraus))
            {
                throw new ArgumentException("O ângulo vertical não pode ser indefinido.", nameof(anguloVerticalGraus));
            }

            double resto = Math.Abs(anguloVerticalGraus % 180.0);
            if (resto < ToleranciaZenitalGraus || resto > (180.0 - ToleranciaZenitalGraus))
            {
                throw new SingularidadeGeometricaException(anguloVerticalGraus);
            }
        }

        public static double Normalizar360(double angulo)
        {
            angulo %= 360.0;
            if (angulo < 0) angulo += 360.0;
            return angulo;
        }

        public static double NormalizarErroAngular(double erroGraus)
        {
            erroGraus %= 360.0;
            if (erroGraus <= -180.0) erroGraus += 360.0;
            if (erroGraus > 180.0) erroGraus -= 360.0;
            return erroGraus;
        }

        public static double CalcularAzimutePorCoordenadas(double xEstacao, double yEstacao, double xRe, double yRe)
        {
            double deltaX = xRe - xEstacao;
            double deltaY = yRe - yEstacao;

            double azimuteRad = Math.Atan2(deltaX, deltaY);
            double azimuteGraus = azimuteRad * 180.0 / Math.PI;

            if (azimuteGraus < 0)
            {
                azimuteGraus += 360;
            }

            return azimuteGraus;
        }

        public static double CalcularDistanciaHorizontal(double distanciaInclinada, double anguloVerticalGraus)
        {
            ValidarParametrosZenitais(distanciaInclinada, anguloVerticalGraus);
            double radianos = ConversorAngulos.ParaRadianos(anguloVerticalGraus);
            return distanciaInclinada * Math.Sin(radianos);
        }

        public static double CalcularDesnivel(double distanciaInclinada, double anguloVerticalGraus, double hi, double hp)
        {
            ValidarParametrosZenitais(distanciaInclinada, anguloVerticalGraus);
            double radianos = ConversorAngulos.ParaRadianos(anguloVerticalGraus);
            return ((distanciaInclinada * Math.Cos(radianos)) + hi - hp);
        }

        public static (double deltaX, double deltaY) CalcularProjecao(double distancia, double azimuteDecimal)
        {
            double radianos = ConversorAngulos.ParaRadianos(azimuteDecimal);
            double deltaX = distancia * Math.Sin(radianos);
            double deltaY = distancia * Math.Cos(radianos);
            return (deltaX, deltaY);
        }

        public static (double x, double y) CalcularCoordenada(double xAnterior, double yAnterior, double deltaX, double deltaY)
            => (xAnterior + deltaX, yAnterior + deltaY);

        public static (double erroX, double erroY, double erroLinearTotal, double precisaoRelativa)
            CalcularErroFechamento(PontoCoordenada pontoCalculado, PontoCoordenada pontoConhecido, double perimetroTotal)
        {
            double erroX = pontoCalculado.X - pontoConhecido.X;
            double erroY = pontoCalculado.Y - pontoConhecido.Y;

            double erroLinearTotal = Math.Sqrt((erroX * erroX) + (erroY * erroY));

            double precisaoRelativa = 0;
            if (erroLinearTotal > 0.0001)
            {
                precisaoRelativa = erroLinearTotal / perimetroTotal;
            }

            return (erroX, erroY, erroLinearTotal, precisaoRelativa);
        }

        public static PontoCoordenada CalcularPontoIrradiado(PontoCoordenada estacao, LeituraEstacaoTotal leitura, double azimuteRe)
        {
            double azimuteVante = Normalizar360(azimuteRe + leitura.AnguloHorizontal);
            double distHorizontal = CalcularDistanciaHorizontal(leitura.DistanciaInclinada, leitura.AnguloVertical);

            var (deltaX, deltaY) = CalcularProjecao(distHorizontal, azimuteVante);
            var (novoX, novoY) = CalcularCoordenada(estacao.X, estacao.Y, deltaX, deltaY);

            double desnivel = CalcularDesnivel(leitura.DistanciaInclinada, leitura.AnguloVertical, leitura.AlturaInstrumento, leitura.AlturaPrisma);
            double novoZ = estacao.Z + desnivel;

            return new PontoCoordenada
            {
                Nome = leitura.PontoVisado,
                X = novoX,
                Y = novoY,
                Z = novoZ,
                EhPontoPoligonal = false
            };
        }
    }
}