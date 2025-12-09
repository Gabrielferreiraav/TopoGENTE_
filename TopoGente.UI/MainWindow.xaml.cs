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

namespace TopoGente.UI
{
    public partial class MainWindow : Window
    {
        // Instâncias dos serviços
        private readonly LeituraArquivoService _leitorService;
        private readonly LevantamentoProcessor _processadorService;

        private ObservableCollection<LeituraEstacaoTotal> _leituraEmMemoria;

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
                // Preenche com os valores do Enum
                colTipo.ItemsSource = Enum.GetValues(typeof(TipoLeitura));
            }
        }
        private void DesenharLevantamento(List<PontoCoordenada> pontos)
        {
            canvasDesenho.Children.Clear();

            if (pontos == null || pontos.Count == 0) return;

            // Descobrir os limites para o Zoom Extents
            double minX = pontos.Min(p => p.X);
            double maxX = pontos.Max(p => p.X);
            double minY = pontos.Min(p => p.Y);
            double maxY = pontos.Max(p => p.Y);

            // Adiciona uma margem de 10% para não ficar colado na borda
            double larguraReal = maxX - minX;
            double alturaReal = maxY - minY;

            // Proteção para caso seja um ponto só ou muito pequeno
            if (larguraReal == 0) larguraReal = 10;
            if (alturaReal == 0) alturaReal = 10;

            double margem = Math.Max(larguraReal, alturaReal) * 0.1;
            minX -= margem; maxX += margem;
            minY -= margem; maxY += margem;
            larguraReal = maxX - minX;
            alturaReal = maxY - minY;

            //  Calcular Fator de Escala (Pixels por Metro)
            // Usa o ActualWidth do Canvas
            double telaW = canvasDesenho.ActualWidth;
            double telaH = canvasDesenho.ActualHeight;

            if (telaW == 0) telaW = 800; // Fallback se a aba não tiver sido carregada
            if (telaH == 0) telaH = 500;

            double escalaX = telaW / larguraReal;
            double escalaY = telaH / alturaReal;
            double escala = Math.Min(escalaX, escalaY); // Usa a menor escala para caber tudo

            Point ParaTela(double x, double y)
            {
                double xTela = (x - minX) * escala;
                // Y na tela cresce para baixo, então invertemos
                double yTela = (maxY - y) * escala;
                return new Point(xTela, yTela);
            }

            // Precisamos conectar Estação -> Irradiação ou Estação -> Próxima Estação

            // ESQUELETO DA POLIGONAL
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

            // DESENHAR AS IRRADIAÇÕES 
            // Para simplificar desenhar apenas os PONTOS 
            // (Para desenhar linhas de irradiação, precisaríamos saber o "Pai" de cada ponto na lista final)

            foreach (var p in pontos)
            {
                Point pos = ParaTela(p.X, p.Y);

                Ellipse pontoGeo = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = p.EhPontoPoligonal ? Brushes.Blue : Brushes.Green
                };

                // Centralizar na coordenada
                Canvas.SetLeft(pontoGeo, pos.X - 3);
                Canvas.SetTop(pontoGeo, pos.Y - 3);

                // Tooltip para ver o nome ao passar o mouse
                pontoGeo.ToolTip = $"{p.Nome}\nX: {p.X:F3}\nY: {p.Y:F3}";

                canvasDesenho.Children.Add(pontoGeo);

                TextBlock texto = new TextBlock
                {
                    Text = p.Nome,
                    FontSize = 10,
                    Foreground = Brushes.Black
                };
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
            try
            {
                // Nota: Depois utilizar TryParse para evitar erros de digitação.
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

                var resultado = _processadorService.Processar(pM1,azimuteInicial, listaParaProcessar);

                gridResultados.ItemsSource = resultado.TodosOsPontos;

                canvasDesenho.UpdateLayout();
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

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro no processamento: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}