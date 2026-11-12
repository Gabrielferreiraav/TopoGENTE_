using System;
using TopoGente.Core.Entities;
using TopoGente.Test.Builders;
using Xunit;

namespace TopoGente.Test
{
    public class ResultadoLevantamentoZeroAllocTests
    {
        [Fact]
        public void TodosOsPontos_PrimeiraLeitura_DeveMaterializarERetornarInstanciaIdenticaEmLeiturasSubsequentes()
        {
            // Arrange
            var resultado = new ResultadoLevantamentoBuilder()
                .ComPoligonal("E1", "P1")
                .ComIrradiacao("1")
                .Build();

            // Act
            var primeiraInstancia = resultado.TodosOsPontos;
            var segundaInstancia = resultado.TodosOsPontos;

            // Assert
            Assert.NotNull(primeiraInstancia);
            Assert.Equal(3, primeiraInstancia.Count);
            Assert.Same(primeiraInstancia, segundaInstancia);
        }

        [Fact]
        public void TodosOsPontos_IndexacaoOrdinal_DevePreservarSequenciaEstritaPoligonalAntesDeIrradiacoes()
        {
            // Arrange
            var resultado = new ResultadoLevantamentoBuilder()
                .ComPoligonal("E1", "P1")
                .ComIrradiacao("1", "2")
                .Build();

            // Assert (Assert.Collection elimina Assertion Roulette)
            Assert.Collection(resultado.TodosOsPontos,
                ponto => Assert.Equal("E1", ponto.Nome),
                ponto => Assert.Equal("P1", ponto.Nome),
                ponto => Assert.Equal("1", ponto.Nome),
                ponto => Assert.Equal("2", ponto.Nome)
            );
        }

        [Fact]
        public void TodosOsPontos_InstanciaSemPontos_DeveRetornarColecaoVaziaSemAlocarMemoria()
        {
            var resultado = new ResultadoLevantamento();

            Assert.NotNull(resultado.TodosOsPontos);
            Assert.Empty(resultado.TodosOsPontos);
        }

        [Fact]
        public void TodosOsPontos_ConsultasSequenciaisRepetidas_DevemTerCustoDeAlocacaoEstritamenteZero()
        {
            // Arrange
            var resultado = new ResultadoLevantamentoBuilder()
                .ComMalhaSintetica(100)
                .Build();

            // Warm-up do JIT e materialização preguiçosa única
            var colecao = resultado.TodosOsPontos;
            _ = colecao[0].Z;

            // Act & Assert: Medição contínua sem variáveis acumuladoras dummy
            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < colecao.Count; i++)
            {
                _ = colecao[i].Z;
            }

            long memoriaDepois = GC.GetAllocatedBytesForCurrentThread();
            long bytesAlocados = memoriaDepois - memoriaAntes;

            Assert.Equal(0, bytesAlocados);
        }
    }
}
