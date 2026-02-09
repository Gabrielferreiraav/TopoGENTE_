using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using Xunit;



namespace TopoGente.Test
{
    public class QaCheckServiceTests
    {
        [Fact]
        public void Deve_gerar_Check_Delta_E_Flags()
        {
            
            var timeStamp = DateTime.Parse("2026-02-04T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

            var estacao = new Estacao
            {
                Id = "SET1",
                Nome = "E1",
                CoordenadaConhecida = new PontoCoordenada
                {
                    Nome = "E1",
                    X = 0,
                    Y = 0.000,
                    Z = 0.000,
                    EhPontoPoligonal = true
                },
                Leituras = new List<LeituraEstacaoTotal>
                {
                    new LeituraEstacaoTotal
                    {
                        SetupId = "SET1",
                        TimeStamp = timeStamp,
                        EstacaoOcupada = "E1",
                        PontoVisado = "P1",
                        AnguloHorizontal = 0,
                        AnguloVertical = 90,
                        DistanciaInclinada = 0.000,
                        AlturaInstrumento = 00,
                        AlturaPrisma = 00,
                        Observacao = "purpose=check",
                        Purpose = "check"
                    }
                }
            };

            var estacoes = new List<Estacao> { estacao };

            var resultado = new ResultadoLevantamento             {
                Poligonal = new List<PontoCoordenada>()
                {
                    new PontoCoordenada {
                        Nome = "E1",
                        X = 0,
                        Y = 0.000,
                        Z = 0.000,
                        EhPontoPoligonal = true,
                        AzimuteChegada = 0

                    }

                }
            };

            var pontosConhecidos = new Dictionary<string, PontoCoordenada>
            {
                { "P1", new PontoCoordenada { Nome = "P1", X = 0.02, Y = 0.0, Z = 0.03 } }
            };

            var sut = new QaCheckService();

            var rel = sut.GerarRelatorioQaChecks(estacoes, resultado, pontosConhecidos, toleranciaDeltaXY: 0.01, toleranciaDeltaZ: 0.02);

            var ev = Assert.Single(rel.Checks);

            Assert.Equal("SET1", ev.SetupId);
            Assert.Equal("P1", ev.TargetPoint);
            Assert.Equal(timeStamp, ev.TimeStamp);

            Assert.NotNull(ev.DeltaXY);
            Assert.NotNull(ev.DeltaZ);
            Assert.Equal(0.02, ev.DeltaXY.Value, 6);
            Assert.Equal(0.03, ev.DeltaZ.Value, 6);

            Assert.True(ev.ExcedeuDeltaXY);
            Assert.True(ev.ExcedeuDeltaZ);

        }
    }
}