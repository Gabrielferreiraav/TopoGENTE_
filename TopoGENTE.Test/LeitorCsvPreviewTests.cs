using System;
using System.Collections.Generic;
using FluentAssertions;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using Xunit;

namespace TopoGENTE.Test
{
    public class LeitorCsvPreviewTests
    {
        [Fact]
        public void GerarPreviewReativo_LinhaCoordPontoApoio_DeveReconhecerComoValidaEPreencherTokens()
        {
            var config = new ConfiguracaoImportacao
            {
                Delimitador = ';',
                SeparadorDecimal = SeparadorDecimalEnum.Ponto,
                SeparadorMilhar = SeparadorMilharEnum.Nenhum,
                Magnitude = MagnitudeCoordenadaEnum.UtmCompleta,
                FormatoAngular = FormatoAngularEnum.SexagesimalDddMmSs
            };

            var linhas = new[]
            {
                "COORD;A;721803.217;7702490.066;650.354",
                "COORD;REF;721810.622;7702474.274;0.000",
                "A;34.2821;VANTE;90.5033;50.662;1.58;B;1.58"
            };

            var previews = LeitorCsvEngine.GerarPreviewReativo(linhas, config, FormatoArquivoEntrada.CsvPadrao);

            previews.Should().HaveCount(3);

            // Linha 1: COORD Estação A
            previews[0].Valida.Should().BeTrue("linha COORD de ponto de apoio A não deve ser marcada como Erro Formato");
            previews[0].Status.Should().Be("OK (Coordenada Fixa)");
            previews[0].EstacaoOcupada.Should().Be("A");
            previews[0].Observacao.Should().Contain("COORD");

            // Linha 2: COORD Ré REF
            previews[1].Valida.Should().BeTrue("linha COORD do ponto de ré REF deve ser reconhecida com sucesso");
            previews[1].Status.Should().Be("OK (Coordenada Fixa)");
            previews[1].EstacaoOcupada.Should().Be("REF");

            // Linha 3: Leitura Polar normal
            previews[2].Valida.Should().BeTrue();
            previews[2].Status.Should().Be("OK (Polar)");
            previews[2].EstacaoOcupada.Should().Be("A");
            previews[2].PontoVisado.Should().Be("B");
        }
    }
}
