using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Infrastructure.Adapters.Leitores;
using Xunit;

namespace TopoGente.Test
{
    public class ClassificacaoVisadasHexagonalTests
    {
        [Fact]
        public void LeitorFbk_Deve_Mapear_Purpose_Sem_Definir_Tipo_Topologico()
        {
            var leitor = new LeitorFbk();
            var linhas = new[]
            {
                "STN \"E1\" 1.500",
                "BS \"REF\" 0.0000",
                "AD VA \"E2\" 10.0000 100.000 90.0000 \"VANTE\""
            };

            var leituras = leitor.Ler(linhas).SelectMany(e => e.Leituras).ToList();

            Assert.All(leituras, leitura => Assert.Equal(TipoLeitura.Irradiacao, leitura.Tipo));
            Assert.Equal("re", leituras[0].Purpose);
            Assert.Equal("vante", leituras[1].Purpose);
        }

        [Fact]
        public void LeitorCsv_Deve_Mapear_Purpose_Sem_Definir_Tipo_Topologico()
        {
            var leitor = new LeitorCsvPadrao();
            var linhas = new[]
            {
                "E1,1.500,REF,RE,0.0000,90.0000,100.000,1.500,1",
                "E1,1.500,E2,VANTE,10.0000,90.0000,100.000,1.500,2"
            };

            var leituras = leitor.Ler(linhas).SelectMany(e => e.Leituras).ToList();

            Assert.All(leituras, leitura => Assert.Equal(TipoLeitura.Irradiacao, leitura.Tipo));
            Assert.Equal("re", leituras[0].Purpose);
            Assert.Equal("vante", leituras[1].Purpose);
        }

        [Fact]
        public void Classificador_Deve_Promover_Re_Apenas_Quando_PontoVisado_For_NomeRe()
        {
            var estacoes = new List<Estacao>
            {
                new Estacao
                {
                    Nome = "E1",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "REF", Purpose = "re" },
                        new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E2", Purpose = "vante" },
                        new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "P100" }
                    }
                }
            };

            var metadados = new MetadadosCenario
            {
                NomeRe = "REF",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2" }
            };

            new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados);

            Assert.Equal(TipoLeitura.Re, estacoes[0].Leituras[0].Tipo);
            Assert.Equal(TipoLeitura.Poligonal, estacoes[0].Leituras[1].Tipo);
            Assert.Equal(TipoLeitura.Irradiacao, estacoes[0].Leituras[2].Tipo);
        }

        [Fact]
        public void Classificador_Deve_Classificar_Aresta_Reversa_Topologica_Como_ReLocal()
        {
            var leituraReversa = new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E1" };
            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal> { leituraReversa } }
            };

            var metadados = new MetadadosCenario
            {
                NomeRe = "REF",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2" }
            };

            new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados);

            Assert.Equal(TipoLeitura.ReLocal, leituraReversa.Tipo);
        }

        [Fact]
        public void Classificador_Nao_Deve_Falhar_Quando_Re_Local_Nao_For_NomeRe_Normativo()
        {
            var reLocal = new LeituraEstacaoTotal { EstacaoOcupada = "P1", PontoVisado = "E1", Purpose = "re" };
            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "P1", Leituras = new List<LeituraEstacaoTotal> { reLocal } }
            };

            var metadados = new MetadadosCenario
            {
                NomeRe = "E4",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2" }
            };

            new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados);

            Assert.Equal(TipoLeitura.ReLocal, reLocal.Tipo);
        }

        [Fact]
        public void Classificador_Deve_Diferenciar_Ocupacoes_Com_Mesmo_Nome_Na_Sequencia()
        {
            var primeiraE1ParaP1 = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "P1", Purpose = "vante" };
            var p1ParaE1 = new LeituraEstacaoTotal { EstacaoOcupada = "P1", PontoVisado = "E1", Purpose = "re" };
            var p1ParaP2 = new LeituraEstacaoTotal { EstacaoOcupada = "P1", PontoVisado = "P2", Purpose = "vante" };
            var p2ParaP1 = new LeituraEstacaoTotal { EstacaoOcupada = "P2", PontoVisado = "P1", Purpose = "re" };
            var p2ParaE1 = new LeituraEstacaoTotal { EstacaoOcupada = "P2", PontoVisado = "E1", Purpose = "vante" };
            var segundaE1ParaP2 = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "P2", Purpose = "re" };
            var segundaE1ParaE4 = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E4", Purpose = "vante" };

            var primeiraE1 = new Estacao { Nome = "E1", AlturaInstrumento = 1.410, Leituras = new List<LeituraEstacaoTotal> { primeiraE1ParaP1 } };
            var p1 = new Estacao { Nome = "P1", AlturaInstrumento = 1.510, Leituras = new List<LeituraEstacaoTotal> { p1ParaE1, p1ParaP2 } };
            var p2 = new Estacao { Nome = "P2", AlturaInstrumento = 1.529, Leituras = new List<LeituraEstacaoTotal> { p2ParaP1, p2ParaE1 } };
            var segundaE1 = new Estacao { Nome = "E1", AlturaInstrumento = 1.483, Leituras = new List<LeituraEstacaoTotal> { segundaE1ParaP2, segundaE1ParaE4 } };

            var estacoes = new List<Estacao> { primeiraE1, p1, p2, segundaE1 };
            var metadados = new MetadadosCenario
            {
                NomeRe = "E4",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "P1", "P2", "E1" }
            };

            new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados);

            Assert.Equal(TipoLeitura.Poligonal, primeiraE1ParaP1.Tipo);
            Assert.Equal(TipoLeitura.ReLocal, p1ParaE1.Tipo);
            Assert.Equal(TipoLeitura.Poligonal, p1ParaP2.Tipo);
            Assert.Equal(TipoLeitura.ReLocal, p2ParaP1.Tipo);
            Assert.Equal(TipoLeitura.Poligonal, p2ParaE1.Tipo);
            Assert.Equal(TipoLeitura.ReLocal, segundaE1ParaP2.Tipo);
            Assert.Equal(TipoLeitura.Re, segundaE1ParaE4.Tipo);
        }

        [Fact]
        public void Classificador_Deve_Falhar_Quando_Purpose_Vante_Nao_Bater_Com_Sequencia()
        {
            var estacoes = new List<Estacao>
            {
                new Estacao
                {
                    Nome = "E1",
                    Leituras = new List<LeituraEstacaoTotal>
                    {
                        new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E3", Purpose = "vante" }
                    }
                }
            };

            var metadados = new MetadadosCenario
            {
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2" }
            };

            Assert.Throws<DadosInsuficientesException>(
                () => new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados));
        }

        [Fact]
        public void Classificador_Nao_Deve_Forcar_NomeChegada_Como_Poligonal_Fora_Da_Sequencia()
        {
            var leituraChegadaAntecipada = new LeituraEstacaoTotal
            {
                EstacaoOcupada = "E1",
                PontoVisado = "M_FINAL"
            };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal> { leituraChegadaAntecipada } }
            };

            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Enquadrada,
                NomeChegada = "M_FINAL",
                SequenciaEstacoesSelecionadas = new List<string> { "E1", "E2" }
            };

            new ClassificadorGrafo().ClassificarArestasGrafo(estacoes, metadados);

            Assert.Equal(TipoLeitura.Irradiacao, leituraChegadaAntecipada.Tipo);
        }
    }
}
