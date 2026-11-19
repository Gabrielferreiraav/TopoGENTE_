using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;

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
        /// Lança ArgumentOutOfRangeException caso os minutos ou segundos sejam superiores ou iguais a 60.
        /// </summary>
        public static double DeFormatoCompacto(double gggmmss)
        {
            if (!TryDeFormatoCompacto(gggmmss, out double grausDecimais, out string? erro))
            {
                throw new ArgumentOutOfRangeException(nameof(gggmmss), erro);
            }

            return grausDecimais;
        }

        /// <summary>
        /// Valida os componentes (Graus, Minutos, Segundos) de um valor no formato compacto GGG.MMSS.
        /// </summary>
        public static bool ValidarFormatoCompacto(double gggmmss, out int graus, out int minutos, out double segundos, out string? erro)
        {
            graus = (int)Math.Truncate(gggmmss);
            double resto = Math.Abs(gggmmss - graus);

            double minutosComSegundos = resto * 100.0;
            minutosComSegundos = Math.Round(minutosComSegundos, 6);
            minutos = (int)Math.Truncate(minutosComSegundos);

            segundos = (minutosComSegundos - minutos) * 100.0;
            segundos = Math.Round(segundos, 5);

            if (minutos >= 60)
            {
                erro = $"Minutos inválidos ({minutos} >= 60). No formato GGG.MMSS, os minutos devem estar entre 00 e 59.";
                return false;
            }

            if (segundos >= 60.0)
            {
                erro = $"Segundos inválidos ({segundos:00.##} >= 60). No formato GGG.MMSS, os segundos devem estar entre 00 e 59.";
                return false;
            }

            erro = null;
            return true;
        }

        /// <summary>
        /// Tenta converter um valor numérico em formato compacto GGG.MMSS para Graus Decimais.
        /// Retorna false e preenche a mensagem de erro caso minutos ou segundos sejam inválidos.
        /// </summary>
        public static bool TryDeFormatoCompacto(double gggmmss, out double grausDecimais, out string? erro)
        {
            if (!ValidarFormatoCompacto(gggmmss, out int graus, out int minutos, out double segundos, out erro))
            {
                grausDecimais = 0;
                return false;
            }

            grausDecimais = ParaDecimal(graus, minutos, segundos);
            return true;
        }

        /// <summary>
        /// Tenta converter uma string digitada pelo usuário em formato compacto GGG.MMSS (aceita ponto ou vírgula)
        /// para Graus Decimais e gera o texto legível correspondente (ex: 154° 30' 45.00").
        /// </summary>
        public static bool TryDeFormatoCompactoTexto(string? texto, out double grausDecimais, out string gmsFormatado, out string? erro)
        {
            grausDecimais = 0;
            gmsFormatado = string.Empty;
            erro = null;

            if (string.IsNullOrWhiteSpace(texto))
            {
                gmsFormatado = "0° 00' 00.00\"";
                return true;
            }

            string normalizado = texto.Trim().Replace(',', '.');
            if (!double.TryParse(normalizado, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double valorCompacto))
            {
                erro = $"Formato numérico inválido: '{texto}'. Use o formato GGG.MMSS (ex: 154.3045).";
                return false;
            }

            if (!ValidarFormatoCompacto(valorCompacto, out int graus, out int minutos, out double segundos, out erro))
            {
                return false;
            }

            grausDecimais = ParaDecimal(graus, minutos, segundos);
            gmsFormatado = $"{graus}° {minutos:D2}' {segundos:00.00}\"";
            return true;
        }

        /// <summary>
        /// Converte Graus Decimais para o formato compacto GGG.MMSS (ex: 154.877722° -> 154.52398).
        /// Essencial para preencher campos de azimute calculados analiticamente sem corromper a representação sexagesimal.
        /// </summary>
        public static double ParaFormatoCompacto(double decimalGraus)
        {
            // Normalizar para [0, 360)
            double norm = decimalGraus % 360.0;
            if (norm < 0) norm += 360.0;

            var (graus, minutos, segundos) = ParaGMS(norm);
            double resultado = graus + (minutos / 100.0) + (segundos / 10000.0);
            return Math.Round(resultado, 6);
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
            return $"{g}° {m:D2}' {s:00.00}\"";
        }

        /// <summary>
        /// Converte ângulo em Gradiados (gons, círculo de 400g) para Graus Decimais (círculo de 360°).
        /// </summary>
        public static double DeGradiadosParaDecimal(double gons)
        {
            return gons * 0.9; // 360.0 / 400.0
        }

        /// <summary>
        /// Converte um valor numérico de entrada para Graus Decimais conforme o formato angular especificado.
        /// </summary>
        public static double ConverterParaDecimal(double valor, FormatoAngularEnum formato)
        {
            return formato switch
            {
                FormatoAngularEnum.SexagesimalDddMmSs => DeFormatoCompacto(valor),
                FormatoAngularEnum.GradiadosGons => DeGradiadosParaDecimal(valor),
                FormatoAngularEnum.GrausDecimais => valor,
                _ => valor
            };
        }
    }
}
