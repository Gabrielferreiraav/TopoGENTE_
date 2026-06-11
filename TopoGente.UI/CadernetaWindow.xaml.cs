using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using TopoGente.Core.Entities;
using TopoGente.UI.Eventing;

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

        public void AtualizarResultados(IReadOnlyList<PontoCoordenada> pontos)
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
                gridCaderneta.ItemsSource = estacaoSelecionada.Leituras;
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

        private void MenuItem_RemoverLeitura_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Verifica se há uma leitura selecionada no grid e uma estação ativa no ComboBox
            if (gridCaderneta.SelectedItem is LeituraEstacaoTotal leituraSelecionada &&
                cmbEstacoes.SelectedItem is Estacao estacaoAtual)
            {
                var confirmacao = System.Windows.MessageBox.Show(
                    $"ATENÇÃO: Confirma a exclusão da visada para o alvo '{leituraSelecionada.PontoVisado}'?\n\n" +
                    "Se esta leitura fizer parte do caminhamento principal da poligonal, o cálculo estrutural poderá falhar.",
                    "Auditoria Topológica", 
                    System.Windows.MessageBoxButton.YesNo, 
                    System.Windows.MessageBoxImage.Warning);

                if (confirmacao == System.Windows.MessageBoxResult.Yes)
                {
                    // Mutação direta na raiz de agregação (Domínio)
                    estacaoAtual.Leituras.Remove(leituraSelecionada);

                    // Restabelecimento da sincronia de renderização
                    // Como estamos usando List<T> no domínio e não ObservableCollection, o Refresh é obrigatório.
                    gridCaderneta.Items.Refresh();
                }
            }
        }
    }
}