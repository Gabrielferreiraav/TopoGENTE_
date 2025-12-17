using System;
using System.Collections.Generic;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;

namespace TopoGENTE.Test
{
    public class OrganizarCaminhamentoTest
    {
        [Fact]
        public void UnificarEstacoes()
        {
            var listaBruta = new List<Estacao>
            {
                new Estacao
                { Nome = "A1", Leituras = { new LeituraEstacaoTotal { PontoVisado = "RÉ" } },
                },
                new Estacao { Nome = "A1", Leituras = { new LeituraEstacaoTotal { PontoVisado = "VANTE" } }, }

            };

            var orgazinador = new OrganizarCaminhamento();

            var resultado = orgazinador.UnificarEstacoes(listaBruta);

            Assert.Single(resultado);
            Assert.Equal("A1", resultado[0].Nome);
            Assert.Equal(2, resultado[0].Leituras.Count);
        }
        [Fact]
        public void OrganizarVante_OrdemSequencial()
        {
            var estacaoA = new Estacao { Nome = "A" };
            estacaoA.Leituras.Add(new LeituraEstacaoTotal { PontoVisado = "B", Tipo = TipoLeitura.Poligonal });

            var estacaoB = new Estacao { Nome = "B" };
            estacaoB.Leituras.Add(new LeituraEstacaoTotal { PontoVisado = "C", Tipo = TipoLeitura.Poligonal });

            var estacaoC = new Estacao { Nome = "C" };
            var listaBruta = new List<Estacao> { estacaoB, estacaoC, estacaoA };
            var organizador = new OrganizarCaminhamento();
            var resultado = organizador.OrganizarPorVante(listaBruta,"A");

            Assert.Contains(resultado, l => l.EstacaoOcupada == "A" && l.PontoVisado == "B");
            Assert.NotEmpty(resultado);


        }
    }
}
