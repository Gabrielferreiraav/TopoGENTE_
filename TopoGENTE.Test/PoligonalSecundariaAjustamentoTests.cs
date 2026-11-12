using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Test.Builders;

namespace TopoGente.Test
{
    public class PoligonalSecundariaAjustamentoTests
    {
        [Fact]
        public void PoligonalSecundaria_QuandoAncoradaEmEstacaoDaPrincipal_HerdaCoordenadasCompensadas()
        {
            // Arrange: Poligonal principal P1 -> P2 e secundária ancorada em P2 -> AUX1 construídas via Test Data Builder
            var builderPrincipal = new PoligonalTestBuilder()
                .ComoAbertaOrientada("P1", x: 100.0, y: 100.0, z: 10.0, azimutePartida: 0.0)
                .ComPontoConhecido("P1", 100.0, 100.0, 10.0)
                .ComEstacao("P1")
                .VisandoRe("REF", anguloHz: 0.0, distanciaInclinada: 100.0)
                .VisandoVante("P2", anguloHz: 90.0, distanciaInclinada: 100.0);

            var builderSecundaria = new PoligonalTestBuilder()
                .ComoAbertaOrientada("P2", x: 0.0, y: 0.0, z: 0.0, azimutePartida: 0.0)
                .ComEstacao("P2")
                .VisandoRe("P1", anguloHz: 0.0, distanciaInclinada: 100.0)
                .VisandoVante("AUX1", anguloHz: 90.0, distanciaInclinada: 50.0);

            // Ajustar o tipo da leitura vante de P2 na secundária para Auxiliar (conforme o domínio de apoio suplementar)
            var estacaoP2Secundaria = builderSecundaria.BuildEstacoes()[0];
            estacaoP2Secundaria.Leituras.First(l => l.PontoVisado == "AUX1").Tipo = TipoLeitura.Auxiliar;

            // P1 principal com metadados de ré em REF no norte (Az = 0)
            var seqPrincipal = builderPrincipal.BuildSequencia("Principal", ehPrincipal: true, estacoes: new List<string> { "P1", "P2" });
            seqPrincipal.Metadados.UsarCoordenadaRe = true;
            seqPrincipal.Metadados.ReX = 100.0;
            seqPrincipal.Metadados.ReY = 200.0;
            seqPrincipal.Metadados.ReZ = 10.0;

            var seqSecundaria = builderSecundaria.BuildSequencia("Secundaria", ehPrincipal: false, ancoragem: "P2", estacoes: new List<string> { "P2", "AUX1" });
            seqSecundaria.Metadados.UsarCoordenadaRe = false;
            seqSecundaria.Metadados.AzimutePartida = 0.0;

            var estacoes = builderPrincipal.BuildEstacoes().Concat(builderSecundaria.BuildEstacoes()).ToList();
            var sequencias = new List<SequenciaPoligonal> { seqPrincipal, seqSecundaria };

            var processador = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());

            // Act
            var resultado = processador.Processar(sequencias, estacoes, builderPrincipal.BuildPontosConhecidos());

            // Assert: Tolerância métrica explícita de 1 mm (0.001 m)
            Assert.NotNull(resultado);
            var p2 = resultado.Poligonal.First(p => p.Nome == "P2");
            Assert.Equal(200.0, p2.X, precision: 3);
            Assert.Equal(100.0, p2.Y, precision: 3);

            var aux1 = resultado.Poligonal.FirstOrDefault(p => p.Nome == "AUX1");
            Assert.NotNull(aux1);
            Assert.Equal(250.0, aux1.X, precision: 3);
            Assert.Equal(100.0, aux1.Y, precision: 3);
        }

        [Fact]
        public void Processar_QuandoEstacaoAncoragemNaoExisteNoGrafo_LancaExcecaoDadosInsuficientes()
        {
            // Arrange
            var builderPrincipal = new PoligonalTestBuilder()
                .ComoAbertaOrientada("P1", x: 100.0, y: 100.0, z: 10.0)
                .ComEstacao("P1");

            var seqPrincipal = builderPrincipal.BuildSequencia("Principal", ehPrincipal: true, estacoes: new List<string> { "P1", "P2" });

            var builderSecundaria = new PoligonalTestBuilder()
                .ComoAbertaOrientada("P99", x: 0.0, y: 0.0, z: 0.0);

            var seqSecundaria = builderSecundaria.BuildSequencia("Secundaria", ehPrincipal: false, ancoragem: "P99", estacoes: new List<string> { "P99", "AUX1" });

            var sequencias = new List<SequenciaPoligonal> { seqPrincipal, seqSecundaria };
            var estacoes = builderPrincipal.BuildEstacoes();
            var processador = new LevantamentoProcessor(new ClassificadorGrafo(), new CompensacaoStrategyFactory());

            // Act & Assert
            Assert.Throws<DadosInsuficientesException>(() => processador.Processar(sequencias, estacoes, new Dictionary<string, PontoCoordenada>()));
        }
    }
}
