using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using netDxf;
using netDxf.Tables;
using netDxf.Entities;
using netDxf.Header;
using TopoGente.Core.Entities;
using System.Drawing;

namespace TopoGente.Core.Services
{
    public class ExportadorDxfService 
    {

        public void SalvarDxf(List<PontoCoordenada> pontos, string caminhoArquivo)
        {
            var doc = new DxfDocument();
            doc.DrawingVariables.AcadVer = DxfVersion.AutoCad2000; // Versão padrão, funciona tanto em softwares antigos e novos

            // Layers
            var layerPol = new Layer("TOPO_POLIGONAL") { Color = AciColor.Red };
            var layerIrrad = new Layer("TOPO_IRRADIACOES") { Color = AciColor.Green };
            var layerNomes = new Layer("TOPO_NOMES") { Color = AciColor.LightGray };
            var layerCotas = new Layer("TOPO_COTAS") { Color = AciColor.Yellow };

            doc.Layers.Add(layerPol);
            doc.Layers.Add(layerIrrad);
            doc.Layers.Add(layerNomes);
            doc.Layers.Add(layerCotas);

            var estacoes = pontos.Where(p => p.EhPontoPoligonal).ToList();
            // Desenho Poligonal
            for (int i = 0; i < estacoes.Count - 1 ; i++)
            {
                var linha = new Line(
                    new Vector3(estacoes[i].X, estacoes[i].Y, estacoes[i].Z),
                    new Vector3(estacoes[i + 1].X, estacoes[i + 1].Y, estacoes[i + 1].Z));

                linha.Layer = layerPol;
                doc.Entities.Add(linha);
            }

            // Desenho Pontos e Txt
            foreach (var p in pontos)
            {
                var layerPonto = p.EhPontoPoligonal ? layerPol : layerIrrad;

                var pointEntity = new netDxf.Entities.Point(new Vector3(p.X, p.Y, p.Z));
                pointEntity.Layer = layerPonto;
                doc.Entities.Add(pointEntity);

                var textNome = new Text(p.Nome, new Vector3(p.X + 0.2, p.Y + 0.2, p.Z), 0.5);
                textNome.Layer = layerNomes;
                doc.Entities.Add(textNome);

                var textCota = new Text(p.Z.ToString("F3"), new Vector3(p.X + 0.2, p.Y - 0.5, p.Z), 0.3);
                textCota.Layer = layerCotas;
                doc.Entities.Add(textCota);
            }

            doc.Save(caminhoArquivo);
        }

       
    }
}
