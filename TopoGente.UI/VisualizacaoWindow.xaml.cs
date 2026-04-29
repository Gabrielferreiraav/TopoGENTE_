using System;
using System.Collections.Generic;
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

namespace TopoGente.UI
{
    public partial class VisualizacaoWindow : MetroWindow
    {
        private readonly IUiEventHub _uiEventHub;
        private Point _origemMouse;
        private bool _estaArrastando;

        public VisualizacaoWindow(IUiEventHub uiEventHub)
        {
            InitializeComponent();

            _uiEventHub = uiEventHub;
            _uiEventHub.ResultadoAtualizado += OnResultadoAtualizado;

            Closing += VisualizacaoWindow_Closing;
        }

        public void AtualizarDesenho(List<PontoCoordenada> pontos)
        {
            canvasDesenho.UpdateLayout();
            DesenharLevantamento(pontos);
        }

        private void OnResultadoAtualizado(object? sender, ResultadoEventArgs e)
        {
            if (!IsVisible)
            {
                Show();
            }

            AtualizarDesenho(e.Resultado.TodosOsPontos);
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

        private void DesenharLevantamento(List<PontoCoordenada> pontos)
        {
            transformacaoCanvas.Matrix = Matrix.Identity;
            canvasDesenho.Children.Clear();

            if (pontos == null || pontos.Count == 0) return;

            double minX = pontos.Min(p => p.X);
            double maxX = pontos.Max(p => p.X);
            double minY = pontos.Min(p => p.Y);
            double maxY = pontos.Max(p => p.Y);

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

            if (pontos.Count > 50)
            {
                chkMostrarNomes.IsChecked = false;
            }
            else
            {
                chkMostrarNomes.IsChecked = true;
            }

            Visibility visibilidadeTexto = (chkMostrarNomes.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

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

            foreach (var p in pontos)
            {
                Point pos = ParaTela(p.X, p.Y);

                Ellipse pontoGeo = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = p.EhPontoPoligonal ? Brushes.Blue : Brushes.Green,
                    ToolTip = $"{p.Nome}\nE: {p.X:F3}\nN: {p.Y:F3}\nZ: {p.Z:F3}"
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