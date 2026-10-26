using System;
using System.IO;
using System.Linq;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGente.Test
{
    public class LeitorFbkTests
    {
        // Dataset base: poligonal fechada sintética com 4 estações e 1 linha sentinela residual ao final.
        // Representa fielmente o padrão gerado por coletores Topcon/Sokkia ao exportar para FBK.
        private static readonly string[] _fbkEmMemoria = new[]
        {
            "UNIT METER DECDEG",
            "NEZ \"E1\" 100 100 100",
            "STN \"E1\" 1.500",
            "AD VA \"P1\" 10 10 90 \"V\"",
            "STN \"E2\" 1.500",
            "AD VA \"P2\" 10 10 90 \"V\"",
            "STN \"E3\" 1.500",
            "AD VA \"P3\" 10 10 90 \"V\"",
            "STN \"E1\" 1.500",
            "AD VA \"P4\" 10 10 90 \"V\"",
            "NEZ  0.0000 0.0000 0.0000"   // linha sentinela/residual — sem nome, todos zerados
        };

        // ──────────────────────────────────────────────────────────────────────────────
        // Testes de comportamento principal
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Ler_DeveExtrairTodasEstacoes_QuandoFbkContemLinhaSentinelaFinal()
        {
            // Garante que a linha sentinela "NEZ  0.0000 0.0000 0.0000" NÃO aborta a leitura.
            // O arquivo _fbkEmMemoria contém a sentinela — a leitura deve suceder normalmente.
            var leitor = new LeitorFbk();

            var estacoes = leitor.Ler(_fbkEmMemoria);

            Assert.NotNull(estacoes);
            Assert.NotEmpty(estacoes);
            Assert.Equal(4, estacoes.Count);
            Assert.Equal(2, estacoes.Count(e => e.Nome == "E1"));
        }

        [Fact]
        public void Ler_DeveRegistrarAviso_QuandoLinhaSentinelaZeradaForEncontrada()
        {
            // O aviso deve conter "NEZ residual ignorado" para rastreabilidade no painel de diagnóstico.
            var leitor = new LeitorFbk();

            leitor.Ler(_fbkEmMemoria);

            Assert.Contains(
                leitor.UltimosAvisos,
                aviso => aviso.Contains("NEZ residual ignorado"));
        }

        [Fact]
        public void Ler_DeveFalhar_QuandoNezTemNomeValidoMasCoordenadasCorrompidas()
        {
            // NEZ com identificador válido mas valor não numérico nas coordenadas:
            // deve acumular falha e abortar ao final com FormatException.
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ \"PONTO_A\" abc 722000.0 652.1",
                "STN \"E1\" 1.500"
            };

            var ex = Assert.Throws<FormatException>(() => leitor.Ler(linhas));

            Assert.Contains("falharam no parsing", ex.Message);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Testes de detecção sentinela — variações de formato residual
        // ──────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("NEZ  0.0000 0.0000 0.000")]       // espaço duplo, 3 casas decimais (arquivo real 113503.FBK)
        [InlineData("NEZ  0.0000 0.0000 0.0000")]      // espaço duplo, 4 casas decimais
        [InlineData("NEZ 0 0 0")]                       // inteiros zerados sem nome
        [InlineData("NEZ 0.0 0.0 0.0")]                // decimais curtos zerados sem nome
        [InlineData("NE  0.0000 0.0000")]              // variante NE (sem Z)
        [InlineData("NE 0 0")]                         // variante NE inteiros
        public void Ler_DeveIgnorarLinhaComoSentinela_QuandoTodosValoresZeradosSemNomeValido(string linhaResidual)
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "STN \"E1\" 1.500",
                "AD VA \"P1\" 10.0 15.0 90.0 \"V\"",
                linhaResidual
            };

            // Não deve lançar exceção — a sentinela deve ser ignorada silenciosamente
            var estacoes = leitor.Ler(linhas);

            Assert.Single(estacoes);
            Assert.Contains(
                leitor.UltimosAvisos,
                aviso => aviso.Contains("NEZ residual ignorado") || aviso.Contains("NE residual ignorado"));
        }

        [Fact]
        public void Ler_NaoDeveIgnorar_QuandoNezComNomeZeroTemCoordenadasReais()
        {
            // `NEZ 0 100.0 200.0 300.0`: nome "0" é inteiro válido, coordenadas reais → NÃO é sentinela.
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ 0 100.0 200.0 300.0",
                "STN 0 1.500"
            };

            var estacoes = leitor.Ler(linhas);

            Assert.Single(estacoes);
            Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("0"),
                "Ponto de nome '0' com coordenadas reais deve ser registrado como ponto de controle.");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Testes de NomePontoValido — inteiros permitidos, decimais rejeitados
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Ler_DeveAceitarNezComNomeInteiro()
        {
            // Identificadores numéricos inteiros são válidos — consistência com AD VA 1, AD VA 125.
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ 1 7702688.0 722000.0 652.0",
                "NEZ 100 7702600.0 722100.0 651.0",
                "STN 1 1.500"
            };

            var estacoes = leitor.Ler(linhas);

            Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("1"));
            Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("100"));
        }

        [Fact]
        public void Ler_DeveAceitarFbkCompleto_ArquivoReal113503()
        {
            // Smoke test com o arquivo real 113503.FBK que originou o bug.
            // Contém 2 NEZ de controle válidos + 2 linhas sentinela ao final.
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ \"E4\" 7702688.6120 722000.4820 652.186",
                "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
                "STN \"E1\" 1.4100 E",
                "BS \"E4\" 0.0000",
                "PRISM 1.5800",
                "AD VA \"E4\" 0.0000 56.5410 89.5681 \"R\"",
                "AD VA \"P1\" 331.9444 35.6840 89.9445 \"V\"",
                "STN \"P1\" 1.5290 E",
                "BS \"E1\" 0.0000",
                "AD VA \"E1\" 0.0000 35.6840 89.9219 \"R\"",
                "NEZ  0.0000 0.0000 0.000 ",   // sentinela linha 1
                "NEZ  0.0000 0.0000 0.000 "    // sentinela linha 2
            };

            var estacoes = leitor.Ler(linhas);

            Assert.Equal(2, estacoes.Count);
            Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("E4"));
            Assert.True(leitor.UltimosPontosConhecidos.ContainsKey("E1"));
            Assert.Equal(2, leitor.UltimosAvisos.Count(a => a.Contains("NEZ residual ignorado")));
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Testes existentes mantidos
        // ──────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("UNIT FOOT DECDEG")]
        [InlineData("UNIT METER GON")]
        public void Ler_Deve_Falhar_Fast_Para_Unidades_Nao_Suportadas(string linhaUnit)
        {
            var leitor = new LeitorFbk();

            Assert.Throws<NotSupportedException>(() => leitor.Ler(new[] { linhaUnit }));
        }

        [Fact]
        public void Ler_Deve_Bloquear_Arquivo_Quando_Numero_Usar_Virgula_Decimal()
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "STN \"E1\" 1.500",
                "AD VA \"P1\" 331,9444 35.6840 89.9445 \"V\""
            };

            var ex = Assert.Throws<FormatException>(() => leitor.Ler(linhas));

            Assert.Contains("separador decimal é ponto", ex.Message);
            Assert.Contains("331,9444", ex.Message);
        }

        [Fact]
        public void Ler_Deve_Notificar_Nez_Duplicado_Com_Coordenadas_Equivalentes()
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
                "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
                "STN \"E1\" 1.4100"
            };

            leitor.Ler(linhas);

            Assert.Contains(leitor.UltimosAvisos, aviso => aviso.Contains("NEZ DUPLICADO"));
        }

        [Fact]
        public void Ler_Deve_Notificar_Conflito_Nez_Duplicado_Divergente()
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ \"E1\" 7702635.7870 722020.7120 651.934",
                "NEZ \"E1\" 7702635.7870 722020.7200 651.934",
                "STN \"E1\" 1.4100"
            };

            leitor.Ler(linhas);

            Assert.Contains(leitor.UltimosAvisos, aviso => aviso.Contains("CONFLITO NEZ"));
        }
    }
}
