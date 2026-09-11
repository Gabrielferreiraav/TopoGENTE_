using System;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGENTE.Test
{
    public class ImportacaoCadernetaTests
    {
        private readonly LeitorCsvPadrao _leitor = new();

        [Fact]
        public void Deve_Processar_Arquivo_CSV_Com_Formato_Valido()
        {
            var linhas = new[]
            {
                "E1,108.0000,Vante,90.0000,100.000,1.500,E2,1.500",
                "E2,0.0000,Re,90.0000,100.000,1.500,E1,1.500"
            };
            var resultado = _leitor.Ler(linhas);
            Assert.Equal(2, resultado.Count);
            Assert.All(resultado, l => Assert.NotNull(l.Nome));
        }

        [Fact]
        public void Deve_Ignorar_Linha_Com_Colunas_Insuficientes()
        {
            var linhas = new[] { "E1,108.0000,Vante" };
            var resultado = _leitor.Ler(linhas);
            Assert.Empty(resultado);
        }

        [Fact]
        public void Deve_Ignorar_Linhas_Vazias_E_Comentarios()
        {
            var linhas = new[]
            {
                "# comentario",
                "",
                "E1,108.0000,Vante,90.0000,100.000,1.500,E2,1.500"
            };
            var resultado = _leitor.Ler(linhas);
            Assert.Single(resultado);
        }

        [Fact]
        public void Deve_Aceitar_Separador_PontoVirgula()
        {
            var linhas = new[] { "E1;108.0000;Vante;90.0000;100.000;1.500;E2;1.500" };
            var resultado = _leitor.Ler(linhas);
            Assert.Single(resultado);
            Assert.Equal("E1", resultado[0].Nome);
        }
    }
}