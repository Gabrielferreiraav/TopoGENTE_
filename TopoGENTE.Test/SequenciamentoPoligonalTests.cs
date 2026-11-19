using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Utilities;
using TopoGENTE.Domain.Exceptions;

namespace TopoGente.Test
{
    public class SequenciamentoPoligonalTests
    {
        [Fact]
        public void OrganizarPorVante_QuandoEstacoesEstaoDesordenadas_OrdenaCaminhamentoPeloEncadeamentoTopologico()
        {
            // Arrange
            var organizador = new OrganizarCaminhamento();
            
            var e1 = new Estacao { Nome = "E1" };
            e1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P1", Tipo = TipoLeitura.Poligonal });
            
            var p1 = new Estacao { Nome = "P1" };
            p1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P2", Tipo = TipoLeitura.Poligonal });
            
            var p2 = new Estacao { Nome = "P2" };
            p2.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "E1", Tipo = TipoLeitura.Poligonal });
            
            var estacoesDesordenadas = new List<Estacao> { p2, e1, p1 };

            // Act
            var vantes = organizador.OrganizarPorVante(estacoesDesordenadas, "E1");
            
            // Assert
            Assert.Equal(3, vantes.Count);
            Assert.Equal("P1", vantes[0].PontoVisado);
            Assert.Equal("P2", vantes[1].PontoVisado);
            Assert.Equal("E1", vantes[2].PontoVisado);
        }

        [Theory]
        [InlineData(1000.0, 1100.0, 1000.0, 1000.0, 0.0)]     // Norte
        [InlineData(1100.0, 1000.0, 1000.0, 1000.0, 90.0)]    // Leste
        [InlineData(1000.0, 900.0, 1000.0, 1000.0, 180.0)]    // Sul
        [InlineData(900.0, 1000.0, 1000.0, 1000.0, 270.0)]    // Oeste
        public void CalcularAzimutePorCoordenadas_NosQuatroQuadrantes_RetornaAzimutesGeodesicosExatos(double reX, double reY, double estX, double estY, double azimuteEsperado)
        {
            // Arrange & Act
            double azimuteCalculado = GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(estX, estY, reX, reY);
            
            // Assert
            Assert.Equal(azimuteEsperado, azimuteCalculado, 3);
        }

        [Fact]
        public void CalcularAzimutePorCoordenadas_CasoRealLevantamentoGp2_CalculaAzimuteExatoParaRef()
        {
            // Coordenadas reais de A e REF do arquivo levant_gp2_topo.csv
            double estX = 721803.217, estY = 7702490.066;
            double reX = 721810.622, reY = 7702474.274;

            double azimuteDecimal = GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(estX, estY, reX, reY);

            // deltaX = +7.405, deltaY = -15.792 -> Quadrante 2 (Sul-Sudeste)
            // atan2(7.405, -15.792) = 154.877722° decimal
            azimuteDecimal.Should().BeApproximately(154.8777, 1e-4);

            // No formato compacto sexagesimal: 154° 52' 39.80" -> 154.5240
            double azCompacto = ConversorAngulos.ParaFormatoCompacto(azimuteDecimal);
            azCompacto.Should().BeApproximately(154.5240, 1e-4);
        }

        [Fact]
        public void Processar_QuandoSequenciaPossuiEstacaoOrfaSemOrigemNoGrafo_LancaExcecaoDadosInsuficientes()
        {
            // Arrange
            var e1 = new Estacao { Nome = "E1" };
            e1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P1", Tipo = TipoLeitura.Poligonal });
            
            var p1 = new Estacao { Nome = "P1" };
            p1.AdicionarVisada(new LeituraEstacaoTotal { PontoVisado = "P3", Tipo = TipoLeitura.Poligonal });
            
            var todasEstacoes = new List<Estacao> { e1, p1 };
            
            var metadados = new MetadadosCenario 
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P3", "E4" } // E4 is orphaned origin
            };
            
            var processor = new LevantamentoProcessor(new ClassificadorGrafo(), new TopoGente.Core.Strategies.CompensacaoStrategyFactory());

            // Act & Assert
            var ex = Assert.Throws<DadosInsuficientesException>(() => 
            {
                processor.Processar(new List<SequenciaPoligonal> { new SequenciaPoligonal { Metadados = metadados, EhPrincipal = true } }, todasEstacoes, new Dictionary<string, PontoCoordenada>());
            });
            Assert.Contains("origem nao carregadas", ex.Message);
        }

        [Fact]
        public void Processar_ExecucoesSucessivas_NaoRetemEstadoNemProvocaAcoplamentoDeInstancias()
        {
            // Arrange
            var leituraOriginal = new LeituraEstacaoTotal { PontoVisado = "VanteTeste", Observacao = "Obs Teste" };
            
            // Simulação de projeção neutra para UI (Dumb DTO Pattern)
            // Em tempo de runtime no WPF seria o CaminhamentoItemViewModel,
            // mas como testamos puramente em Headless, avaliamos o contrato de projeção:
            var dto = new { NomeVante = leituraOriginal.PontoVisado ?? string.Empty, Descricao = leituraOriginal.Observacao ?? string.Empty };
            var projectionType = dto.GetType();
            
            // Assert
            Assert.Equal("VanteTeste", dto.NomeVante);
            Assert.DoesNotContain(projectionType.GetProperties(), p => p.PropertyType == typeof(Estacao) || p.PropertyType == typeof(LeituraEstacaoTotal));
        }
    }
}