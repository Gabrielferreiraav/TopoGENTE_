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

        public ObservableCollection<SequenciaPoligonalViewModel> Poligonais { get; } = new();

        private SequenciaPoligonalViewModel? _poligonalSelecionada;
        public SequenciaPoligonalViewModel? PoligonalSelecionada
        {
            get => _poligonalSelecionada;
            set
            {
                if (SetProperty(ref _poligonalSelecionada, value))
                {
                    (RemoverPoligonalCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (AdicionarSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RemoverSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SubirSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DescerSequenciaCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<string> EstacoesDisponiveis { get; } = new();

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

        private int _formatoArquivoIndex = 0;
        public int FormatoArquivoIndex
        {
            get => _formatoArquivoIndex;
            set => SetProperty(ref _formatoArquivoIndex, value);
        }

        // Commands
        public ICommand CarregarArquivoCommand { get; }
        public ICommand ProcessarCommand { get; }
        public ICommand ExportarTxtCommand { get; }
        public ICommand AdicionarPoligonalCommand { get; }
        public ICommand RemoverPoligonalCommand { get; }
        public ICommand AdicionarSequenciaCommand { get; }
        public ICommand RemoverSequenciaCommand { get; }
        public ICommand SubirSequenciaCommand { get; }
        public ICommand DescerSequenciaCommand { get; }
        public ICommand ExibirCadernetaCommand { get; }
        public ICommand ExibirGraficoCommand { get; }
        public ICommand ExibirDiagnosticoCommand { get; }

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
            
            AdicionarPoligonalCommand = new RelayCommand(OnAdicionarPoligonal);
            RemoverPoligonalCommand = new RelayCommand(OnRemoverPoligonal, _ => PoligonalSelecionada != null && !PoligonalSelecionada.EhPrincipal);
            
            AdicionarSequenciaCommand = new RelayCommand(OnAdicionarSequencia, _ => PoligonalSelecionada != null);
            RemoverSequenciaCommand = new RelayCommand(OnRemoverSequencia, _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));
            SubirSequenciaCommand = new RelayCommand(OnSubirSequencia, _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));
            DescerSequenciaCommand = new RelayCommand(OnDescerSequencia, _ => PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada));

            ExibirCadernetaCommand = new RelayCommand(_ => AbrirJanela<CadernetaWindow>());
            ExibirGraficoCommand = new RelayCommand(OnExibirGrafico);
            ExibirDiagnosticoCommand = new RelayCommand(OnExibirDiagnostico);

            // Adiciona a poligonal principal por padrão
            var principal = new SequenciaPoligonalViewModel { Nome = "Poligonal Principal", EhPrincipal = true };
            principal.PropertyChanged += (s, e) => {
                (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
            Poligonais.Add(principal);
            PoligonalSelecionada = principal;
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

                    if (_estacoesEmMemoria.Count > 0 && Poligonais.Count > 0)
                    {
                        var primeiraEstacao = _estacoesEmMemoria[0];
                        var polPrincipal = Poligonais[0];

                        if (primeiraEstacao.CoordenadaConhecida != null)
                        {
                            polPrincipal.PartidaX = primeiraEstacao.CoordenadaConhecida.X.ToString("F3");
                            polPrincipal.PartidaY = primeiraEstacao.CoordenadaConhecida.Y.ToString("F3");
                            polPrincipal.PartidaZ = primeiraEstacao.CoordenadaConhecida.Z.ToString("F3");
                        }
                        else
                        {
                            polPrincipal.PartidaX = "0.000"; polPrincipal.PartidaY = "0.000"; polPrincipal.PartidaZ = "0.000";
                        }

                        var leituraRe = primeiraEstacao.Leituras?.FirstOrDefault(l => string.Equals((l.Purpose ?? string.Empty).Trim(), "re", StringComparison.OrdinalIgnoreCase));
                        if (leituraRe != null)
                        {
                            polPrincipal.NomeRe = leituraRe.PontoVisado ?? string.Empty;
                            if (!string.IsNullOrEmpty(leituraRe.PontoVisado) && resultadoLeitura.PontosConhecidosGlobais.TryGetValue(leituraRe.PontoVisado, out var coordenadaRe))
                            {
                                polPrincipal.ReX = coordenadaRe.X.ToString("F3");
                                polPrincipal.ReY = coordenadaRe.Y.ToString("F3");
                                polPrincipal.ReZ = coordenadaRe.Z.ToString("F3");
                                polPrincipal.UsarCoordenadaRe = true;
                            }
                            else
                            {
                                polPrincipal.UsarAzimute = true;
                                polPrincipal.Azimute = leituraRe.AnguloHorizontal.ToString("F4");
                            }
                        }
                    }

                    _uiEventHub.PublicarEstacoes(_estacoesEmMemoria);
                    PublicarEsbocoGeodesicoSobDemanda();
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
            if (Poligonais.Count == 0) return false;

            foreach (var pol in Poligonais)
            {
                if (!ValidarCoordenadas(pol.PartidaX, pol.PartidaY, pol.PartidaZ)) return false;

                if (pol.UsarAzimute)
                {
                    if (string.IsNullOrWhiteSpace(pol.Azimute)) return false;
                }
                else
                {
                    if (!ValidarCoordenadas(pol.ReX, pol.ReY, pol.ReZ)) return false;
                }

                if (pol.CenarioIndex == 0) // Enquadrada
                {
                    if (!ValidarCoordenadas(pol.ChegadaX, pol.ChegadaY, pol.ChegadaZ)) return false;
                }

                if (pol.Estacoes.Count == 0) return false;
            }

            return true;
        }

        private void ProcessamentoSilencioso()
        {
            var listaSequencias = Poligonais.Select(p => p.ToEntity()).ToList();
            
            var pontosConhecidos = _estacoesEmMemoria
                .Where(e => e.CoordenadaConhecida != null)
                .Select(e => e.CoordenadaConhecida!)
                .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var principal = listaSequencias.FirstOrDefault(p => p.EhPrincipal);
            if (principal != null)
            {
                _classificadorGrafo.ClassificarArestasGrafo(_estacoesEmMemoria, principal.Metadados);
            }
            
            // Opcional: Se houver falha de validação primária, aborte silenciosamente
            var leiturasClassificadas = _estacoesEmMemoria.SelectMany(e => e.Leituras).ToList();
            if (leiturasClassificadas.Any(l => (l.Tipo == TipoLeitura.Poligonal || l.Tipo == TipoLeitura.Re) && !LeituraValidator.Validar(l).IsValid))
            {
                PublicarEsbocoGeodesicoSobDemanda();
                return; 
            }

            _resultadoAtual = _processadorService.Processar(listaSequencias, _estacoesEmMemoria, pontosConhecidos);
            var relatorioQa = _qaCheckService.GerarRelatorioQaChecks(_estacoesEmMemoria, _resultadoAtual, pontosConhecidos);

            _uiEventHub.PublicarResultado(_resultadoAtual);
            (ExportarTxtCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        private void OnExibirGrafico(object? parameter)
        {
            var window = new VisualizacaoWindow(_uiEventHub);
            window.ShowDialog();
        }

        private void OnExibirDiagnostico(object? parameter)
        {
            if (_resultadoAtual == null) return;
            var window = new DiagnosticoErrosWindow(_resultadoAtual);
            window.ShowDialog();
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

        private void OnAdicionarPoligonal(object? parameter)
        {
            var sec = new SequenciaPoligonalViewModel { Nome = $"Ramal Secundário {Poligonais.Count}" };
            sec.PropertyChanged += (s, e) => {
                (ProcessarCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
            Poligonais.Add(sec);
            PoligonalSelecionada = sec;
            PublicarEsbocoGeodesicoSobDemanda();
        }

        private void OnRemoverPoligonal(object? parameter)
        {
            if (PoligonalSelecionada != null && !PoligonalSelecionada.EhPrincipal)
            {
                Poligonais.Remove(PoligonalSelecionada);
                PoligonalSelecionada = Poligonais.FirstOrDefault();
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnAdicionarSequencia(object? parameter)
        {
            if (PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoDisponivelSelecionada))
            {
                PoligonalSelecionada.Estacoes.Add(EstacaoDisponivelSelecionada);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnRemoverSequencia(object? parameter)
        {
            if (PoligonalSelecionada != null && !string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada))
            {
                PoligonalSelecionada.Estacoes.Remove(EstacaoSequenciaSelecionada);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnSubirSequencia(object? parameter)
        {
            if (PoligonalSelecionada == null || string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;
            var index = PoligonalSelecionada.Estacoes.IndexOf(EstacaoSequenciaSelecionada);
            if (index > 0)
            {
                PoligonalSelecionada.Estacoes.Move(index, index - 1);
                PublicarEsbocoGeodesicoSobDemanda();
            }
        }

        private void OnDescerSequencia(object? parameter)
        {
            if (PoligonalSelecionada == null || string.IsNullOrWhiteSpace(EstacaoSequenciaSelecionada)) return;
            var index = PoligonalSelecionada.Estacoes.IndexOf(EstacaoSequenciaSelecionada);
            if (index >= 0 && index < PoligonalSelecionada.Estacoes.Count - 1)
            {
                PoligonalSelecionada.Estacoes.Move(index, index + 1);
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

            if (_estacoesEmMemoria.Count == 0 || nomesOcupados.Count == 0 || Poligonais.Count == 0) return;

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
                var polPrincipal = Poligonais[0];
                polPrincipal.Estacoes.Clear();
                foreach (var nome in sequenciaSugerida)
                {
                    polPrincipal.Estacoes.Add(nome);
                }
            }
        }

        private void PublicarEsbocoGeodesicoSobDemanda()
        {
            if (_estacoesEmMemoria == null || _estacoesEmMemoria.Count == 0) return;
            try
            {
                var listaSequencias = Poligonais.Select(p => p.ToEntity()).ToList();
                var dtoPreliminar = _processadorService.GerarEsbocoBruto(listaSequencias, _estacoesEmMemoria);
                _uiEventHub.PublicarResultado(dtoPreliminar);
            }
            catch (TopoGente.Core.Entities.DadosInsuficientesException) { /* Ignorar falhas apenas de topologia incompleta no preview */ }
            catch (FormatException) { /* Ignorar erros de parse durante edição live */ }
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

    }
}
