using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;
using TopoGente.Core.Utilities;
using Xunit;

namespace TopoGente.Test;

/// <summary>
/// Testes unitários do serviço de domínio — VerificacaoLinhaBaseStrategy.
/// Cobertura:
///   1. Cálculo analítico de Δ E, Δ N, Δ Z e e_l em cenário sintético.
///   2. Aprovação com discrepância dentro da tolerância.
///   3. Reprovação por erro linear excessivo.
///   4. Reprovação por erro angular excessivo.
///   5. Benchmark com distância 56.560 m e discrepância de 3 mm (arquivo 121377.FBK).
///   6. Propriedade LaudoLinhaBase preenchida corretamente.
///   7. Formato de saída dos campos textuais.
///   8. Guard: leituras vazias.
///   9. Guard: chegada não informada.
/// </summary>
public sealed class VerificacaoLinhaBaseTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    // Helpers de fixture
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Monta uma linha de base sintética em linha reta (azimute 90°).
    /// Partida em (0, 0, 100). Duas estações intermediárias, cada DH = distanciaSeg / 2.
    /// Chegada teórica em (distanciaTotal, 0, 100).
    /// </summary>
    private static CompensacaoPoligonalInputDTO CriarInputLinhaReta(
        double distanciaTotal,
        double erroXChegada = 0.0,
        double erroYChegada = 0.0,
        double erroZChegada = 0.0,
        ParametrosTolerancia? tolerancias = null)
    {
        double dh = distanciaTotal / 2.0;
        var partida = new PontoCoordenada { Nome = "E1", X = 0, Y = 0, Z = 100.0, AzimuteChegada = 90.0 };
        var chegada = new PontoCoordenada
        {
            Nome = "E4",
            X = distanciaTotal + erroXChegada,
            Y = 0 + erroYChegada,
            Z = 100.0 + erroZChegada
        };

        var leituras = new List<LeituraEstacaoTotal>
        {
            new()
            {
                EstacaoOcupada = "E1", PontoVisado = "E2",
                DistanciaInclinada = dh, AnguloVertical = 90.0,
                AnguloHorizontal = 0.0,
                AlturaInstrumento = 1.5, AlturaPrisma = 1.5,
                Tipo = TipoLeitura.Poligonal
            },
            new()
            {
                EstacaoOcupada = "E2", PontoVisado = "E4",
                DistanciaInclinada = dh, AnguloVertical = 90.0,
                AnguloHorizontal = 180.0,
                AlturaInstrumento = 1.5, AlturaPrisma = 1.5,
                Tipo = TipoLeitura.Poligonal
            }
        };

        return new CompensacaoPoligonalInputDTO
        {
            PontoPartida   = partida,
            PontoChegada   = chegada,
            AzimuteInicial = 90.0,
            Leituras       = leituras,
            PoligonalBruta = new List<PontoCoordenada> { partida },
            Tolerancias    = tolerancias ?? ParametrosTolerancia.Padrao,
            Metadados      = new MetadadosCenario
            {
                TipoCenario   = TipoCenarioPoligonal.VerificacaoLinhaBase,
                PartidaX = 0, PartidaY = 0, PartidaZ = 100.0,
                ChegadaX = distanciaTotal + erroXChegada,
                ChegadaY = erroYChegada,
                ChegadaZ = 100.0 + erroZChegada,
                NomeChegada = "E4"
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 1. Cálculo de discrepâncias em cenário sem erro
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TransporteAnalitico_SemErro_ProduzeDiscrepanciasZero()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarInputLinhaReta(distanciaTotal: 100.0);

        var resultado = strategy.Compensar(input);

        Assert.NotNull(resultado.LaudoLinhaBase);
        var laudo = resultado.LaudoLinhaBase;

        Assert.Equal(0.0, laudo.DeltaEsteMetros,  precision: 6);
        Assert.Equal(0.0, laudo.DeltaNorteMetros, precision: 6);
        Assert.Equal(0.0, laudo.DeltaZMetros,     precision: 6);
        Assert.Equal(0.0, laudo.ErroLinearMm,     precision: 3);
        Assert.True(laudo.Aprovado);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 2. Aprovação com discrepância dentro da tolerância
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verificacao_DiscrepanciaDentroTolerancia_Aprovado()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        // Tolerância Padrão (1:12000) para 100 m: Tl = 100/12000 ≈ 8.33 mm
        // Erro de 3 mm deve aprovar
        var input = CriarInputLinhaReta(distanciaTotal: 100.0, erroXChegada: 0.003);

        var resultado = strategy.Compensar(input);

        Assert.True(resultado.AprovadoNorma);
        Assert.NotNull(resultado.LaudoLinhaBase);
        Assert.True(resultado.LaudoLinhaBase.Aprovado);
        Assert.Empty(resultado.LaudoLinhaBase.MotivoReprovacao);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 3. Reprovação por erro linear excessivo
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verificacao_ErroLinearExcessivo_Reprovado()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        // Tolerância ClasseIP (1:10000) para 100 m: Tl = 100/10000 = 10 mm
        // Erro de 50 mm deve reprovar
        var input = CriarInputLinhaReta(
            distanciaTotal: 100.0,
            erroXChegada: 0.050,
            tolerancias: ParametrosTolerancia.ClasseIP);

        var resultado = strategy.Compensar(input);

        Assert.False(resultado.AprovadoNorma);
        Assert.NotNull(resultado.LaudoLinhaBase);
        Assert.False(resultado.LaudoLinhaBase.Aprovado);
        Assert.Contains("Erro linear", resultado.LaudoLinhaBase.MotivoReprovacao);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 4. DTO: campos ErroX / ErroY / ErroLinearTotal populados corretamente
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verificacao_ResultadoDTO_CamposPopuladosCorretamente()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        double erroX = 0.012; // 12 mm
        var input = CriarInputLinhaReta(distanciaTotal: 56.56, erroXChegada: erroX);

        var resultado = strategy.Compensar(input);

        // ErroX = ΔE = coord_calculada.X - chegada.X
        Assert.Equal(-erroX, resultado.ErroX, precision: 5);
        Assert.NotNull(resultado.LaudoLinhaBase);
        Assert.Equal(-erroX, resultado.LaudoLinhaBase.DeltaEsteMetros, precision: 5);
        Assert.Equal(Math.Abs(erroX) * 1000.0, resultado.LaudoLinhaBase.ErroLinearMm, precision: 2);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 5. Benchmark 121377.FBK — linha de base E1→E4 ≈ 56.560 m, Δ ≈ 3 mm
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Benchmark_LinhaBase56560m_DiscrepanciaMaxima3mm()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarInputLinhaReta(
            distanciaTotal: 56.560,
            erroXChegada: 0.003,  // 3 mm de discrepância em E
            tolerancias: ParametrosTolerancia.Padrao);

        var resultado = strategy.Compensar(input);
        var laudo = resultado.LaudoLinhaBase;

        Assert.NotNull(laudo);
        Assert.True(laudo.Aprovado,
            $"A discrepância de 3 mm deveria aprovar. Motivo: {laudo.MotivoReprovacao}");
        Assert.Equal(3.0, laudo.ErroLinearMm, precision: 1);

        double denominadorEsperado = 56.560 / 0.003;
        Assert.Equal(denominadorEsperado, 1.0 / laudo.PrecisaoRelativa, precision: 0);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 6. Formatação dos campos textuais do laudo
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LaudoFormatacao_CamposTextuais_FormatoCorreto()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = CriarInputLinhaReta(distanciaTotal: 100.0, erroXChegada: 0.0124);
        var laudo = strategy.Compensar(input).LaudoLinhaBase!;

        Assert.Equal("-12.4 mm", laudo.DeltaEsteMmFormatado);
        Assert.StartsWith("+12.4", laudo.ErroLinearFormatado);
        Assert.Contains(":", laudo.PrecisaoRelativaFormatada);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 7. Guard: leituras vazias → reprovação com mensagem clara
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Guard_LeiturasVazias_RetornaReprovado()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = new CompensacaoPoligonalInputDTO
        {
            PontoPartida   = new PontoCoordenada { Nome = "E1", X = 0, Y = 0, Z = 100 },
            PontoChegada   = new PontoCoordenada { Nome = "E4", X = 100, Y = 0, Z = 100 },
            Leituras       = new List<LeituraEstacaoTotal>(),
            PoligonalBruta = new List<PontoCoordenada>(),
            Tolerancias    = ParametrosTolerancia.Padrao,
            Metadados      = new MetadadosCenario()
        };

        var resultado = strategy.Compensar(input);

        Assert.False(resultado.AprovadoNorma);
        Assert.Contains("Nenhuma leitura", resultado.AlertaReprovacao);
        Assert.Null(resultado.LaudoLinhaBase);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 8. Guard: ponto de chegada não informado → reprovação com mensagem clara
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Guard_SemCoordenadaChegada_RetornaReprovado()
    {
        var strategy = new VerificacaoLinhaBaseStrategy();
        var input = new CompensacaoPoligonalInputDTO
        {
            PontoPartida   = new PontoCoordenada { Nome = "E1", X = 0, Y = 0, Z = 100 },
            PontoChegada   = new PontoCoordenada { Nome = "E4", X = 0, Y = 0, Z = 0 },
            Leituras = new List<LeituraEstacaoTotal>
            {
                new() { DistanciaInclinada = 50, AnguloVertical = 90, AnguloHorizontal = 0, Tipo = TipoLeitura.Poligonal }
            },
            PoligonalBruta = new List<PontoCoordenada>(),
            Tolerancias    = ParametrosTolerancia.Padrao,
            Metadados      = new MetadadosCenario()
        };

        var resultado = strategy.Compensar(input);

        Assert.False(resultado.AprovadoNorma);
        Assert.Contains("coordenadas conhecidas", resultado.AlertaReprovacao);
        Assert.Null(resultado.LaudoLinhaBase);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 9. CompensacaoStrategyFactory inclui VerificacaoLinhaBase
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_VerificacaoLinhaBase_InstanciaCorreta()
    {
        var factory = new CompensacaoStrategyFactory();
        var strategy = factory.Criar(TipoCenarioPoligonal.VerificacaoLinhaBase);

        Assert.IsType<VerificacaoLinhaBaseStrategy>(strategy);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // 10. LaudoDiscrepanciaLinhaBase: PrecisaoRelativaFormatada com precisão zero
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Laudo_PrecisaoZero_FormatadoComo1Infinito()
    {
        var laudo = new LaudoDiscrepanciaLinhaBase
        {
            PrecisaoRelativa = 0.0
        };

        Assert.Equal("1 : ∞", laudo.PrecisaoRelativaFormatada);
    }
}
