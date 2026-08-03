using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Infrastructure.Adapters.Leitores;

namespace TopoGENTE.Test
{
    public class AuxiliarTests
    {
        [Fact]
        public void LeitorCsv_Deve_Mapear_Purpose_Auxiliar_Como_Neutro()
        {
            // Arrange
            var leitor = new LeitorCsvPadrao();
            var linhas = new List<string>
            {
                "E1;0.00;AUXILIAR;90.00;100.00;1.50;P_AUX1;1.60"
            };

            // Act
            var estacoes = leitor.Ler(linhas);

            // Assert
            Assert.Single(estacoes);
            var leitura = estacoes.First().Leituras.First();
            
            Assert.Equal("auxiliar", leitura.Purpose);
            Assert.Equal(TipoLeitura.Irradiacao, leitura.Tipo);
        }

        [Fact]
        public void Classificador_Deve_Promover_Visada_A_Auxiliar_Quando_Purpose_For_Aux()
        {
            // Arrange
            var classificador = new ClassificadorGrafo();
            var estacao1 = new Estacao { Nome = "E1" };
            estacao1.AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "P_AUX1",
                Purpose = "aux",
                Tipo = TipoLeitura.Irradiacao // Antes da classificação
            });

            var estacoes = new List<Estacao> { estacao1 };
            
            // Act
            classificador.ClassificarArestasGrafo(estacoes, new MetadadosCenario());

            // Assert
            var leitura = estacoes.First().Leituras.First();
            Assert.Equal(TipoLeitura.Auxiliar, leitura.Tipo);
        }

        [Fact]
        public void Classificador_Deve_Lançar_Excecao_Se_Auxiliar_For_Forçada_Como_Vante()
        {
            // Arrange
            var classificador = new ClassificadorGrafo();
            var estacao1 = new Estacao { Nome = "E1" };
            estacao1.AdicionarVisada(new LeituraEstacaoTotal
            {
                PontoVisado = "P_AUX1",
                Purpose = "vante" // O operador forçou Vante para P_AUX1
            });
            var estacao2 = new Estacao { Nome = "E2" };

            var estacoes = new List<Estacao> { estacao1, estacao2 };

            // Act & Assert
            var ex = Assert.Throws<DadosInsuficientesException>(() => 
                classificador.ClassificarArestasGrafo(estacoes, new MetadadosCenario()));
                
            Assert.Contains("Ruptura Topológica", ex.Message);
        }
    }
}
