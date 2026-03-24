using Microsoft.Win32;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TopoGente.Core.Entities;
using TopoGente.Core.Services;
using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using TopoGente.Core.Validators;
using TopoGente.Core.Interfaces;

namespace TopoGente.UI
{
    public partial class MainWindow : Window
    {
        // Instâncias dos serviços
        private readonly ILeituraArquivoFactory _leitorService;
        private readonly ILevantamentoProcessor _processadorService;
        private readonly IArquivoProjetoService _projetoService;
        private readonly IOrganizarCaminhamento _organizador;
        private readonly IExportadorDxfService _dxfService;
        private readonly IQaCheckService _qaCheckService;
        private readonly IClassificadorGrafo _classificadorGrafo;

        private ObservableCollection<LeituraEstacaoTotal> _leituraEmMemoria;
        private List<Estacao> _estacoesEmMemoria;
        private RelatorioQA? _relatorioQaAtual;
        private MetadadosCenario? _metadadosAtuais;
        private ResultadoLevantamento? _resultadoAtual;
        private Point _origemMouse;
        private bool _estaArrastando = false;

        public MainWindow(ILeituraArquivoFactory leitorService,
        ILevantamentoProcessor processadorService,
        IArquivoProjetoService projetoService,IOrganizarCaminhamento organizador,
        IExportadorDxfService dxfService, IQaCheckService qaCheckService)
        {
            InitializeComponent();
            
            _leitorService = leitorService;
            _processadorService = processadorService;
            _organizador = organizador;
            _dxfService = dxfService;
            _qaCheckService = qaCheckService;
            _projetoService = projetoService;

            _leituraEmMemoria = new ObservableCollection<LeituraEstacaoTotal>();
            
            ConfigurarComboTipo();
        }
        private void ConfigurarComboTipo()
        {
            var colTipo = gridCaderneta.Columns[4] as DataGridComboBoxColumn;
            if (colTipo != null)
            {
                colTipo.ItemsSource = Enum.GetValues(typeof(TipoLeitura));
            }
        }
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mat = transformacaoCanvas.Matrix;

            double escala = e.Delta > 0 ? 1.15 : 0.85;

            // pega a posição do mouse para dar zoom onde o mouse está apontando
            Point mousePos = e.GetPosition(canvasDesenho);

            // aplica a escala na matriz
            mat.ScaleAt(escala, escala, mousePos.X, mousePos.Y);
            transformacaoCanvas.Matrix = mat;

            e.Handled = true; // Impede que o scroll propague
        }
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                var border = sender as IInputElement;
                if (border != null)
                {
                    _origemMouse = e.GetPosition(border);
                    _estaArrastando = true;
                    border.CaptureMouse();
                    // indicar movimento
                    Cursor = Cursors.SizeAll;
                }
            }
        }
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_estaArrastando && e.ChangedButton == MouseButton.Middle)
            {
                var border = sender as IInputElement;
                if (border != null)
                {
                    _estaArrastando = false;
                    border?.ReleaseMouseCapture();
                    Cursor = Cursors.Arrow;
                }
            }
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_estaArrastando && e.MiddleButton == MouseButtonState.Pressed)
            {
                var border = sender as IInputElement;
                if (border != null)
                {
                    var posicaoAtual = e.GetPosition(border);

                    // deslocamento do mouse
                    var delta = posicaoAtual - _origemMouse;

                    // aplica o deslocamento na matriz
                    var mat = transformacaoCanvas.Matrix;
                    mat.Translate(delta.X, delta.Y);
                    transformacaoCanvas.Matrix = mat;
                    _origemMouse = posicaoAtual;
                }
            }
        }
        private void bntResetZoom_Click(object sender, RoutedEventArgs e)
        {
            // reseta a transformação
            transformacaoCanvas.Matrix = Matrix.Identity;
        }
        private void chkMostrarNomes_Changed(object sender, RoutedEventArgs e)
        {
            if (canvasDesenho == null)
            {
                return;
            }
            foreach (var child in canvasDesenho.Children)
            {
                if (child is TextBlock texto)
                {
                    texto.Visibility = (chkMostrarNomes.IsChecked == true)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }
        private void DesenharLevantamento(List<PontoCoordenada> pontos)
        {
            transformacaoCanvas.Matrix = Matrix.Identity;
            canvasDesenho.Children.Clear();

            if (pontos == null || pontos.Count == 0) return;

            // Descobrir os limites  para o Zoom
            double minX = pontos.Min(p => p.X);
            double maxX = pontos.Max(p => p.X);
            double minY = pontos.Min(p => p.Y);
            double maxY = pontos.Max(p => p.Y);

            // largura e altura real do levantamento
            double larguraReal = maxX - minX;
            double alturaReal = maxY - minY;

            if (larguraReal == 0) larguraReal = 10;
            if (alturaReal == 0) alturaReal = 10;

            double margem = Math.Max(larguraReal, alturaReal) * 0.1;
            minX -= margem; maxX += margem;
            minY -= margem; maxY += margem;

            larguraReal = maxX - minX;
            alturaReal = maxY - minY;

            // Usa o ActualWidth do Canvas 
            double telaW = canvasDesenho.ActualWidth;
            double telaH = canvasDesenho.ActualHeight;

            if (telaW == 0) telaW = 800;
            if (telaH == 0) telaH = 500;

            double escalaX = telaW / larguraReal;
            double escalaY = telaH / alturaReal;

            // Usa a menor escala para garantir que tudo caiba
            double escala = Math.Min(escalaX, escalaY);

            // para converter Coordenada Real -> Pixel na Tela
            Point ParaTela(double x, double y)
            {
                double xTela = (x - minX) * escala;
                double yTela = (maxY - y) * escala;
                return new Point(xTela, yTela);
            }

            // Se tiver mais de 50 pontos, desliga os nomes por padrão para não poluir
            if (pontos.Count > 50)
            {
                chkMostrarNomes.IsChecked = false;
            }
            else
            {
                chkMostrarNomes.IsChecked = true;
            }

            // Define a visibilidade baseada no estado atual do CheckBox
            Visibility visibilidadeTexto = (chkMostrarNomes.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            // Desenhar Linhas da Poligonal
            // Filtra apenas os pontos que fazem parte da poligonal principal
            var poligonal = pontos.Where(p => p.EhPontoPoligonal).ToList();

            for (int i = 0; i < poligonal.Count - 1; i++)
            {
                Point p1 = ParaTela(poligonal[i].X, poligonal[i].Y);
                Point p2 = ParaTela(poligonal[i + 1].X, poligonal[i + 1].Y);

                Line linha = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2
                };
                canvasDesenho.Children.Add(linha);
            }

            // Desenhar Pontos e Textos
            foreach (var p in pontos)
            {
                Point pos = ParaTela(p.X, p.Y);

                Ellipse pontoGeo = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    // Azul para Poligonal, Verde para Irradiação
                    Fill = p.EhPontoPoligonal ? Brushes.Blue : Brushes.Green,
                    // Tooltip para ver coordenadas ao passar o mouse
                    ToolTip = $"{p.Nome}\nE: {p.X:F3}\nN: {p.Y:F3}\nZ: {p.Z:F3}"
                };

                // Centralizar a bolinha na coordenada exata 
                Canvas.SetLeft(pontoGeo, pos.X - 3);
                Canvas.SetTop(pontoGeo, pos.Y - 3);
                canvasDesenho.Children.Add(pontoGeo);


                TextBlock texto = new TextBlock
                {
                    Text = p.Nome,
                    FontSize = 10,
                    Foreground = Brushes.Black,
                    Visibility = visibilidadeTexto
                };

                // Posiciona o texto um pouco ao lado e acima do ponto
                Canvas.SetLeft(texto, pos.X + 5);
                Canvas.SetTop(texto, pos.Y - 5);
                canvasDesenho.Children.Add(texto);
            }
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

                    var estacoesBrutas = _leitorService.ProcessarArquivo(formato, linhas);
                    _estacoesEmMemoria = _organizador.UnificarEstacoes(estacoesBrutas);

                    cmbEstacoes.ItemsSource = _estacoesEmMemoria;
                    if (_estacoesEmMemoria.Count > 0)
                    {
                        var estacaoInicial = _estacoesEmMemoria[0];
                        if (estacaoInicial.CoordenadaConhecida != null)
                        {
                            txtX.Text = estacaoInicial.CoordenadaConhecida.X.ToString("F3");
                            txtY.Text = estacaoInicial.CoordenadaConhecida.Y.ToString("F3");
                            txtZ.Text = estacaoInicial.CoordenadaConhecida.Z.ToString("F3");
                        }
                    }
                    AplicarGateUIAposCarregarouAbrir(formato, System.IO.Path.GetFileName(openFileDialog.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao ler arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void cmbEstacoes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbEstacoes.SelectedItem is Estacao estacaoSelecionada)
            {
                gridCaderneta.ItemsSource = estacaoSelecionada.Leituras;
                txtInfoEstacao.Text = $"Altura do Instrumento: {estacaoSelecionada.AlturaInstrumento:F3} m";
            }
            else
            {
                gridCaderneta.ItemsSource = null;
                txtInfoEstacao.Text = "Hi: -";
            }
        }

        private static double ConverterAzimute(string entrada)
        {
            if (string.IsNullOrWhiteSpace(entrada))
                return 0;

            entrada = entrada.Trim();

            // precisa atribuir
            entrada = entrada.Replace(',', '.');

            //  tem ponto -> pode ser "GGG.MMSS" (compacto) OU decimal puro
            if (entrada.Contains('.'))
            {
                var partes = entrada.Split('.', 2);
                var parteInteira = partes[0];
                var parteDecimal = partes.Length > 1 ? partes[1] : "0";

                // Se tiver pelo menos 4 casas, pode ser compacto.
                // Decide pelo MM/SS: se invalido, trata como decimal puro.
                if (parteDecimal.Length >= 4)
                {
                    var mmss = parteDecimal.PadRight(4, '0')[..4];

                    int mm = int.Parse(mmss[..2]);
                    int ss = int.Parse(mmss.Substring(2, 2));

                    if (mm < 60 && ss < 60)
                    {
                        
                        var compactoTexto = $"{parteInteira}.{mmss}";
                        double compacto = double.Parse(compactoTexto, System.Globalization.CultureInfo.InvariantCulture);
                        return TopoGente.Core.Utilities.ConversorAngulos.DeFormatoCompacto(compacto);
                    }
                }

                // Decimal puro
                double decimalPuro = double.Parse(entrada, System.Globalization.CultureInfo.InvariantCulture);

                if (decimalPuro < 0 || decimalPuro >= 360)
                {
                    throw new FormatException(
                        $"Azimute fora do intervalo válido: {decimalPuro}°. " +
                        "Azimutes devem estar entre 0° e 360°.");
                }

                return decimalPuro;
            }

            // sem ponto -> pode ser "GGMMSS" (ex: 1351245) ou graus inteiros (ex: 135)
            if (!double.TryParse(entrada, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                throw new FormatException($"Azimute inválido: '{entrada}'.");

            if (valor < 360)
                return valor; // graus inteiros (ou <360)

            //  Interpretar como GGMMSS e converter para GGG.MMSS antes de chamar DeFormatoCompacto
            var digits = entrada;

            // precisa ter pelo menos 5 dígitos para existir MMSS
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
            double compacto2 = double.Parse(compactoSemPonto, System.Globalization.CultureInfo.InvariantCulture);
            return TopoGente.Core.Utilities.ConversorAngulos.DeFormatoCompacto(compacto2);
        }


        private void cmbCenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pnlChegada == null) return;

            var tag = (cmbCenario.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            // Ponto de Chegada (visivel para enquqadrada)
            pnlChegada.Visibility = tag == "Enquadrada" ? Visibility.Visible : Visibility.Collapsed;

            // Fechada e Enquadrada 
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


        /// <sumary>
        /// Lê os campos de entrada da UI e devolve <see cref="MetadadosCenario"/> para ser usado no processamento do levantamento. 
        /// </sumary>
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
                PartidaX = double.Parse(txtX.Text),
                PartidaY = double.Parse(txtY.Text),
                PartidaZ = double.Parse(txtZ.Text),
                UsarCoordenadaRe = usarRe,
                AzimutePartida = usarRe ? 0 : ConverterAzimute(txtAzimute.Text),
                ReX = usarRe ? double.Parse(txtReX.Text) : 0,
                ReY = usarRe ? double.Parse(txtReY.Text) : 0,
                ReZ = usarRe ? double.Parse(txtZ.Text) : 0,
                AzimuteChegada = null
            };

            if (cenario == TipoCenarioPoligonal.Enquadrada)
            {
                meta.ChegadaX = double.Parse(txtChegadaX.Text);
                meta.ChegadaY = double.Parse(txtChegadaY.Text);
                meta.ChegadaZ = double.Parse(txtChegadaZ.Text);
                meta.AzimuteChegada = ConverterAzimute(txtAzimuteChegada.Text);
            }

            return meta;
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
                
                var pontosConhecidos = _estacoesEmMemoria
                    .Where(e => e.CoordenadaConhecida != null)
                    .Select(e => e.CoordenadaConhecida!)
                    .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g=> g.Key, g=> g.First(), StringComparer.OrdinalIgnoreCase);


                // Se cenário Enquadrada, adicionar ponto de chegada aos conhecidos
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

                //Processar
                _classificadorGrafo.ClassificarArestasGrafo(_estacoesEmMemoria, _metadadosAtuais);

                _resultadoAtual = _processadorService.Processar(_metadadosAtuais, _estacoesEmMemoria, pontosConhecidos);

                _relatorioQaAtual = _qaCheckService.GerarRelatorioQaChecks(_estacoesEmMemoria, _resultadoAtual, pontosConhecidos);

                gridResultados.ItemsSource = _resultadoAtual.TodosOsPontos;
                canvasDesenho.UpdateLayout();
                DesenharLevantamento(_resultadoAtual.TodosOsPontos);

                txtPerimetro.Text = $"{_resultadoAtual.Perimetro:F2} m";

                if (_resultadoAtual.PoligonalFechada)
                {
                    txtErro.Text = $"{_resultadoAtual.ErroLinear:F3} m";
                    txtPrecisao.Text = $"1:{_resultadoAtual.Precisao:F0}";
                }
                else
                {
                    txtErro.Text = "-";
                    txtPrecisao.Text = "-";
                }

                //btnExportarDxf.IsEnabled = true;
                tabsPrincipal.SelectedIndex = 1;
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

        private void bntExportarTxt_Click(object sender, EventArgs e)
        {
            var pontosParaExportar = gridResultados.ItemsSource as List<PontoCoordenada>;

            if (pontosParaExportar == null || pontosParaExportar.Count == 0 || _resultadoAtual == null )
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
                    var exportacaoService = new ExportarTxtService();

                    string caminho = saveDialog.FileName;

                    string diretorio = System.IO.Path.GetDirectoryName(caminho) ?? "";
                    string nomeSemExtensao = System.IO.Path.GetFileNameWithoutExtension(caminho);
                    string nomeExtensao = System.IO.Path.GetExtension(caminho);

                    string caminhoMemoria = System.IO.Path.Combine(diretorio, $"{nomeSemExtensao}_MemoriaCalculo{nomeExtensao}");

                    exportacaoService.ExportarCoordenadasGestor(_resultadoAtual, caminho);
                    exportacaoService.ExportarMemoriaCalculo(_resultadoAtual, caminhoMemoria);

                    MessageBox.Show($"Arquivos exportados com sucesso em:\n\n1. {caminho}\n2. {caminhoMemoria}",
                            "Sucesso Geométrico", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                catch (Exception ex) { 
                    MessageBox.Show($"Erro ao exportar arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /*
        public void btnSalvarProjeto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_estacoesEmMemoria == null || _estacoesEmMemoria.Count == 0)
                {
                    MessageBox.Show("Nenhum projeto carregado para salvar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                double x = 0, y = 0, z = 0;
                double.TryParse(txtX.Text, out x);
                double.TryParse(txtY.Text, out y);
                double.TryParse(txtZ.Text, out z);

                double azimuteInicial = 0;
                if (rbAzimute.IsChecked == true)
                {
                    azimuteInicial = ConverterAzimute(txtAzimute.Text);
                }

                var projeto = new ProjetoTopo
                {
                    StartX = x,
                    StartY = y,
                    StartZ = z,
                    StartAzimute = azimuteInicial,
                    Estacoes = _estacoesEmMemoria,
                    RelatorioQA = _relatorioQaAtual,
                    Metadados = _metadadosAtuais
                };

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Arquivo de Projeto TopoGente (*.topo)|*.topo|Todos os Arquivos (*.*)|*.*",
                    FileName = "ProjetoTopo.topo",
                    DefaultExt = ".topo",
                };

                if (saveDialog.ShowDialog() == true)
                {
                    _projetoService.SalvarProjeto(projeto, saveDialog.FileName);
                    MessageBox.Show("Projeto salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar projeto: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void btnAbrirProjeto_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Arquivo de Projeto TopoGente (*.topo)|*.topo|Todos os Arquivos (*.*)|*.*",
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    var projeto = _projetoService.CarregarProjeto(openDialog.FileName);

                    txtX.Clear(); txtY.Clear(); txtZ.Clear(); txtAzimute.Clear();
                    cmbEstacoes.ItemsSource = null;
                    gridCaderneta.ItemsSource = null;
                    gridResultados.ItemsSource = null;
                    canvasDesenho.Children.Clear();
                    txtPerimetro.Text = "-"; txtErro.Text = "-"; txtPrecisao.Text = "-";

                    txtX.Text = projeto.StartX.ToString();
                    txtY.Text = projeto.StartY.ToString();
                    txtZ.Text = projeto.StartZ.ToString();
                    txtAzimute.Text = projeto.StartAzimute.ToString();

                    _estacoesEmMemoria = projeto.Estacoes;
                    _metadadosAtuais = projeto.Metadados;
                    
                    RestaurarMetadadosNaUI(projeto.Metadados);
                    AplicarGateUIAposCarregarouAbrir(null, System.IO.Path.GetFileName(openDialog.FileName));

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir projeto: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }*/
        private void RestaurarMetadadosNaUI(MetadadosCenario? meta)
        {
            if (meta == null) return;

            // Cenário
            cmbCenario.SelectedIndex = meta.TipoCenario switch
            {
                TipoCenarioPoligonal.Enquadrada => 0,
                TipoCenarioPoligonal.Fechada => 1,
                TipoCenarioPoligonal.AbertaOrientada => 2,
                _ => 1
            };

            // Partida
            txtX.Text = meta.PartidaX.ToString("F3");
            txtY.Text = meta.PartidaY.ToString("F3");
            txtZ.Text = meta.PartidaZ.ToString("F3");

            // Orientação
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

            // Chegada
            if (meta.TipoCenario == TipoCenarioPoligonal.Enquadrada)
            {
                txtChegadaX.Text = meta.ChegadaX?.ToString("F3") ?? "0.0";
                txtChegadaY.Text = meta.ChegadaY?.ToString("F3") ?? "0.0";
                txtChegadaZ.Text = meta.ChegadaZ?.ToString("F3") ?? "0.0";
            }
        }
        /*
        public void btnExportarDxf_Click(object sender, RoutedEventArgs e)
        {
            var pontosParaExportar = gridResultados.ItemsSource as List<PontoCoordenada>;

            if (pontosParaExportar == null || pontosParaExportar.Count == 0)
            {
                MessageBox.Show("Não há coordenadas calculadas para exportar !", "Aviso");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Arquivo DXF (*.dxf)|*.dxf|Todos os Arquivos (*.*)|*.*",
                FileName = "LevantamentoTopoGente.dxf",
                DefaultExt = ".dxf",
            };
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    _dxfService.SalvarDxf(pontosParaExportar, saveDialog.FileName);
                    MessageBox.Show("Arquivo DXF exportado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao exportar DXF: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }*/

        private sealed class GateResultado
        {
            public bool PodeCalcular { get; init; }
            public string Motivo { get; init; } = string.Empty;
        }

        private static List<LeituraEstacaoTotal> ColetarLeituras(List<Estacao> estacoes)
            => estacoes?.SelectMany(e => e.Leituras ?? new List<LeituraEstacaoTotal>()).ToList()
            ?? new List<LeituraEstacaoTotal>();

        private static List<PontoCoordenada> ColetarPontosCoordenada(List<Estacao> estacoes)
            => estacoes?
            .Where(e => e.CoordenadaConhecida != null)
            .Select(e => e.CoordenadaConhecida!)
            .Select(p => new PontoCoordenada
            {
                Nome = p.Nome,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                EhPontoPoligonal = false
            }).ToList() ?? new List<PontoCoordenada>();

        private static GateResultado AvaliarGateCalculo(List<Estacao> estacoes)
        {
            if (estacoes == null || estacoes.Count ==0)
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Nenhuma estacao encontrada" };
            }

            var leituras = ColetarLeituras(estacoes);

            if (leituras.Count == 0)
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Nenhuma leitura encontrada" };
            }

            if (!leituras.Any(l => l.Tipo == TipoLeitura.Re))
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Não há leituras de Re" };
            }

            if (!leituras.Any(l => l.Tipo == TipoLeitura.Poligonal))
            {
                return new GateResultado { PodeCalcular = false, Motivo = "Não há leituras de Poligonal" };
            }

            foreach (var leitura in leituras.Where(l => l.Tipo is TipoLeitura.Re or TipoLeitura.Poligonal))
            {
                var valid = LeituraValidator.Validar(leitura);
                if (!valid.IsValid)
                {
                    return new GateResultado
                    {
                        PodeCalcular = false,
                        Motivo = " Leituras invalidas " + string.Join(" | ", valid.Errors)
                    };
                }
            }

            return new GateResultado { PodeCalcular = true };
        }

        private void EntraModoPontos(string mensagem, List<PontoCoordenada> pontos)
        {
            btnProcessar.IsEnabled = false;
            //btnExportarDxf.IsEnabled = pontos.Count > 0;

            gridResultados.ItemsSource = pontos;
            canvasDesenho.UpdateLayout();
            DesenharLevantamento(pontos);

            tabsPrincipal.SelectedIndex = 1; // Coordenadas Calculadas
            MessageBox.Show(mensagem, "Importacao", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AplicarGateUIAposCarregarouAbrir(FormatoArquivoEntrada? formato, string nomeArquivoParaLabel)
        {
            cmbEstacoes.ItemsSource = _estacoesEmMemoria;
            if (_estacoesEmMemoria != null && _estacoesEmMemoria.Count > 0 )
            {
                cmbEstacoes.SelectedIndex = 0;
            }

            var gate = AvaliarGateCalculo(_estacoesEmMemoria);

            if (gate.PodeCalcular)
            {
                btnProcessar.IsEnabled = true;
                //btnExportarDxf.IsEnabled = false;
                return;
            }

            var pontos = ColetarPontosCoordenada(_estacoesEmMemoria);

            // abrir modo caso ja tenha pontos conhecidos

            if (pontos.Count > 0)
            {
                EntraModoPontos(
                    "Dados carregados, porem sem observacoes suficientes para calculode poligonal. \n \n" +
                    "Use exportação/visualização ou importe outro formato. \n \n" +
                    $"Motivo : {gate.Motivo}", pontos);
                return;
            }

            btnProcessar.IsEnabled = false;
            //btnExportarDxf.IsEnabled = false;

            MessageBox.Show(
                "Dados carregados, porem na há observacoes suficientes para calculo e nem pontos conhecidos suficientes para exibicao \n \n" +
                $"Motivo : {gate.Motivo}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void gridCaderneta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}