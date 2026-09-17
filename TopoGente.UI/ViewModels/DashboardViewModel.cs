using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

using System.Linq;

using System.Threading.Tasks;

using System.Windows.Input;

using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using CommunityToolkit.Mvvm.Messaging;

using TopoGente.Core.Entities;

using TopoGente.Core.Domain;

using TopoGente.Core.Interfaces;

using TopoGENTE.Domain.ValueObjects;

using TopoGENTE.Domain.Ports;

using TopoGENTE.Domain.Exceptions;

using TopoGente.UI.Messages;

using TopoGente.UI.CadInteraction;

using TopoGente.UI.Services;

using TopoGente.UI.Spatial;

using TopoGente.UI.Collections;



namespace TopoGente.UI.ViewModels
{
    public enum WorkspaceModulo
    {
        Planimetria,
        ModelagemMdt,
        Altimetria,
        PlantaGeral
    }

    public partial class DashboardViewModel : ObservableObject,

        IRecipient<ResultadoMdtMensagem>,

        IRecipient<TopologyChangedMessage>,

        ICadCanvasContext,

        IDisposable

    {

        // === Dependências (Portas Hexagonais) ===

        private readonly Func<ITerrainTriangulator> _triangulatorFactory;
#pragma warning disable IDE0044 // O campo não pode ser readonly devido à invalidação de estado do Módulo 3
        private ITerrainTriangulator _triangulator;
#pragma warning restore IDE0044

        private readonly Func<ITopographicAnalytics> _analyticsFactory;
        private readonly ITopographicAnalytics _analytics;
        private ITopographicAnalytics Analytics => (_triangulator as ITopographicAnalytics) ?? _analytics;

        private readonly ILeituraArquivoFactory _leitorFactory;

        private readonly IOrganizarCaminhamento _organizador;

        private readonly ILevantamentoProcessor _processador;

        private readonly IQaCheckService _qaCheckService;

        private readonly IClassificadorGrafo _classificadorGrafo;

        private readonly IArquivoProjetoService _projetoService;

        private readonly IExportarTxtService _exportarTxtService;

        private readonly IExportadorDxfService _exportadorDxfService;

        private readonly IDialogService _dialogService;

        private readonly IFileService _fileService;

        private readonly IMessageService _messageService;

        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private bool _disposed;



        // === Estado de Domínio ===

        private List<Estacao> _estacoesEmMemoria = [];

        private ResultadoLevantamento? _resultadoAtual;



        // =========================================================================

// 1. ESTADO REATIVO DO MOTOR (Alinhado com as Triggers do XAML)

// =========================================================================

private EstadoMotor _estadoAtualMotor = EstadoMotor.Bruto;

public EstadoMotor EstadoAtualMotor

{

    get => _estadoAtualMotor;

    private set

    {

        // SetProperty dispara o PropertyChanged para atualização síncrona

        if (SetProperty(ref _estadoAtualMotor, value))

        {

            OnPropertyChanged(nameof(TextoStatusBar));

            NotifyCommandStates(); // Invalida o CanExecute de exportações e cálculos

        }

    }

}



// Texto reativo consumido pelo TextBlock da StatusBar no WPF

public string TextoStatusBar => EstadoAtualMotor switch

{

    EstadoMotor.Bruto      => "Bruto – Aguardando Processamento",

    EstadoMotor.Calculando => "Calculando...",

    EstadoMotor.Compensado => " Compensado (NBR 13.133 Atendida)",

    EstadoMotor.ReprovadoNorma => " Erro de Tolerância (NBR Reprovada)",

    _                      => string.Empty

};



// =========================================================================

// GEOMETRIAS DO DRAFT (Camada de Esboço e Vetor de Erro)

// =========================================================================

private Geometry _staticDraftGeometry = Geometry.Empty;

public Geometry StaticDraftGeometry 

{ 

    get => _staticDraftGeometry; 

    set => SetProperty(ref _staticDraftGeometry, value); 

}



private Geometry _staticPoligonalCompensadaGeometry = Geometry.Empty;

public Geometry StaticPoligonalCompensadaGeometry

{

    get => _staticPoligonalCompensadaGeometry;

    set => SetProperty(ref _staticPoligonalCompensadaGeometry, value);

}



private Geometry _staticErrorVectorGeometry = Geometry.Empty;

public Geometry StaticErrorVectorGeometry 

{ 

    get => _staticErrorVectorGeometry; 

    set => SetProperty(ref _staticErrorVectorGeometry, value); 

}



// =========================================================================

// 3. OPCOES DE VISIBILIDADE DAS CAMADAS CAD 

// =========================================================================
// 3. WORKSPACES CONTEXTUAIS E OPCOES DE VISIBILIDADE DAS CAMADAS CAD 
// =========================================================================

private WorkspaceModulo _moduloAtivo = WorkspaceModulo.Planimetria;
public WorkspaceModulo ModuloAtivo
{
    get => _moduloAtivo;
    set
    {
        if (SetProperty(ref _moduloAtivo, value))
        {
            AplicarPresetModulo(value);
            OnPropertyChanged(nameof(EhModuloPlanimetria));
            OnPropertyChanged(nameof(EhModuloMdt));
            OnPropertyChanged(nameof(EhModuloAltimetria));
            OnPropertyChanged(nameof(EhModuloPlantaGeral));
        }
    }
}

public bool EhModuloPlanimetria => ModuloAtivo == WorkspaceModulo.Planimetria;
public bool EhModuloMdt => ModuloAtivo == WorkspaceModulo.ModelagemMdt;
public bool EhModuloAltimetria => ModuloAtivo == WorkspaceModulo.Altimetria;
public bool EhModuloPlantaGeral => ModuloAtivo == WorkspaceModulo.PlantaGeral;

public IRelayCommand<WorkspaceModulo> MudarModuloCommand { get; }

public void MudarModulo(WorkspaceModulo modulo)
{
    ModuloAtivo = modulo;
}

public void AplicarPresetModulo(WorkspaceModulo modulo)
{
    switch (modulo)
    {
        case WorkspaceModulo.Planimetria:
            MostrarPoligonalCompensada = true;
            MostrarEsbocoBruto = true;
            MostrarPontos = true;
            MostrarTin = false;
            MostrarBreaklines = false;
            MostrarCurvasMestras = false;
            MostrarCurvasSecundarias = false;
            break;

        case WorkspaceModulo.ModelagemMdt:
            MostrarTin = true;
            MostrarBreaklines = true;
            MostrarPontos = true;
            MostrarPoligonalCompensada = false;
            MostrarEsbocoBruto = false;
            MostrarCurvasMestras = false;
            MostrarCurvasSecundarias = false;
            break;

        case WorkspaceModulo.Altimetria:
            MostrarCurvasMestras = true;
            MostrarCurvasSecundarias = true;
            MostrarPoligonalCompensada = true;
            MostrarPontos = false;
            MostrarTin = false;
            MostrarEsbocoBruto = false;
            MostrarBreaklines = false;
            break;

        case WorkspaceModulo.PlantaGeral:
            MostrarPoligonalCompensada = true;
            MostrarPontos = true;
            MostrarCurvasMestras = true;
            MostrarCurvasSecundarias = true;
            MostrarBreaklines = true;
            MostrarEsbocoBruto = false;
            MostrarTin = false;
            break;
    }
}

// === Propriedades Informativas de Superfície (MDT / Altimetria / Planta Geral) ===

private int _totalTriangulosMdt;
public int TotalTriangulosMdt
{
    get => _totalTriangulosMdt;
    set => SetProperty(ref _totalTriangulosMdt, value);
}

private int _totalVerticesMdt;
public int TotalVerticesMdt
{
    get => _totalVerticesMdt;
    set => SetProperty(ref _totalVerticesMdt, value);
}

private double _cotaMinimaTerreno;
public double CotaMinimaTerreno
{
    get => _cotaMinimaTerreno;
    set => SetProperty(ref _cotaMinimaTerreno, value);
}

private double _cotaMaximaTerreno;
public double CotaMaximaTerreno
{
    get => _cotaMaximaTerreno;
    set => SetProperty(ref _cotaMaximaTerreno, value);
}

private double _desnivelTerreno;
public double DesnivelTerreno
{
    get => _desnivelTerreno;
    set => SetProperty(ref _desnivelTerreno, value);
}

public int TotalPontos => _resultadoAtual?.TodosOsPontos.Count() ?? 0;

// === Camadas de Visibilidade CAD ===

private bool _mostrarEsbocoBruto = true;
public bool MostrarEsbocoBruto { get => _mostrarEsbocoBruto; set => SetProperty(ref _mostrarEsbocoBruto, value); }

private bool _mostrarPoligonalCompensada = true;
public bool MostrarPoligonalCompensada { get => _mostrarPoligonalCompensada; set => SetProperty(ref _mostrarPoligonalCompensada, value); }

private bool _mostrarPontos = true;
public bool MostrarPontos { get => _mostrarPontos; set => SetProperty(ref _mostrarPontos, value); }

private bool _mostrarTin;
public bool MostrarTin { get => _mostrarTin; set => SetProperty(ref _mostrarTin, value); }

private bool _mostrarBreaklines;
public bool MostrarBreaklines { get => _mostrarBreaklines; set => SetProperty(ref _mostrarBreaklines, value); }

private bool _mostrarCurvasMestras;
public bool MostrarCurvasMestras { get => _mostrarCurvasMestras; set => SetProperty(ref _mostrarCurvasMestras, value); }

private bool _mostrarCurvasSecundarias;
public bool MostrarCurvasSecundarias { get => _mostrarCurvasSecundarias; set => SetProperty(ref _mostrarCurvasSecundarias, value); }



// =========================================================================

//  COLECAO TABULAR DE DIAGN"STICO (QA Checks NBR 13.133)

// =========================================================================

public ObservableCollection<string> AvisosDiagnostico { get; } = [];



        private string _nomeArquivoAtual = "Sem Título";

        public string NomeArquivoAtual { get => _nomeArquivoAtual; private set => SetProperty(ref _nomeArquivoAtual, value); }



        private bool _projetoModificado;

        public bool ProjetoModificado

        {

            get => _projetoModificado;

            private set { if (SetProperty(ref _projetoModificado, value)) OnPropertyChanged(nameof(TituloJanela)); }

        }

        public string TituloJanela =>
            $"TopoGENTE - Plataforma de Processamento Gráfico{(NomeArquivoAtual == "Sem Título" || string.IsNullOrWhiteSpace(NomeArquivoAtual) ? "" : $" - {NomeArquivoAtual}")}{(ProjetoModificado ? "*" : "")}";


        private RelatorioQA? _relatorioQaAtual;

        public RelatorioQA? RelatorioQaAtual { get => _relatorioQaAtual; private set => SetProperty(ref _relatorioQaAtual, value); }



        // === Geometrias de Renderização ===

        private Geometry _staticTinGeometry = Geometry.Empty;

        public Geometry StaticTinGeometry { get => _staticTinGeometry; set => SetProperty(ref _staticTinGeometry, value); }

        private Geometry _staticMajorContoursGeometry = Geometry.Empty;

        public Geometry StaticMajorContoursGeometry { get => _staticMajorContoursGeometry; set => SetProperty(ref _staticMajorContoursGeometry, value); }

        private Geometry _staticMinorContoursGeometry = Geometry.Empty;

        public Geometry StaticMinorContoursGeometry { get => _staticMinorContoursGeometry; set => SetProperty(ref _staticMinorContoursGeometry, value); }

        private Geometry _staticBreaklinesGeometry = Geometry.Empty;

        public Geometry StaticBreaklinesGeometry { get => _staticBreaklinesGeometry; set => SetProperty(ref _staticBreaklinesGeometry, value); }

        private Geometry _staticPointsGeometry = Geometry.Empty;

        public Geometry StaticPointsGeometry { get => _staticPointsGeometry; set => SetProperty(ref _staticPointsGeometry, value); }



        // === Camadas (Etapa 5) ===

        private bool _exibirTin = true;

        public bool ExibirTin { get => _exibirTin; set => SetProperty(ref _exibirTin, value); }

        private bool _exibirCurvasMenores = true;

        public bool ExibirCurvasMenores { get => _exibirCurvasMenores; set => SetProperty(ref _exibirCurvasMenores, value); }

        private bool _exibirCurvasMestras = true;

        public bool ExibirCurvasMestras { get => _exibirCurvasMestras; set => SetProperty(ref _exibirCurvasMestras, value); }

        private bool _exibirBreaklines = true;

        public bool ExibirBreaklines { get => _exibirBreaklines; set => SetProperty(ref _exibirBreaklines, value); }

        private bool _exibirPontos = true;

        public bool ExibirPontos { get => _exibirPontos; set => SetProperty(ref _exibirPontos, value); }



        // === Câmera e Viewport CAD ===

        private Matrix _cameraMatrix = Matrix.Identity;

        public Matrix CameraMatrix { get => _cameraMatrix; set => SetProperty(ref _cameraMatrix, value); }

        private double _viewportWidth = 800;
        private double _viewportHeight = 600;

        // Origem Flutuante (Floating Origin Pattern para coordenadas geodésicas/UTM de grande magnitude)
        private double _originX = 0;
        private double _originY = 0;
        public double OriginX => _originX;
        public double OriginY => _originY;

        public void AtualizarDimensoesViewport(double width, double height)
        {
            if (width > 10 && height > 10)
            {
                _viewportWidth = width;
                _viewportHeight = height;
            }
        }

        public void ZoomExtents(IEnumerable<PontoCoordenada>? pontos = null)
        {
            var pts = pontos?.ToList() 
                ?? _resultadoAtual?.TodosOsPontos?.ToList() 
                ?? _resultadoAtual?.Poligonal?.ToList()
                ?? _resultadoAtual?.PoligonalBruta?.ToList();

            if (pts == null || pts.Count == 0) return;

            // Se a origem do levantamento ainda não foi calibrada, define-a a partir do ponto mínimo
            if (_originX == 0 && _originY == 0 && (Math.Abs(pts[0].X) > 1000 || Math.Abs(pts[0].Y) > 1000))
            {
                _originX = pts.Min(p => p.X);
                _originY = pts.Min(p => p.Y);
            }

            double minX = pts.Min(p => p.X) - _originX;
            double maxX = pts.Max(p => p.X) - _originX;
            double minY = pts.Min(p => p.Y) - _originY;
            double maxY = pts.Max(p => p.Y) - _originY;

            double deltaX = maxX - minX;
            double deltaY = maxY - minY;

            if (deltaX < 1.0) deltaX = 1.0;
            if (deltaY < 1.0) deltaY = 1.0;

            double w = _viewportWidth > 50 ? _viewportWidth : 800;
            double h = _viewportHeight > 50 ? _viewportHeight : 600;

            double scaleX = w / deltaX;
            double scaleY = h / deltaY;
            double scale = Math.Min(scaleX, scaleY) * 0.90; // 10% margem de segurança

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;

            double offsetX = (w / 2.0) - (centerX * scale);
            double offsetY = (h / 2.0) + (centerY * scale);

            CameraMatrix = new Matrix(scale, 0, 0, -scale, offsetX, offsetY);
        }

        public void AplicarPan(double dx, double dy)
        {
            var m = CameraMatrix;
            CameraMatrix = new Matrix(m.M11, m.M12, m.M21, m.M22, m.OffsetX + dx, m.OffsetY + dy);
        }

        public void AplicarZoom(double factor, double mousePixelX, double mousePixelY)
        {
            var m = CameraMatrix;
            double det = m.M11 * m.M22 - m.M12 * m.M21;
            double currentScale = Math.Sqrt(Math.Abs(det));
            double newScale = currentScale * factor;

            if (newScale < 1e-5 || newScale > 1e6) return;

            double newOffsetX = mousePixelX * (1.0 - factor) + m.OffsetX * factor;
            double newOffsetY = mousePixelY * (1.0 - factor) + m.OffsetY * factor;

            CameraMatrix = new Matrix(m.M11 * factor, m.M12 * factor, m.M21 * factor, m.M22 * factor, newOffsetX, newOffsetY);
        }

        public ICommand ZoomExtentsCommand { get; }

        public System.Windows.Point GetModelCoordinates(System.Windows.Point screenPoint)
        {
            Matrix inv = CameraMatrix;
            if (!inv.HasInverse) return screenPoint;
            inv.Invert();
            var local = inv.Transform(screenPoint);
            return new System.Windows.Point(local.X + _originX, local.Y + _originY);
        }

        public void SetRubberBand(double? startX, double? startY, double? endX, double? endY)
        {
            RubberBandStartX = startX; RubberBandStartY = startY;
            RubberBandEndX = endX; RubberBandEndY = endY;

            if (startX.HasValue && startY.HasValue && endX.HasValue && endY.HasValue)
            {
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(new System.Windows.Point(startX.Value - _originX, startY.Value - _originY), false, false);
                    ctx.LineTo(new System.Windows.Point(endX.Value - _originX, endY.Value - _originY), true, false);
                }
                geom.Freeze();
                DynamicRubberBandGeometry = geom;
            }
            else DynamicRubberBandGeometry = Geometry.Empty;

        }

        public double? RubberBandStartX { get; private set; }

        public double? RubberBandStartY { get; private set; }

        public double? RubberBandEndX { get; private set; }

        public double? RubberBandEndY { get; private set; }

        private Geometry _dynamicRubberBandGeometry = Geometry.Empty;

        public Geometry DynamicRubberBandGeometry { get => _dynamicRubberBandGeometry; set => SetProperty(ref _dynamicRubberBandGeometry, value); }



        // === fÍndice Espacial ===

        public KdTree2D? SpatialIndex { get; private set; }

        public BvhTree2D? EdgeSpatialIndex { get; private set; }

        private bool _isTopologyRebuilding;

        public bool IsTopologyRebuilding { get => _isTopologyRebuilding; private set => SetProperty(ref _isTopologyRebuilding, value); }



        // === Coleffues ===

        public BulkObservableCollection<LeituraViewModel> Visadas { get; } = [];
        public ICollectionView VisadasView { get; }

        public ObservableCollection<EstacaoCaminhamentoItemViewModel> EstacoesCaminhamento { get; } = [];
        private Dictionary<string, PontoCoordenada> _pontosConhecidosGlobais = new(StringComparer.OrdinalIgnoreCase);

                public BulkObservableCollection<PontoCoordenada> ResultadosCaderneta { get; } = [];

        public ObservableCollection<SequenciaPoligonalViewModel> Poligonais { get; } = [];

        private SequenciaPoligonalViewModel? _poligonalSelecionada;

        public SequenciaPoligonalViewModel? PoligonalSelecionada

        {

            get => _poligonalSelecionada;

            set { if (SetProperty(ref _poligonalSelecionada, value)) NotifyCommandStates(); }

        }

        public ObservableCollection<string> EstacoesDisponiveis { get; } = [];

        private string? _estacaoDisponivelSelecionada;

        public string? EstacaoDisponivelSelecionada { get => _estacaoDisponivelSelecionada; set => SetProperty(ref _estacaoDisponivelSelecionada, value); }

        private string? _estacaoSequenciaSelecionada;

        public string? EstacaoSequenciaSelecionada { get => _estacaoSequenciaSelecionada; set => SetProperty(ref _estacaoSequenciaSelecionada, value); }

        private int _formatoArquivoIndex = 0;

        public int FormatoArquivoIndex { get => _formatoArquivoIndex; set => SetProperty(ref _formatoArquivoIndex, value); }



        // === Parâmetros TIN ===

        private double _stepInterval = 1.0;
        public double StepInterval
        {
            get => _stepInterval;
            set
            {
                if (value > 0.05 && SetProperty(ref _stepInterval, value))
                {
                    if (_resultadoAtual != null && _resultadoAtual.TodosOsPontos.Any() && !IsTopologyRebuilding)
                    {
                        IsTopologyRebuilding = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                ProcessarGeometriasAtuais();
                            }
                            finally
                            {
                                RunOnUiThread(() => IsTopologyRebuilding = false);
                            }
                        });
                    }
                }
            }
        }

        public ObservableCollection<double> EquidistanciasDisponiveis { get; } = [0.25, 0.5, 1.0, 2.0, 5.0];

        private readonly double _anchorElevation = 100.0;
        public double AnchorElevation => _anchorElevation;

        private readonly int _majorContourFrequency = 5;
        public int MajorContourFrequency => _majorContourFrequency;



        // === Undo/Redo ===

        private readonly Stack<Commands.IUndoableCommand> _undoStack = new();

        private readonly Stack<Commands.IUndoableCommand> _redoStack = new();

        public ICommand UndoCommand { get; }

        public ICommand RedoCommand { get; }

        public void ExecuteCommand(Commands.IUndoableCommand command)

        {

            command.Execute();

            _undoStack.Push(command);

            _redoStack.Clear();

            NotifyUndoRedo();

        }

        public void Undo()

        {

            if (_undoStack.Count > 0) { var c = _undoStack.Pop(); c.Undo(); _redoStack.Push(c); NotifyUndoRedo(); }

        }

        public void Redo()

        {

            if (_redoStack.Count > 0) { var c = _redoStack.Pop(); c.Execute(); _undoStack.Push(c); NotifyUndoRedo(); }

        }

        private void NotifyUndoRedo()

        {

            (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();

        }



        // === Commands ===

        public ICommand ImportarCadernetaCommand { get; }

        public ICommand AbrirProjetoCommand { get; }

        public ICommand SalvarProjetoCommand { get; }

        public ICommand AdicionarPoligonalCommand { get; }

        public ICommand RemoverPoligonalCommand { get; }

        public ICommand AdicionarSequenciaCommand { get; }

        public ICommand RemoverSequenciaCommand { get; }

        public ICommand SubirSequenciaCommand { get; }

        public ICommand DescerSequenciaCommand { get; }

        public ICommand ProcessarCommand { get; }

        public ICommand ExportarTxtCommand { get; }

        public ICommand ExportarDxfCommand { get; }
        public ICommand RecalcularMalhaCommand { get; }
        public ICommand ExportarRelatorioQaCommand { get; }

        public ICommand ConsolidarBreaklineCommand { get; }

        public ICommand DeletarElementoSelecionadoCommand { get; }

        public ICommand RowEditEndingCommand { get; }



        // === Construtor ===

        public DashboardViewModel(

            Func<ITerrainTriangulator> triangulatorFactory,

            Func<ITopographicAnalytics> analyticsFactory,

            ILeituraArquivoFactory leitorFactory,

            IOrganizarCaminhamento organizador,

            ILevantamentoProcessor processador,

            IQaCheckService qaCheckService,

            IClassificadorGrafo classificadorGrafo,

            IArquivoProjetoService projetoService,

            IExportarTxtService exportarTxtService,

            IExportadorDxfService exportadorDxfService,

            IDialogService dialogService,

            IFileService fileService,

            IMessageService messageService)

        {

            _triangulatorFactory = triangulatorFactory; _analyticsFactory = analyticsFactory; _triangulator = _triangulatorFactory(); _analytics = (_triangulator as ITopographicAnalytics) ?? _analyticsFactory();

            _leitorFactory = leitorFactory; _organizador = organizador;

            _processador = processador; _qaCheckService = qaCheckService;

            _classificadorGrafo = classificadorGrafo; _projetoService = projetoService;

            _exportarTxtService = exportarTxtService; _exportadorDxfService = exportadorDxfService;

            _dialogService = dialogService; _fileService = fileService; _messageService = messageService;

            _dispatcher = System.Windows.Threading.Dispatcher.FromThread(System.Threading.Thread.CurrentThread)
                       ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

            WeakReferenceMessenger.Default.Register<ResultadoMdtMensagem>(this);

            WeakReferenceMessenger.Default.Register<TopologyChangedMessage>(this);

            RowEditEndingCommand              = new AsyncRelayCommand<object>(OnRowEditEndingAsync);

            ConsolidarBreaklineCommand        = new AsyncRelayCommand<Breakline>(ConsolidarBreaklineAsync);

            DeletarElementoSelecionadoCommand = new AsyncRelayCommand(DeletarElementoSelecionadoAsync);

            UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);

            RedoCommand = new RelayCommand(_ => Redo(), _ => _redoStack.Count > 0);

            ImportarCadernetaCommand = new AsyncRelayCommand(ImportarCadernetaAsync);

            AbrirProjetoCommand      = new AsyncRelayCommand(AbrirProjetoAsync);

            SalvarProjetoCommand     = new AsyncRelayCommand(SalvarProjetoAsync);

            AdicionarPoligonalCommand = new RelayCommand(_ => OnAdicionarPoligonal());

            RemoverPoligonalCommand   = new RelayCommand(_ => OnRemoverPoligonal(),   _ => PoligonalSelecionada?.EhPrincipal == false);

            AdicionarSequenciaCommand = new RelayCommand(_ => OnAdicionarSequencia(), _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoDisponivelSelecionada));

            RemoverSequenciaCommand   = new RelayCommand(_ => OnRemoverSequencia(),   _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));

            SubirSequenciaCommand     = new RelayCommand(_ => OnSubirSequencia(),     _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));

            DescerSequenciaCommand    = new RelayCommand(_ => OnDescerSequencia(),    _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));

            ProcessarCommand   = new AsyncRelayCommand(ProcessarAsync, PodeProcessar);

            ExportarTxtCommand = new RelayCommand(_ => OnExportarTxt(), _ => _resultadoAtual?.TodosOsPontos.Any() == true && EstadoAtualMotor == EstadoMotor.Compensado);

            ExportarDxfCommand = new RelayCommand(_ => OnExportarDxf(), _ => _resultadoAtual?.TodosOsPontos.Any() == true && EstadoAtualMotor == EstadoMotor.Compensado);

            MudarModuloCommand = new RelayCommand<WorkspaceModulo>(MudarModulo);

            RecalcularMalhaCommand = new AsyncRelayCommand(async () =>
            {
                if (_resultadoAtual != null && _resultadoAtual.TodosOsPontos.Any())
                {
                    await RenderizarTinDeResultadoAsync(_resultadoAtual);
                }
            }, () => _resultadoAtual?.TodosOsPontos.Any() == true && !IsTopologyRebuilding);

            ExportarRelatorioQaCommand = new RelayCommand(_ => OnExportarRelatorioQa(), _ => RelatorioQaAtual != null);

            ZoomExtentsCommand = new RelayCommand(_ => ZoomExtents());

            AplicarPresetModulo(WorkspaceModulo.Planimetria);

            var principal = new SequenciaPoligonalViewModel { Nome = "Poligonal Principal", EhPrincipal = true };

            principal.PropertyChanged += (_, _) => ((AsyncRelayCommand)ProcessarCommand).NotifyCanExecuteChanged();

            Poligonais.Add(principal);

            PoligonalSelecionada = principal;

            VisadasView = CollectionViewSource.GetDefaultView(Visadas);
            if (VisadasView.GroupDescriptions != null)
            {
                VisadasView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LeituraViewModel.IdentificadorSessao)));
            }
        }

        private void RunOnUiThread(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            if (_dispatcher == null || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action, priority);
            }
        }

        private async Task RunOnUiThreadAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
        {
            if (_dispatcher == null || _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                action();
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await _dispatcher.InvokeAsync(action, priority);
            }
        }



        // === ETAPA 1 - Importação ===

        private async Task ImportarCadernetaAsync()

        {

            var fileName = _dialogService.SelecionarArquivoAbertura(

                "Arquivos Topográficos (*.txt;*.csv;*.fbk;*.xml)|*.txt;*.csv;*.fbk;*.xml|Todos (*.*)|*.*",

                "Selecione a Caderneta de Campo");

            if (string.IsNullOrEmpty(fileName)) return;

            try

            {

                var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                if (extension == ".fbk") FormatoArquivoIndex = 1;
                else if (extension == ".xml" || extension == ".landxml") FormatoArquivoIndex = 2;
                else FormatoArquivoIndex = 0;

                var formato = FormatoArquivoIndex switch { 1 => FormatoArquivoEntrada.Fbk, 2 => FormatoArquivoEntrada.LandXml, _ => FormatoArquivoEntrada.CsvPadrao };

                var linhas  = await Task.Run(() => _fileService.LerLinhas(fileName));

                var result  = await Task.Run(() => _leitorFactory.ProcessarArquivoComResultado(formato, linhas));

                _estacoesEmMemoria = _organizador.UnificarEstacoes(result.Estacoes);

                InvalidarResultado();

                AtualizarListaEstacoes();

                SugerirSequenciaPoligonalPorVante();

                ResultadosCaderneta.Clear();

                Visadas.Clear();



                var leituras = new List<LeituraViewModel>();

                foreach (var est in _estacoesEmMemoria)

                {

                    foreach (var l in est.Leituras)

                    {

                        leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento, NumeroSessao = est.NumeroSessao });

                    }

                }

                Visadas.AddRange(leituras);



                var pontos = _estacoesEmMemoria

                    .SelectMany(e => e.Leituras

                        .Select(l => new PontoCoordenada { Nome = l.PontoVisado ?? string.Empty }))

                    .ToList();

                ResultadosCaderneta.AddRange(pontos);

                if (_estacoesEmMemoria.Count > 0 && Poligonais.Count > 0)

                {

                    var pe = _estacoesEmMemoria[0];

                    if (pe.CoordenadaConhecida != null)

                    {

                        Poligonais[0].PartidaX = pe.CoordenadaConhecida.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

                        Poligonais[0].PartidaY = pe.CoordenadaConhecida.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

                        Poligonais[0].PartidaZ = pe.CoordenadaConhecida.Z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

                    }

                    _pontosConhecidosGlobais = new Dictionary<string, PontoCoordenada>(result.PontosConhecidosGlobais, StringComparer.OrdinalIgnoreCase);

                    // Auto-preenchimento da Ré e Azimute a partir da leitura de Ré (BS)
                    var leituraRe = pe.Leituras
                        .FirstOrDefault(l => string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase));
                    if (leituraRe != null)
                    {
                        Poligonais[0].NomeRe = leituraRe.PontoVisado ?? string.Empty;

                        if (pe.CoordenadaConhecida != null &&
                            _pontosConhecidosGlobais.TryGetValue(leituraRe.PontoVisado ?? "", out var pontoRe) &&
                            pontoRe != null)
                        {
                            Poligonais[0].UsarCoordenadaRe = true;
                            Poligonais[0].ReX = pontoRe.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                            Poligonais[0].ReY = pontoRe.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                            Poligonais[0].ReZ = pontoRe.Z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

                            var az = TopoGente.Core.Utilities.GeometriaTopograficaHelper.CalcularAzimutePorCoordenadas(
                                pe.CoordenadaConhecida.X, pe.CoordenadaConhecida.Y, pontoRe.X, pontoRe.Y);
                            Poligonais[0].Azimute = az.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (leituraRe.AnguloHorizontal != 0)
                        {
                            Poligonais[0].UsarCoordenadaRe = false;
                            Poligonais[0].Azimute = leituraRe.AnguloHorizontal.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }

                    // Geração preliminar de esboço bruto logo após importação
                    try
                    {
                        var seqs = Poligonais.Select(p => p.ToEntity()).ToList();
                        var esboco = _processador.GerarEsbocoBruto(seqs, _estacoesEmMemoria);
                        var todosPontosEsboco = esboco.TodosOsPontos.ToList();
                        if (todosPontosEsboco.Count > 0)
                        {
                            _originX = todosPontosEsboco.Min(p => p.X);
                            _originY = todosPontosEsboco.Min(p => p.Y);
                        }

                        if (esboco.PoligonalBruta?.Count >= 2)
                        {
                            var draft = new StreamGeometry();
                            using (var ctx = draft.Open())
                            {
                                var pts = esboco.PoligonalBruta;
                                bool coincidentes = pts.Count >= 3 &&
                                    Math.Abs(pts[0].X - pts[^1].X) < 1e-3 &&
                                    Math.Abs(pts[0].Y - pts[^1].Y) < 1e-3;
                                bool isClosed = pts.Count >= 3 && (coincidentes || esboco.PoligonalFechada || esboco.TipoCenario == TipoCenarioPoligonal.Fechada);
                                int count = (coincidentes && isClosed) ? pts.Count - 1 : pts.Count;
                                ctx.BeginFigure(new System.Windows.Point(pts[0].X - _originX, pts[0].Y - _originY), false, isClosed);
                                for (int i = 1; i < count; i++)
                                    ctx.LineTo(new System.Windows.Point(pts[i].X - _originX, pts[i].Y - _originY), true, false);
                            }
                            draft.Freeze();
                            StaticDraftGeometry = draft;

                            if (todosPontosEsboco.Any())
                            {
                                var ptsGeom = new StreamGeometry();
                                using (var ctx = ptsGeom.Open())
                                {
                                    foreach (var p in todosPontosEsboco)
                                    {
                                        double px = p.X - _originX;
                                        double py = p.Y - _originY;
                                        ctx.BeginFigure(new System.Windows.Point(px - 0.75, py), false, false);
                                        ctx.LineTo(new System.Windows.Point(px + 0.75, py), true, false);
                                        ctx.BeginFigure(new System.Windows.Point(px, py - 0.75), false, false);
                                        ctx.LineTo(new System.Windows.Point(px, py + 0.75), true, false);
                                    }
                                }
                                ptsGeom.Freeze();
                                StaticPointsGeometry = ptsGeom;
                                SpatialIndex = new KdTree2D([.. todosPontosEsboco.Select((p, i) => new KdNode(p.X, p.Y, i))]);
                            }

                            ZoomExtents(todosPontosEsboco);
                        }
                    }
                    catch { }

                }

                NomeArquivoAtual = System.IO.Path.GetFileName(fileName);

                ProjetoModificado = true;

            }

            catch (Exception ex) { _messageService.MostrarErro($"Erro ao ler arquivo:\n\n{ex.Message}", "Erro de Leitura"); }

        }



        private void AtualizarListaEstacoes()

        {

            EstacoesDisponiveis.Clear();

            foreach (var n in _estacoesEmMemoria.Select(e => e.Nome).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())

                EstacoesDisponiveis.Add(n!);

        }



        private void SugerirSequenciaPoligonalPorVante()

        {

            if (_estacoesEmMemoria.Count == 0 || Poligonais.Count == 0) return;

            var nomes     = _estacoesEmMemoria.Select(e => e.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seq       = new List<string>();

            var visitadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? cur   = _estacoesEmMemoria[0].Nome;

            while (!string.IsNullOrWhiteSpace(cur) && visitadas.Add(cur))

            {

                seq.Add(cur);

                var est    = _estacoesEmMemoria.FirstOrDefault(e => string.Equals(e.Nome, cur, StringComparison.OrdinalIgnoreCase));

                var vante  = est?.Leituras.FirstOrDefault(l => string.Equals((l.Purpose ?? "").Trim(), "vante", StringComparison.OrdinalIgnoreCase));

                if (vante == null || string.IsNullOrWhiteSpace(vante.PontoVisado)) break;

                var prox = vante.PontoVisado.Trim();

                if (visitadas.Contains(prox) || !nomes.Contains(prox)) { seq.Add(prox); break; }

                cur = prox;

            }

            if (seq.Count > 1) { Poligonais[0].Estacoes.Clear(); foreach (var n in seq) Poligonais[0].Estacoes.Add(n); }

        }



        // === ETAPA 2 - Poligonais ===

        private void OnAdicionarPoligonal()

        {

            var sec = new SequenciaPoligonalViewModel { Nome = $"Ramal Secundario {Poligonais.Count}" };

            sec.PropertyChanged += (_, _) => ((AsyncRelayCommand)ProcessarCommand).NotifyCanExecuteChanged();

            Poligonais.Add(sec); PoligonalSelecionada = sec;

        }

        private void OnRemoverPoligonal()

        {

            if (PoligonalSelecionada?.EhPrincipal == false) { Poligonais.Remove(PoligonalSelecionada); PoligonalSelecionada = Poligonais.FirstOrDefault(); InvalidarResultado(); }

        }

        private void OnAdicionarSequencia()

        {

            if (PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoDisponivelSelecionada))

            { PoligonalSelecionada.Estacoes.Add(EstacaoDisponivelSelecionada!); InvalidarResultado(); }

        }

        private void OnRemoverSequencia()

        {

            if (PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada))

            { PoligonalSelecionada.Estacoes.Remove(EstacaoSequenciaSelecionada!); InvalidarResultado(); }

        }

        private void OnSubirSequencia()

        {

            if (PoligonalSelecionada == null || string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;

            var i = PoligonalSelecionada.Estacoes.IndexOf(EstacaoSequenciaSelecionada!); if (i > 0) PoligonalSelecionada.Estacoes.Move(i, i - 1);

        }

        private void OnDescerSequencia()

        {

            if (PoligonalSelecionada == null || string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;

            var i = PoligonalSelecionada.Estacoes.IndexOf(EstacaoSequenciaSelecionada!);

            if (i >= 0 && i < PoligonalSelecionada.Estacoes.Count - 1) PoligonalSelecionada.Estacoes.Move(i, i + 1);

        }



        // === ETAPA 3 - Processamento e Tolerâncias ===

private bool PodeProcessar() => 

    _estacoesEmMemoria.Count > 0 && 

    Poligonais.Count > 0 && 

    Poligonais.All(p => p.Estacoes.Count > 0) && 

    EstadoAtualMotor != EstadoMotor.Calculando;



private async Task ProcessarAsync()

{

    if (!PodeProcessar()) return;



    // Transição Reativa I: Bloqueia interface e acende StatusBar em Amarelo/Laranja

    EstadoAtualMotor = EstadoMotor.Calculando;

    AvisosDiagnostico.Clear(); // Apaga o laudo anterior preventivamente



    try

    {

        // 1. Extração Passiva das Entidades Logicas do Core 

        var sequencias = Poligonais.Select(p => p.ToEntity()).ToList();

        var ptosConhecidos = new Dictionary<string, PontoCoordenada>(_pontosConhecidosGlobais, StringComparer.OrdinalIgnoreCase);
        foreach (var e in _estacoesEmMemoria.Where(e => e.CoordenadaConhecida != null))
        {
            ptosConhecidos[e.Nome] = e.CoordenadaConhecida!;
        }



        var principal = sequencias.FirstOrDefault(p => p.EhPrincipal);

        

        // 2. Processamento Geodésico em Thread Pool Secundária (Desafoga a Thread STA)

        var resultado = await Task.Run(() =>

        {

            if (principal != null)

            {

                _classificadorGrafo.ClassificarArestasGrafo(_estacoesEmMemoria, principal.Metadados);

            }

            return _processador.Processar(sequencias, _estacoesEmMemoria, ptosConhecidos);

        });



        // 3. Geração do Relat3rio de Auditoria da NBR 13.133 (QA checks)

        var relatorio = _qaCheckService.GerarRelatorioQaChecks(_estacoesEmMemoria, resultado, ptosConhecidos);



        // 4. Armazenamento do Estado de Cálculo e Atualização das Propriedades Reativas

        _resultadoAtual = resultado;
        OnPropertyChanged(nameof(TotalPontos));

        relatorio.ErroAngular = resultado.ErroAngular;
        relatorio.ErroLinear = resultado.ErroLinear;
        relatorio.ErroAltimetrico = resultado.ErroFechamentoZ;

        RelatorioQaAtual = relatorio;



        // 5. Preenche as Coleções Tabulares de Apresentação O(N) de forma rápida

        EstacoesCaminhamento.Clear();
        var seqEstacoes = principal?.Metadados?.SequenciaEstacoesSelecionadas ?? [];
        if (seqEstacoes.Count > 0)
        {
            var usadasPorNome = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < seqEstacoes.Count; i++)
            {
                string nomeOrigem = seqEstacoes[i].Trim();
                usadasPorNome.TryGetValue(nomeOrigem, out int usadas);

                var estacaoOrigem = _estacoesEmMemoria
                    .Where(e => string.Equals(e.Nome, nomeOrigem, StringComparison.OrdinalIgnoreCase))
                    .Skip(usadas)
                    .FirstOrDefault();
                usadasPorNome[nomeOrigem] = usadas + 1;

                string nomeRe;
                string nomeVante;
                double hzVante = 0;
                double distVante = 0;

                if (i == 0)
                {
                    nomeRe = principal?.Metadados?.NomeRe ?? "";
                    if (string.IsNullOrEmpty(nomeRe))
                    {
                        var lRe = estacaoOrigem?.Leituras.FirstOrDefault(l => string.Equals(l.Purpose, "re", StringComparison.OrdinalIgnoreCase));
                        nomeRe = lRe?.PontoVisado ?? "Ré";
                    }
                    nomeVante = (seqEstacoes.Count > 1) ? seqEstacoes[1] : "";
                }
                else if (i < seqEstacoes.Count - 1)
                {
                    nomeRe = seqEstacoes[i - 1];
                    nomeVante = seqEstacoes[i + 1];
                }
                else
                {
                    nomeRe = (i > 0) ? seqEstacoes[i - 1] : "Ré";
                    var lFecho = estacaoOrigem?.Leituras.FirstOrDefault(l => 
                        !string.Equals(l.PontoVisado, nomeRe, StringComparison.OrdinalIgnoreCase) && 
                        (string.Equals(l.Purpose, "vante", StringComparison.OrdinalIgnoreCase) || string.Equals(l.PontoVisado, principal?.Metadados?.NomeRe, StringComparison.OrdinalIgnoreCase)));
                    nomeVante = lFecho?.PontoVisado ?? (principal?.Metadados?.NomeRe ?? seqEstacoes[0]);
                }

                var visadaVante = estacaoOrigem?.Leituras.FirstOrDefault(l =>
                    string.Equals(l.PontoVisado, nomeVante, StringComparison.OrdinalIgnoreCase));

                if (visadaVante != null)
                {
                    hzVante = visadaVante.AnguloHorizontal;
                    distVante = visadaVante.DistanciaInclinada;
                }

                EstacoesCaminhamento.Add(new EstacaoCaminhamentoItemViewModel
                {
                    NomeEstacao = nomeOrigem,
                    NomeRe = nomeRe,
                    AnguloHorizontalVante = hzVante,
                    NomeVante = nomeVante,
                    DistanciaVante = distVante
                });
            }
        }



        // 6. Atualização síncrona de tabelas de visualização na Thread Gráfica STA

        await RunOnUiThreadAsync(() =>

        {

            ResultadosCaderneta.Clear();

            Visadas.Clear();

            var leituras = new List<LeituraViewModel>();

            foreach (var est in _estacoesEmMemoria)

            {

                foreach (var l in est.Leituras)

                {

                    leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento, NumeroSessao = est.NumeroSessao });

                }

            }

            Visadas.AddRange(leituras);

            var todosPontosProc = resultado.TodosOsPontos.ToList();
            if (todosPontosProc.Count > 0)
            {
                _originX = todosPontosProc.Min(p => p.X);
                _originY = todosPontosProc.Min(p => p.Y);
            }

            // Esboço bruto da poligonal (StaticDraftGeometry)
            if (resultado.PoligonalBruta?.Count >= 2)
            {
                var draft = new StreamGeometry();
                using (var ctx = draft.Open())
                {
                    var pts = resultado.PoligonalBruta;
                    bool coincidentes = pts.Count >= 3 &&
                        Math.Abs(pts[0].X - pts[^1].X) < 1e-3 &&
                        Math.Abs(pts[0].Y - pts[^1].Y) < 1e-3;
                    bool isClosed = pts.Count >= 3 && (coincidentes || resultado.PoligonalFechada || resultado.TipoCenario == TipoCenarioPoligonal.Fechada);
                    int count = (coincidentes && isClosed) ? pts.Count - 1 : pts.Count;
                    ctx.BeginFigure(new System.Windows.Point(pts[0].X - _originX, pts[0].Y - _originY), false, isClosed);
                    for (int i = 1; i < count; i++)
                        ctx.LineTo(new System.Windows.Point(pts[i].X - _originX, pts[i].Y - _originY), true, false);
                }
                draft.Freeze();
                StaticDraftGeometry = draft;
            }
            else
            {
                StaticDraftGeometry = Geometry.Empty;
            }

            // Poligonal Compensada Oficial (StaticPoligonalCompensadaGeometry)
            if (resultado.Poligonal?.Count >= 2)
            {
                var compGeom = new StreamGeometry();
                using (var ctx = compGeom.Open())
                {
                    var pts = resultado.Poligonal;
                    bool coincidentes = pts.Count >= 3 &&
                        Math.Abs(pts[0].X - pts[^1].X) < 1e-3 &&
                        Math.Abs(pts[0].Y - pts[^1].Y) < 1e-3;
                    bool isClosed = pts.Count >= 3 && (coincidentes || resultado.PoligonalFechada || resultado.TipoCenario == TipoCenarioPoligonal.Fechada);
                    int count = (coincidentes && isClosed) ? pts.Count - 1 : pts.Count;
                    ctx.BeginFigure(new System.Windows.Point(pts[0].X - _originX, pts[0].Y - _originY), false, isClosed);
                    for (int i = 1; i < count; i++)
                        ctx.LineTo(new System.Windows.Point(pts[i].X - _originX, pts[i].Y - _originY), true, false);
                }
                compGeom.Freeze();
                StaticPoligonalCompensadaGeometry = compGeom;
            }
            else
            {
                StaticPoligonalCompensadaGeometry = Geometry.Empty;
            }

            // Pontos Calculados (StaticPointsGeometry) - renderização visual imediata
            if (todosPontosProc.Any())
            {
                var ptsGeom = new StreamGeometry();
                using (var ctx = ptsGeom.Open())
                {
                    foreach (var p in todosPontosProc)
                    {
                        double px = p.X - _originX;
                        double py = p.Y - _originY;
                        // Micro-cruz de 1.5m (amplitude ±0.75m) centrada no vértice para renderização nítida
                        ctx.BeginFigure(new System.Windows.Point(px - 0.75, py), false, false);
                        ctx.LineTo(new System.Windows.Point(px + 0.75, py), true, false);
                        ctx.BeginFigure(new System.Windows.Point(px, py - 0.75), false, false);
                        ctx.LineTo(new System.Windows.Point(px, py + 0.75), true, false);
                    }
                }
                ptsGeom.Freeze();
                StaticPointsGeometry = ptsGeom;
            }

            // Vetor de Erro de Fechamento (StaticErrorVectorGeometry) se houver erro
            if (resultado.PoligonalBruta?.Count >= 2 && resultado.Poligonal?.Count >= 2)
            {
                var ultBruto = resultado.PoligonalBruta[^1];
                var ultComp = resultado.Poligonal[^1];
                var erroGeom = new StreamGeometry();
                using (var ctx = erroGeom.Open())
                {
                    ctx.BeginFigure(new System.Windows.Point(ultBruto.X - _originX, ultBruto.Y - _originY), false, false);
                    ctx.LineTo(new System.Windows.Point(ultComp.X - _originX, ultComp.Y - _originY), true, false);
                }
                erroGeom.Freeze();
                StaticErrorVectorGeometry = erroGeom;
            }

            // Inicialização imediata do índice espacial KdTree2D para snapping imediato no CAD
            if (todosPontosProc.Any())
            {
                SpatialIndex = new KdTree2D([.. todosPontosProc.Select((p, i) => new KdNode(p.X, p.Y, i))]);
            }

            ResultadosCaderneta.AddRange(todosPontosProc);

            // Ajuste automático do enquadramento (Zoom Extents)
            ZoomExtents(todosPontosProc);

        });



        // =========================================================================

        // 7. MAQUINA DE DECISAO DE COMPENSACAO (Tolerâncias da NBR 13.133)

        // =========================================================================

        if (!resultado.AprovadoNorma)

        {

            // Transição Reativa II (Reprovado): Acende StatusBar em Vermelho Crítico

            EstadoAtualMotor = EstadoMotor.ReprovadoNorma;



            if (resultado.Alertas != null)

            {

                foreach (var alerta in resultado.Alertas)

                {

                    AvisosDiagnostico.Add(alerta);

                }

            }



            _messageService.MostrarAviso(

                $@"LEVANTAMENTO REPROVADO (NBR 13.133):\n\n{string.Join("\n", resultado.Alertas ?? [])}\n\n" +

                "As coordenadas calculadas são puramente brutos e improprias para uso cartográfico legal.", 

                "Falha de Tolerância"

            );

        }

        else

        {

            // Transição Reativa III (Compensado): Acende StatusBar em Verde Esmeralda (Sucesso)

            EstadoAtualMotor = EstadoMotor.Compensado;

            

            // Dispara regeneração assíncrona do Modelo MDT e Curvas de Nível (Tinfour.NET)

            await RenderizarTinDeResultadoAsync(resultado);



            if (resultado.TipoCenario == TipoCenarioPoligonal.AbertaOrientada)

            {

                _messageService.MostrarAviso("Poligonal ABERTA: deriva linear não auditada.", "Aviso");

            }

            else

            {

                _messageService.MostrarSucesso("Compensação concluída. NBR 13.133 atendida com sucesso.", "Sucesso");

            }

        }

    }

    catch (DadosInsuficientesException ex)

    {

        EstadoAtualMotor = EstadoMotor.Bruto;

        _messageService.MostrarErro($"Ruptura topologica na caderneta de campo:\n\n{ex.Message}", "Falha de Consistancia");

    }

    catch (Exception ex)

    {

        EstadoAtualMotor = EstadoMotor.Bruto;

        _messageService.MostrarErro($"Erro crítico no motor de cálculo:\n\n{ex.Message}", "Falha Física");

    }

}



// === ETAPA 4 - Projeto ===

        private async Task AbrirProjetoAsync()

        {

            var caminho = _dialogService.SelecionarArquivoAbertura("Projeto TopoGENTE (*.topogente)|*.topogente|Todos (*.*)|*.*", "Abrir Projeto");

            if (string.IsNullOrEmpty(caminho)) return;

            try

            {

                var projeto = await Task.Run(() => _projetoService.CarregarProjeto(caminho));

                _estacoesEmMemoria = projeto.Estacoes ?? [];

                InvalidarResultado(); AtualizarListaEstacoes();

                ResultadosCaderneta.Clear();

                Visadas.Clear();



                var leituras = new List<LeituraViewModel>();

                foreach (var est in _estacoesEmMemoria)

                {

                    foreach (var l in est.Leituras)

                    {

                        leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento, NumeroSessao = est.NumeroSessao });

                    }

                }

                Visadas.AddRange(leituras);



                var pontos = _estacoesEmMemoria

                    .SelectMany(e => e.Leituras

                        .Select(l => new PontoCoordenada { Nome = l.PontoVisado ?? string.Empty }))

                    .ToList();

                ResultadosCaderneta.AddRange(pontos);





                NomeArquivoAtual = System.IO.Path.GetFileName(caminho); ProjetoModificado = false;

            }

            catch (Exception ex) { _messageService.MostrarErro($"Erro ao abrir: {ex.Message}", "Erro"); }

        }

        private async Task SalvarProjetoAsync()

        {

            var caminho = _dialogService.SelecionarArquivoSalvamento("Projeto TopoGENTE (*.topogente)|*.topogente|Todos (*.*)|*.*", "Salvar Projeto", NomeArquivoAtual.Replace(".topogente", ""), ".topogente");

            if (string.IsNullOrEmpty(caminho)) return;

            try

            {

                var projeto = new ProjetoTopo { Estacoes = _estacoesEmMemoria };

                await Task.Run(() => _projetoService.SalvarProjeto(projeto, caminho));

                NomeArquivoAtual = System.IO.Path.GetFileName(caminho); ProjetoModificado = false;

                _messageService.MostrarSucesso($"Projeto salvo:\n{caminho}", "Salvo");

            }

            catch (Exception ex) { _messageService.MostrarErro($"Erro ao salvar: {ex.Message}", "Erro"); }

        }



        // === ETAPA 6-  Exportação ===

        private void OnExportarTxt()

        {

            if (_resultadoAtual == null) return;

            var caminho = _dialogService.SelecionarArquivoSalvamento("Texto (*.txt)|*.txt|Todos (*.*)|*.*", "Salvar Coordenadas", "Levantamento.txt", ".txt");

            if (string.IsNullOrEmpty(caminho)) return;

            try

            {

                string dir = System.IO.Path.GetDirectoryName(caminho) ?? "";

                string sem = System.IO.Path.GetFileNameWithoutExtension(caminho);

                string mem = System.IO.Path.Combine(dir, $"{sem}_MemoriaCalculo.txt");

                _exportarTxtService.ExportarCoordenadasGestor(_resultadoAtual, caminho);

                _exportarTxtService.ExportarMemoriaCalculo(_resultadoAtual, mem);

                _messageService.MostrarSucesso($"Exportado:\n1. {caminho}\n2. {mem}", "Exportação Concluída");

            }

            catch (Exception ex) { _messageService.MostrarErro($"Erro TXT: {ex.Message}", "Erro"); }

        }

        private void OnExportarDxf()
        {
            if (_resultadoAtual == null) return;
            var caminho = _dialogService.SelecionarArquivoSalvamento("DXF (*.dxf)|*.dxf|Todos (*.*)|*.*", "Salvar DXF", "Levantamento.dxf", ".dxf");
            if (string.IsNullOrEmpty(caminho)) return;
            try
            {
                _exportadorDxfService.SalvarDxf([.. _resultadoAtual.TodosOsPontos], caminho);
                _messageService.MostrarSucesso($"DXF: {caminho}", "Exportado");
            }
            catch (Exception ex) { _messageService.MostrarErro($"Erro DXF: {ex.Message}", "Erro"); }
        }

        private void OnExportarRelatorioQa()
        {
            if (RelatorioQaAtual == null) return;
            var caminho = _dialogService.SelecionarArquivoSalvamento("Relatório de Auditoria (*.txt)|*.txt|Todos (*.*)|*.*", "Exportar Laudo Técnico QA", "Relatorio_Auditoria_QA.txt", ".txt");
            if (string.IsNullOrEmpty(caminho)) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("          LAUDO TÉCNICO DE AUDITORIA E CONTROLE DE QUALIDADE (NBR 13.133)       ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Data de Emissão: {RelatorioQaAtual.GeradoEm:dd/MM/yyyy HH:mm:ss}");
                sb.AppendLine($"Arquivo Base:    {NomeArquivoAtual}");
                sb.AppendLine($"Status do Motor: {TextoStatusBar}");
                sb.AppendLine();
                sb.AppendLine("FECHAMENTOS E TOLERÂNCIAS:");
                sb.AppendLine($"  - Erro Angular:     {RelatorioQaAtual.ErroAngular}");
                sb.AppendLine($"  - Erro Linear:      {RelatorioQaAtual.ErroLinear:F4} m");
                sb.AppendLine($"  - Erro Altimétrico: {RelatorioQaAtual.ErroAltimetrico:F4} m");
                sb.AppendLine();
                sb.AppendLine("ESTATÍSTICAS DA SUPERFÍCIE:");
                sb.AppendLine($"  - Total de Pontos:      {TotalPontos}");
                sb.AppendLine($"  - Triângulos TIN (MDT): {TotalTriangulosMdt}");
                sb.AppendLine($"  - Cota Mínima:          {CotaMinimaTerreno:F3} m");
                sb.AppendLine($"  - Cota Máxima:          {CotaMaximaTerreno:F3} m");
                sb.AppendLine($"  - Desnível Total:       {DesnivelTerreno:F3} m");
                sb.AppendLine();
                sb.AppendLine("INSPEÇÃO DE CHECKPOINTS E HOMÓLOGOS:");
                if (RelatorioQaAtual.Checks != null && RelatorioQaAtual.Checks.Count > 0)
                {
                    foreach (var chk in RelatorioQaAtual.Checks)
                    {
                        sb.AppendLine($"  * Ponto: {chk.TargetPoint,-10} | Estação: {chk.EstacaoOcupada,-10} | dXY: {chk.DeltaXY?.ToString("F4") ?? "N/A",8} m | dZ: {chk.DeltaZ?.ToString("F4") ?? "N/A",8} m | Status: {(chk.ExcedeuDeltaXY || chk.ExcedeuDeltaZ ? "REPROVADO" : "CONFORME")}");
                        if (!string.IsNullOrWhiteSpace(chk.Mensagem))
                        {
                            sb.AppendLine($"    Detalhes: {chk.Mensagem}");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("  (Nenhum evento de ultrapassagem de tolerância registrado)");
                }
                sb.AppendLine();
                sb.AppendLine("================================================================================");

                System.IO.File.WriteAllText(caminho, sb.ToString(), System.Text.Encoding.UTF8);
                _messageService.MostrarSucesso($"Relatório de Auditoria QA exportado com sucesso:\n{caminho}", "Laudo Técnico Exportado");
            }
            catch (Exception ex)
            {
                _messageService.MostrarErro($"Erro ao exportar relatório QA: {ex.Message}", "Falha na Exportação");
            }
        }



        // === ICadCanvasContext ===

        public void EmitirLinhaVetorizada(int startVertexId, int endVertexId)

        {

            var bl = new Breakline(startVertexId, endVertexId);

            if (ConsolidarBreaklineCommand.CanExecute(bl)) ConsolidarBreaklineCommand.Execute(bl);

        }

        private async Task ConsolidarBreaklineAsync(Breakline bl)

        {

            try { ExecuteCommand(new Commands.AddBreaklineCommand(_triangulator, bl)); }

            catch (BreaklineConflictException ex) { WeakReferenceMessenger.Default.Send(new RollbackVetorizacaoMensagem(bl.StartVertexId, bl.EndVertexId, ex.Message)); }

            await Task.CompletedTask;

        }

        private async Task DeletarElementoSelecionadoAsync() { await Task.Yield(); }

        public bool ExisteBreakline(Breakline bl) => _triangulator.ExisteBreakline(bl);

        public void NotificarConflitoSelecao(Breakline bl) =>

            WeakReferenceMessenger.Default.Send(new RollbackVetorizacaoMensagem(bl.StartVertexId, bl.EndVertexId, $"Linha entre {bl.StartVertexId} e {bl.EndVertexId} removida concorrentemente."));



        // === AVISOS ===

        public void Receive(TopologyChangedMessage message)
        {
            if (IsTopologyRebuilding) return;
            IsTopologyRebuilding = true;
            Task.Run(() => { try { ProcessarGeometriasAtuais(); } finally { RunOnUiThread(() => IsTopologyRebuilding = false); } });
        }

        public void Receive(ResultadoMdtMensagem message) { Task.Run(() => ProcessarResultadoTopologico(message.Value)); }



        // === Pipeline TIN ===

        private async Task RenderizarTinDeResultadoAsync(ResultadoLevantamento resultado)
        {
            if (!resultado.TodosOsPontos.Any()) return;

            var verts = resultado.TodosOsPontos.Select((p, i) => new TerrainVertex(p.X, p.Y, p.Z, i)).ToArray();

            if (_originX == 0 && _originY == 0 && verts.Length > 0 && (Math.Abs(verts[0].X) > 1000 || Math.Abs(verts[0].Y) > 1000))
            {
                _originX = verts.Min(v => v.X);
                _originY = verts.Min(v => v.Y);
            }

            if (IsTopologyRebuilding) return;
            IsTopologyRebuilding = true;

            try
            {
                var dadosMalha = await Task.Run(() =>
                {
                    var (mv, tr) = _triangulator.GenerateBaseDelaunayMesh(verts, [], 0.001);
                    var iso = Analytics.ComputeContourMap(_stepInterval, _anchorElevation).ToArray();
                    var (tin, minor, major) = ConstruirBuffersGeometricos(mv, tr, iso);

                    var pts = new StreamGeometry();
                    using (var ctx = pts.Open())
                    {
                        foreach (var v in verts)
                        {
                            double px = v.X - _originX;
                            double py = v.Y - _originY;
                            ctx.BeginFigure(new System.Windows.Point(px - 0.75, py), false, false);
                            ctx.LineTo(new System.Windows.Point(px + 0.75, py), true, false);
                            ctx.BeginFigure(new System.Windows.Point(px, py - 0.75), false, false);
                            ctx.LineTo(new System.Windows.Point(px, py + 0.75), true, false);
                        }
                    }
                    pts.Freeze();

                    int totalTri = tr.Length;
                    int totalVerts = mv.Length;
                    double minZ = verts.Length > 0 ? verts.Min(v => v.Z) : 0;
                    double maxZ = verts.Length > 0 ? verts.Max(v => v.Z) : 0;
                    double desnivel = maxZ - minZ;

                    return (tin, minor, major, pts, totalTri, totalVerts, minZ, maxZ, desnivel);
                });

                await RunOnUiThreadAsync(() =>
                {
                    SpatialIndex = new KdTree2D([.. verts.Select(v => new KdNode(v.X, v.Y, v.Id))]);
                    StaticTinGeometry = dadosMalha.tin;
                    StaticMinorContoursGeometry = dadosMalha.minor;
                    StaticMajorContoursGeometry = dadosMalha.major;
                    StaticPointsGeometry = dadosMalha.pts;
                    TotalTriangulosMdt = dadosMalha.totalTri;
                    TotalVerticesMdt = dadosMalha.totalVerts;
                    CotaMinimaTerreno = dadosMalha.minZ;
                    CotaMaximaTerreno = dadosMalha.maxZ;
                    DesnivelTerreno = dadosMalha.desnivel;
                    OnPropertyChanged(nameof(TotalPontos));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao renderizar TIN de resultado: {ex.Message}");
            }
            finally
            {
                RunOnUiThread(() => IsTopologyRebuilding = false);
            }
        }

        private void RenderizarTinDeResultado(ResultadoLevantamento resultado)
        {
            _ = RenderizarTinDeResultadoAsync(resultado);
        }

        private void ProcessarResultadoTopologico(ResultadoLevantamento resultado) { RenderizarTinDeResultado(resultado); }

        private void ProcessarGeometriasAtuais()
        {
            try
            {
                var (mv, tr) = _triangulator.GetCurrentMesh();
                var iso = Analytics.ComputeContourMap(_stepInterval, _anchorElevation).ToArray();
                var (tin, minor, major) = ConstruirBuffersGeometricos(mv, tr, iso);

                int totalTri = tr.Length;
                int totalVerts = mv.Length;
                double minZ = mv.Length > 0 ? mv.Min(v => v.Z) : 0;
                double maxZ = mv.Length > 0 ? mv.Max(v => v.Z) : 0;
                double desnivel = maxZ - minZ;

                Action atualizarUi = () =>
                {
                    StaticTinGeometry = tin;
                    StaticMinorContoursGeometry = minor;
                    StaticMajorContoursGeometry = major;
                    TotalTriangulosMdt = totalTri;
                    TotalVerticesMdt = totalVerts;
                    CotaMinimaTerreno = minZ;
                    CotaMaximaTerreno = maxZ;
                    DesnivelTerreno = desnivel;
                };

                RunOnUiThread(atualizarUi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao processar geometrias atuais: {ex.Message}");
            }
        }

        private (StreamGeometry Tin, StreamGeometry Minor, StreamGeometry Major) ConstruirBuffersGeometricos(
            TerrainVertex[] meshVertices, TerrainTriangle[] triangles, Isoline[] isolines)
        {
            var tin = new StreamGeometry(); var minor = new StreamGeometry(); var major = new StreamGeometry();
            int maxId = meshVertices.Length > 0 ? meshVertices.Max(v => v.Id) : 0;
            var lk = new TerrainVertex[maxId + 1];
            foreach (var v in meshVertices) lk[v.Id] = v;

            using (var ctx = tin.Open()) foreach (var t in triangles)
            {
                var v0 = lk[t.V0]; var v1 = lk[t.V1]; var v2 = lk[t.V2];
                ctx.BeginFigure(new System.Windows.Point(v0.X - _originX, v0.Y - _originY), true, true);
                ctx.LineTo(new System.Windows.Point(v1.X - _originX, v1.Y - _originY), true, false);
                ctx.LineTo(new System.Windows.Point(v2.X - _originX, v2.Y - _originY), true, false);
            }
            tin.Freeze();

            using (var ctxMi = minor.Open()) using (var ctxMa = major.Open()) foreach (var iso in isolines)
            {
                double rel = (iso.Elevation - _anchorElevation) / _stepInterval;
                double rnd = Math.Round(rel);
                bool mestra = Math.Abs(rel - rnd) < 1e-7 && ((int)rnd % _majorContourFrequency == 0);
                var ctx2 = mestra ? ctxMa : ctxMi;
                var pts2 = iso.Vertices.Span;
                if (pts2.Length > 1)
                {
                    ctx2.BeginFigure(new System.Windows.Point(pts2[0].X - _originX, pts2[0].Y - _originY), false, false);
                    var wp = new System.Windows.Point[pts2.Length - 1];
                    for (int i = 1; i < pts2.Length; i++) wp[i - 1] = new System.Windows.Point(pts2[i].X - _originX, pts2[i].Y - _originY);
                    ctx2.PolyLineTo(wp, true, false);
                }
            }

            minor.Freeze(); major.Freeze();

            return (tin, minor, major);

        }



        // === Helpers ===

        private void InvalidarResultado()

{

    _resultadoAtual = null;

    RelatorioQaAtual = null;

    StaticDraftGeometry = Geometry.Empty;
    StaticPoligonalCompensadaGeometry = Geometry.Empty;
    StaticPointsGeometry = Geometry.Empty;
    StaticErrorVectorGeometry = Geometry.Empty;
    StaticTinGeometry = Geometry.Empty;
    StaticMinorContoursGeometry = Geometry.Empty;
    StaticMajorContoursGeometry = Geometry.Empty;
    StaticBreaklinesGeometry = Geometry.Empty;

    TotalTriangulosMdt = 0;
    TotalVerticesMdt = 0;
    CotaMinimaTerreno = 0;
    CotaMaximaTerreno = 0;
    DesnivelTerreno = 0;
    OnPropertyChanged(nameof(TotalPontos));

    _originX = 0;
    _originY = 0;

    // Reseta os relatórios de QA

    AvisosDiagnostico.Clear(); 

    // Reseta o estado do motor de volta a Bruto (StatusBar Cinza)

    if (EstadoAtualMotor != EstadoMotor.Calculando)

    {

        EstadoAtualMotor = EstadoMotor.Bruto;

    }

    NotifyCommandStates(); // Atualiza o CanExecute dos botões de exportação

}



// Método interceptado reativamente pela edição de linha da caderneta de campo

private async Task OnRowEditEndingAsync(object? args)

{

    InvalidarResultado();

    await Task.Yield();

}





        private void NotifyCommandStates()

        {

            ((AsyncRelayCommand)ProcessarCommand).NotifyCanExecuteChanged();

            (ExportarTxtCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (ExportarDxfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportarRelatorioQaCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RecalcularMalhaCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();

            (RemoverPoligonalCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (AdicionarSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (RemoverSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (SubirSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (DescerSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();

        }

        



        // === IDisposable ===

        public void Dispose()
        {
            if (_disposed) return; 
            _disposed = true;

            // Desinscreve o ViewModel de todas as mensagens do barramento fraco
            WeakReferenceMessenger.Default.UnregisterAll(this);

            _undoStack.Clear(); 
            _redoStack.Clear();

            StaticTinGeometry = Geometry.Empty; 
            StaticMajorContoursGeometry = Geometry.Empty;
            StaticMinorContoursGeometry = Geometry.Empty; 
            StaticBreaklinesGeometry = Geometry.Empty;
            StaticPointsGeometry = Geometry.Empty; 
            DynamicRubberBandGeometry = Geometry.Empty;

            // Libera o adaptador do Tinfour
            if (_triangulator is IDisposable disposableTriangulator)
            {
                disposableTriangulator.Dispose();
            }

            // Alerta o Garbage Collector para desconsiderar o finalizador (Prevenção de vazamento de Gen1)
            GC.SuppressFinalize(this);
        }

    }

}