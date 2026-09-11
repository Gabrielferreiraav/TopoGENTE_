using System;
using Xunit;
using TopoGENTE.Domain.ValueObjects;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Infrastructure.Adapters;
using TopoGente.Core.Entities; // Assumindo TerrainVertex ou PontoCoordenada aqui

namespace TopoGENTE.Test.Domain
{
    public class BreaklineConstraintTests
    {
        [Fact]
        public void GenerateMesh_CrossingBreaklines_ThrowsBreaklineConflictException()
        {
            // Arrange: Instanciação dos quatro vértices geodésicos formando um quadrado unitário
            var v0 = new TerrainVertex(0.0, 0.0, 100.0, 0);
            var v1 = new TerrainVertex(1.0, 0.0, 101.0, 1);
            var v2 = new TerrainVertex(1.0, 1.0, 102.0, 2);
            var v3 = new TerrainVertex(0.0, 1.0, 101.5, 3);

            var vertices = new[] { v0, v1, v2, v3 };

            // Arrange: Definição de duas linhas obrigatórias (breaklines) que se cruzam na diagonal
            // Breakline A conecta (0,0) a (1,1). Breakline B conecta (1,0) a (0,1).
            // No plano, o ponto de interseção é (0.5, 0.5) (violação da planaridade de Delaunay).
            var breaklineA = new Breakline(0, 2);
            var breaklineB = new Breakline(1, 3);

            var breaklines = new[] { breaklineA, breaklineB };

            var adapter = new RichFeatureTinfourAdapter();

            // Act & Assert: Garantir que o isolamento fail-fast (SweepLineValidator) intercepte
            var exception = Assert.Throws<BreaklineConflictException>(() =>
            {
                // A execução no adapter dispara preventivamente o SweepLineValidator.ValidarCruzamentos
                // com uma tolerância milimétrica (0.001) para a verificação matemática em ponto flutuante.
                adapter.GenerateBaseDelaunayMesh(vertices, breaklines, 0.001);
            });

            // Assert: A exceção não deve ser nula
            Assert.NotNull(exception);

            // Assert: A mensagem diagnóstica deve ser explícita quanto aos identificadores colidentes
            Assert.Contains("0", exception.Message);
            Assert.Contains("2", exception.Message);
            Assert.Contains("1", exception.Message);
            Assert.Contains("3", exception.Message);
        }
    }
}
