using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Tables;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Services;
using TopoGENTE.Domain.Exceptions;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.Infrastructure.Adapters.Exportadores;

public class ExportadorDxfService : IExportadorDxfService
{
    private const double OffsetRoseta = 0.30;
    private const double AlturaTextoNome = 0.40;
    private const double AlturaTextoAtributo = 0.30;
    private const double AlturaTextoCurvaMestra = 0.35;

    public void SalvarDxf(DxfExportPayload payload, string caminhoArquivo)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(caminhoArquivo))
            throw new ArgumentException("O caminho do arquivo DXF não pode ser vazio.", nameof(caminhoArquivo));

        bool temPoligonal = payload.Poligonal != null && payload.Poligonal.Count > 0;
        bool temIrradiacoes = payload.Irradiacoes != null && payload.Irradiacoes.Count > 0;
        bool temTin = payload.TinTriangulos != null && payload.TinTriangulos.Count > 0 && payload.TinVertices != null && payload.TinVertices.Count > 0;
        bool temBreaklines = payload.Breaklines != null && payload.Breaklines.Count > 0 && payload.TinVertices != null && payload.TinVertices.Count > 0;
        bool temCurvas = payload.CurvasNivel != null && payload.CurvasNivel.Count > 0;

        if (!temPoligonal && !temIrradiacoes && !temTin && !temBreaklines && !temCurvas)
        {
            throw new ExportacaoPayloadVazioException("O payload para exportação CAD/DXF não contém dados gráficos exportáveis.");
        }

        var doc = new DxfDocument();
        doc.DrawingVariables.AcadVer = DxfVersion.AutoCad2000;

        // 1. Camadas Fixas Padronizadas para Levantamento Cadastral
        var layerPol = ObterOuCriarCamada(doc, "TOPO_POLIGONAL", AciColor.Red);
        var layerIrrad = ObterOuCriarCamada(doc, "TOPO_IRRADIACOES", AciColor.Green);
        var layerNomes = ObterOuCriarCamada(doc, "TOPO_NOMES", AciColor.LightGray);
        var layerCotas = ObterOuCriarCamada(doc, "TOPO_COTAS", AciColor.Yellow);
        var layerDesc = ObterOuCriarCamada(doc, "TOPO_DESCRICOES", AciColor.Cyan);

        // 2. Alinhamento da Poligonal (Linhas Consecutivas)
        if (temPoligonal && payload.Poligonal!.Count > 1)
        {
            for (int i = 0; i < payload.Poligonal.Count - 1; i++)
            {
                var p1 = payload.Poligonal[i];
                var p2 = payload.Poligonal[i + 1];

                var linha = new Line(
                    new Vector3(p1.X, p1.Y, p1.Z),
                    new Vector3(p2.X, p2.Y, p2.Z))
                {
                    Layer = layerPol
                };

                doc.Entities.Add(linha);
            }
        }

        // 3. Pontos e Rótulos Radiais (Roseta Divergente)
        var todosPontos = new List<PontoCoordenada>();
        if (temPoligonal) todosPontos.AddRange(payload.Poligonal!);
        if (temIrradiacoes) todosPontos.AddRange(payload.Irradiacoes!);

        foreach (var p in todosPontos)
        {
            string nomeCamadaPonto = SanitizadorCamadaDxf.ObterNomeCamadaPonto(p);
            var layerPonto = ObterOuCriarCamada(doc, nomeCamadaPonto, ObterCorPadraoCamada(nomeCamadaPonto));

            // Entidade Ponto 3D
            var pointEntity = new netDxf.Entities.Point(new Vector3(p.X, p.Y, p.Z))
            {
                Layer = layerPonto
            };
            doc.Entities.Add(pointEntity);

            // Rótulo: Nome do Ponto (Quadrante I: X0 + δ, Y0 + δ, Z) - BottomLeft, LightGray (ACI 9), h=0.40m
            if (!string.IsNullOrWhiteSpace(p.Nome))
            {
                var posNome = new Vector3(p.X + OffsetRoseta, p.Y + OffsetRoseta, p.Z);
                var textNome = new Text(p.Nome, posNome, AlturaTextoNome)
                {
                    Layer = layerNomes,
                    Alignment = TextAlignment.BottomLeft
                };
                doc.Entities.Add(textNome);
            }

            // Rótulo: Cota Topográfica Z (Quadrante IV: X0 + δ, Y0 - δ, Z) - TopLeft, Yellow (ACI 2), h=0.30m, F3
            var posCota = new Vector3(p.X + OffsetRoseta, p.Y - OffsetRoseta, p.Z);
            var textCota = new Text(p.Z.ToString("F3", CultureInfo.InvariantCulture), posCota, AlturaTextoAtributo)
            {
                Layer = layerCotas,
                Alignment = TextAlignment.TopLeft
            };
            doc.Entities.Add(textCota);

            // Rótulo: Descrição de Campo (Quadrante III: X0 - δ, Y0 - δ, Z) - TopRight, Cyan (ACI 4), h=0.30m
            if (!string.IsNullOrWhiteSpace(p.Descricao))
            {
                var posDesc = new Vector3(p.X - OffsetRoseta, p.Y - OffsetRoseta, p.Z);
                var textDesc = new Text(p.Descricao, posDesc, AlturaTextoAtributo)
                {
                    Layer = layerDesc,
                    Alignment = TextAlignment.TopRight
                };
                doc.Entities.Add(textDesc);
            }
        }

        // Dicionário de vértices para suporte ao MDT e Breaklines
        Dictionary<int, TerrainVertex>? vertexLookup = null;
        if (payload.TinVertices != null && payload.TinVertices.Count > 0)
        {
            vertexLookup = payload.TinVertices.ToDictionary(v => v.Id, v => v);
        }

        // 4. Incremento 2: Malha Triangular do Terreno (MDT)
        if (temTin && vertexLookup != null)
        {
            var layerTinFaces = ObterOuCriarCamada(
                doc,
                "TOPO_TIN_FACES",
                AciColor.DarkGray,
                isVisible: false);

            var layerTinArestas = ObterOuCriarCamada(
                doc,
                "TOPO_TIN_ARESTAS",
                AciColor.DarkGray,
                isVisible: true,
                lineweight: Lineweight.W13);

            var arestasDeduplicadas = new HashSet<UnorderedEdge>();

            foreach (var tri in payload.TinTriangulos!)
            {
                if (!vertexLookup.TryGetValue(tri.V0, out var v0) ||
                    !vertexLookup.TryGetValue(tri.V1, out var v1) ||
                    !vertexLookup.TryGetValue(tri.V2, out var v2))
                {
                    continue;
                }

                // Normalização de orientação dos vértices para sentido Anti-Horário (Counter-Clockwise - CCW)
                double cross2D = (v1.X - v0.X) * (v2.Y - v0.Y) - (v1.Y - v0.Y) * (v2.X - v0.X);
                if (cross2D < 0)
                {
                    (v1, v2) = (v2, v1);
                }

                var face3d = new Face3D(
                    new Vector3(v0.X, v0.Y, v0.Z),
                    new Vector3(v1.X, v1.Y, v1.Z),
                    new Vector3(v2.X, v2.Y, v2.Z))
                {
                    Layer = layerTinFaces
                };
                doc.Entities.Add(face3d);

                // Deduplicação estrita de arestas da malha (O(1) por inserção via HashSet)
                AdicionarArestaSeInedita(doc, layerTinArestas, arestasDeduplicadas, v0, v1);
                AdicionarArestaSeInedita(doc, layerTinArestas, arestasDeduplicadas, v1, v2);
                AdicionarArestaSeInedita(doc, layerTinArestas, arestasDeduplicadas, v2, v0);
            }
        }

        // 5. Incremento 2: Linhas de Quebra (Breaklines) Encadeadas em Polyline3D
        if (temBreaklines && vertexLookup != null)
        {
            var layerBreaklines = ObterOuCriarCamada(
                doc,
                "TOPO_TIN_BREAKLINES",
                AciColor.Cyan,
                isVisible: true,
                lineweight: Lineweight.W30);

            var cadeias = EncadeadorBreaklinesService.Encadear(payload.Breaklines, vertexLookup);

            foreach (var cadeia in cadeias)
            {
                if (cadeia.Vertices.Count < 2) continue;

                var pontos3d = cadeia.Vertices
                    .Select(v => new Vector3(v.X, v.Y, v.Z))
                    .ToList();

                var polyline3d = new Polyline3D(pontos3d, cadeia.EhFechada)
                {
                    Layer = layerBreaklines
                };

                doc.Entities.Add(polyline3d);
            }
        }

        // 6. Incremento 3: Curvas de Nível (Polyline2D) e Rotulagem Tangencial (Text)
        if (temCurvas)
        {
            var layerCurvasMestras = ObterOuCriarCamada(
                doc,
                "TOPO_CURVAS_MESTRAS",
                AciColor.Magenta,
                isVisible: true,
                lineweight: Lineweight.W30);

            var layerCurvasSecundarias = ObterOuCriarCamada(
                doc,
                "TOPO_CURVAS_SECUNDARIAS",
                AciColor.DarkGray,
                isVisible: true,
                lineweight: Lineweight.W13);

            var layerCurvasRotulos = ObterOuCriarCamada(
                doc,
                "TOPO_CURVAS_ROTULOS",
                AciColor.Magenta,
                isVisible: true);

            double step = payload.EquidistanciaCurva > 0 ? payload.EquidistanciaCurva : 1.0;
            int freq = payload.FrequenciaCurvaMestra > 0 ? payload.FrequenciaCurvaMestra : 5;
            double anchor = payload.AnchorElevation;

            foreach (var iso in payload.CurvasNivel!)
            {
                var span = iso.Vertices.Span;
                if (span.Length < 2) continue;

                // Critério analítico de classificação de curvas mestras vs. secundárias
                double rel = (iso.Elevation - anchor) / step;
                double rnd = Math.Round(rel);
                bool ehMestra = Math.Abs(rel - rnd) < 1e-4 && ((int)rnd % freq == 0);

                var camadaAlvo = ehMestra ? layerCurvasMestras : layerCurvasSecundarias;

                // Conversão de vértices para coordenadas 2D do plano horizontal
                var vertices2D = new List<Polyline2DVertex>(span.Length);
                for (int i = 0; i < span.Length; i++)
                {
                    vertices2D.Add(new Polyline2DVertex(span[i].X, span[i].Y));
                }

                // Detecção de fechamento da curva (anel)
                double dx = span[0].X - span[^1].X;
                double dy = span[0].Y - span[^1].Y;
                bool isClosed = (dx * dx + dy * dy) < 1e-6;

                var poly2d = new Polyline2D(vertices2D, isClosed)
                {
                    Layer = camadaAlvo,
                    Elevation = iso.Elevation,
                    Color = AciColor.ByLayer
                };
                doc.Entities.Add(poly2d);

                // Rotulagem altimétrica tangencial exclusivamente em curvas mestras
                if (ehMestra)
                {
                    var rotulo = ContourLabeler.ObterRotuloCurvaMestra(iso);
                    if (rotulo.HasValue)
                    {
                        var (rx, ry, rz, angulo, texto) = rotulo.Value;
                        var posRotulo = new Vector3(rx, ry, rz);

                        var textRotulo = new Text(texto, posRotulo, AlturaTextoCurvaMestra)
                        {
                            Layer = layerCurvasRotulos,
                            Alignment = TextAlignment.MiddleCenter,
                            Rotation = angulo,
                            Color = AciColor.ByLayer
                        };

                        doc.Entities.Add(textRotulo);
                    }
                }
            }
        }

        doc.Save(caminhoArquivo);
    }

    private static void AdicionarArestaSeInedita(
        DxfDocument doc,
        Layer layerArestas,
        HashSet<UnorderedEdge> arestasProcessadas,
        TerrainVertex va,
        TerrainVertex vb)
    {
        var aresta = new UnorderedEdge(va.Id, vb.Id);
        if (arestasProcessadas.Add(aresta))
        {
            var linha = new Line(
                new Vector3(va.X, va.Y, va.Z),
                new Vector3(vb.X, vb.Y, vb.Z))
            {
                Layer = layerArestas
            };
            doc.Entities.Add(linha);
        }
    }

    private static Layer ObterOuCriarCamada(
        DxfDocument doc,
        string nomeCamada,
        AciColor corPadrao,
        bool isVisible = true,
        Lineweight lineweight = Lineweight.Default)
    {
        if (doc.Layers.Contains(nomeCamada))
        {
            return doc.Layers[nomeCamada];
        }

        var novaCamada = new Layer(nomeCamada)
        {
            Color = corPadrao,
            IsVisible = isVisible,
            Lineweight = lineweight
        };
        doc.Layers.Add(novaCamada);
        return novaCamada;
    }

    private static AciColor ObterCorPadraoCamada(string nomeCamada)
    {
        return nomeCamada switch
        {
            "TOPO_POLIGONAL" => AciColor.Red,
            "TOPO_IRRADIACOES" => AciColor.Green,
            "TOPO_NOMES" => AciColor.LightGray,
            "TOPO_COTAS" => AciColor.Yellow,
            "TOPO_DESCRICOES" => AciColor.Cyan,
            "TOPO_TIN_FACES" => AciColor.DarkGray,
            "TOPO_TIN_ARESTAS" => AciColor.DarkGray,
            "TOPO_TIN_BREAKLINES" => AciColor.Cyan,
            "TOPO_CURVAS_MESTRAS" => AciColor.Magenta,
            "TOPO_CURVAS_SECUNDARIAS" => AciColor.DarkGray,
            "TOPO_CURVAS_ROTULOS" => AciColor.Magenta,
            _ when nomeCamada.StartsWith("TOPO_FEICAO_", StringComparison.OrdinalIgnoreCase) => AciColor.Cyan,
            _ => AciColor.Default
        };
    }
}
