using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using TopoGente.Core.Entities;
using TopoGente.UI.Eventing;
using TopoGente.UI.ViewModels;

namespace TopoGente.UI
{
    public partial class CadernetaWindow : MetroWindow
    {
        private readonly IUiEventHub _uiEventHub;

        public CadernetaWindow(IUiEventHub uiEventHub)
        {
            InitializeComponent();

            _uiEventHub = uiEventHub;
            _uiEventHub.EstacoesCarregadas += OnEstacoesCarregadas;
            _uiEventHub.ResultadoAtualizado += OnResultadoAtualizado;

            Closing += CadernetaWindow_Closing;
        }

        public void AtualizarEstacoes(IReadOnlyList<Estacao> estacoes)
        {
            cmbEstacoes.ItemsSource = estacoes;
            cmbEstacoes.SelectedIndex = estacoes.Count > 0 ? 0 : -1;
        }

        public void AtualizarResultados(IEnumerable<PontoCoordenada> pontos)
        {
            gridResultados.ItemsSource = pontos;
        }

        private void OnEstacoesCarregadas(object? sender, EstacoesEventArgs e)
        {
            if (e.Estacoes.Count == 0)
            {
                return;
            }

            if (!IsVisible)
            {
                Show();
            }

            AtualizarEstacoes(e.Estacoes);
        }

        private void OnResultadoAtualizado(object? sender, ResultadoEventArgs e)
        {
            AtualizarResultados(e.Resultado.TodosOsPontos);
        }

        private void CadernetaWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void cmbEstacoes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbEstacoes.SelectedItem is Estacao estacaoSelecionada)
            {
                var viewModels = new ObservableCollection<LeituraViewModel>(
                    estacaoSelecionada.Leituras.Select(l => new LeituraViewModel(l))
                );
                gridCaderneta.ItemsSource = viewModels;
                // Injeção do objeto passivo no campo de cabeçalho
                txtInfoEstacao.DataContext = estacaoSelecionada;
            }
            else
            {
                gridCaderneta.ItemsSource = null;
                txtInfoEstacao.DataContext = null;
            }
        }

        private void gridCaderneta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void gridCaderneta_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Conforme orientação de salvaguarda do PO: forçar commit ou pegar os dados diretos da VM.
                // Como o binding cuida de atualizar o ViewModel, vamos fazer o Dispatcher adiar a execução
                // para logo após o commit real do DataGrid atualizar a UI e ViewModel completamente.
                
                var viewModel = e.Row.Item as LeituraViewModel;
                var estacaoAtual = cmbEstacoes.SelectedItem as Estacao;

                if (viewModel != null && estacaoAtual != null)
                {
                    // Usa dispatcher para garantir que o binding foi resolvido
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _uiEventHub.SolicitarEdicaoLeitura(estacaoAtual, viewModel.Id, viewModel);
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void MenuItem_RemoverLeitura_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Verifica se há uma leitura selecionada no grid e uma estação ativa no ComboBox
            if (gridCaderneta.SelectedItem is LeituraViewModel leituraSelecionadaVM &&
                cmbEstacoes.SelectedItem is Estacao estacaoAtual)
            {
                var leituraOriginal = estacaoAtual.Leituras.FirstOrDefault(l => l.Id == leituraSelecionadaVM.Id);
                if (leituraOriginal == null) return;

                var confirmacao = System.Windows.MessageBox.Show(
                    $"ATENÇÃO: Confirma a exclusão da visada para o alvo '{leituraSelecionadaVM.PontoVisado}'?\n\n" +
                    "Se esta leitura fizer parte do caminhamento principal da poligonal, o cálculo estrutural poderá falhar.",
                    "Auditoria Topológica", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Warning);

                if (confirmacao == System.Windows.MessageBoxResult.Yes)
                {
                    _uiEventHub.SolicitarRemocaoLeitura(estacaoAtual, leituraOriginal);
                }
            }
        }
    }
}