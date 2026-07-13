using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Validators;
using TopoGente.UI.Eventing;

namespace TopoGente.UI
{
    public partial class MainWindow : MetroWindow
    {
        private readonly ILeituraArquivoFactory _leitorService;
        private readonly ILevantamentoProcessor _processadorService;
        private readonly IArquivoProjetoService _projetoService;
        private readonly IOrganizarCaminhamento _organizador;
        private readonly IExportadorDxfService _dxfService;
        private readonly IExportarTxtService _exportarTxtService;
        private readonly IQaCheckService _qaCheckService;
        private readonly IClassificadorGrafo _classificadorGrafo;
        private readonly IUiEventHub _uiEventHub;

        private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");

        private List<Estacao> _estacoesEmMemoria = new();
        private RelatorioQA? _relatorioQaAtual;
        private MetadadosCenario? _metadadosAtuais;
        private ResultadoLevantamento? _resultadoAtual;

        public MainWindow(
            ILeituraArquivoFactory leitorService,
            ILevantamentoProcessor processadorService,
            IArquivoProjetoService projetoService,
            IOrganizarCaminhamento organizador,
            IExportadorDxfService dxfService,
            IExportarTxtService exportarTxtService,
            IQaCheckService qaCheckService,
            IClassificadorGrafo classificadorGrafo,
            IUiEventHub uiEventHub)
        {
            InitializeComponent();

            _leitorService = leitorService;
            _processadorService = processadorService;
            _organizador = organizador;
            _dxfService = dxfService;
            _exportarTxtService = exportarTxtService;
            _qaCheckService = qaCheckService;
            _projetoService = projetoService;
            _classificadorGrafo = classificadorGrafo;
            _uiEventHub = uiEventHub;

            AtualizarListaEstacoes();
        }

        private static FormatoArquivoEntrada ObterFormatoEntrada(ComboBox cmbFormatoArquivo)
        {
            return cmbFormatoArquivo.SelectedIndex switch
            {
                0 => FormatoArquivoEntrada.CsvPadrao,
                1 => FormatoArquivoEntrada.Fbk,
                2 => FormatoArquivoEntrada.LandXml,
                _ => FormatoArquivoEntrada.CsvPadrao,
            };
        }

        private void btnCarregarArquivo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos Topográficos (*.txt;*.csv;*.fbk;*.xml)|*.txt;*.csv;*.fbk;*.xml|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione a Caderneta de Campo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var formato = ObterFormatoEntrada(cmbFormatoArquivo);
                    var linhas = File.ReadAllLines(openFileDialog.FileName);

                    var resultadoLeitura = _leitorService.ProcessarArquivoComResultado(formato, linhas);
                    var estacoesBrutas = resultadoLeitura.Estacoes;
                    _estacoesEmMemoria = _organizador.UnificarEstacoes(estacoesBrutas);
                    AtualizarListaEstacoes();
                    SugerirSequenciaPoligonalPorPurpose();

                    if (_estacoesEmMemoria.Count > 0)
                    {
                        var primeiraEstacao = _estacoesEmMemoria[0];
                        // 1 e 2. Extração do Ponto de Partida e Fallback Seguro
                        if (primeiraEstacao.CoordenadaConhecida != null)
                        {
                            txtX.Text = primeiraEstacao.CoordenadaConhecida.X.ToString("F3");
                            txtY.Text = primeiraEstacao.CoordenadaConhecida.Y.ToString("F3");
                            txtZ.Text = primeiraEstacao.CoordenadaConhecida.Z.ToString("F3");
                        }
                        else
                        {
                            txtX.Text = "0.000";
                            txtY.Text = "0.000";
                            txtZ.Text = "0.000";
                        }

                        // 3. Extração do Ponto de Referência (Ré)
                        var leituraRe = primeiraEstacao.Leituras?.FirstOrDefault(l => PurposeEh(l, "re"));

                        if (leituraRe != null)
                        {
                            txtNomeRe.Text = leituraRe.PontoVisado ?? string.Empty;

                            var pontosConhecidos = resultadoLeitura.PontosConhecidosGlobais;

                            // 4. O Gatilho de Automação do Azimute
                            if (!string.IsNullOrEmpty(leituraRe.PontoVisado) &&
                                pontosConhecidos.TryGetValue(leituraRe.PontoVisado, out var coordenadaRe))
                            {
                                txtReX.Text = coordenadaRe.X.ToString("F3");
                                txtReY.Text = coordenadaRe.Y.ToString("F3");
                                txtReZ.Text = coordenadaRe.Z.ToString("F3");
                                rbCoordenadaRe.IsChecked = true;
                            }
                            else
                            {
                                rbAzimute.IsChecked = true;
                                txtAzimute.Text = leituraRe.AnguloHorizontal.ToString("F4");
                            }
                        }

                        _uiEventHub.PublicarEstacoes(_estacoesEmMemoria);
                        AplicarGateUIAposCarregarouAbrir(formato, Path.GetFileName(openFileDialog.FileName));
                        PublicarEsbocoGeodesicoSobDemanda();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao ler arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static double ConverterAzimute(string entrada)
        {
            if (string.IsNullOrWhiteSpace(entrada))
                return 0;

            entrada = entrada.Trim();
            entrada = entrada.Replace(',', '.');

            if (entrada.Contains('.'))
            {
                var partes = entrada.Split('.', 2);
                var parteInteira = partes[0];
                var parteDecimal = partes.Length > 1 ? partes[1] : "0";

                if (parteDecimal.Length >= 4)
                {
                    var mmss = parteDecimal.PadRight(4, '0')[..4];

                    int mm = int.Parse(mmss[..2]);
                    int ss = int.Parse(mmss.Substring(2, 2));

                    if (mm < 60 && ss < 60)
                    {
                        var compactoTexto = $"{parteInteira}.{mmss}";
                        double compacto = double.Parse(compactoTexto, CultureInfo.InvariantCulture);
                        return TopoGente.Core.Utilities.ConversorAngulos.DeFormatoCompacto(compacto);
                    }
                }

                double decimalPuro = double.Parse(entrada, CultureInfo.InvariantCulture);

                if (decimalPuro < 0 || decimalPuro >= 360)
                {
                    throw new FormatException(
                        $"Azimute fora do intervalo válido: {decimalPuro}°. " +
                        "Azimutes devem estar entre 0° e 360°.");
                }

                return decimalPuro;
            }

            if (!double.TryParse(entrada, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
                throw new FormatException($"Azimute inválido: '{entrada}'.");

            if (valor < 360)
                return valor;

            var digits = entrada;

            if (digits.Length < 5)
                throw new FormatException($"Formato GGMMSS inválido: '{entrada}'.");

            var grausTexto = digits[..^4];
            var mmssTexto = digits[^4..];

            int mm2 = int.Parse(mmssTexto[..2]);
            int ss2 = int.Parse(mmssTexto.Substring(2, 2));

            if (mm2 >= 60 || ss2 >= 60)
            {
                throw new FormatException(
                    $"Formato de ângulo inválido: '{entrada}' = {grausTexto}°{mm2}'{ss2}\". " +
                    $"Minutos ({mm2}) e segundos ({ss2}) devem ser < 60.");
            }

            var compactoSemPonto = $"{grausTexto}.{mmssTexto}";
            double compacto2 = double.Parse(compactoSemPonto, CultureInfo.InvariantCulture);
            return TopoGente.Core.Utilities.ConversorAngulos.DeFormatoCompacto(compacto2);
        }

        private void cmbCenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlChegada == null) return;

            var tag = (cmbCenario.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            pnlChegada.Visibility = tag == "Enquadrada" ? Visibility.Visible : Visibility.Collapsed;

            rbAzimute.IsChecked = true;
            rbCoordenadaRe.IsEnabled = true;
        }

        private void rbOrientacao_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlAzimute == null || pnlCoordenadaRe == null) return;

            bool usarAzimute = rbAzimute.IsChecked == true;
            if (usarAzimute)
            {
                rbCoordenadaRe.IsChecked = false;
            }
            pnlAzimute.Visibility = usarAzimute ? Visibility.Visible : Visibility.Collapsed;
            pnlCoordenadaRe.Visibility = usarAzimute ? Visibility.Collapsed : Visibility.Visible;
        }

        private static double LerDoubleUi(string? texto, string nomeCampo)
        {
            var s = (texto ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(s))
                throw new FormatException($"Campo '{nomeCampo}' está vazio.");

            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;

            if (double.TryParse(s, styles, CulturaPtBr, out var vPt))
                return vPt;

            if (double.TryParse(s, styles, CultureInfo.InvariantCulture, out var vInv))
                return vInv;

            var sn = s.Replace(" ", "");
            var lastComma = sn.LastIndexOf(',');
            var lastDot = sn.LastIndexOf('.');

            if (lastComma >= 0 || lastDot >= 0)
            {
                var decimalSep = lastComma > lastDot ? ',' : '.';
                var groupSep = decimalSep == ',' ? '.' : ',';

                sn = sn.Replace(groupSep.ToString(), "");
                if (decimalSep != '.')
                    sn = sn.Replace(decimalSep, '.');

                if (double.TryParse(sn, NumberStyles.Float, CultureInfo.InvariantCulture, out var vHeur))
                    return vHeur;
            }

            throw new FormatException($"Valor inválido no campo '{nomeCampo}': '{texto}'.");
        }

        private static bool PurposeEh(LeituraEstacaoTotal leitura, string purpose)
        {
            return string.Equals(
                (leitura.Purpose ?? string.Empty).Trim(),
                purpose,
                StringComparison.OrdinalIgnoreCase);
        }

        private void SugerirSequenciaPoligonalPorPurpose()
        {
            var nomesOcupados = _estacoesEmMemoria
                .Select(e => e.Nome)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_estacoesEmMemoria.Count == 0 || nomesOcupados.Count == 0)
            {
                return;
            }

            var sequenciaSugerida = new List<string>();
            var visitadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? nomeAtual = _estacoesEmMemoria[0].Nome;

            while (!string.IsNullOrWhiteSpace(nomeAtual) && visitadas.Add(nomeAtual))
            {
                sequenciaSugerida.Add(nomeAtual);

                var estacaoAtual = _estacoesEmMemoria
                    .FirstOrDefault(e => string.Equals(e.Nome, nomeAtual, StringComparison.OrdinalIgnoreCase));

                var leituraVante = estacaoAtual?.Leituras.FirstOrDefault(l => PurposeEh(l, "vante"));
                if (leituraVante == null || string.IsNullOrWhiteSpace(leituraVante.PontoVisado))
                {
                    break;
                }

                var proximoNome = leituraVante.PontoVisado.Trim();
                if (visitadas.Contains(proximoNome))
                {
                    sequenciaSugerida.Add(proximoNome);
                    break;
                }

                if (!nomesOcupados.Contains(proximoNome))
                {
                    sequenciaSugerida.Add(proximoNome);
                    break;
                }

                nomeAtual = proximoNome;
            }

            if (sequenciaSugerida.Count <= 1)
            {
                return;
            }

            lstSequenciaPoligonal.Items.Clear();
            foreach (var nome in sequenciaSugerida)
            {
                lstSequenciaPoligonal.Items.Add(nome);
            }
        }

        private MetadadosCenario ColetarMetadadosDaUI()
        {
            var tag = (cmbCenario.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Fechada";

            var cenario = tag switch
            {
                "Enquadrada" => TipoCenarioPoligonal.Enquadrada,
                "Fechada" => TipoCenarioPoligonal.Fechada,
                "AbertaOrientada" => TipoCenarioPoligonal.AbertaOrientada,
                _ => TipoCenarioPoligonal.Fechada
            };

            bool usarRe = rbCoordenadaRe.IsChecked == true;

            var meta = new MetadadosCenario
            {
                TipoCenario = cenario,
                PartidaX = LerDoubleUi(txtX.Text, "X (Partida)"),
                PartidaY = LerDoubleUi(txtY.Text, "Y (Partida)"),
                PartidaZ = LerDoubleUi(txtZ.Text, "Z (Partida)"),
                UsarCoordenadaRe = usarRe,
                AzimutePartida = usarRe ? 0 : ConverterAzimute(txtAzimute.Text),
                ReX = usarRe ? LerDoubleUi(txtReX.Text, "X (Ré)") : 0,
                ReY = usarRe ? LerDoubleUi(txtReY.Text, "Y (Ré)") : 0,
                ReZ = usarRe ? LerDoubleUi(txtReZ.Text, "Z (Ré)") : 0,
                AzimuteChegada = null,
                NomeRe = txtNomeRe.Text.Trim(),
                SequenciaEstacoesSelecionadas = ColetarSequenciaSelecionada()
            };

            if (cenario == TipoCenarioPoligonal.Enquadrada)
            {
                meta.ChegadaX = LerDoubleUi(txtChegadaX.Text, "X (Chegada)");
                meta.ChegadaY = LerDoubleUi(txtChegadaY.Text, "Y (Chegada)");
                meta.ChegadaZ = LerDoubleUi(txtChegadaZ.Text, "Z (Chegada)");
                meta.AzimuteChegada = ConverterAzimute(txtAzimuteChegada.Text);
                meta.NomeChegada = txtNomeChegada.Text.Trim();
            }

            return meta;
        }

        private List<string> ColetarSequenciaSelecionada()
        {
            return lstSequenciaPoligonal.Items
                .OfType<string>()
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
        }

        private void btnProcessar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_estacoesEmMemoria == null || _estacoesEmMemoria.Count == 0)
                {
                    MessageBox.Show("Nenhuma estação carregada.", "Aviso");
                    return;
                }

                _metadadosAtuais = ColetarMetadadosDaUI();

                if (_metadadosAtuais.SequenciaEstacoesSelecionadas.Count == 0)
                {
                    MessageBox.Show("Selecione a sequência de estações da poligonal antes de calcular.", "Pré-condição Falhou", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pontosConhecidos = _estacoesEmMemoria
                    .Where(e => e.CoordenadaConhecida != null)
                    .Select(e => e.CoordenadaConhecida!)
                    .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                if (_metadadosAtuais.TipoCenario == TipoCenarioPoligonal.Enquadrada)
                {
                    pontosConhecidos["CHEGADA"] = new PontoCoordenada
                    {
                        Nome = "CHEGADA",
                        X = _metadadosAtuais.ChegadaX ?? 0,
                        Y = _metadadosAtuais.ChegadaY ?? 0,
                        Z = _metadadosAtuais.ChegadaZ ?? 0,
                        EhPontoPoligonal = true
                    };
                }

                _classificadorGrafo.ClassificarArestasGrafo(_estacoesEmMemoria, _metadadosAtuais);

                var leiturasClassificadas = _estacoesEmMemoria.SelectMany(e => e.Leituras).ToList();

                foreach (var leitura in leiturasClassificadas.Where(l => l.Tipo == TipoLeitura.Poligonal || l.Tipo == TipoLeitura.Re))
                {
                    var valid = LeituraValidator.Validar(leitura);
                    if (!valid.IsValid)
                    {
                        MessageBox.Show($"Dados corrompidos na estação '{leitura.EstacaoOcupada}' visando '{leitura.PontoVisado}':\n" + string.Join("\n", valid.Errors),
                        "Falha de Validação Geométrica", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                _resultadoAtual = _processadorService.Processar(_metadadosAtuais, _estacoesEmMemoria, pontosConhecidos);
                _relatorioQaAtual = _qaCheckService.GerarRelatorioQaChecks(_estacoesEmMemoria, _resultadoAtual, pontosConhecidos);

                _uiEventHub.PublicarResultado(_resultadoAtual);

                if (_metadadosAtuais.TipoCenario == TipoCenarioPoligonal.AbertaOrientada)
                {
                    MessageBox.Show(
                        "Este levantamento é do tipo ABERTO. As coordenadas finais não foram auditadas contra erros de fechamento.\n\n" +
                        "Qualquer erro angular na primeira estação deslocará linearmente todas as estações subsequentes (efeito alavanca).",
                        "⚠️ Aviso — Poligonal Aberta",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else if (!_resultadoAtual.AprovadoNorma)
                {
                    string erros = string.Join("\n", _resultadoAtual.Alertas);
                    MessageBox.Show($"LEVANTAMENTO REPROVADO (NBR 13.133):\n\n{erros}\n\nA compensação foi abortada. As coordenadas exibidas são puramente BRUTAS e impróprias para uso final.",
                    "Falha de Tolerância", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Cálculo realizado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("Verifique se todos os campos numéricos estão preenchidos corretamente.", "Erro de Formato", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro no processamento: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void bntExportarTxt_Click(object sender, System.EventArgs e)
        {
            if (_resultadoAtual == null || _resultadoAtual.TodosOsPontos.Count == 0)
            {
                MessageBox.Show("Não há Levantamento para exportar ", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = "Salvar Aqruvio de Levantamento",
                Filter = "Arquivo de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                FileName = "LevantamentoTopoGente.txt",
                DefaultExt = ".txt",
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    string caminho = saveDialog.FileName;

                    string diretorio = Path.GetDirectoryName(caminho) ?? "";
                    string nomeSemExtensao = Path.GetFileNameWithoutExtension(caminho);
                    string nomeExtensao = Path.GetExtension(caminho);

                    string caminhoMemoria = Path.Combine(diretorio, $"{nomeSemExtensao}_MemoriaCalculo{nomeExtensao}");

                    _exportarTxtService.ExportarCoordenadasGestor(_resultadoAtual, caminho);
                    _exportarTxtService.ExportarMemoriaCalculo(_resultadoAtual, caminhoMemoria);

                    MessageBox.Show($"Arquivos exportados com sucesso em:\n\n1. {caminho}\n2. {caminhoMemoria}",
                            "Sucesso Geométrico", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao exportar arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private sealed class GateResultado
        {
            public bool PodeCalcular { get; init; }
            public string Motivo { get; init; } = string.Empty;
        }

        private static List<LeituraEstacaoTotal> ColetarLeituras(List<Estacao> estacoes)
            => estacoes?.SelectMany(e => e.Leituras ?? new List<LeituraEstacaoTotal>()).ToList()
            ?? new List<LeituraEstacaoTotal>();

        private static GateResultado AvaliarGateCalculo(List<Estacao> estacoes)
        {
            if (estacoes == null || estacoes.Count == 0)
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Nenhuma estacao encontrada" };
            }

            var leituras = ColetarLeituras(estacoes);

            if (leituras.Count == 0)
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Nenhuma leitura encontrada" };
            }

            return new GateResultado { PodeCalcular = true };
        }

        private void AplicarGateUIAposCarregarouAbrir(FormatoArquivoEntrada? formato, string nomeArquivoParaLabel)
        {
            var gate = AvaliarGateCalculo(_estacoesEmMemoria);

            if (gate.PodeCalcular)
            {
                btnProcessar.IsEnabled = true;
                return;
            }

            btnProcessar.IsEnabled = false;

            MessageBox.Show(
                "Dados carregados, porém não há observações suficientes para cálculo.\n\n" +
                $"Motivo: {gate.Motivo}",
                "Aviso",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void AtualizarListaEstacoes()
        {
            // Extrai estritamente os nomes das estações ocupadas (vértices da poligonal)
            var estacoesOcupadas = _estacoesEmMemoria
                .Select(e => e.Nome)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .Distinct()
                .ToList();

            // ATRIBUIÇÃO NA UI
            lstEstacoesDisponiveis.ItemsSource = estacoesOcupadas;
        }

        private void RestaurarMetadadosNaUI(MetadadosCenario? meta)
        {
            if (meta == null) return;

            cmbCenario.SelectedIndex = meta.TipoCenario switch
            {
                TipoCenarioPoligonal.Enquadrada => 0,
                TipoCenarioPoligonal.Fechada => 1,
                TipoCenarioPoligonal.AbertaOrientada => 2,
                _ => 1
            };

            txtX.Text = meta.PartidaX.ToString("F3");
            txtY.Text = meta.PartidaY.ToString("F3");
            txtZ.Text = meta.PartidaZ.ToString("F3");

            if (meta.UsarCoordenadaRe)
            {
                rbCoordenadaRe.IsChecked = true;
                txtReX.Text = meta.ReX.ToString("F3");
                txtReY.Text = meta.ReY.ToString("F3");
                txtReZ.Text = meta.ReZ.ToString("F3");
            }
            else
            {
                rbAzimute.IsChecked = true;
                txtAzimute.Text = meta.AzimutePartida.ToString();
            }

            if (meta.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                txtChegadaX.Text = meta.ChegadaX?.ToString("F3") ?? "0.0";
                txtChegadaY.Text = meta.ChegadaY?.ToString("F3") ?? "0.0";
                txtChegadaZ.Text = meta.ChegadaZ?.ToString("F3") ?? "0.0";
            }
        }

        private void btnExibirCaderneta_Click(object sender, RoutedEventArgs e)
        {
            var window = Application.Current.Windows.OfType<CadernetaWindow>().FirstOrDefault();
            if (window != null)
            {
                window.Show();
                window.Activate();
            }
        }

        private void btnExibirGrafico_Click(object sender, RoutedEventArgs e)
        {
            var window = Application.Current.Windows.OfType<VisualizacaoWindow>().FirstOrDefault();
            if (window != null)
            {
                window.Show();
                window.Activate();
            }
        }

        private void btnAdicionarSequencia_Click(object sender, RoutedEventArgs e)
        {
            if (lstEstacoesDisponiveis.SelectedItem is not string selecionada)
            {
                return;
            }

            lstSequenciaPoligonal.Items.Add(selecionada);
            PublicarEsbocoGeodesicoSobDemanda();
        }

        private void btnRemoverSequencia_Click(object sender, RoutedEventArgs e)
        {
            if (lstSequenciaPoligonal.SelectedItem is not string selecionada)
            {
                return;
            }

            lstSequenciaPoligonal.Items.Remove(selecionada);
            PublicarEsbocoGeodesicoSobDemanda();
        }

        private void btnSubirSequencia_Click(object sender, RoutedEventArgs e)
        {
            var indice = lstSequenciaPoligonal.SelectedIndex;
            if (indice <= 0)
            {
                return;
            }

            var item = lstSequenciaPoligonal.Items[indice];
            lstSequenciaPoligonal.Items.RemoveAt(indice);
            lstSequenciaPoligonal.Items.Insert(indice - 1, item);
            lstSequenciaPoligonal.SelectedIndex = indice - 1;
            PublicarEsbocoGeodesicoSobDemanda();
        }

        private void btnDescerSequencia_Click(object sender, RoutedEventArgs e)
        {
            var indice = lstSequenciaPoligonal.SelectedIndex;
            if (indice < 0 || indice >= lstSequenciaPoligonal.Items.Count - 1)
            {
                return;
            }

            var item = lstSequenciaPoligonal.Items[indice];
            lstSequenciaPoligonal.Items.RemoveAt(indice);
            lstSequenciaPoligonal.Items.Insert(indice + 1, item);
            lstSequenciaPoligonal.SelectedIndex = indice + 1;
            PublicarEsbocoGeodesicoSobDemanda();
        }

        private void PublicarEsbocoGeodesicoSobDemanda()
        {
            if (_estacoesEmMemoria == null || _estacoesEmMemoria.Count == 0) return;
            try
            {
                var metadados = ColetarMetadadosDaUI();
                var dtoPreliminar = _processadorService.GerarEsbocoBruto(metadados, _estacoesEmMemoria);
                _uiEventHub.PublicarResultado(dtoPreliminar);
            }
            catch
            {
                // Ignora falhas para manter a fluidez da UI ao ajustar a sequência
            }
        }
    }
}
