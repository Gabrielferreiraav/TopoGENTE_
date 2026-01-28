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
        public IReadOnlyList<string> UltimosAvisos => _ultimosAvisos;
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
            if (indexXml > 0)
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

            var unitsTag = doc.Root?.Element(_ns + "Units"); //Busca a Tag <Units>
            if (unitsTag == null)
            {
                return fatores;
            }

            var metricTag = unitsTag.Element(_ns + "Metric"); //Busca a Tag <Metric>
            if (metricTag != null)
            {
                //verificar unidade angular
                string angUni = metricTag.Attribute("angularUnit")?.Value.ToLower() ?? "degrees";
                fatores.Angular = ObterFatorAngular(angUni);
            }
            else
            {
                var imperialTag = unitsTag.Element(_ns + "Imperial"); //Busca a Tag <Imperial>
                if (imperialTag != null)
                {
                    //verificar unidade angular
                    string linearUnit = imperialTag.Attribute("linearUnit")?.Value.ToLower() ?? "foot";
                    string angUni = imperialTag.Attribute("angularUnit")?.Value.ToLower() ?? "degrees";

                    fatores.Angular = ObterFatorAngular(angUni);

                    if (linearUnit == "ussurveyfoot") fatores.Linear = 1200.0 / 3937.0; //1 US Survey Foot = 1200/3937 metros
                    else if (linearUnit == "foot" || linearUnit == "internationalfoot") fatores.Linear = 0.3048; //1 foot = 0.3048 metros
                }
            }
            return fatores;
        }

        private double ObterFatorAngular(string unitName)
        {
            return unitName switch
            {
                "radians" => 180.0 / Math.PI, //rad -> graus
                "grads" or "gon" => 0.9, //100 gons = 90 graus
                "degrees" => 1.0, // graus
                _ => 1.0
            };
        }



        private Dictionary<string, PontoCoordenada> MapearCgPoints(XDocument doc, FatoresConversao fatores)
        {
            var dict = new Dictionary<string, PontoCoordenada>(StringComparer.OrdinalIgnoreCase);
            var cultura = CultureInfo.InvariantCulture;
            var pontos = doc.Descendants(_ns + "CgPoint");
            foreach (var p in pontos)
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

                if (!string.Equals(nomeFinal, nomeOriginal, StringComparison.OrdinalIgnoreCase))
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

        private string GarantirNomeUnico(Dictionary<string, PontoCoordenada> dict, string nomeOriginal)
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
        private Estacao ProcessarInstrumentSetup(XElement setup, Dictionary<string, PontoCoordenada> coords, FatoresConversao fatores)
        {
            var cultura = CultureInfo.InvariantCulture;
            string idSetup = setup.Attribute("id")?.Value;
            string nomeEstacao = setup.Attribute("stationName")?.Value ?? idSetup;

            double hi = 0.0;
            // altura do instrumento tbm sofre conversao do fator linear
            if (setup.Attribute("instrumentHeigth") != null)
            {
                hi = double.Parse(setup.Attribute("instrumentHeigth").Value, cultura) * fatores.Linear;
            }

            var novaEstacao = new Estacao
            {
                Nome = nomeEstacao,
                AlturaInstrumento = hi,
                Id = idSetup,
                Leituras = new List<LeituraEstacaoTotal>()
            };

            if (coords.ContainsKey(nomeEstacao))
            {
                novaEstacao.CoordenadaConhecida = coords[nomeEstacao];
            }

            var backsights = setup.Descendants(_ns + "Backsight");
            foreach (var bs in backsights)
            {
                string alvoBs = bs.Attribute("targetPoint").Value;
                double anguloBs = 0.0;
                // se tiver leitura angular no BS
                if (bs.Attribute("azimuth") != null)
                {
                    anguloBs = double.Parse(bs.Attribute("azimuth").Value, cultura) * fatores.Angular;
                }
                else if (bs.Attribute("circle") != null)
                {
                    anguloBs = double.Parse(bs.Attribute("circle").Value, cultura) * fatores.Angular;
                }

                if (!string.IsNullOrEmpty(alvoBs))
                {
                    novaEstacao.Leituras.Add((new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = nomeEstacao,
                        PontoVisado = alvoBs,
                        AnguloHorizontal = anguloBs,
                        AlturaInstrumento = hi,
                        Tipo = TipoLeitura.Re,
                        Observacao = "Leitura de Backsight"
                    }));
                }
            }

            // processar as observações
            var observacoes = setup.Descendants(_ns + "Observation");
            foreach (var obs in observacoes)
            {
                string alvo = obs.Attribute("targetPoint")?.Value;

                if (string.IsNullOrEmpty(alvo)) continue;

                double horizAngle = 0, zenithAngle = 0, slopeDist = 0, targetHeigth = 0;

                // conversao angular

                if (obs.Attribute("horizAngle") != null)
                {
                    horizAngle = double.Parse(obs.Attribute("horizAngle").Value, cultura) * fatores.Angular;
                }

                if (obs.Attribute("zenithAngle") != null)
                {
                    zenithAngle = double.Parse(obs.Attribute("zenithAngle").Value, cultura) * fatores.Angular;
                }

                // conversao linear

                if (obs.Attribute("slopeDist") != null)
                {
                    slopeDist = double.Parse(obs.Attribute("slopeDist").Value, cultura) * fatores.Linear;
                }

                if (obs.Attribute("targetHeigth") != null)
                {
                    targetHeigth = double.Parse(obs.Attribute("targetHeigth").Value, cultura) * fatores.Linear;
                }

                string desc = obs.Attribute("desc").Value ?? "";

                TipoLeitura tipo = TipoLeitura.Irradiacao;

                if (!string.IsNullOrEmpty(desc) && (desc.ToUpper().Contains("VANTE") || desc.ToUpper().Contains("V")))
                {
                    tipo = TipoLeitura.Poligonal;
                }

                novaEstacao.Leituras.Add((new LeituraEstacaoTotal
                {
                    EstacaoOcupada = nomeEstacao,
                    PontoVisado = alvo,
                    AlturaInstrumento = hi,
                    AlturaPrisma = targetHeigth,
                    AnguloHorizontal = horizAngle,
                    AnguloVertical = zenithAngle,
                    DistanciaInclinada = slopeDist,
                    Tipo = tipo,
                    Observacao = desc
                }));
            }
            return novaEstacao;
        }
    }
}
