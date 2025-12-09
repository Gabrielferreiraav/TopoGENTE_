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

namespace TopoGente.UI
{
    public partial class MainWindow : Window
    {
        // Instâncias dos serviços
        private readonly LeituraArquivoService _leitorService;
        private readonly LevantamentoProcessor _processadorService;

        private ObservableCollection<LeituraEstacaoTotal> _leituraEmMemoria;

        private Point _origemMouse;
        private bool _estaArrastando = false;

        public MainWindow()
        {
            InitializeComponent();
            _leitorService = new LeituraArquivoService();
            _processadorService = new LevantamentoProcessor();
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
        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // pega o mouse para começar a arrastar
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
        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_estaArrastando)
            {
                _estaArrastando = false;
                var border = sender as IInputElement;
                border?.ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
        }
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_estaArrastando)
            {
                var border = sender as IInputElement;
                var posicaoAtual = e.GetPosition(border);

                // deslocamento do mouse
                var delta = posicaoAtual - _origemMouse;

                // aplica o deslocamento na matriz
                var mat = transformacaoCanvas.Matrix;
                mat.Translate(delta.X, delta.Y);
                transformacaoCanvas.Matrix = mat;

                // mouse agora tem sua posição atual como origem
                _origemMouse = posicaoAtual;
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

            // 5. Desenhar Linhas da Poligonal (Esqueleto Azul)
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

                // A Bolinha 
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

        private void btnCarregarArquivo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos de Texto (*.txt;*.csv)|*.txt;*.csv|Todos os Arquivos (*.*)|*.*",
                Title = "Selecione a Caderneta de Campo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var linhas = File.ReadAllLines(openFileDialog.FileName);
                    var listaLida = _leitorService.LerArquivo(linhas);
                    _leituraEmMemoria = new ObservableCollection<LeituraEstacaoTotal>(listaLida);
                    gridCaderneta.ItemsSource = _leituraEmMemoria;
                    lblArquivo.Text = System.IO.Path.GetFileName(openFileDialog.FileName);
                    btnProcessar.IsEnabled = true; // Habilita o botão de calcular
                    tabsPrincipal.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao ler arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void btnProcessar_Click(object sender, RoutedEventArgs e)
        {
            var cronometro = new Stopwatch();
            try
            {
                // Depois utilizar TryParse para evitar erros de digitação.
                double x = double.Parse(txtX.Text);
                double y = double.Parse(txtY.Text);
                double z = double.Parse(txtZ.Text);
                double azimuteInicial = double.Parse(txtAzimute.Text);
                var pM1 = new PontoCoordenada
                {
                    Nome = "M1",
                    X = x,
                    Y = y,
                    Z = z,
                    EhPontoPoligonal = true
                };


                // Converter o Arquivo em Objetos
                var listaParaProcessar = _leituraEmMemoria.ToList();
                cronometro.Start();
                var resultado = _processadorService.Processar(pM1,azimuteInicial, listaParaProcessar);
                long tempoCalculo = cronometro.ElapsedMilliseconds;
                gridResultados.ItemsSource = resultado.TodosOsPontos;
                cronometro.Restart();
                canvasDesenho.UpdateLayout();
                cronometro.Stop();
                long tempoDesenho = cronometro.ElapsedMilliseconds;
                DesenharLevantamento(resultado.TodosOsPontos);

                txtPerimetro.Text = $"{resultado.Perimetro:F2} m";

                if (resultado.PoligonalFechada)
                {
                    txtErro.Text = $"{resultado.ErroLinear:F3} m";
                    txtPrecisao.Text = $"1:{resultado.Precisao:F0}";
                }
                else
                {
                    txtErro.Text = "-";
                    txtPrecisao.Text = "-";
                }

                tabsPrincipal.SelectedIndex = 1;
                MessageBox.Show("Cálculo realizado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                MessageBox.Show($"Cálculo Matemático: {tempoCalculo} ms\n" + $"Renderização Gráfica: {tempoDesenho} ms\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro no processamento: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}