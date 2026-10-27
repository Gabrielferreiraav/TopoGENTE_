using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using TopoGente.Infrastructure.Adapters.Exportadores;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;
using Xunit;

namespace TopoGENTE.Test.MDT;

public class ExportadorDxfTests
{
    [Theory]
    [InlineData("Cerca de Arame", "CERCA_DE_ARAME")]
    [InlineData("Árvore 10m (Grande)", "ARVORE_10M_GRANDE")]
    [InlineData("Eixo - Pista 1", "EIXO_PISTA_1")]
    [InlineData("Talude / Crista", "TALUDE_CRISTA")]
    [InlineData("Muro de Arrimo (H=2.5m)", "MURO_DE_ARRIMO_H_2_5M")]
    public void SanitizarNomeCamada_DescricaoComAcentosEEspacos_DeveRetornarIdentificadorSanitizado(string entrada, string esperado)
    {
        string? resultado = SanitizadorCamadaDxf.SanitizarNomeCamada(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("---___---")]
    [InlineData("!@#$%¨&*()")]
    public void SanitizarNomeCamada_DescricaoVaziaOuCaracteresInvalidos_DeveRetornarNull(string? entrada)
    {
        string? resultado = SanitizadorCamadaDxf.SanitizarNomeCamada(entrada);
        Assert.Null(resultado);
    }

    [Fact]
    public void ObterNomeCamadaPonto_PontoPoligonalComDescricao_DevePrevalecerTopoPoligonal()
    {
        var ponto = new PontoCoordenada
        {
            Nome = "P1",
            X = 100,
            Y = 200,
            Z = 50,
            EhPontoPoligonal = true,
            Descricao = "POSTE_ENERGIA"
        };

        string camada = SanitizadorCamadaDxf.ObterNomeCamadaPonto(ponto);
        Assert.Equal("TOPO_POLIGONAL", camada);
    }

    [Fact]
    public void ObterNomeCamadaPonto_IrradiacaoComDescricaoValida_DeveRetornarCamadaTematicaSanitizada()
    {
        var ponto = new PontoCoordenada
        {
            Nome = "101",
            X = 120,
            Y = 230,
            Z = 48,
            EhPontoPoligonal = false,
            Descricao = "Árvore (Ipê)"
        };

        string camada = SanitizadorCamadaDxf.ObterNomeCamadaPonto(ponto);
        Assert.Equal("TOPO_FEICAO_ARVORE_IPE", camada);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("---")]
    public void ObterNomeCamadaPonto_IrradiacaoSemDescricaoOuInvalida_DeveRetornarTopoIrradiacoes(string? descricao)
    {
        var ponto = new PontoCoordenada
        {
            Nome = "102",
            X = 150,
            Y = 250,
            Z = 45,
            EhPontoPoligonal = false,
            Descricao = descricao ?? string.Empty
        };

        string camada = SanitizadorCamadaDxf.ObterNomeCamadaPonto(ponto);
        Assert.Equal("TOPO_IRRADIACOES", camada);
    }

    [Fact]
    public void ExportarDxf_PayloadVazio_DeveLancarExportacaoPayloadVazioException()
    {
        var servico = new ExportadorDxfService();
        var payload = new DxfExportPayload(
            new List<PontoCoordenada>(),
            new List<PontoCoordenada>()
        );

        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_vazio_{Guid.NewGuid():N}.dxf");

        try
        {
            Assert.Throws<ExportacaoPayloadVazioException>(() => servico.SalvarDxf(payload, tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportarDxf_PayloadNulo_DeveLancarArgumentNullException()
    {
        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_null_{Guid.NewGuid():N}.dxf");

        Assert.Throws<ArgumentNullException>(() => servico.SalvarDxf(null!, tempPath));
    }

    [Fact]
    public void ExportarDxf_PoligonalEIrradiacoes_DeveGerarArquivoDxfValidoComTodasEntidadesERoseta()
    {
        var p1 = new PontoCoordenada { Nome = "P1", X = 1000.000, Y = 2000.000, Z = 100.500, EhPontoPoligonal = true };
        var p2 = new PontoCoordenada { Nome = "P2", X = 1050.000, Y = 2030.000, Z = 102.125, EhPontoPoligonal = true };
        var irr1 = new PontoCoordenada { Nome = "IRR1", X = 1020.000, Y = 2010.000, Z = 101.200, EhPontoPoligonal = false, Descricao = "CERCA_ARAME" };
        var irr2 = new PontoCoordenada { Nome = "IRR2", X = 1030.000, Y = 2020.000, Z = 101.800, EhPontoPoligonal = false, Descricao = "" };

        var payload = new DxfExportPayload(
            new List<PontoCoordenada> { p1, p2 },
            new List<PontoCoordenada> { irr1, irr2 }
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_dxf_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            Assert.True(File.Exists(tempPath));

            // Recarregar via netDxf para auditoria geométrica rigorosa
            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);
            Assert.Equal(DxfVersion.AutoCad2000, dxf.DrawingVariables.AcadVer);

            // Validação de Camadas Padronizadas e Temáticas
            Assert.True(dxf.Layers.Contains("TOPO_POLIGONAL"));
            Assert.True(dxf.Layers.Contains("TOPO_IRRADIACOES"));
            Assert.True(dxf.Layers.Contains("TOPO_NOMES"));
            Assert.True(dxf.Layers.Contains("TOPO_COTAS"));
            Assert.True(dxf.Layers.Contains("TOPO_DESCRICOES"));
            Assert.True(dxf.Layers.Contains("TOPO_FEICAO_CERCA_ARAME"));

            // Validação das Cores ACI
            Assert.Equal(AciColor.Red, dxf.Layers["TOPO_POLIGONAL"].Color);
            Assert.Equal(AciColor.Green, dxf.Layers["TOPO_IRRADIACOES"].Color);
            Assert.Equal(AciColor.LightGray, dxf.Layers["TOPO_NOMES"].Color);
            Assert.Equal(AciColor.Yellow, dxf.Layers["TOPO_COTAS"].Color);
            Assert.Equal(AciColor.Cyan, dxf.Layers["TOPO_DESCRICOES"].Color);
            Assert.Equal(AciColor.Cyan, dxf.Layers["TOPO_FEICAO_CERCA_ARAME"].Color);

            // Validação da Linha de Alinhamento da Poligonal (P1 -> P2)
            var linhas = dxf.Entities.Lines.ToList();
            Assert.Single(linhas);
            Assert.Equal("TOPO_POLIGONAL", linhas[0].Layer.Name);
            Assert.Equal(1000.0, linhas[0].StartPoint.X, 3);
            Assert.Equal(2000.0, linhas[0].StartPoint.Y, 3);
            Assert.Equal(1050.0, linhas[0].EndPoint.X, 3);
            Assert.Equal(2030.0, linhas[0].EndPoint.Y, 3);

            // Validação das Entidades Ponto 3D
            var pontosDxf = dxf.Entities.Points.ToList();
            Assert.Equal(4, pontosDxf.Count);

            var pontoP1 = pontosDxf.FirstOrDefault(pt => Math.Abs(pt.Position.X - 1000.0) < 1e-4);
            Assert.NotNull(pontoP1);
            Assert.Equal("TOPO_POLIGONAL", pontoP1.Layer.Name);
            Assert.Equal(100.500, pontoP1.Position.Z, 3);

            var pontoIrr1 = pontosDxf.FirstOrDefault(pt => Math.Abs(pt.Position.X - 1020.0) < 1e-4);
            Assert.NotNull(pontoIrr1);
            Assert.Equal("TOPO_FEICAO_CERCA_ARAME", pontoIrr1.Layer.Name);

            var pontoIrr2 = pontosDxf.FirstOrDefault(pt => Math.Abs(pt.Position.X - 1030.0) < 1e-4);
            Assert.NotNull(pontoIrr2);
            Assert.Equal("TOPO_IRRADIACOES", pontoIrr2.Layer.Name);

            // Validação da Roseta de Textos Radiais
            var textos = dxf.Entities.Texts.ToList();

            // Rótulos de Nomes: 4 pontos, layer TOPO_NOMES, alinhamento BottomLeft, offset (+0.30, +0.30)
            var textosNomes = textos.Where(t => t.Layer.Name == "TOPO_NOMES").ToList();
            Assert.Equal(4, textosNomes.Count);
            var nomeP1 = textosNomes.FirstOrDefault(t => t.Value == "P1");
            Assert.NotNull(nomeP1);
            Assert.Equal(TextAlignment.BottomLeft, nomeP1.Alignment);
            Assert.Equal(1000.30, nomeP1.Position.X, 2);
            Assert.Equal(2000.30, nomeP1.Position.Y, 2);
            Assert.Equal(0.40, nomeP1.Height, 2);

            // Rótulos de Cotas: 4 pontos, layer TOPO_COTAS, alinhamento TopLeft, offset (+0.30, -0.30), F3
            var textosCotas = textos.Where(t => t.Layer.Name == "TOPO_COTAS").ToList();
            Assert.Equal(4, textosCotas.Count);
            var cotaP1 = textosCotas.FirstOrDefault(t => t.Value == "100.500");
            Assert.NotNull(cotaP1);
            Assert.Equal(TextAlignment.TopLeft, cotaP1.Alignment);
            Assert.Equal(1000.30, cotaP1.Position.X, 2);
            Assert.Equal(1999.70, cotaP1.Position.Y, 2);
            Assert.Equal(0.30, cotaP1.Height, 2);

            var cotaP2 = textosCotas.FirstOrDefault(t => t.Value == "102.125");
            Assert.NotNull(cotaP2);

            // Rótulos de Descrições: Apenas irr1 tem descrição ("CERCA_ARAME"), offset (-0.30, -0.30), TopRight
            var textosDesc = textos.Where(t => t.Layer.Name == "TOPO_DESCRICOES").ToList();
            Assert.Single(textosDesc);
            Assert.Equal("CERCA_ARAME", textosDesc[0].Value);
            Assert.Equal(TextAlignment.TopRight, textosDesc[0].Alignment);
            Assert.Equal(1019.70, textosDesc[0].Position.X, 2);
            Assert.Equal(2009.70, textosDesc[0].Position.Y, 2);
            Assert.Equal(0.30, textosDesc[0].Height, 2);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportarDxf_SemTinESemCurvas_DeveDegradarGraciosamente()
    {
        var p1 = new PontoCoordenada { Nome = "P1", X = 100, Y = 200, Z = 10, EhPontoPoligonal = true };
        var payload = new DxfExportPayload(
            new List<PontoCoordenada> { p1 },
            new List<PontoCoordenada>(),
            TinVertices: null,
            TinTriangulos: null,
            CurvasNivel: null
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_degrade_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            // Sem triângulos nem polilinhas de curvas
            Assert.Empty(dxf.Entities.Faces3D);
            Assert.Empty(dxf.Entities.Polylines2D);
            Assert.Empty(dxf.Entities.Polylines3D);

            // Contém apenas o ponto e os rótulos de nome e cota
            Assert.Single(dxf.Entities.Points);
            Assert.Equal(2, dxf.Entities.Texts.Count()); // Nome + Cota
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void EncadeadorBreaklines_DeveEncadearSegmentosContiguosEmPolylineAberta()
    {
        var v1 = new TerrainVertex(100, 200, 10, 1);
        var v2 = new TerrainVertex(110, 205, 11, 2);
        var v3 = new TerrainVertex(120, 210, 12, 3);
        var v4 = new TerrainVertex(130, 215, 13, 4);

        var lookup = new Dictionary<int, TerrainVertex>
        {
            [1] = v1, [2] = v2, [3] = v3, [4] = v4
        };

        var breaklines = new List<Breakline>
        {
            new(1, 2),
            new(2, 3),
            new(3, 4)
        };

        var cadeias = EncadeadorBreaklinesService.Encadear(breaklines, lookup);

        Assert.Single(cadeias);
        var cadeia = cadeias[0];
        Assert.False(cadeia.EhFechada);
        Assert.Equal(4, cadeia.Vertices.Count);
        Assert.Equal(1, cadeia.Vertices[0].Id);
        Assert.Equal(2, cadeia.Vertices[1].Id);
        Assert.Equal(3, cadeia.Vertices[2].Id);
        Assert.Equal(4, cadeia.Vertices[3].Id);
    }

    [Fact]
    public void EncadeadorBreaklines_DeveDetectarPoligonalFechada_ComEhFechadaTrue()
    {
        var v1 = new TerrainVertex(0, 0, 10, 1);
        var v2 = new TerrainVertex(10, 0, 11, 2);
        var v3 = new TerrainVertex(5, 10, 12, 3);

        var lookup = new Dictionary<int, TerrainVertex>
        {
            [1] = v1, [2] = v2, [3] = v3
        };

        var breaklines = new List<Breakline>
        {
            new(1, 2),
            new(2, 3),
            new(3, 1)
        };

        var cadeias = EncadeadorBreaklinesService.Encadear(breaklines, lookup);

        Assert.Single(cadeias);
        var anel = cadeias[0];
        Assert.True(anel.EhFechada);
        Assert.Equal(3, anel.Vertices.Count);
    }

    [Fact]
    public void EncadeadorBreaklines_Bifurcacao_DeveInterromperCadeiaNosTerminais()
    {
        // Nó 2 tem grau 3 (conecta a 1, 3 e 4)
        var v1 = new TerrainVertex(0, 0, 10, 1);
        var v2 = new TerrainVertex(10, 0, 10, 2);
        var v3 = new TerrainVertex(20, 0, 10, 3);
        var v4 = new TerrainVertex(10, 10, 10, 4);

        var lookup = new Dictionary<int, TerrainVertex>
        {
            [1] = v1, [2] = v2, [3] = v3, [4] = v4
        };

        var breaklines = new List<Breakline>
        {
            new(1, 2),
            new(2, 3),
            new(2, 4)
        };

        var cadeias = EncadeadorBreaklinesService.Encadear(breaklines, lookup);

        // Deve particionar em 3 cadeias simples conectadas na bifurcação
        Assert.Equal(3, cadeias.Count);
        Assert.All(cadeias, c => Assert.False(c.EhFechada));
        Assert.All(cadeias, c => Assert.Equal(2, c.Vertices.Count));
    }

    [Fact]
    public void ExportarDxf_MalhaTin_DeveCriarFaces3DComIsVisibleFalseEArestasDeduplicadas()
    {
        // 4 vértices formando 2 triângulos vizinhos com 1 aresta compartilhada (1 <-> 3)
        var v1 = new TerrainVertex(0, 0, 10, 1);
        var v2 = new TerrainVertex(10, 0, 12, 2);
        var v3 = new TerrainVertex(10, 10, 14, 3);
        var v4 = new TerrainVertex(0, 10, 11, 4);

        var t1 = new TerrainTriangle(v1, v2, v3);
        var t2 = new TerrainTriangle(v1, v3, v4);

        var payload = new DxfExportPayload(
            Poligonal: new List<PontoCoordenada>(),
            Irradiacoes: new List<PontoCoordenada>(),
            TinVertices: new List<TerrainVertex> { v1, v2, v3, v4 },
            TinTriangulos: new List<TerrainTriangle> { t1, t2 },
            Breaklines: null
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_tin_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            // Validação das Camadas
            Assert.True(dxf.Layers.Contains("TOPO_TIN_FACES"));
            Assert.True(dxf.Layers.Contains("TOPO_TIN_ARESTAS"));

            var layerFaces = dxf.Layers["TOPO_TIN_FACES"];
            var layerArestas = dxf.Layers["TOPO_TIN_ARESTAS"];

            Assert.False(layerFaces.IsVisible, "TOPO_TIN_FACES deve estar desligada por padrão.");
            Assert.Equal(AciColor.DarkGray, layerFaces.Color);

            Assert.True(layerArestas.IsVisible, "TOPO_TIN_ARESTAS deve estar ligada por padrão.");
            Assert.Equal(AciColor.DarkGray, layerArestas.Color);
            Assert.Equal(Lineweight.W13, layerArestas.Lineweight);

            // Validação das Faces 3D
            Assert.Equal(2, dxf.Entities.Faces3D.Count());

            // Validação da Deduplicação: 2 triângulos compartilham 1 aresta -> 5 arestas únicas (não 6)
            var linhas = dxf.Entities.Lines.Where(l => l.Layer.Name == "TOPO_TIN_ARESTAS").ToList();
            Assert.Equal(5, linhas.Count);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportarDxf_MalhaTin_DeveGarantirOrientacaoCCWNasFaces3D()
    {
        // Vértices definidos intencionalmente em sentido horário (CW) no plano XY
        var v0 = new TerrainVertex(0, 0, 10, 0);
        var v1 = new TerrainVertex(0, 10, 12, 1);
        var v2 = new TerrainVertex(10, 0, 11, 2);

        var t1 = new TerrainTriangle(v0, v1, v2);

        var payload = new DxfExportPayload(
            Poligonal: new List<PontoCoordenada>(),
            Irradiacoes: new List<PontoCoordenada>(),
            TinVertices: new List<TerrainVertex> { v0, v1, v2 },
            TinTriangulos: new List<TerrainTriangle> { t1 }
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_ccw_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            var face = Assert.Single(dxf.Entities.Faces3D);

            // Verifica o produto vetorial 2D da face exportada: deve ser estritamente > 0 (CCW)
            double cross = (face.SecondVertex.X - face.FirstVertex.X) * (face.ThirdVertex.Y - face.FirstVertex.Y) -
                           (face.SecondVertex.Y - face.FirstVertex.Y) * (face.ThirdVertex.X - face.FirstVertex.X);

            Assert.True(cross > 0, "A Face3D exportada deve ter orientação anti-horária (CCW) com produto vetorial positivo.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportarDxf_Breaklines_DeveCriarPolyline3DComEspessuraW30ECorCiano()
    {
        var v1 = new TerrainVertex(100, 200, 15, 1);
        var v2 = new TerrainVertex(110, 205, 16, 2);
        var v3 = new TerrainVertex(120, 210, 17, 3);

        var payload = new DxfExportPayload(
            Poligonal: new List<PontoCoordenada>(),
            Irradiacoes: new List<PontoCoordenada>(),
            TinVertices: new List<TerrainVertex> { v1, v2, v3 },
            TinTriangulos: new List<TerrainTriangle>(),
            Breaklines: new List<Breakline> { new(1, 2), new(2, 3) }
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_bl_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            Assert.True(dxf.Layers.Contains("TOPO_TIN_BREAKLINES"));
            var layerBl = dxf.Layers["TOPO_TIN_BREAKLINES"];
            Assert.True(layerBl.IsVisible);
            Assert.Equal(AciColor.Cyan, layerBl.Color);
            Assert.Equal(Lineweight.W30, layerBl.Lineweight);

            var polylines = dxf.Entities.Polylines3D.ToList();
            var poly = Assert.Single(polylines);
            Assert.Equal("TOPO_TIN_BREAKLINES", poly.Layer.Name);
            Assert.False(poly.IsClosed);
            Assert.Equal(3, poly.Vertexes.Count);

            Assert.Equal(100.0, poly.Vertexes[0].X, 3);
            Assert.Equal(110.0, poly.Vertexes[1].X, 3);
            Assert.Equal(120.0, poly.Vertexes[2].X, 3);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ContourLabeler_DeveAncorarNoSegmentoMaisLongoEComputarAnguloTangencial()
    {
        // Isolinha com 4 vértices onde o segmento do meio (índice 1 -> 2) é o mais longo (L = 8m)
        var v0 = new TerrainVertex(0, 0, 150.0, 0);
        var v1 = new TerrainVertex(2, 0, 150.0, 1);
        var v2 = new TerrainVertex(10, 0, 150.0, 2);
        var v3 = new TerrainVertex(12, 1, 150.0, 3);

        var isoline = new Isoline(150.0, new[] { v0, v1, v2, v3 });

        var rotulo = ContourLabeler.ObterRotuloCurvaMestra(isoline);

        Assert.NotNull(rotulo);
        var (x, y, z, angulo, texto) = rotulo.Value;

        Assert.Equal(6.0, x, 3); // Ponto médio de (2,0) e (10,0)
        Assert.Equal(0.0, y, 3);
        Assert.Equal(150.0, z, 3);
        Assert.Equal(0.0, angulo, 2); // Linha horizontal orientada para leste
        Assert.Equal("150.00", texto);
    }

    [Fact]
    public void ContourLabeler_DeveNormalizarAnguloParaIntervaloDeLeituraDireta()
    {
        // Segmento orientado a -135 graus: (10, 10) -> (0, 0)
        var v0 = new TerrainVertex(10, 10, 200.0, 0);
        var v1 = new TerrainVertex(0, 0, 200.0, 1);

        var isoline = new Isoline(200.0, new[] { v0, v1 });

        var rotulo = ContourLabeler.ObterRotuloCurvaMestra(isoline);

        Assert.NotNull(rotulo);
        var (_, _, _, angulo, _) = rotulo.Value;

        // Deve normalizar de -135° para +45° (intervalo [-90°, +90°])
        Assert.InRange(angulo, -90.0, 90.0);
        Assert.Equal(45.0, angulo, 2);
    }

    [Fact]
    public void ContourLabeler_IsolinhaComMenosDe2Vertices_DeveRetornarNull()
    {
        var v0 = new TerrainVertex(10, 10, 200.0, 0);
        var isoline = new Isoline(200.0, new[] { v0 });

        var rotulo = ContourLabeler.ObterRotuloCurvaMestra(isoline);
        Assert.Null(rotulo);
    }

    [Fact]
    public void ExportarDxf_CurvasDeNivel_DeveSepararMestrasESecundariasComCoresEEspessurasCorretas()
    {
        // Equidistância = 1.0m, Frequência = 5, Cota Âncora = 100.0m
        // 105.0m -> Mestra (múltiplo de 5x1m)
        // 103.0m -> Secundária
        var ptsMestra = new[]
        {
            new TerrainVertex(0, 0, 105.0, 1),
            new TerrainVertex(50, 0, 105.0, 2)
        };
        var ptsSecundaria = new[]
        {
            new TerrainVertex(0, 10, 103.0, 3),
            new TerrainVertex(50, 10, 103.0, 4)
        };

        var isoMestra = new Isoline(105.0, ptsMestra);
        var isoSecundaria = new Isoline(103.0, ptsSecundaria);

        var payload = new DxfExportPayload(
            Poligonal: new List<PontoCoordenada>(),
            Irradiacoes: new List<PontoCoordenada>(),
            CurvasNivel: new List<Isoline> { isoMestra, isoSecundaria },
            EquidistanciaCurva: 1.0,
            FrequenciaCurvaMestra: 5,
            AnchorElevation: 100.0
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_curvas_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            // Validação das Camadas
            Assert.True(dxf.Layers.Contains("TOPO_CURVAS_MESTRAS"));
            Assert.True(dxf.Layers.Contains("TOPO_CURVAS_SECUNDARIAS"));
            Assert.True(dxf.Layers.Contains("TOPO_CURVAS_ROTULOS"));

            var layerMestras = dxf.Layers["TOPO_CURVAS_MESTRAS"];
            var layerSecundarias = dxf.Layers["TOPO_CURVAS_SECUNDARIAS"];

            Assert.Equal(AciColor.Magenta, layerMestras.Color);
            Assert.Equal(Lineweight.W30, layerMestras.Lineweight);

            Assert.Equal(AciColor.DarkGray, layerSecundarias.Color);
            Assert.Equal(Lineweight.W13, layerSecundarias.Lineweight);

            // Validação das Polilinhas 2D (LWPOLYLINE)
            var polylines = dxf.Entities.Polylines2D.ToList();
            Assert.Equal(2, polylines.Count);

            var polyMestra = polylines.FirstOrDefault(p => p.Layer.Name == "TOPO_CURVAS_MESTRAS");
            Assert.NotNull(polyMestra);
            Assert.Equal(105.0, polyMestra.Elevation, 3);

            var polySecundaria = polylines.FirstOrDefault(p => p.Layer.Name == "TOPO_CURVAS_SECUNDARIAS");
            Assert.NotNull(polySecundaria);
            Assert.Equal(103.0, polySecundaria.Elevation, 3);

            // Validação da Rotulagem: Apenas a mestra deve gerar rótulo na camada TOPO_CURVAS_ROTULOS
            var textosRotulos = dxf.Entities.Texts.Where(t => t.Layer.Name == "TOPO_CURVAS_ROTULOS").ToList();
            var rotulo = Assert.Single(textosRotulos);
            Assert.Equal("105.00", rotulo.Value);
            Assert.Equal(TextAlignment.MiddleCenter, rotulo.Alignment);
            Assert.Equal(0.35, rotulo.Height, 2);
            Assert.Equal(25.0, rotulo.Position.X, 2); // Ponto médio de (0,0) a (50,0)
            Assert.Equal(0.0, rotulo.Position.Y, 2);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportarDxf_CurvasDeNivel_DeveDetectarIsolinhaFechada()
    {
        // Polilinha fechada onde o primeiro vértice coincide com o último
        var ptsFechada = new[]
        {
            new TerrainVertex(0, 0, 110.0, 1),
            new TerrainVertex(20, 0, 110.0, 2),
            new TerrainVertex(20, 20, 110.0, 3),
            new TerrainVertex(0, 20, 110.0, 4),
            new TerrainVertex(0, 0, 110.0, 5)
        };

        var iso = new Isoline(110.0, ptsFechada);

        var payload = new DxfExportPayload(
            Poligonal: new List<PontoCoordenada>(),
            Irradiacoes: new List<PontoCoordenada>(),
            CurvasNivel: new List<Isoline> { iso }
        );

        var servico = new ExportadorDxfService();
        string tempPath = Path.Combine(Path.GetTempPath(), $"teste_anel_{Guid.NewGuid():N}.dxf");

        try
        {
            servico.SalvarDxf(payload, tempPath);

            var dxf = DxfDocument.Load(tempPath);
            Assert.NotNull(dxf);

            var poly = Assert.Single(dxf.Entities.Polylines2D);
            Assert.True(poly.IsClosed, "Curva com vértices inicial e final coincidentes deve ter IsClosed = true.");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
