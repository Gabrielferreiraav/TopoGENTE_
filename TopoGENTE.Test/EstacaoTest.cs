using System;
using System.Linq;
using FluentAssertions;
using TopoGente.Core.Entities;
using Xunit;

namespace TopoGENTE.Test
{
    public class EstacaoTest
    {
        [Fact]
        public void SubstituirLeitura_DeveRemoverAntigaECriarNovaComMesmoSetup()
        {
            // Arrange
            var estacao = new Estacao { Nome = "E1" };
            var leituraOriginal = new LeituraEstacaoTotal
            {
                Id = Guid.NewGuid().ToString(),
                PontoVisado = "V1",
                AnguloHorizontal = 100,
                AnguloVertical = 90,
                DistanciaInclinada = 50,
                AlturaPrisma = 1.5,
                Tipo = TipoLeitura.Poligonal
            };
            estacao.AdicionarVisada(leituraOriginal);

            string idOriginal = leituraOriginal.Id;

            // Act
            estacao.SubstituirLeitura(idOriginal, "V2", 150, 95, 60, 1.6, "Nova observacao");

            // Assert
            Assert.Single(estacao.Leituras);
            var leituraSubstituida = estacao.Leituras.First();

            Assert.NotEqual(idOriginal, leituraSubstituida.Id); // Garante novo Id físico
            Assert.Equal("V2", leituraSubstituida.PontoVisado);
            leituraSubstituida.AnguloHorizontal.Should().BeApproximately(150.0, 1e-4);
            leituraSubstituida.AnguloVertical.Should().BeApproximately(95.0, 1e-4);
            leituraSubstituida.DistanciaInclinada.Should().BeApproximately(60.0, 1e-4);
            leituraSubstituida.AlturaPrisma.Should().BeApproximately(1.6, 1e-4);
            Assert.Equal("Nova observacao", leituraSubstituida.Observacao);
            Assert.Equal(TipoLeitura.Poligonal, leituraSubstituida.Tipo); // Copiou as imutáveis
        }

        [Fact]
        public void SubstituirLeitura_IdInexistente_NaoDeveFalhar_EvitaDoubleFireDoWpf()
        {
            // Arrange
            var estacao = new Estacao { Nome = "E1" };
            
            // Act - O retorno deve ser silencioso conforme a regra de evitar double-fire da UI
            estacao.SubstituirLeitura(Guid.NewGuid().ToString(), "V2", 150, 95, 60, 1.6, "Nova observacao");
            
            // Assert
            Assert.Empty(estacao.Leituras);
        }
    }
}
