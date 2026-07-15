using System;
using System.IO;
using System.Linq;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGente.Test
{
    public class LeitorFbkTests
    {
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
            "NEZ  0.0000 0.0000 0.0000"
        };

        [Fact]
        public void Ler_DeveExtrairEstacoesDeFbkReal_Quando_Nez_Final_Malformado_For_Removido()
        {
            var leitor = new LeitorFbk();
            var linhas = _fbkEmMemoria
                .Where(linha => !linha.TrimStart().StartsWith("NEZ  0.0000"))
                .ToArray();

            var estacoes = leitor.Ler(linhas);

            Assert.NotNull(estacoes);
            Assert.NotEmpty(estacoes);
            Assert.Equal(4, estacoes.Count);
            Assert.Equal(2, estacoes.Count(e => e.Nome == "E1"));
        }

        [Fact]
        public void Ler_Deve_Falhar_Para_FbkReal_Com_Nez_Sem_Nome()
        {
            var leitor = new LeitorFbk();

            var ex = Assert.Throws<FormatException>(() => leitor.Ler(_fbkEmMemoria));

            Assert.Contains("NEZ", ex.Message);
            Assert.Contains("falharam no parsing", ex.Message);
        }

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

        [Fact]
        public void Ler_Deve_Bloquear_Nez_Sem_Nome_De_Ponto()
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "UNIT METER DECDEG",
                "NEZ  0.0000 0.0000 0.000"
            };

            var ex = Assert.Throws<FormatException>(() => leitor.Ler(linhas));

            Assert.Contains("NEZ", ex.Message);
        }
    }
}
