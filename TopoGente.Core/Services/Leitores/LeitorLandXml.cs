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

                var fatores = LerUnidadesProjeto(doc);
                var dicionarioCoordenadas = MapearCgPoints(doc, fatores);

                var surveys = doc.Descendants(_ns + "Survey").ToList();
                if (surveys.Count > 1)
                {
                    _ultimosAvisos.Add($"Foram encontrados {surveys.Count} elementos <Survey>. Processando apenas o primeiro.");
                }

                var survey = surveys.FirstOrDefault();
                if (survey == null)
                {
                    _ultimosAvisos.Add("Nenhum elemento <Survey> encontrado no LandXML.");
                    return estacoes;
                }

                var setups = survey.Descendants(_ns + "InstrumentSetup");
                foreach (var setup in setups)
                {
                    var estacao = ProcessarInstrumentSetup(setup, dicionarioCoordenadas, fatores);
                    if (estacao != null)
                    {
                        estacoes.Add(estacao);
                    }
                }
            }
            catch (Exception op)
            {
                throw new Exception($"Erro ao processar LandXML : {op.Message}");
            }

            return estacoes;
        }

        private FatoresConversao LerUnidadesProjeto(XDocument doc)
        {
            var fatores = new FatoresConversao();

            var unitsTag = doc.Root?.Element(_ns + "Units");
            if (unitsTag == null)
            {
                return fatores;
            }

            var metricTag = unitsTag.Element(_ns + "Metric");
            if (metricTag != null)
            {
                string angUni = metricTag.Attribute("angularUnit")?.Value.ToLower() ?? "degrees";
                fatores.Angular = ObterFatorAngular(angUni);

                string? linearUnit = metricTag.Attribute("linearUnit")?.Value.ToLower();
                if (!string.IsNullOrWhiteSpace(linearUnit))
                {
                    fatores.Linear = linearUnit switch
                    {
                        "meter" or "metre" or "meters" => 1.0,
                        "millimeter" or "millimetre" or "mm" => 0.001,
                        "centimeter" or "centimetre" or "cm" => 0.01,
                        _ => fatores.Linear
                    };
                }
            }
            else
            {
                var imperialTag = unitsTag.Element(_ns + "Imperial");
                if (imperialTag != null)
                {
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
                "radians" => 180.0 / Math.PI,
                "grads" or "gon" => 0.9,
                "degrees" => 1.0,
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

                // (Northing Easting Elevation) => Y X Z
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

                dict[nomeFinal] = new PontoCoordenada { Nome = nomeFinal, X = x, Y = y, Z = z };
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

        private static TipoLeitura MapearTipoLeituraPorPurpose(string? purpose)
        {
            if (string.IsNullOrWhiteSpace(purpose))
            {
                return TipoLeitura.Irradiacao;
            }

            return purpose.Trim().ToLowerInvariant() switch
            {
                "traverse" => TipoLeitura.Poligonal,
                "sideshot" => TipoLeitura.Irradiacao,
                // check por enquanto como Irradiacao, mas fica marcado em Observacao
                "check" => TipoLeitura.Irradiacao,
                _ => TipoLeitura.Irradiacao
            };
        }

        private static string? ObterPontoVisadoRawObservation(XElement rawObs)
        {
            var alvo = rawObs.Attribute("targetPoint")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(alvo))
            {
                return alvo;
            }

            return rawObs.Element(rawObs.Name.Namespace + "TargetPoint")?.Attribute("name")?.Value?.Trim();
        }

        private static DateTime? ObterTimeStampDoTargetPoint(XElement rawObs)
        {
            var tp = rawObs.Element(rawObs.Name.Namespace + "TargetPoint");
            if (tp == null) return null;

            var ts = tp.Attribute("timeStamp")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(ts)) return null;

            if (DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt;
            }

            return null;
        }

        private static PontoCoordenada? ResolverPointTypeParaCoordenada(
            XElement? pointTypeElement,
            Dictionary<string, PontoCoordenada> cgPoints,
            FatoresConversao fatores,
            string nomeFallback)
        {
            if (pointTypeElement == null) return null;

            // pntRef => CgPoint
            var pntRef = pointTypeElement.Attribute("pntRef")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(pntRef) && cgPoints.TryGetValue(pntRef, out var pk))
            {
                return new PontoCoordenada { Nome = nomeFallback, X = pk.X, Y = pk.Y, Z = pk.Z, EhPontoPoligonal = true };
            }

            // texto interno "north east [elev]" => Y X [Z]
            var raw = pointTypeElement.Value?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var cultura = CultureInfo.InvariantCulture;
            var valores = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (valores.Length < 2) return null;

            if (!double.TryParse(valores[0], NumberStyles.Float, cultura, out var y)) return null;
            if (!double.TryParse(valores[1], NumberStyles.Float, cultura, out var x)) return null;

            double z = 0;
            if (valores.Length > 2)
            {
                double.TryParse(valores[2], NumberStyles.Float, cultura, out z);
            }

            return new PontoCoordenada
            {
                Nome = nomeFallback,
                X = x * fatores.Linear,
                Y = y * fatores.Linear,
                Z = z * fatores.Linear,
                EhPontoPoligonal = true
            };
        }

        private Estacao ProcessarInstrumentSetup(XElement setup, Dictionary<string, PontoCoordenada> coords, FatoresConversao fatores)
        {
            var cultura = CultureInfo.InvariantCulture;
            string idSetup = setup.Attribute("id")?.Value ?? Guid.NewGuid().ToString();
            string nomeEstacao = setup.Attribute("stationName")?.Value ?? idSetup;

            double hi = 0.0;
            // altura do instrumento tbm sofre conversao do fator linear
            if (setup.Attribute("instrumentHeight") != null)
            {
                hi = double.Parse(setup.Attribute("instrumentHeight")!.Value, cultura) * fatores.Linear;
            }

            var novaEstacao = new Estacao
            {
                Nome = nomeEstacao,
                AlturaInstrumento = hi,
                Id = idSetup,
                Leituras = new List<LeituraEstacaoTotal>(),
                PontosCalculados = new List<PontoCoordenada>()
            };

            // Coordenada de estação InstrumentPoint (coords diretas ou pntRef)
            var instrumentPoint = setup.Elements(_ns + "InstrumentPoint").FirstOrDefault();
            novaEstacao.CoordenadaConhecida = ResolverPointTypeParaCoordenada(instrumentPoint, coords, fatores, nomeEstacao);

            // compatibilidade com método anterior (nome da estação igual a CgPoint)
            if (novaEstacao.CoordenadaConhecida == null && coords.TryGetValue(nomeEstacao, out var pk2))
            {
                novaEstacao.CoordenadaConhecida = new PontoCoordenada { Nome = nomeEstacao, X = pk2.X, Y = pk2.Y, Z = pk2.Z, EhPontoPoligonal = true };
            }

            var backsights = setup.Descendants(_ns + "Backsight");
            foreach (var bs in backsights)
            {
                // Backsight geralmente referencia o alvo via TargetPoint e não necessariamente por targetPoint
                string alvoBs = bs.Attribute("targetPoint")?.Value
                                ?? bs.Element(_ns + "TargetPoint")?.Attribute("name")?.Value
                                ?? bs.Element(_ns + "TargetPoint")?.Attribute("pntRef")?.Value;

                double anguloBs = 0.0;
                if (bs.Attribute("azimuth") != null)
                {
                    anguloBs = double.Parse(bs.Attribute("azimuth")!.Value, cultura) * fatores.Angular;
                }
                else if (bs.Attribute("circle") != null)
                {
                    anguloBs = double.Parse(bs.Attribute("circle")!.Value, cultura) * fatores.Angular;
                }

                if (!string.IsNullOrEmpty(alvoBs))
                {
                    novaEstacao.Leituras.Add(new LeituraEstacaoTotal
                    {
                        EstacaoOcupada = nomeEstacao,
                        PontoVisado = alvoBs,
                        AnguloHorizontal = anguloBs,
                        AlturaInstrumento = hi,
                        Tipo = TipoLeitura.Re,
                        Observacao = "Backsight"
                    });
                }
            }

            var cultura =

            // processar as observações brutas (RawObservation) e as que estão dentro de ObservationGroup
            var rawObservacoes = setup.Descendants(_ns + "RawObservation");
            foreach (var raw in rawObservacoes)
            {
                string? alvo = ObterPontoVisadoRawObservation(raw);
                if (string.IsNullOrWhiteSpace(alvo)) continue;

                double horizAngle = 0, zenithAngle = 0, slopeDist = 0, targetHeight = 0;

                // conversao angular
                if (raw.Attribute("horizAngle") != null)
                {
                    horizAngle = double.Parse(raw.Attribute("horizAngle")!.Value, cultura) * fatores.Angular;
                }

                if (raw.Attribute("zenithAngle") != null)
                {
                    zenithAngle = double.Parse(raw.Attribute("zenithAngle")!.Value, cultura) * fatores.Angular;
                }

                // conversao linear
                if (raw.Attribute("slopeDistance") != null)
                {
                    slopeDist = double.Parse(raw.Attribute("slopeDistance")!.Value, cultura) * fatores.Linear;
                }

                if (raw.Attribute("targetHeight") != null)
                {
                    targetHeight = double.Parse(raw.Attribute("targetHeight")!.Value, cultura) * fatores.Linear;
                }

                string? purpose = raw.Attribute("purpose")?.Value;
                TipoLeitura tipo = MapearTipoLeituraPorPurpose(purpose);

                var timeStamp = ObterTimeStampDoTargetPoint(raw);

                novaEstacao.Leituras.Add(new LeituraEstacaoTotal
                {
                    SetupId = idSetup,
                    TimeStamp = timeStamp,
                    EstacaoOcupada = nomeEstacao,
                    PontoVisado = alvo,
                    AlturaInstrumento = hi,
                    AlturaPrisma = targetHeight,
                    AnguloHorizontal = horizAngle,
                    AnguloVertical = zenithAngle,
                    DistanciaInclinada = slopeDist,
                    Tipo = tipo,
                    Observacao = string.Empty,
                    Purpose = purpose
                });
            }

            return novaEstacao;
        }
    }
}
