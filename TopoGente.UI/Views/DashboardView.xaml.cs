using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TopoGente.UI.ViewModels;
using TopoGente.UI.CadInteraction;
using TopoGente.UI.Spatial;

namespace TopoGente.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private CadStateMachine? _stateMachine;
        private DashboardViewModel? ViewModel => DataContext as DashboardViewModel;

        public DashboardView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                _stateMachine = new CadStateMachine(ViewModel);
            }
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_stateMachine == null || ViewModel == null) return;

            Point pixelCoords = e.GetPosition((IInputElement)sender);
            Point modelCoords = ProjetarPixelParaGeodesico(pixelCoords);

            _stateMachine.HandleMouseDown(modelCoords.X, modelCoords.Y);
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_stateMachine == null || ViewModel == null) return;

            Point pixelCoords = e.GetPosition((IInputElement)sender);
            Point modelCoords = ProjetarPixelParaGeodesico(pixelCoords);
            KdNode? nearestNode = ObterNoAtraido(modelCoords);

            _stateMachine.HandleMouseMove(modelCoords.X, modelCoords.Y, nearestNode);
        }

        private void OnCanvasRightClick(object sender, MouseButtonEventArgs e)
        {
            if (_stateMachine == null) return;
            _stateMachine.HandleRightClick();
        }

        private void OnCanvasPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_stateMachine == null) return;
            
            CadInteractionKey key = e.Key switch
            {
                Key.Escape => CadInteractionKey.Escape,
                Key.Delete => CadInteractionKey.Delete,
                Key.Enter  => CadInteractionKey.Enter,
                _          => CadInteractionKey.None
            };

            if (key != CadInteractionKey.None)
            {
                _stateMachine.HandleKeyDown(key);
                e.Handled = true;
            }
        }

        private Point ProjetarPixelParaGeodesico(Point pixelCoords)
        {
            if (ViewModel == null) return pixelCoords;
            
            Matrix cameraMatrix = ViewModel.CameraMatrix; 
            if (cameraMatrix.HasInverse)
            {
                cameraMatrix.Invert(); 
                return cameraMatrix.Transform(pixelCoords); 
            }
            return pixelCoords;
        }

        private KdNode? ObterNoAtraido(Point modelCoords)
        {
            if (ViewModel?.SpatialIndex == null) return null;

            Matrix matrix = ViewModel.CameraMatrix;
            
            double det = matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21;
            double escalaZoom = Math.Sqrt(Math.Abs(det));

            if (escalaZoom < 1e-9) return null;

            double tolerânciaGeodesica = 15.0 / escalaZoom; 

            return ViewModel.SpatialIndex.FindNearest(modelCoords.X, modelCoords.Y, tolerânciaGeodesica);
        }
    }
}
