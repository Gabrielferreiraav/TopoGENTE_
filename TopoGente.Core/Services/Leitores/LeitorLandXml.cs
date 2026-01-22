using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TopoGente.Core.Services.Leitores
{
    public class LeitorLandXml : ILeitorArquivo
    {
        public string NomeFormato => "LandXML 1.2";

        private readonly XNamespace _ns = "http://www.landxml.org/schema/LandXML-1.2";
        private IReadOnlyList<string> UltimosAvisos => _ultimosAvisos;
        private readonly List<string> _ultimosAvisos = new();

        private class FatoresConversao
        {
            public Double Linear { get; set; } = 1.0; //metros
            public Double Angular { get; set; } = 1.0; // graus dec
        }
        public List<Estacao> Ler(string[] linhas)
        {
            var estacoes = new List<Estacao>();

            string conteudoXXML = string.Join(Environment.NewLine, linhas); //Transforma XML em um texto unico

            int indexXml = conteudoXXML.IndexOf("<");
            if (indexXml > 0 ) 
            {
                conteudoXXML = conteudoXXML.Substring(indexXml);
            }
            try
            {
                var doc = XDocument.Parse(conteudoXXML);

                var fatores = LerUnidadesProjeto(doc); // idetifica units no projeto

                var dicionarioCoordenadas = MapearCgPoints(doc, fatores);

                var surveys = doc.Descendants(_ns + "Survey");
                foreach (var survey in surveys)
                {
                    var setups = survey.Descendants(_ns + "InstrumentSetup");
                    foreach (var setup in setups)
                    {
                        //converter leituras e alturas
                        var estacao = ProcessarInstrumentSetup(setup, dicionarioCoordenadas, fatores);
                        if (estacao != null)
                        {
                            estacoes.Add(estacao);
                        }
                    }
                }
            }
            catch (Exception op)
            {
                throw new Exception($"Erro ao processar LandXML : {op.Message}");
            }

            return estacoes;
        }

        private FatoresConversao LerUnidadesProjeto(XDocument doc) // incompleto
        {
            var fatores = new FatoresConversao();
            return fatores;
        }

        private double ObterFatorAngular(string unitName)
        {
            return unitName switch
            {
                "radians" => 180.0 / Math.PI, //rad -> graus
                "grads" or "gon" => 0.9, //100 gons = 90 graus
                "degress" => 1.0, //´graus
                _ => 1.0
            };
        }

        

        private Dictionary<string,PontoCoordenada> MapearCgPoints(XDocument doc, FatoresConversao fatores)
        {
            var dict = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);
            var cultura = CultureInfo.InvariantCulture;
            var pontos = doc.Descendants(_ns + "CgPoint");
            foreach(var p in pontos)
            {
                var nomeOriginal = p.Attribute("name")?.Value?.Trim();
                if (string.IsNullOrEmpty(nomeOriginal)) continue;
                // quebra a string em y x z
                var valores = p.Value.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (valores.Length < 2) continue;

                double y = double.Parse(valores[0], cultura) * fatores.Linear;
                double x = double.Parse(valores[1], cultura) * fatores.Linear;
                double z = valores.Length > 2 ? double.Parse(valores[2], cultura) * fatores.Linear : 0.0;

                var nomeFinal = GarantirNomeUnico(dict, nomeOriginal);

                if (!string.Equals(nomeFinal,nomeOriginal, StringComparison.OrdinalIgnoreCase)
                {
                    _ultimosAvisos.Add($"CgPoint duplicado '{nomeOriginal} renomeado para {nomeFinal}'.");
                }

                dict[nomeFinal] = new PontoCoordenada
                {
                    Nome = nomeFinal,
                    X = x,
                    Y = y,
                    Z = z
                };
            }

            return dict;
        }

        private string GarantirNomeUnico (Dictionary<string, PontoCoordenada> dict, string nomeOriginal)
        {
            if (!dict.ContainsKey(nomeOriginal))
            {
                return nomeOriginal;
            }
            int i = 1;
            string candidato;
            do
            {
                candidato = $"{nomeOriginal}+{i}";
                i++;
            }
            while (dict.ContainsKey(candidato));

            return candidato;

        }
        //incompleto
        private Estacao ProcessarInstrumentSetup(XElement setup, Dictionary<string,PontoCoordenada> coords, FatoresConversao fatores)
        {
            var esatacao = new Estacao();
            return esatacao;
        }
    }
}
