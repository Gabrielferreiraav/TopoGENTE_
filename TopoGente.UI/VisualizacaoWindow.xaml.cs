using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using TopoGente.Core.Entities;
using TopoGente.UI.Eventing;
using TopoGente.UI.ViewModels;

namespace TopoGente.UI
{
    public partial class VisualizacaoWindow : MetroWindow
    {
        private readonly IUiEventHub _uiEventHub;
        private Point _origemMouse;
        private bool _estaArrastando;
        private ResultadoLevantamento? _ultimoResultado; // Cache local

        public ObservableCollection<FiltroCamada> CamadasDisponiveis { get; } = new();

        public VisualizacaoWindow(IUiEventHub uiEventHub)
        {
            InitializeComponent();
            DataContext = this; // Vincula a UI nela mesma para expor as CamadasDisponiveis

            _uiEventHub = uiEventHub;
            _uiEventHub.ResultadoAtualizado += OnResultadoAtualizado;

            Closing += VisualizacaoWindow_Closing;
        }

        public void AtualizarDesenho(ResultadoLevantamento resultado)
        {
            canvasDesenho.UpdateLayout();
            DesenharLevantamento(resultado);
        }

        private void OnResultadoAtualizado(object? sender, ResultadoEventArgs e)
        {
            if (!IsVisible) Show();

            _ultimoResultado = e.Resultado;
            ExtrairCamadasSemanticas(_ultimoResultado);
            AtualizarDesenho(_ultimoResultado);
        }

        private void ExtrairCamadasSemanticas(ResultadoLevantamento resultado)
        {
            if (resultado == null || !resultado.Irradiacoes.Any()) return;

            var descricoesUnicas = resultado.Irradiacoes
                .Select(p => string.IsNullOrWhiteSpace(p.Descricao) ? "SEM DESCRIÇÃO" : p.Descricao.Trim().ToUpper())
                .Distinct()
                .OrderBy(d => d).ToList();

            var ativasAnteriormente = new HashSet<string>(CamadasDisponiveis.Where(c => c.IsVisivel).Select(c => c.Nome));
            bool isPrimeiraVez = CamadasDisponiveis.Count == 0;

            CamadasDisponiveis.Clear();

            foreach (var desc in descricoesUnicas)
            {
                bool visivel = isPrimeiraVez || ativasAnteriormente.Contains(desc);
                var filtro = new FiltroCamada { Nome = desc, IsVisivel = visivel };
                
                // Quando a check for clicada, redesenha sem passar pelo Core
                filtro.VisibilidadeAlterada += (s, ev) => AtualizarDesenho(_ultimoResultado!);
                CamadasDisponiveis.Add(filtro);
            }
        }

        private void VisualizacaoWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mat = transformacaoCanvas.Matrix;

            double escala = e.Delta > 0 ? 1.15 : 0.85;
            Point mousePos = e.GetPosition(canvasDesenho);

            mat.ScaleAt(escala, escala, mousePos.X, mousePos.Y);
            transformacaoCanvas.Matrix = mat;

            e.Handled = true;
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
                    border.ReleaseMouseCapture();
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
                    var delta = posicaoAtual - _origemMouse;

                    var mat = transformacaoCanvas.Matrix;
                    mat.Translate(delta.X, delta.Y);
                    transformacaoCanvas.Matrix = mat;
                    _origemMouse = posicaoAtual;
                }
            }
        }

        private void bntResetZoom_Click(object sender, RoutedEventArgs e)
        {
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

        private void DesenharLevantamento(ResultadoLevantamento resultado)
        {
            transformacaoCanvas.Matrix = Matrix.Identity;
            canvasDesenho.Children.Clear();

            if (resultado == null || (!resultado.TodosOsPontos.Any() && (resultado.PoligonalBruta == null || resultado.PoligonalBruta.Count == 0))) return;

            // 1. Extrai os pontos definitivos do Domínio (Limpos)
            var pontos = resultado.TodosOsPontos;

            // 2. Clona a lista estritamente para o cálculo da Câmera (Bounding Box)
            var pontosParaExtensao = new List<PontoCoordenada>(pontos);

            // 3. Injeta o Esboço Geodésico na Câmera (Se houver malha bruta)
            if (resultado.PoligonalBruta != null && resultado.PoligonalBruta.Count > 0)
            {
                pontosParaExtensao.AddRange(resultado.PoligonalBruta);
            }

            // 4. Calcula os limites espaciais extremos (agora imunes ao Ponto Cego)
            double minX = pontosParaExtensao.Min(p => p.X);
            double maxX = pontosParaExtensao.Max(p => p.X);
            double minY = pontosParaExtensao.Min(p => p.Y);
            double maxY = pontosParaExtensao.Max(p => p.Y);

            double larguraReal = maxX - minX;
            double alturaReal = maxY - minY;

            if (larguraReal == 0) larguraReal = 10;
            if (alturaReal == 0) alturaReal = 10;

            double margem = Math.Max(larguraReal, alturaReal) * 0.1;
            minX -= margem; maxX += margem;
            minY -= margem; maxY += margem;

            larguraReal = maxX - minX;
            alturaReal = maxY - minY;

            double telaW = canvasDesenho.ActualWidth;
            double telaH = canvasDesenho.ActualHeight;

            if (telaW == 0) telaW = 800;
            if (telaH == 0) telaH = 500;

            double escalaX = telaW / larguraReal;
            double escalaY = telaH / alturaReal;

            double escala = Math.Min(escalaX, escalaY);

            Point ParaTela(double x, double y)
            {
                double xTela = (x - minX) * escala;
                double yTela = (maxY - y) * escala;
                return new Point(xTela, yTela);
            }

            if (pontos.Skip(50).Any())
            {
                chkMostrarNomes.IsChecked = false;
            }
            else
            {
                chkMostrarNomes.IsChecked = true;
            }

            Visibility visibilidadeTexto = (chkMostrarNomes.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            // Renderização do Esboço Geodésico (Poligonal Bruta Tracejada)
            // Inserir antes da renderização da "poligonal" compensada atual
            if (resultado.PoligonalBruta != null && resultado.PoligonalBruta.Count > 1)
            {
                for (int i = 0; i < resultado.PoligonalBruta.Count - 1; i++)
                {
                    Point p1Bruto = ParaTela(resultado.PoligonalBruta[i].X, resultado.PoligonalBruta[i].Y);
                    Point p2Bruto = ParaTela(resultado.PoligonalBruta[i + 1].X, resultado.PoligonalBruta[i + 1].Y);

                    Line linhaBruta = new Line
                    {
                        X1 = p1Bruto.X,
                        Y1 = p1Bruto.Y,
                        X2 = p2Bruto.X,
                        Y2 = p2Bruto.Y,
                        Stroke = Brushes.Red,           // Contraste para o dado não-compensado
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 2 }, // Linha tracejada (Esboço)
                        ToolTip = "Poligonal Bruta (Sem Ajustamento Linear)"
                    };
                    canvasDesenho.Children.Add(linhaBruta);
                }
            }

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

            // CARREGA QUAIS LAYERS ESTÃO ATIVOS (O(1) HashSet)
            var camadasVisiveis = new HashSet<string>(CamadasDisponiveis.Where(c => c.IsVisivel).Select(c => c.Nome));

            foreach (var p in pontos)
            {
                // O BLOQUEIO DE CAMADA VAI AQUI:
                if (!p.EhPontoPoligonal) // Os pontos da poligonal (verdes/azuis) nunca somem
                {
                    string chave = string.IsNullOrWhiteSpace(p.Descricao) ? "SEM DESCRIÇÃO" : p.Descricao.Trim().ToUpper();
                    if (!camadasVisiveis.Contains(chave))
                        continue; // Pula o cálculo deste ponto, não desenha no Canvas
                }

                Point pos = ParaTela(p.X, p.Y);

                string toolTipText = $"{p.Nome}\n\nCOMPENSADO\nE: {p.X:F3}\nN: {p.Y:F3}\nZ: {p.Z:F3}";
                if (p.XBruto != 0 || p.YBruto != 0) // Assumindo que dados brutos foram populados
                {
                    toolTipText += $"\n\nBRUTO\nE: {p.XBruto:F3}\nN: {p.YBruto:F3}\nZ: {p.ZBruto:F3}";
                }

                Ellipse pontoGeo = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = p.EhPontoPoligonal ? Brushes.Blue : Brushes.Green,
                    ToolTip = toolTipText
                };

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

                Canvas.SetLeft(texto, pos.X + 5);
                Canvas.SetTop(texto, pos.Y - 5);
                canvasDesenho.Children.Add(texto);
            }
        }
    }
}