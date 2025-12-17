using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;

namespace TopoGENTE.Test
{
    public class ExportadorDxfTest
    {
        [Fact]
        public void SalvarDxf_Sucesso()
        {
            var servicoExportacao = new ExportadorDxfService();
            string caminhoArquivo = Path.GetTempFileName() + ".dxf";
            var pontos = new List<PontoCoordenada>
            {
                new PontoCoordenada {Nome = "A1", X = 100, Y = 200, Z = 10, EhPontoPoligonal = true },
                new PontoCoordenada { Nome ="P1",X = 110, Y = 210, Z = 11,EhPontoPoligonal = false },
            };

            try
            {
                servicoExportacao.SalvarDxf(pontos, caminhoArquivo);
                Assert.True(File.Exists(caminhoArquivo));
                string conteudo = File.ReadAllText(caminhoArquivo);
                Assert.Contains("SECTION", conteudo);
                Assert.Contains("HEADER", conteudo);
                Assert.Contains("M1", conteudo);
                Assert.Contains("TOPO_POLIGONAL", conteudo);
            }
            finally
            {
                if (File.Exists(caminhoArquivo))
                {
                    File.Delete(caminhoArquivo);
                }
            }
        }
    }
}
