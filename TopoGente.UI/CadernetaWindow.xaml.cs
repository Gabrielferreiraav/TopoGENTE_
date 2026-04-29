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
                txtInfoEstacao.Text = $"Altura do Instrumento: {estacaoSelecionada.AlturaInstrumento:F3} m";
            }
            else
            {
                gridCaderneta.ItemsSource = null;
                txtInfoEstacao.Text = "Hi: -";
            }
        }

        private void gridCaderneta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}