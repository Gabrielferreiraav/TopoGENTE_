using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;
using TopoGente.Core.Validators;
using TopoGente.UI.Eventing;
using TopoGente.UI.Services;

namespace TopoGente.UI.ViewModels
{
    public class MainViewModel : ObservableObject
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
        private readonly IDialogService _dialogService;
        private readonly IMessageService _messageService;
        private readonly IFileService _fileService;

        private List<Estacao> _estacoesEmMemoria = new();
        private ResultadoLevantamento? _resultadoAtual;

        // UI Properties
        private int _formatoArquivoIndex = 1; // Default FBK
        public int FormatoArquivoIndex
        {
            get => _formatoArquivoIndex;
            set => SetProperty(ref _formatoArquivoIndex, value);
        }

        private int _cenarioIndex = 1; // Default Fechada
        public int CenarioIndex
        {
            get => _cenarioIndex;
            set
            {
                if (SetProperty(ref _cenarioIndex, value))
                {
                    OnPropertyChanged(nameof(MostrarPainelChegada));
                    (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool MostrarPainelChegada => CenarioIndex == 0; // 0 = Enquadrada

        private string _partidaX = "1000,000";
        public string PartidaX { get => _partidaX; set { if (SetProperty(ref _partidaX, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _partidaY = "1000,000";
        public string PartidaY { get => _partidaY; set { if (SetProperty(ref _partidaY, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _partidaZ = "100,000";
        public string PartidaZ { get => _partidaZ; set { if (SetProperty(ref _partidaZ, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private bool _usarAzimute = true;
        public bool UsarAzimute
        {
            get => _usarAzimute;
            set
            {
                if (SetProperty(ref _usarAzimute, value))
                {
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
        public string Azimute { get => _azimute; set { if (SetProperty(ref _azimute, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _reX = "0";
        public string ReX { get => _reX; set { if (SetProperty(ref _reX, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _reY = "0";
        public string ReY { get => _reY; set { if (SetProperty(ref _reY, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _reZ = "0";
        public string ReZ { get => _reZ; set { if (SetProperty(ref _reZ, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _nomeRe = "REF";
        public string NomeRe { get => _nomeRe; set => SetProperty(ref _nomeRe, value); }

        private string _chegadaX = "0";
        public string ChegadaX { get => _chegadaX; set { if (SetProperty(ref _chegadaX, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _chegadaY = "0";
        public string ChegadaY { get => _chegadaY; set { if (SetProperty(ref _chegadaY, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _chegadaZ = "0";
        public string ChegadaZ { get => _chegadaZ; set { if (SetProperty(ref _chegadaZ, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        private string _nomeChegada = "M99";
        public string NomeChegada { get => _nomeChegada; set => SetProperty(ref _nomeChegada, value); }

        private string _azimuteChegada = "0";
        public string AzimuteChegada { get => _azimuteChegada; set { if (SetProperty(ref _azimuteChegada, value)) (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

        public ObservableCollection<string> EstacoesDisponiveis { get; } = new();
        public ObservableCollection<string> SequenciaPoligonal { get; } = new();
        public ObservableCollection<FiltroCamada> CamadasDisponiveis { get; } = new();

        private string? _estacaoDisponivelSelecionada;
        public string? EstacaoDisponivelSelecionada
        {
            get => _estacaoDisponivelSelecionada;
            set => SetProperty(ref _estacaoDisponivelSelecionada, value);
        }

        private string? _estacaoSequenciaSelecionada;
        public string? EstacaoSequenciaSelecionada
        {
            get => _estacaoSequenciaSelecionada;
            set => SetProperty(ref _estacaoSequenciaSelecionada, value);
        }

        // Commands
        public ICommand CarregarArquivoCommand { get; }
        public ICommand ProcessarCommand { get; }
        public ICommand ExportarTxtCommand { get; }
        public ICommand AdicionarSequenciaCommand { get; }
        public ICommand RemoverSequenciaCommand { get; }
        public ICommand SubirSequenciaCommand { get; }
        public ICommand DescerSequenciaCommand { get; }
        public ICommand ExibirCadernetaCommand { get; }
        public ICommand ExibirGraficoCommand { get; }

        public MainViewModel(
            ILeituraArquivoFactory leitorService,
            ILevantamentoProcessor processadorService,
            IArquivoProjetoService projetoService,
            IOrganizarCaminhamento organizador,
            IExportadorDxfService dxfService,
            IExportarTxtService exportarTxtService,
            IQaCheckService qaCheckService,
            IClassificadorGrafo classificadorGrafo,
            IUiEventHub uiEventHub,
            IDialogService dialogService,
            IMessageService messageService,
            IFileService fileService)
        {
            _leitorService = leitorService;
            _processadorService = processadorService;
            _projetoService = projetoService;
            _organizador = organizador;
            _dxfService = dxfService;
            _exportarTxtService = exportarTxtService;
            _qaCheckService = qaCheckService;
            _classificadorGrafo = classificadorGrafo;
            _uiEventHub = uiEventHub;
            _uiEventHub.LeituraRemovida += OnLeituraRemovida;
            _uiEventHub.LeituraEditada += OnLeituraEditada;
            _dialogService = dialogService;
            _messageService = messageService;
            _fileService = fileService;

            CarregarArquivoCommand = new RelayCommand(OnCarregarArquivo);
            ProcessarCommand = new RelayCommand(OnProcessar, PodeCalcularCompensacao);
            ExportarTxtCommand = new RelayCommand(OnExportarTxt, _ => _resultadoAtual?.TodosOsPontos.Any() == true);
            
            AdicionarSequenciaCommand = new RelayCommand(OnAdicionarSequencia);
            RemoverSequenciaCommand = new RelayCommand(OnRemoverSequencia);
            SubirSequenciaCommand = new RelayCommand(OnSubirSequencia);
            DescerSequenciaCommand = new RelayCommand(OnDescerSequencia);

            ExibirCadernetaCommand = new RelayCommand(_ => AbrirJanela<CadernetaWindow>());
            ExibirGraficoCommand = new RelayCommand(_ => AbrirJanela<VisualizacaoWindow>());
        }

        private void OnLeituraRemovida(object? sender, LeituraRemovidaEventArgs e)
        {
            if (e.Estacao != null && e.Leitura != null)
            {
                // 1. Remove a visada (Delegação para o Aggregate Root)
                e.Estacao.RemoverVisada(e.Leitura);

                // 2. Destrói matematicamente o cálculo anterior para evitar ESTADO ZUMBI
                _resultadoAtual = null;
                
                // 3. Bloqueia a exportação DXF/TXT de dados fantasmas
                (ExportarTxtCommand as RelayCommand)?.RaiseCanExecuteChanged();

                // 4. Repreencha a interface gráfica
                _uiEventHub.PublicarEstacoes(_estacoesEmMemoria);

                // 5. O dado mudou, a compensação anterior morreu. Força o modo esboço.
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnLeituraEditada(object? sender, LeituraEditadaEventArgs e)
        {
            if (e.Estacao != null && e.LeituraIdAntiga != null && e.NovosDados != null)
            {
                try
                {
                    e.Estacao.SubstituirLeitura(
                        e.LeituraIdAntiga,
                        e.NovosDados.PontoVisado,
                        e.NovosDados.AnguloHorizontal,
                        e.NovosDados.AnguloVertical,
                        e.NovosDados.DistanciaInclinada,
                        e.NovosDados.AlturaPrisma,
                        e.NovosDados.Observacao
                    );

                    // 1. Destruição Prévia do Resultado (Prevenção de Estado Zumbi conforme salvaguarda)
                    _resultadoAtual = null;
                    
                    // Bloqueia a exportação DXF/TXT de dados fantasmas
                    (ExportarTxtCommand as RelayCommand)?.RaiseCanExecuteChanged();

                    // 2. Repreencha a interface gráfica para manter a consistência da view
                    _uiEventHub.PublicarEstacoes(_estacoesEmMemoria);

                    // 3. O dado mudou, a compensação anterior morreu. Força o modo esboço.
                    PublicarEsbocoGeodesicoSobDemanda();
                }
                catch (Exception ex)
                {
                    _messageService.MostrarErro($"Erro ao substituir leitura: {ex.Message}", "Erro");
                }
            }
        }

        private FormatoArquivoEntrada ObterFormatoEntrada()
        {
            return FormatoArquivoIndex switch
            {
                0 => FormatoArquivoEntrada.CsvPadrao,
                1 => FormatoArquivoEntrada.Fbk,
                2 => FormatoArquivoEntrada.LandXml,
                _ => FormatoArquivoEntrada.CsvPadrao,
            };
        }

        private void OnCarregarArquivo(object? parameter)
        {
            var fileName = _dialogService.SelecionarArquivoAbertura(
                "Arquivos Topográficos (*.txt;*.csv;*.fbk;*.xml)|*.txt;*.csv;*.fbk;*.xml|Todos os Arquivos (*.*)|*.*",
                "Selecione a Caderneta de Campo"
            );

            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    var formato = ObterFormatoEntrada();
                    var linhas = _fileService.LerLinhas(fileName);

                    var resultadoLeitura = _leitorService.ProcessarArquivoComResultado(formato, linhas);
                    _estacoesEmMemoria = _organizador.UnificarEstacoes(resultadoLeitura.Estacoes);
                    
                    AtualizarListaEstacoes();
                    SugerirSequenciaPoligonalPorPurpose();

                    if (_estacoesEmMemoria.Count > 0)
                    {
                        var primeiraEstacao = _estacoesEmMemoria[0];
                        if (primeiraEstacao.CoordenadaConhecida != null)
                        {
                            PartidaX = primeiraEstacao.CoordenadaConhecida.X.ToString("F3");
                            PartidaY = primeiraEstacao.CoordenadaConhecida.Y.ToString("F3");
                            PartidaZ = primeiraEstacao.CoordenadaConhecida.Z.ToString("F3");
                        }
                        else
                        {
                            PartidaX = "0.000"; PartidaY = "0.000"; PartidaZ = "0.000";
                        }

                        var leituraRe = primeiraEstacao.Leituras?.FirstOrDefault(l => string.Equals((l.Purpose ?? string.Empty).Trim(), "re", StringComparison.OrdinalIgnoreCase));
                        if (leituraRe != null)
                        {
                            NomeRe = leituraRe.PontoVisado ?? string.Empty;
                            if (!string.IsNullOrEmpty(leituraRe.PontoVisado) && resultadoLeitura.PontosConhecidosGlobais.TryGetValue(leituraRe.PontoVisado, out var coordenadaRe))
                            {
                                ReX = coordenadaRe.X.ToString("F3");
                                ReY = coordenadaRe.Y.ToString("F3");
                                ReZ = coordenadaRe.Z.ToString("F3");
                                UsarCoordenadaRe = true;
                            }
                            else
                            {
                                UsarAzimute = true;
                                Azimute = leituraRe.AnguloHorizontal.ToString("F4");
                            }
                        }

                        _uiEventHub.PublicarEstacoes(_estacoesEmMemoria);
                        PublicarEsbocoGeodesicoSobDemanda();
                    }
                }
                catch (Exception ex)
                {
                    _messageService.MostrarErro($"Erro ao ler arquivo: {ex.Message}", "Erro");
                }
            }
        }

        private bool ValidarCoordenadas(string? x, string? y, string? z)
        {
            return !string.IsNullOrWhiteSpace(x) && 
                   !string.IsNullOrWhiteSpace(y) && 
                   !string.IsNullOrWhiteSpace(z);
        }

        private bool PodeCalcularCompensacao(object? parameter)
        {
            if (_estacoesEmMemoria == null || _estacoesEmMemoria.Count == 0) return false;

            if (!ValidarCoordenadas(PartidaX, PartidaY, PartidaZ)) return false;

            if (UsarAzimute)
            {
                if (string.IsNullOrWhiteSpace(Azimute)) return false;
            }
            else
            {
                if (!ValidarCoordenadas(ReX, ReY, ReZ)) return false;
            }

            if (CenarioIndex == 0) // Enquadrada
            {
                if (!ValidarCoordenadas(ChegadaX, ChegadaY, ChegadaZ)) return false;
            }

            var leituras = _estacoesEmMemoria.SelectMany(e => e.Leituras ?? new List<LeituraEstacaoTotal>()).ToList();
            if (leituras.Count == 0) return false;
            if (SequenciaPoligonal.Count == 0) return false;

            return true;
        }

        private void ProcessamentoSilencioso()
        {
            var metadados = ColetarMetadadosDaUI();
            var pontosConhecidos = _estacoesEmMemoria
                .Where(e => e.CoordenadaConhecida != null)
                .Select(e => e.CoordenadaConhecida!)
                .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);



            _classificadorGrafo.ClassificarArestasGrafo(_estacoesEmMemoria, metadados);
            
            // Opcional: Se houver falha de validação primária, aborte silenciosamente
            var leiturasClassificadas = _estacoesEmMemoria.SelectMany(e => e.Leituras).ToList();
            if (leiturasClassificadas.Any(l => (l.Tipo == TipoLeitura.Poligonal || l.Tipo == TipoLeitura.Re) && !LeituraValidator.Validar(l).IsValid))
            {
                PublicarEsbocoGeodesicoSobDemanda();
                return; 
            }

            _resultadoAtual = _processadorService.Processar(metadados, _estacoesEmMemoria, pontosConhecidos);
            var relatorioQa = _qaCheckService.GerarRelatorioQaChecks(_estacoesEmMemoria, _resultadoAtual, pontosConhecidos);

            _uiEventHub.PublicarResultado(_resultadoAtual);
            (ExportarTxtCommand as RelayCommand)?.RaiseCanExecuteChanged();
            ExtrairCamadasSemanticas(_resultadoAtual);
        }

        private void OnProcessar(object? parameter)
        {
            if (!PodeCalcularCompensacao(null))
            {
                _messageService.MostrarAviso("Não é possível calcular. Verifique se as coordenadas de controle e a sequência do caminhamento estão preenchidas.", "Dados Insuficientes");
                return;
            }

            try
            {
                ProcessamentoSilencioso();

                if (_resultadoAtual == null)
                {
                    _messageService.MostrarErro("O motor de cálculo falhou sem gerar resultados. Verifique a integridade da caderneta.", "Falha Interna");
                    return;
                }

                // Auditoria Normativa Rigorosa
                if (_resultadoAtual.TipoCenario == TipoCenarioPoligonal.AbertaOrientada)
                {
                    _messageService.MostrarAviso("Este levantamento é do tipo ABERTO. As coordenadas finais não foram auditadas contra erros de fechamento.\n\nQualquer erro angular na primeira estação deslocará linearmente todas as estações subsequentes (efeito alavanca).", "Aviso — Poligonal Aberta");
                }
                else if (!_resultadoAtual.AprovadoNorma)
                {
                    string erros = string.Join("\n", _resultadoAtual.Alertas);
                    _messageService.MostrarErro($"LEVANTAMENTO REPROVADO (NBR 13.133):\n\n{erros}\n\nA compensação foi abortada. As coordenadas exibidas são puramente BRUTAS e impróprias para uso final.", "Falha de Tolerância");
                }
                else
                {
                    _messageService.MostrarSucesso("Cálculo realizado e compensado com sucesso!", "Norma Atendida");
                }
            }
            catch (TopoGente.Core.Entities.DadosInsuficientesException ex)
            {
                _resultadoAtual = null;
                PublicarEsbocoGeodesicoSobDemanda();
                _messageService.MostrarErro(ex.Message, "Ruptura Topológica");
            }
            catch (Exception ex)
            {
                _resultadoAtual = null;
                PublicarEsbocoGeodesicoSobDemanda();
                _messageService.MostrarErro($"Erro crítico durante o processamento trigonométrico: {ex.Message}", "Erro Fatal");
            }
        }

        private void OnExportarTxt(object? parameter)
        {
            if (_resultadoAtual == null || !_resultadoAtual.TodosOsPontos.Any()) return;

            var caminho = _dialogService.SelecionarArquivoSalvamento(
                "Arquivo de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                "Salvar Arquivo de Levantamento",
                "LevantamentoTopoGente.txt",
                ".txt"
            );

            if (!string.IsNullOrEmpty(caminho))
            {
                try
                {
                    string diretorio = System.IO.Path.GetDirectoryName(caminho) ?? "";
                    string nomeSemExtensao = System.IO.Path.GetFileNameWithoutExtension(caminho);
                    string nomeExtensao = System.IO.Path.GetExtension(caminho);
                    string caminhoMemoria = System.IO.Path.Combine(diretorio, $"{nomeSemExtensao}_MemoriaCalculo{nomeExtensao}");

                    _exportarTxtService.ExportarCoordenadasGestor(_resultadoAtual, caminho);
                    _exportarTxtService.ExportarMemoriaCalculo(_resultadoAtual, caminhoMemoria);

                    _messageService.MostrarSucesso($"Arquivos exportados com sucesso em:\n\n1. {caminho}\n2. {caminhoMemoria}",
                            "Sucesso Geométrico");
                }
                catch (Exception ex)
                {
                    _messageService.MostrarErro($"Erro ao exportar arquivo: {ex.Message}", "Erro");
                }
            }
        }

        private void OnAdicionarSequencia(object? parameter)
        {
            if (!string.IsNullOrWhiteSpace(EstacaoDisponivelSelecionada))
            {
                SequenciaPoligonal.Add(EstacaoDisponivelSelecionada);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnRemoverSequencia(object? parameter)
        {
            if (!string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada))
            {
                SequenciaPoligonal.Remove(EstacaoSequenciaSelecionada);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnSubirSequencia(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;
            var index = SequenciaPoligonal.IndexOf(EstacaoSequenciaSelecionada);
            if (index > 0)
            {
                SequenciaPoligonal.Move(index, index - 1);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnDescerSequencia(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;
            var index = SequenciaPoligonal.IndexOf(EstacaoSequenciaSelecionada);
            if (index >= 0 && index < SequenciaPoligonal.Count - 1)
            {
                SequenciaPoligonal.Move(index, index + 1);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void AbrirJanela<T>() where T : System.Windows.Window
        {
            var window = System.Windows.Application.Current.Windows.OfType<T>().FirstOrDefault();
            if (window != null)
            {
                window.Show();
                window.Activate();
            }
        }

        private void AtualizarListaEstacoes()
        {
            EstacoesDisponiveis.Clear();
            var estacoesOcupadas = _estacoesEmMemoria
                .Select(e => e.Nome)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .Distinct()
                .ToList();

            foreach (var est in estacoesOcupadas)
            {
                EstacoesDisponiveis.Add(est);
            }
        }

        private void SugerirSequenciaPoligonalPorPurpose()
        {
            var nomesOcupados = _estacoesEmMemoria
                .Select(e => e.Nome)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (_estacoesEmMemoria.Count == 0 || nomesOcupados.Count == 0) return;

            var sequenciaSugerida = new List<string>();
            var visitadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? nomeAtual = _estacoesEmMemoria[0].Nome;

            while (!string.IsNullOrWhiteSpace(nomeAtual) && visitadas.Add(nomeAtual))
            {
                sequenciaSugerida.Add(nomeAtual);
                var estacaoAtual = _estacoesEmMemoria.FirstOrDefault(e => string.Equals(e.Nome, nomeAtual, StringComparison.OrdinalIgnoreCase));
                var leituraVante = estacaoAtual?.Leituras.FirstOrDefault(l => string.Equals((l.Purpose ?? string.Empty).Trim(), "vante", StringComparison.OrdinalIgnoreCase));
                if (leituraVante == null || string.IsNullOrWhiteSpace(leituraVante.PontoVisado)) break;

                var proximoNome = leituraVante.PontoVisado.Trim();
                if (visitadas.Contains(proximoNome) || !nomesOcupados.Contains(proximoNome))
                {
                    sequenciaSugerida.Add(proximoNome);
                    break;
                }
                nomeAtual = proximoNome;
            }

            if (sequenciaSugerida.Count > 1)
            {
                SequenciaPoligonal.Clear();
                foreach (var nome in sequenciaSugerida)
                {
                    SequenciaPoligonal.Add(nome);
                }
            }
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
            catch (TopoGente.Core.Entities.DadosInsuficientesException) { /* Ignorar falhas apenas de topologia incompleta no preview */ }
        }

        private MetadadosCenario ColetarMetadadosDaUI()
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
                SequenciaEstacoesSelecionadas = SequenciaPoligonal.ToList()
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

        private void ExtrairCamadasSemanticas(ResultadoLevantamento resultado)
        {
            // Elimina duplicações e ignora espaços em branco/case sensível
            var descricoesUnicas = resultado.Irradiacoes
                .Select(p => string.IsNullOrWhiteSpace(p.Descricao) ? "SEM DESCRIÇÃO" : p.Descricao.Trim().ToUpper())
                .Distinct()
                .OrderBy(d => d);

            CamadasDisponiveis.Clear();
            
            foreach (var desc in descricoesUnicas)
            {
                var filtro = new FiltroCamada { Nome = desc, IsVisivel = true };
                // Liga o evento de mudança do CheckBox à rotina de atualização do Canvas
                filtro.VisibilidadeAlterada += (s, e) => PublicarMalhaFiltrada(); 
                CamadasDisponiveis.Add(filtro);
            }
        }

        private void PublicarMalhaFiltrada()
        {
            if (_resultadoAtual == null) return;

            // Uso de HashSet para performance O(1) na busca das camadas ativas
            var camadasVisiveis = new HashSet<string>(
                CamadasDisponiveis.Where(c => c.IsVisivel).Select(c => c.Nome)
            );

            // Filtra as irradiações (Teoria dos Conjuntos via LINQ)
            var irradiacoesVisiveis = _resultadoAtual.Irradiacoes.Where(p =>
            {
                string chave = string.IsNullOrWhiteSpace(p.Descricao) ? "SEM DESCRIÇÃO" : p.Descricao.Trim().ToUpper();
                return camadasVisiveis.Contains(chave);
            }).ToList();

            // A SANTIDADE DA MALHA: Poligonal é imutável e concatenada de volta às irradiações filtradas
            var malhaRenderizavel = _resultadoAtual.ClonarComFiltro(irradiacoesVisiveis);

            // Publica o novo DTO enxuto para o Canvas desenhar
            _uiEventHub.PublicarResultado(malhaRenderizavel);
        }
    }
}
