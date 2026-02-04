using System;
using System.Globalization;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Services.Leitores;
using Xunit;

namespace TopoGENTE.Test
{
    public class LeitorLandXmlTests
    {
        [Fact]
        public void Deve_Resolver_InstrumentPoint_Por_pntRef()
        {
            var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2">
              <Units>
                <Metric linearUnit="meter" angularUnit="degrees" />
              </Units>
              <Survey>
                <CgPoints>
                  <CgPoint name="P_OCUP">1000 2000 10</CgPoint>
                  <CgPoint name="ALVO">1100 2100 11</CgPoint>
                </CgPoints>

                <InstrumentSetup id="SET1" stationName="E1" instrumentHeight="1.5">
                  <InstrumentPoint pntRef="P_OCUP" />
                  <RawObservation purpose="check" targetPoint="ALVO" horizAngle="0" zenithAngle="90" slopeDistance="10" targetHeight="1.8">
                    <TargetPoint name="ALVO" timeStamp="2026-02-04T10:00:00Z" />
                  </RawObservation>
                </InstrumentSetup>
              </Survey>
            </LandXML>
            """;

            var leitor = new LeitorLandXml();
            var estacoes = leitor.Ler(xml.Split('\n'));

            var e1 = Assert.Single(estacoes);
            Assert.NotNull(e1.CoordenadaConhecida);

            // Ordem N E Z do LandXML => Y X Z no modelo
            Assert.Equal(2000.0, e1.CoordenadaConhecida!.X, 6);
            Assert.Equal(1000.0, e1.CoordenadaConhecida!.Y, 6);
            Assert.Equal(10.0, e1.CoordenadaConhecida!.Z, 6);
        }

        [Fact]
        public void Deve_Resolver_InstrumentPoint_Por_Texto_N_E_Z()
        {
            var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2">
              <Units>
                <Metric linearUnit="meter" angularUnit="degrees" />
              </Units>
              <Survey>
                <InstrumentSetup id="SET1" stationName="E1" instrumentHeight="1.5">
                  <InstrumentPoint>1000 2000 10</InstrumentPoint>
                </InstrumentSetup>
              </Survey>
            </LandXML>
            """;

            var leitor = new LeitorLandXml();
            var estacoes = leitor.Ler(xml.Split('\n'));

            var e1 = Assert.Single(estacoes);
            Assert.NotNull(e1.CoordenadaConhecida);

            Assert.Equal(2000.0, e1.CoordenadaConhecida!.X, 6);
            Assert.Equal(1000.0, e1.CoordenadaConhecida!.Y, 6);
            Assert.Equal(10.0, e1.CoordenadaConhecida!.Z, 6);
        }

        [Fact]
        public void Deve_Preencher_SetupId_TimeStamp_E_Preservar_PurposeCheck()
        {
            var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2">
              <Units>
                <Metric linearUnit="meter" angularUnit="degrees" />
              </Units>
              <Survey>
                <CgPoints>
                  <CgPoint name="P_OCUP">1000 2000 10</CgPoint>
                </CgPoints>

                <InstrumentSetup id="SET123" stationName="E1" instrumentHeight="1.5">
                  <InstrumentPoint pntRef="P_OCUP" />
                  <RawObservation purpose="check" targetPoint="P_CHECK" horizAngle="10" zenithAngle="90" slopeDistance="10" targetHeight="1.7">
                    <TargetPoint name="P_CHECK" timeStamp="2026-02-04T10:00:00Z" />
                  </RawObservation>
                </InstrumentSetup>
              </Survey>
            </LandXML>
            """;

            var leitor = new LeitorLandXml();
            var estacoes = leitor.Ler(xml.Split('\n'));

            var e1 = Assert.Single(estacoes);
            var leitura = Assert.Single(e1.Leituras);

            Assert.Equal("SET123", leitura.SetupId);

            Assert.True(leitura.TimeStamp.HasValue);
            Assert.Equal(DateTime.Parse("2026-02-04T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), leitura.TimeStamp!.Value);

            Assert.Contains("purpose=check", leitura.Observacao, StringComparison.OrdinalIgnoreCase);
        }
    }
}