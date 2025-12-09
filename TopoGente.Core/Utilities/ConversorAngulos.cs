using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Utilities
{
    /// <summary>
    /// Class <c> Conversor Angulos</c> Converte angulos em GMS para decimal, e vice versa. 
    /// </summary>
    public static class ConversorAngulos
    {
        public static double ParaDecimal(int graus, int minutos, double segundos)
        {
            double sinal = graus < 0 ? -1 : 1;
            double grausAbs = Math.Abs(graus);

            return sinal * (grausAbs + (minutos / 60.0) + (segundos / 3600.0));
        }
        /// <summary>
        /// Converte o formato compacto "GGG.MMSS" para Graus Decimais.
        /// </summary>
        public static double DeFormatoCompacto(double gggmmss)
        {
            int graus = (int)Math.Truncate(gggmmss);

            double resto = Math.Abs(gggmmss - graus);

            double minutosComSegundos = resto * 100.0;
            minutosComSegundos = Math.Round(minutosComSegundos,6);
            int minutos = (int)Math.Truncate(minutosComSegundos);

            double segundos = (minutosComSegundos - minutos) * 100.0;

            segundos = Math.Round(segundos, 5);

            return ParaDecimal(graus, minutos, segundos);
        }
        public static (int graus, int minutos, double segundos) ParaGMS(double decimalGraus)
        {
            int sinal = decimalGraus < 0 ? -1 : 1;
            double decimalAbs = Math.Abs(decimalGraus);

            int graus = (int)Math.Floor(decimalAbs);
            double restoMinutos = (decimalAbs - graus) * 60;
            int minutos = (int)Math.Floor(restoMinutos);
            double segundos = (restoMinutos - minutos) * 60;

            segundos = Math.Round(segundos, 3);

            if (segundos >= 60)
            {
                minutos++;
                segundos = 0;
            }
            if (minutos >= 60)
            {
                graus++;
                minutos = 0;
            }

            return (graus * sinal, minutos, segundos);
        }
        /// <summary>
        /// Converte graus decimais para radianos.
        /// </summary>
        public static double ParaRadianos(double grausDecimal)
        {
            return grausDecimal * Math.PI / 180.0;
        }
        public static string ParaTexto(double decimalGraus)
        {
            var (g, m, s) = ParaGMS(decimalGraus);
            return $"{g}° {m}' {s:F2}\"";
        }
    }
}
