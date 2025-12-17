using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using TopoGente.Core.Services.Leitores;
using TopoGente.Core.Entities;

namespace TopoGENTE.Test
{
    public class LeitorFbkTest
    {
        [Fact]
        public void DeveLerCabecalho_E_RetornarTrue()
        {
            var leitor = new LeitorFbk();
            string cabecalho = "UNIT METER DECDEG \n NEZ \"A11\" 100 100 10";
            Assert.True(leitor.IdentificarFormato(cabecalho));
        }

        [Fact]
        public void Ler_IdentificarCoordenadas()
        {
            var leitor = new LeitorFbk();
            string[] linha = new[]
            {
                "UNIT METER", "NEZ \"M1\" 1000.000 500.000 100.000",
                "STN \"M1\" 1.6000", "BS \"M0\" 0.0000",
                "AD VA 1 90.0000 10.000 90.000 \"P1\""
            };

            var resultao = leitor.Ler(linha);

            Assert.Single(resultao);

            var estacao = resultao.First();

            Assert.Equal("M1", estacao.Nome);
            Assert.Equal(1.600, estacao.AlturaInstrumento);

            Assert.NotNull(estacao.CoordenadaConhecida);
            Assert.Equal(500.000, estacao.CoordenadaConhecida.X);
            Assert.Equal(1000.000, estacao.CoordenadaConhecida.Y);

            Assert.Equal(2, estacao.Leituras.Count);
            var ponto1 = estacao.Leituras.Last();
            Assert.Equal("1", ponto1.PontoVisado);
            Assert.Equal(10.000, ponto1.DistanciaInclinada);
        }
    }
}
