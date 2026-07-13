using System.Collections.Generic;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;

namespace TopoGente.Tests.CaixaBranca
{
    public sealed record CompensacaoEnquadradaCaixaBrancaTests
    {
        private static CompensacaoPoligonalInputDTO CriarEntradaBase()
        {
            var partida = new PontoCoordenada { X = 0, Y = 0, Z = 0, Nome = "E0", AzimuteChegada = 90.0 };
            var chegada = new PontoCoordenada { X = 400, Y = 0, Z = 0, Nome = "E4" };

            var metadados = new MetadadosCenario
            {
                ChegadaX = chegada.X,
                ChegadaY = chegada.Y,
                ChegadaZ = chegada.Z,
                NomeChegada = chegada.Nome
            };

            var leituras = new List<LeituraEstacaoTotal>
            {
                new LeituraEstacaoTotal { DistanciaInclinada = 100, AnguloVertical = 90, AnguloHorizontal = 180, AlturaInstrumento = 0, AlturaPrisma = 0, PontoVisado = "E1" },
                new LeituraEstacaoTotal { DistanciaInclinada = 100, AnguloVertical = 90, AnguloHorizontal = 180, AlturaInstrumento = 0, AlturaPrisma = 0, PontoVisado = "E2" },
                new LeituraEstacaoTotal { DistanciaInclinada = 100, AnguloVertical = 90, AnguloHorizontal = 180, AlturaInstrumento = 0, AlturaPrisma = 0, PontoVisado = "E3" },
                new LeituraEstacaoTotal { DistanciaInclinada = 100, AnguloVertical = 90, AnguloHorizontal = 180, AlturaInstrumento = 0, AlturaPrisma = 0, PontoVisado = "E4" }
            };

            var poligonalBruta = new List<PontoCoordenada>
            {
                partida,
                new PontoCoordenada { X = 100, Y = 0, Z = 0, Nome = "E1", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 200, Y = 0, Z = 0, Nome = "E2", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 300, Y = 0, Z = 0, Nome = "E3", AzimuteChegada = 90.0 },
                new PontoCoordenada { X = 400, Y = 0, Z = 0, Nome = "E4", AzimuteChegada = 90.0 }
            };

            return new CompensacaoPoligonalInputDTO
            {
                Metadados = metadados,
                PontoPartida = partida,
                PontoChegada = chegada,
                AzimuteInicial = 270.0,
                AzimuteChegada = 270.0,
                AnguloFechamento = 0.0,
                Leituras = leituras,
                PoligonalBruta = poligonalBruta
            };
        }

        [Fact]
        public void CT01_Deve_Abortar_Quando_Leituras_Ausentes()
        {
            // O record permite a mutação não destrutiva do DTO imutável
            var entrada = CriarEntradaBase() with { Leituras = new List<LeituraEstacaoTotal>() };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Equal("Nenhuma leitura fornecida. Compensação não realizada.", resultado.AlertaReprovacao);
        }

        [Fact]
        public void CT02_Deve_Abortar_Quando_Coordenadas_Chegada_Ausentes()
        {
            // Clona a entrada base, mas injeta metadados com propriedades nulas para romper o contrato
            var entrada = CriarEntradaBase() with
            {
                Metadados = new MetadadosCenario { NomeChegada = "E4" }
            };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Equal("Poligonal enquadrada exige coordenadas de chegada (X, Y, Z).", resultado.AlertaReprovacao);
        }

        [Fact]
        public void CT03_Deve_Abortar_Quando_Erro_Angular_Supera_Tolerancia()
        {
            // Clona o grafo base injetando a anomalia angular diretamente na inicialização
            var entrada = CriarEntradaBase() with { AzimuteChegada = 0.0 };

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Contains("Erro Angular", resultado.AlertaReprovacao);
        }

        [Fact]
        public void CT04_Deve_Abortar_Quando_Precisao_Linear_E_Inferior_A_Norma()
        {
            var entrada = CriarEntradaBase();
            entrada.Metadados.ChegadaX = 1000.0;
            entrada.Metadados.ChegadaY = 0.0;
            entrada.Metadados.ChegadaZ = 0.0;

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Contains("Precisão Linear", resultado.AlertaReprovacao);
        }

        [Fact]
        public void CT05_Deve_Abortar_Quando_Erro_Altimetrico_Supera_Tolerancia()
        {
            var entrada = CriarEntradaBase();
            entrada.Metadados.ChegadaZ = 1000.0;

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.False(resultado.AprovadoNorma);
            Assert.Contains("altimétrico", resultado.AlertaReprovacao.ToLowerInvariant());
        }

        [Fact]
        public void CT06_Deve_Aprovar_Quando_Todos_Os_Criterios_Sao_Atendidos()
        {
            var entrada = CriarEntradaBase();

            var strategy = new CompensacaoEnquadradaStrategy();
            var resultado = strategy.Compensar(entrada);

            Assert.True(resultado.AprovadoNorma);
            Assert.Empty(resultado.AlertaReprovacao);
            Assert.Equal(100.000, resultado.PoligonalCompensada[1].X, precision: 3);
            Assert.Equal(0.000, resultado.PoligonalCompensada[1].Y, precision: 3);
            Assert.Equal(0.000, resultado.PoligonalCompensada[1].Z, precision: 3);
            Assert.Equal(400.000, resultado.PoligonalCompensada[4].X, precision: 3);
            Assert.Equal(0.000, resultado.PoligonalCompensada[4].Y, precision: 3);
            Assert.Equal(0.000, resultado.PoligonalCompensada[4].Z, precision: 3);
        }
    }
}
