using System;
using System.Collections.ObjectModel;
using TopoGente.Core.Entities;
using System.Globalization;
using System.Linq;

namespace TopoGente.UI.ViewModels
{
    public class SequenciaPoligonalViewModel : ObservableObject
    {
        private string _id;
        private string _nome;
        private bool _ehPrincipal;
        private string? _estacaoAncoragemNome;
        
        public SequenciaPoligonalViewModel()
        {
            _id = Guid.NewGuid().ToString();
            _nome = "Nova Sequência";
            _ehPrincipal = false;
            Estacoes = new ObservableCollection<string>();
            AtualizarPreviewAzimute();
            AtualizarPreviewAzimuteChegada();
        }

        public string Id { get => _id; set => SetProperty(ref _id, value); }
        public string Nome { get => _nome; set => SetProperty(ref _nome, value); }
        public bool EhPrincipal { get => _ehPrincipal; set => SetProperty(ref _ehPrincipal, value); }
        public string? EstacaoAncoragemNome { get => _estacaoAncoragemNome; set => SetProperty(ref _estacaoAncoragemNome, value); }

        private int _cenarioIndex = 1; // Default Fechada
        public int CenarioIndex
        {
            get => _cenarioIndex;
            set
            {
                if (SetProperty(ref _cenarioIndex, value))
                {
                    OnPropertyChanged(nameof(MostrarPainelChegada));
                }
            }
        }
        public bool MostrarPainelChegada => CenarioIndex == 0; // 0 = Enquadrada

        private string _partidaX = "1000,000";
        public string PartidaX
        {
            get => _partidaX;
            set
            {
                if (SetProperty(ref _partidaX, value))
                {
                    TentarAtualizarAzimutePorCoordenadas();
                }
            }
        }

        private string _partidaY = "1000,000";
        public string PartidaY
        {
            get => _partidaY;
            set
            {
                if (SetProperty(ref _partidaY, value))
                {
                    TentarAtualizarAzimutePorCoordenadas();
                }
            }
        }

        private string _partidaZ = "100,000";
        public string PartidaZ { get => _partidaZ; set => SetProperty(ref _partidaZ, value); }

        private bool _usarAzimute = true;
        public bool UsarAzimute
        {
            get => _usarAzimute;
            set
            {
                if (SetProperty(ref _usarAzimute, value))
                {
                    OnPropertyChanged(nameof(UsarCoordenadaRe));
                    OnPropertyChanged(nameof(MostrarPainelAzimute));
                    OnPropertyChanged(nameof(MostrarPainelCoordenadaRe));
                }
            }
        }

        public bool UsarCoordenadaRe
        {
            get => !_usarAzimute;
            set => UsarAzimute = !value;
        }

        public bool MostrarPainelAzimute => UsarAzimute;
        public bool MostrarPainelCoordenadaRe => !UsarAzimute;

        private string _azimute = "0";
        public string Azimute
        {
            get => _azimute;
            set
            {
                if (SetProperty(ref _azimute, value))
                {
                    AtualizarPreviewAzimute();
                }
            }
        }

        private string _azimutePreview = "0° 00' 00.00\"";
        public string AzimutePreview { get => _azimutePreview; private set => SetProperty(ref _azimutePreview, value); }

        private bool _azimuteValido = true;
        public bool AzimuteValido { get => _azimuteValido; private set { if (SetProperty(ref _azimuteValido, value)) OnPropertyChanged(nameof(AzimuteInvalido)); } }
        public bool AzimuteInvalido => !AzimuteValido;

        private string? _azimuteMensagemErro;
        public string? AzimuteMensagemErro { get => _azimuteMensagemErro; private set => SetProperty(ref _azimuteMensagemErro, value); }

        private string _reX = "0";
        public string ReX
        {
            get => _reX;
            set
            {
                if (SetProperty(ref _reX, value))
                {
                    TentarAtualizarAzimutePorCoordenadas();
                }
            }
        }

        private string _reY = "0";
        public string ReY
        {
            get => _reY;
            set
            {
                if (SetProperty(ref _reY, value))
                {
                    TentarAtualizarAzimutePorCoordenadas();
                }
            }
        }

        private string _reZ = "0";
        public string ReZ { get => _reZ; set => SetProperty(ref _reZ, value); }

        private string _nomeRe = "REF";
        public string NomeRe { get => _nomeRe; set => SetProperty(ref _nomeRe, value); }

        private string _chegadaX = "0";
        public string ChegadaX { get => _chegadaX; set => SetProperty(ref _chegadaX, value); }

        private string _chegadaY = "0";
        public string ChegadaY { get => _chegadaY; set => SetProperty(ref _chegadaY, value); }

        private string _chegadaZ = "0";
        public string ChegadaZ { get => _chegadaZ; set => SetProperty(ref _chegadaZ, value); }

        private string _nomeChegada = "M99";
        public string NomeChegada { get => _nomeChegada; set => SetProperty(ref _nomeChegada, value); }

        private string _azimuteChegada = "0";
        public string AzimuteChegada
        {
            get => _azimuteChegada;
            set
            {
                if (SetProperty(ref _azimuteChegada, value))
                {
                    AtualizarPreviewAzimuteChegada();
                }
            }
        }

        private string _azimuteChegadaPreview = "0° 00' 00.00\"";
        public string AzimuteChegadaPreview { get => _azimuteChegadaPreview; private set => SetProperty(ref _azimuteChegadaPreview, value); }

        private bool _azimuteChegadaValido = true;
        public bool AzimuteChegadaValido { get => _azimuteChegadaValido; private set { if (SetProperty(ref _azimuteChegadaValido, value)) OnPropertyChanged(nameof(AzimuteChegadaInvalido)); } }
        public bool AzimuteChegadaInvalido => !AzimuteChegadaValido;

        private string? _azimuteChegadaMensagemErro;
        public string? AzimuteChegadaMensagemErro { get => _azimuteChegadaMensagemErro; private set => SetProperty(ref _azimuteChegadaMensagemErro, value); }

        private void AtualizarPreviewAzimute()
        {
            if (TopoGente.Core.Utilities.ConversorAngulos.TryDeFormatoCompactoTexto(_azimute, out _, out string formatado, out string? erro))
            {
                AzimutePreview = formatado;
                AzimuteValido = true;
                AzimuteMensagemErro = null;
            }
            else
            {
                AzimutePreview = "--";
                AzimuteValido = false;
                AzimuteMensagemErro = erro;
            }
        }

        private void AtualizarPreviewAzimuteChegada()
        {
            if (TopoGente.Core.Utilities.ConversorAngulos.TryDeFormatoCompactoTexto(_azimuteChegada, out _, out string formatado, out string? erro))
            {
                AzimuteChegadaPreview = formatado;
                AzimuteChegadaValido = true;
                AzimuteChegadaMensagemErro = null;
            }
            else
            {
                AzimuteChegadaPreview = "--";
                AzimuteChegadaValido = false;
                AzimuteChegadaMensagemErro = erro;
            }
        }

        public void TentarAtualizarAzimutePorCoordenadas()
        {
            if (TryParseDouble(PartidaX, out double px) &&
                TryParseDouble(PartidaY, out double py) &&
                TryParseDouble(ReX, out double rx) &&
                TryParseDouble(ReY, out double ry))
            {
                if (Math.Abs(px - rx) > 1e-4 || Math.Abs(py - ry) > 1e-4)
                {
                    double azDecimal = TopoGente.Core.Utilities.GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(px, py, rx, ry);
                    double azCompacto = TopoGente.Core.Utilities.ConversorAngulos.ParaFormatoCompacto(azDecimal);
                    _azimute = azCompacto.ToString("F4", CultureInfo.InvariantCulture);
                    OnPropertyChanged(nameof(Azimute));
                    AtualizarPreviewAzimute();
                }
            }
        }

        private static bool TryParseDouble(string? s, out double val)
        {
            val = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string normalizado = s.Trim().Replace(',', '.');
            return double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
        }

        public ObservableCollection<string> Estacoes { get; }

        public SequenciaPoligonal ToEntity()
        {
            return new SequenciaPoligonal
            {
                Id = this.Id,
                Nome = this.Nome,
                EhPrincipal = this.EhPrincipal,
                EstacaoAncoragemNome = this.EstacaoAncoragemNome,
                Metadados = ColetarMetadados(),
                Estacoes = new System.Collections.Generic.List<string>(this.Estacoes)
            };
        }

        private MetadadosCenario ColetarMetadados()
        {
            var cenario = CenarioIndex switch
            {
                0 => TipoCenarioPoligonal.Enquadrada,
                1 => TipoCenarioPoligonal.Fechada,
                2 => TipoCenarioPoligonal.AbertaOrientada,
                _ => TipoCenarioPoligonal.Fechada
            };

            var meta = new MetadadosCenario
            {
                TipoCenario = cenario,
                PartidaX = LerDoubleUi(PartidaX, "X (Partida)"),
                PartidaY = LerDoubleUi(PartidaY, "Y (Partida)"),
                PartidaZ = LerDoubleUi(PartidaZ, "Z (Partida)"),
                UsarCoordenadaRe = UsarCoordenadaRe,
                AzimutePartida = UsarCoordenadaRe ? 0 : ConverterAzimute(Azimute),
                ReX = UsarCoordenadaRe ? LerDoubleUi(ReX, "X (Ré)") : 0,
                ReY = UsarCoordenadaRe ? LerDoubleUi(ReY, "Y (Ré)") : 0,
                ReZ = UsarCoordenadaRe ? LerDoubleUi(ReZ, "Z (Ré)") : 0,
                AzimuteChegada = null,
                NomeRe = NomeRe.Trim(),
                SequenciaEstacoesSelecionadas = Estacoes.ToList()
            };

            if (cenario == TipoCenarioPoligonal.Enquadrada)
            {
                meta.ChegadaX = LerDoubleUi(ChegadaX, "X (Chegada)");
                meta.ChegadaY = LerDoubleUi(ChegadaY, "Y (Chegada)");
                meta.ChegadaZ = LerDoubleUi(ChegadaZ, "Z (Chegada)");
                meta.AzimuteChegada = ConverterAzimute(AzimuteChegada);
                meta.NomeChegada = NomeChegada.Trim();
            }

            return meta;
        }

        private static double LerDoubleUi(string? texto, string nomeCampo)
        {
            var s = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(s)) throw new FormatException($"Campo '{nomeCampo}' está vazio.");

            // Se contém apenas ponto decimal e nenhuma vírgula, parse direto invariante (evita interpretar ponto como milhar no pt-BR)
            if (s.Contains('.') && !s.Contains(','))
            {
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var vDot))
                    return vDot;
            }

            // Se contém apenas vírgula decimal e nenhum ponto, parse direto pt-BR
            if (s.Contains(',') && !s.Contains('.'))
            {
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.GetCultureInfo("pt-BR"), out var vComma))
                    return vComma;
            }

            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var culturePt = CultureInfo.GetCultureInfo("pt-BR");

            if (double.TryParse(s, styles, culturePt, out var vPt)) return vPt;
            if (double.TryParse(s, styles, CultureInfo.InvariantCulture, out var vInv)) return vInv;

            var sn = s.Replace(" ", "");
            var lastComma = sn.LastIndexOf(',');
            var lastDot = sn.LastIndexOf('.');

            if (lastComma >= 0 || lastDot >= 0)
            {
                var decimalSep = lastComma > lastDot ? ',' : '.';
                var groupSep = decimalSep == ',' ? '.' : ',';
                sn = sn.Replace(groupSep.ToString(), "");
                if (decimalSep != '.') sn = sn.Replace(decimalSep, '.');
                if (double.TryParse(sn, NumberStyles.Float, CultureInfo.InvariantCulture, out var vHeur)) return vHeur;
            }
            throw new FormatException($"Valor inválido no campo '{nomeCampo}': '{texto}'.");
        }

        private static double ConverterAzimute(string entrada)
        {
            if (string.IsNullOrWhiteSpace(entrada)) return 0;
            entrada = entrada.Trim().Replace(',', '.');

            if (double.TryParse(entrada, NumberStyles.Float, CultureInfo.InvariantCulture, out double valorConvertido))
            {
                return TopoGente.Core.Utilities.ConversorAngulos.DeFormatoCompacto(valorConvertido);
            }

            throw new FormatException($"Azimute inválido: '{entrada}'.");
        }
    }
}
