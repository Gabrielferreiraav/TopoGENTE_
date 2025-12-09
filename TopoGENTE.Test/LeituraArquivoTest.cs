using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Services;

namespace TopoGente.Test
{
    public class LeituraArquivoTest
    {
        [Fact]
        public void Deve_Ler_Arquivo_E_Converter_Angulos_Compactos()
        {
            var leitor = new LeituraArquivoService();

            // Simulando um arquivo CSV
            // Estacao, HI, Visada, Desc, AH(GGG.MMSS), AV, DI, HS, TIPO
            string[] arquivoFake = new string[]
            {
                "Estação,HI,Pid,Desc,AHD,AVD,DI,HS,TIPO", // Cabeçalho
                "M1, 1.55, P1, Poste, 45.3000, 90.0000, 12.50, 1.60, 1", // Irradiação 45° 30' 00"
                "M1, 1.55, M2, Vante, 120.0000, 89.3000, 150.00, 1.55, 2" // Poligonal
            };

            var resultado = leitor.LerArquivo(arquivoFake);

            Assert.Equal(2, resultado.Count);

            // Verificar conversão do ângulo compacto do P1
            // 45.3000 -> 45 graus e 30 minutos -> 45.5 graus decimais
            var p1 = resultado[0];
            Assert.Equal("P1", p1.PontoVisado);
            Assert.Equal(45.5, p1.AnguloHorizontal, precision: 4); // 45°30' = 45.5
            Assert.False(p1.EhLeituraDePoligonal); // Tipo 1

            // Verificar M2
            var m2 = resultado[1];
            Assert.Equal("M2", m2.PontoVisado);
            Assert.True(m2.EhLeituraDePoligonal); // Tipo 2
            Assert.Equal(120.0, m2.AnguloHorizontal);
        }
    }
}
