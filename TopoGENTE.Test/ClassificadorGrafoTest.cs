using System;
using System.Collections.Generic;
using Xunit;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;

namespace TopoGente.Core.Tests
{
    public class ClassificadorGrafoTests
    {
        private readonly ClassificadorGrafo _classificador;

        public ClassificadorGrafoTests()
        {
            // O teste atua como Adaptador Primário isolado do mundo externo
            _classificador = new ClassificadorGrafo();
        }

        [Fact]
        public void Testar_Classe_Equivalencia_Vante_CaminhoPrincipal()
        {
            // Arrange: Partição de Equivalência Válida
            var metadados = new MetadadosCenario { TipoCenario = TipoCenarioPoligonal.AbertaOrientada };

            var leituraE1_E2 = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "E2", Tipo = TipoLeitura.Irradiacao }; // Inicia errado de propósito 
            var leituraE2_E3 = new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E3", Tipo = TipoLeitura.Irradiacao };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal> { leituraE1_E2 } },
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal> { leituraE2_E3 } },
                new Estacao { Nome = "E3", Leituras = new List<LeituraEstacaoTotal>() }
            };

            // Act: O sistema deve analisar a ordem (E1 -> E2 -> E3)
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: Validação estrita de que a visada para a próxima estação é Poligonal (Vante)
            Assert.Equal(TipoLeitura.Poligonal, leituraE1_E2.Tipo);
            Assert.Equal(TipoLeitura.Poligonal, leituraE2_E3.Tipo);
        }

        [Fact]
        public void Testar_Classe_Equivalencia_Re_Orientacao()
        {
            // Arrange: Partição de Equivalência Válida (Comportamento regressivo)
            var metadados = new MetadadosCenario { TipoCenario = TipoCenarioPoligonal.AbertaOrientada };

            var leituraE2_E1 = new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "E1" };
            var leituraE3_E2 = new LeituraEstacaoTotal { EstacaoOcupada = "E3", PontoVisado = "E2" };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal>() },
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal> { leituraE2_E1 } },
                new Estacao { Nome = "E3", Leituras = new List<LeituraEstacaoTotal> { leituraE3_E2 } }
            };

            // Act
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: Visada para a estação imediatamente anterior deve ser Ré
            Assert.Equal(TipoLeitura.Re, leituraE2_E1.Tipo);
            Assert.Equal(TipoLeitura.Re, leituraE3_E2.Tipo);
        }

        [Fact]
        public void Testar_Classe_Equivalencia_Irradiacao()
        {
            // Arrange: Partição de Equivalência Válida (Leitura periférica)
            var metadados = new MetadadosCenario { TipoCenario = TipoCenarioPoligonal.AbertaOrientada };

            var leituraMuro = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "MURO" };
            var leituraPoste = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "POSTE" };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal> { leituraMuro, leituraPoste } },
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal>() } // E2 existe, mas não é visada
            };

            // Act
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: Pontos não contidos no caminho principal e que não são ré, devem ser Irradiação
            Assert.Equal(TipoLeitura.Irradiacao, leituraMuro.Tipo);
            Assert.Equal(TipoLeitura.Irradiacao, leituraPoste.Tipo);
        }

        [Fact]
        public void Testar_Condicao_Limite_Re_EstacaoInicial()
        {
            // Arrange: Análise de Valor Limite (Fronteira Inicial E0)
            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.AbertaOrientada,
                NomeRe = "MARCO_ZERO" // Referência externa forçada via metadados
            };

            var leituraE1_Re = new LeituraEstacaoTotal { EstacaoOcupada = "E1", PontoVisado = "MARCO_ZERO" };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal> { leituraE1_Re } }
            };

            // Act
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: A primeira estação não tem estação anterior. Deve usar metadados para classificar Ré.
            Assert.Equal(TipoLeitura.Re, leituraE1_Re.Tipo);
        }

        [Fact]
        public void Testar_Condicao_Limite_Vante_FechamentoLoop()
        {
            // Arrange: Análise de Valor Limite (Fronteira Final para Poligonal Fechada)
            var metadados = new MetadadosCenario { TipoCenario = TipoCenarioPoligonal.Fechada };

            // A última estação (E3) visa a primeira estação cronológica (E1)
            var leituraFechamento = new LeituraEstacaoTotal { EstacaoOcupada = "E3", PontoVisado = "E1" };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal>() },
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal>() },
                new Estacao { Nome = "E3", Leituras = new List<LeituraEstacaoTotal> { leituraFechamento } }
            };

            // Act
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: No cenário Fechada, a visada final apontando para o início é uma aresta de Vante 
            Assert.Equal(TipoLeitura.Poligonal, leituraFechamento.Tipo);
        }

        [Fact]
        public void Testar_Condicao_Limite_Vante_ChegadaEnquadrada()
        {
            // Arrange: Análise de Valor Limite (Fronteira Final para Poligonal Enquadrada)
            var metadados = new MetadadosCenario
            {
                TipoCenario = TipoCenarioPoligonal.Enquadrada,
                NomeChegada = "M99" // Ponto de controle geodésico de chegada
            };

            var leituraChegada = new LeituraEstacaoTotal { EstacaoOcupada = "E2", PontoVisado = "M99" };

            var estacoes = new List<Estacao>
            {
                new Estacao { Nome = "E1", Leituras = new List<LeituraEstacaoTotal>() },
                new Estacao { Nome = "E2", Leituras = new List<LeituraEstacaoTotal> { leituraChegada } }
            };

            // Act
            _classificador.ClassificarArestasGrafo(estacoes, metadados);

            // Assert: A última visada correspondendo a `NomeChegada` é Poligonal (Vante)
            Assert.Equal(TipoLeitura.Poligonal, leituraChegada.Tipo);
        }

        [Fact]
        public void Testar_Classe_Equivalencia_Invalida_ListaVazia_Nula()
        {
            // Arrange: Partição de Equivalência (Membro de conjunto inválido)
            var metadados = new MetadadosCenario { TipoCenario = TipoCenarioPoligonal.AbertaOrientada };

            // Act & Assert: Garantir que não existam falhas abruptas (NullReferenceException) 
            // O sistema deve processar a interrupção segura e o compilador deve provar isso registrando ausência de exceções.
            var exceptionNulo = Record.Exception(() => _classificador.ClassificarArestasGrafo(null!, metadados));
            Assert.Null(exceptionNulo);

            var exceptionVazio = Record.Exception(() => _classificador.ClassificarArestasGrafo(new List<Estacao>(), metadados));
            Assert.Null(exceptionVazio);
        }
    }
}