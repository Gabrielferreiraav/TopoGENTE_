using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

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



private Geometry _staticErrorVectorGeometry = Geometry.Empty;

public Geometry StaticErrorVectorGeometry 

{ 

    get => _staticErrorVectorGeometry; 

    set => SetProperty(ref _staticErrorVectorGeometry, value); 

}



// =========================================================================

// 3. OPCOES DE VISIBILIDADE DAS CAMADAS CAD 

// =========================================================================

private bool _mostrarEsbocoBruto = true;

public bool MostrarEsbocoBruto { get => _mostrarEsbocoBruto; set => SetProperty(ref _mostrarEsbocoBruto, value); }



private bool _mostrarPontos = true;

public bool MostrarPontos { get => _mostrarPontos; set => SetProperty(ref _mostrarPontos, value); }



private bool _mostrarTin = true;

public bool MostrarTin { get => _mostrarTin; set => SetProperty(ref _mostrarTin, value); }



private bool _mostrarBreaklines = true;

public bool MostrarBreaklines { get => _mostrarBreaklines; set => SetProperty(ref _mostrarBreaklines, value); }



private bool _mostrarCurvasMestras = true;

public bool MostrarCurvasMestras { get => _mostrarCurvasMestras; set => SetProperty(ref _mostrarCurvasMestras, value); }



private bool _mostrarCurvasSecundarias = true;

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



        // === Câmera ===

        private Matrix _cameraMatrix = Matrix.Identity;

        public Matrix CameraMatrix { get => _cameraMatrix; set => SetProperty(ref _cameraMatrix, value); }

        public System.Windows.Point GetModelCoordinates(System.Windows.Point screenPoint)

        {

            Matrix inv = CameraMatrix;

            if (!inv.HasInverse) return screenPoint;

            inv.Invert();

            return inv.Transform(screenPoint);

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

                    ctx.BeginFigure(new System.Windows.Point(startX.Value, startY.Value), false, false);

                    ctx.LineTo(new System.Windows.Point(endX.Value, endY.Value), true, false);

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

                public ObservableCollection<object> EstacoesCaminhamento { get; } = [];

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

        private readonly double _stepInterval = 1.0;

        private readonly double _anchorElevation = 100.0;

        private readonly int _majorContourFrequency = 5;



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

            _triangulatorFactory = triangulatorFactory; _analyticsFactory = analyticsFactory; _triangulator = _triangulatorFactory(); _analytics = _analyticsFactory();

            _leitorFactory = leitorFactory; _organizador = organizador;

            _processador = processador; _qaCheckService = qaCheckService;

            _classificadorGrafo = classificadorGrafo; _projetoService = projetoService;

            _exportarTxtService = exportarTxtService; _exportadorDxfService = exportadorDxfService;

            _dialogService = dialogService; _fileService = fileService; _messageService = messageService;



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



            var principal = new SequenciaPoligonalViewModel { Nome = "Poligonal Principal", EhPrincipal = true };

            principal.PropertyChanged += (_, _) => ((AsyncRelayCommand)ProcessarCommand).NotifyCanExecuteChanged();

            Poligonais.Add(principal);

            PoligonalSelecionada = principal;

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

                        leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento });

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

                        Poligonais[0].PartidaX = pe.CoordenadaConhecida.X.ToString("F3");

                        Poligonais[0].PartidaY = pe.CoordenadaConhecida.Y.ToString("F3");

                        Poligonais[0].PartidaZ = pe.CoordenadaConhecida.Z.ToString("F3");

                    }

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

        var ptosConhecidos = _estacoesEmMemoria

            .Where(e => e.CoordenadaConhecida != null)

            .Select(e => e.CoordenadaConhecida!)

            .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)

            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);



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

        RelatorioQaAtual = relatorio;



        // 5. Preenche as Coleções Tabulares de Apresentação O(N) de forma rápida

        EstacoesCaminhamento.Clear();

        foreach (var p in resultado.Poligonal)

        {

            EstacoesCaminhamento.Add(new 

            { 

                NomeEstacao = p.Nome, 

                NomeRe = "Ré", 

                AnguloHorizontalVante = 0.0, 

                NomeVante = "Vante" 

            });

        }



        // 6. Atualização síncrona de tabelas de visualização na Thread Gráfica STA

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>

        {

            ResultadosCaderneta.Clear();

            Visadas.Clear();

            var leituras = new List<LeituraViewModel>();

            foreach (var est in _estacoesEmMemoria)

            {

                foreach (var l in est.Leituras)

                {

                    leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento });

                }

            }

            Visadas.AddRange(leituras);

            ResultadosCaderneta.AddRange(resultado.TodosOsPontos);

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

            RenderizarTinDeResultado(resultado);



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

                        leituras.Add(new LeituraViewModel(l) { AlturaInstrumento = est.AlturaInstrumento });

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



        // === ICadCanvasContext ===

        public void EmitirLinhaVetorizada(int startVertexId, int endVertexId)

        {

            var bl = new Breakline(startVertexId, endVertexId);

            if (ConsolidarBreaklineCommand.CanExecute(bl)) ConsolidarBreaklineCommand.Execute(bl);

        }

        private async Task ConsolidarBreaklineAsync(Breakline bl)

        {

            try { ExecuteCommand(new Commands.AddBreaklineCommand(_triangulatorFactory(), bl)); }

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

            Task.Run(() => { try { ProcessarGeometriasAtuais(); } finally { System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => IsTopologyRebuilding = false); } });

        }

        public void Receive(ResultadoMdtMensagem message) { Task.Run(() => ProcessarResultadoTopologico(message.Value)); }



        // === Pipeline TIN ===

        private void RenderizarTinDeResultado(ResultadoLevantamento resultado)

        {

            if (!resultado.TodosOsPontos.Any()) return;

            var verts = resultado.TodosOsPontos.Select((p, i) => new TerrainVertex(p.X, p.Y, p.Z, i)).ToArray();

            Task.Run(() =>

            {

                try

                {

                    var (mv, tr) = _triangulator.GenerateBaseDelaunayMesh(verts, [], 0.001);

                    var iso = _analytics.ComputeContourMap(_stepInterval, _anchorElevation).ToArray();

                    var (tin, minor, major) = ConstruirBuffersGeometricos(mv, tr, iso);

                    var pts = new StreamGeometry();

                    using (var ctx = pts.Open()) { foreach (var v in verts) { ctx.BeginFigure(new System.Windows.Point(v.X, v.Y), false, false); ctx.LineTo(new System.Windows.Point(v.X + 0.1, v.Y + 0.1), true, false); } }

                    pts.Freeze();

                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>

                    {

                        SpatialIndex = new KdTree2D([.. verts.Select(v => new KdNode(v.X, v.Y, v.Id))]);

                        StaticTinGeometry = tin; StaticMinorContoursGeometry = minor; StaticMajorContoursGeometry = major; StaticPointsGeometry = pts;

                    }, System.Windows.Threading.DispatcherPriority.Background);

                }

                catch {  }

            });

        }

        private void ProcessarResultadoTopologico(ResultadoLevantamento resultado) { RenderizarTinDeResultado(resultado); }

        private void ProcessarGeometriasAtuais()

        {

            var (mv, tr) = _triangulator.GetCurrentMesh();

            var iso = _analytics.ComputeContourMap(_stepInterval, _anchorElevation).ToArray();

            var (tin, minor, major) = ConstruirBuffersGeometricos(mv, tr, iso);

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>

            { StaticTinGeometry = tin; StaticMinorContoursGeometry = minor; StaticMajorContoursGeometry = major; },

            System.Windows.Threading.DispatcherPriority.Background);

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

                ctx.BeginFigure(new System.Windows.Point(v0.X, v0.Y), true, true);

                ctx.LineTo(new System.Windows.Point(v1.X, v1.Y), true, false);

                ctx.LineTo(new System.Windows.Point(v2.X, v2.Y), true, false);

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

                    ctx2.BeginFigure(new System.Windows.Point(pts2[0].X, pts2[0].Y), false, false);

                    var wp = new System.Windows.Point[pts2.Length - 1];

                    for (int i = 1; i < pts2.Length; i++) wp[i - 1] = new System.Windows.Point(pts2[i].X, pts2[i].Y);

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

    

    // Reseta os relat3rios de QA

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