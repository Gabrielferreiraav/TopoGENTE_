using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Core.Strategies;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;
using Xunit.Abstractions;

namespace TopoGENTE.Test;

public class Analise113503FbkTests
{
    private readonly ITestOutputHelper _output;

    public Analise113503FbkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Analisar_113503_FBK_Detalhadamente()
    {
        string path = @"c:\Users\gabri\source\repos\TopoGENTE_\readme\113503.FBK";
        if (!File.Exists(path)) path = Path.GetFullPath(@"../../../../readme/113503.FBK");
        Assert.True(File.Exists(path), $"Arquivo não encontrado: {path}");

        var linhas = File.ReadAllLines(path);
        var leitor = new LeitorFbk();
        var estacoes = leitor.Ler(linhas, ConfiguracaoImportacao.PadraoFbk);
        var pontosConhecidos = new Dictionary<string, PontoCoordenada>(leitor.UltimosPontosConhecidos, StringComparer.OrdinalIgnoreCase);

        _output.WriteLine($"=== ARQUIVO 113503.FBK ===");
        _output.WriteLine($"Total de linhas: {linhas.Length}");
        _output.WriteLine($"Total de estações lidas: {estacoes.Count}");

        // 1. Cenários de Tolerância no Modo Fechada (Bowditch)
        TestarProcessamento(estacoes, pontosConhecidos, ParametrosTolerancia.ClasseIIP, "1. Preset ClasseIIP (k=2, sigma=5\", P=5.000, c=3.0, Termo=0\")");
        TestarProcessamento(estacoes, pontosConhecidos, ParametrosTolerancia.ClasseIP, "2. Preset ClasseIP (k=2, sigma=2\", P=10.000, c=1.5, Termo=0\")");
        TestarProcessamento(estacoes, pontosConhecidos, ParametrosTolerancia.Padrao, "3. Preset Padrão (k=2, sigma=5\", P=12.000, c=150.0, Termo=10\")");

        // Customizado 1: Estação de 10" (k=2, sigma=10", Termo=0")
        var tolEstacao10 = ParametrosTolerancia.CriarPersonalizado(2.0, 10.0, 5000, 150.0, termoConstanteAngular: 0.0);
        TestarProcessamento(estacoes, pontosConhecidos, tolEstacao10, "4. Personalizado: Estação Total de 10\" (k=2, sigma=10\", Termo=0\")");

        // Customizado 2: Estação de 5" com Termo de Centragem NBR 13.133 de 15"
        var tolCentragem15 = ParametrosTolerancia.CriarPersonalizado(2.0, 5.0, 5000, 150.0, termoConstanteAngular: 15.0);
        TestarProcessamento(estacoes, pontosConhecidos, tolCentragem15, "5. Personalizado: Estação 5\" com Termo Centragem 15\" (NBR 13.133 visadas curtas)");

        // 2. Cenário de Verificação de Linha de Base (E1 -> E4)
        TestarLinhaBase(estacoes, pontosConhecidos);

        // 3. Cenário com correção de campo na visada de retorno E1 -> E4
        TestarCorrecaoLeituraCampo(linhas, pontosConhecidos);
    }

    private void TestarProcessamento(List<Estacao> estacoes, Dictionary<string, PontoCoordenada> ptosConhecidos, ParametrosTolerancia tol, string desc)
    {
        _output.WriteLine($"\n------------------------------------------------------------");
        _output.WriteLine($"TESTE: {desc}");
        _output.WriteLine($"------------------------------------------------------------");
        var classificador = new ClassificadorGrafo();
        var proc = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());

        var seqs = new List<SequenciaPoligonal>
        {
            new SequenciaPoligonal
            {
                Nome = "Poligonal Principal",
                EhPrincipal = true,
                Estacoes = new List<string> { "E1", "P1", "P2", "E1" },
                Metadados = new MetadadosCenario
                {
                    TipoCenario = TipoCenarioPoligonal.Fechada,
                    Tolerancias = tol,
                    NomeRe = "E4",
                    SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
                }
            }
        };

        var resultado = proc.Processar(seqs, estacoes, ptosConhecidos);

        _output.WriteLine($"-> Aprovado na Norma: {resultado.AprovadoNorma}");
        _output.WriteLine($"-> Erro Angular: {resultado.ErroAngular:F6}° ({resultado.ErroAngular * 3600:F2}\")");
        _output.WriteLine($"-> Tolerância Angular: {tol.CalcularToleranciaAngularGraus(3):F6}° ({tol.CalcularToleranciaAngularSegundos(3):F2}\")");
        _output.WriteLine($"-> Erro Fechamento X: {resultado.ErroFechamentoX * 1000:F2} mm");
        _output.WriteLine($"-> Erro Fechamento Y: {resultado.ErroFechamentoY * 1000:F2} mm");
        _output.WriteLine($"-> Erro Linear Total: {resultado.ErroLinear * 1000:F2} mm");
        _output.WriteLine($"-> Perímetro Total: {resultado.Perimetro:F3} m");
        _output.WriteLine($"-> Precisão Obtida (1/P): 1 : {(resultado.Precisao > 0 ? Math.Round(1.0 / resultado.Precisao) : 0)}");
        _output.WriteLine($"-> Precisão Mínima Exigida: 1 : {tol.DenominadorPrecisaoLinear}");
        _output.WriteLine($"-> Erro Altimétrico Z: {resultado.ErroFechamentoZ * 1000:F2} mm");
        _output.WriteLine($"-> Tolerância Altimétrica: {tol.CalcularToleranciaAltimetricaMetros(resultado.Perimetro) * 1000:F2} mm");

        if (resultado.Alertas != null && resultado.Alertas.Count > 0)
        {
            _output.WriteLine("-> Alertas de Reprovação:");
            foreach (var a in resultado.Alertas)
            {
                _output.WriteLine($"   * {a}");
            }
        }
        else
        {
            _output.WriteLine("-> Nenhum alerta. Levantamento 100% CONFORME!");
        }
    }

    private void TestarLinhaBase(List<Estacao> estacoes, Dictionary<string, PontoCoordenada> ptosConhecidos)
    {
        _output.WriteLine($"\n------------------------------------------------------------");
        _output.WriteLine($"TESTE: Verificação de Linha de Base (E1 -> E4)");
        _output.WriteLine($"------------------------------------------------------------");

        var classificador = new ClassificadorGrafo();
        var proc = new LevantamentoProcessor(classificador, new CompensacaoStrategyFactory());

        var ptPartida = ptosConhecidos["E1"];
        var ptRe = ptosConhecidos["E4"];

        var seqs = new List<SequenciaPoligonal>
        {
            new SequenciaPoligonal
            {
                Nome = "Poligonal Linha de Base",
                EhPrincipal = true,
                Estacoes = new List<string> { "E1", "P1", "P2", "E1" },
                Metadados = new MetadadosCenario
                {
                    TipoCenario = TipoCenarioPoligonal.VerificacaoLinhaBase,
                    Tolerancias = ParametrosTolerancia.ClasseIIP,
                    NomeRe = "E4",
                    UsarCoordenadaRe = true,
                    ReX = ptRe.X,
                    ReY = ptRe.Y,
                    ReZ = ptRe.Z,
                    NomeChegada = "E1",
                    ChegadaX = ptPartida.X,
                    ChegadaY = ptPartida.Y,
                    ChegadaZ = ptPartida.Z,
                    SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
                }
            }
        };

        var resultado = proc.Processar(seqs, estacoes, ptosConhecidos);

        _output.WriteLine($"-> Aprovado Linha de Base: {resultado.AprovadoNorma}");
        if (resultado.LaudoLinhaBase != null)
        {
            var l = resultado.LaudoLinhaBase;
            _output.WriteLine($"-> Discrepância Linear (e_l): {l.ErroLinearFormatado} (dx={l.DeltaEsteMetros * 1000:F1} mm, dy={l.DeltaNorteMetros * 1000:F1} mm, dz={l.DeltaZMetros * 1000:F1} mm)");
            _output.WriteLine($"-> Discrepância Angular (e_a): {l.ErroAngularFormatado}");
            _output.WriteLine($"-> Precisão Relativa: {l.PrecisaoRelativaFormatada}");
            _output.WriteLine($"-> Tolerância Linear Admissível: {l.ToleranciaLinearMm:F1} mm");
            _output.WriteLine($"-> Tolerância Angular Admissível: {l.ToleranciaAngularSegundos:F1}\"");
            _output.WriteLine($"-> Status Geral: {(l.Aprovado ? "APROVADO" : "REPROVADO")}");
            if (!l.Aprovado) _output.WriteLine($"-> Motivo: {l.MotivoReprovacao}");
        }
    }

    private void TestarCorrecaoLeituraCampo(string[] linhasOriginais, Dictionary<string, PontoCoordenada> ptosConhecidos)
    {
        _output.WriteLine($"\n------------------------------------------------------------");
        _output.WriteLine($"TESTE: Simulação com Leitura de Campo Corrigida em 30.96\"");
        _output.WriteLine($"------------------------------------------------------------");

        // Na linha 152 original: AD VA "E4" 12.7467 56.5650 89.6436 "V"
        // O erro angular é de +0.0086° (+30.96").
        // Se a leitura do topógrafo fosse 12.7381° (diferença de apenas 31 segundos):
        var linhasModificadas = (string[])linhasOriginais.Clone();
        for (int i = 0; i < linhasModificadas.Length; i++)
        {
            if (linhasModificadas[i].Contains("12.7467"))
            {
                linhasModificadas[i] = linhasModificadas[i].Replace("12.7467", "12.7381");
                _output.WriteLine($"Linha {i+1} alterada: '{linhasOriginais[i]}' -> '{linhasModificadas[i]}'");
                break;
            }
        }

        var leitor = new LeitorFbk();
        var estacoesMod = leitor.Ler(linhasModificadas, ConfiguracaoImportacao.PadraoFbk);
        TestarProcessamento(estacoesMod, ptosConhecidos, ParametrosTolerancia.ClasseIIP, "Simulação Campo: Leitura E1->E4 ajustada em 31\" (com Preset ClasseIIP)");
    }
}
