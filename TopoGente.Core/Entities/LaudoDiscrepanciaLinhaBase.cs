using System;

namespace TopoGente.Core.Entities
{
    /// <summary>
    /// DTO imutável que encapsula as métricas de discrepância de uma verificação de linha de base.
    /// Produzido pela <see cref="Strategies.VerificacaoLinhaBaseStrategy"/>.
    /// </summary>
    public sealed record LaudoDiscrepanciaLinhaBase
    {
        // ── Identificação da base ──────────────────────────────────────────────────

        /// <summary>Nome do ponto de partida conhecido (ex: "E1").</summary>
        public string NomePontoPartida { get; init; } = string.Empty;

        /// <summary>Nome do ponto de chegada conhecido (ex: "E4").</summary>
        public string NomePontoChegada { get; init; } = string.Empty;

        // ── Coordenadas teóricas e medidas ────────────────────────────────────────

        /// <summary>Distância teórica (calculada por coordenadas) entre partida e chegada, em metros.</summary>
        public double DistanciaTeórica { get; init; }

        /// <summary>Azimute teórico da base, em graus decimais.</summary>
        public double AzimuteTeóricoGraus { get; init; }

        /// <summary>Azimute medido (transportado analiticamente), em graus decimais.</summary>
        public double AzimuteMedidoGraus { get; init; }

        // ── Componentes de discrepância ───────────────────────────────────────────

        /// <summary>Discrepância em E (Leste/X): E_calc − E_obs, em metros.</summary>
        public double DeltaEsteMetros { get; init; }

        /// <summary>Discrepância em N (Norte/Y): N_calc − N_obs, em metros.</summary>
        public double DeltaNorteMetros { get; init; }

        /// <summary>Discrepância em Z (cota): Z_calc − Z_obs, em metros.</summary>
        public double DeltaZMetros { get; init; }

        // ── Métricas derivadas ────────────────────────────────────────────────────

        /// <summary>
        /// Erro escalar linear total (planimetria + altimetria), em milímetros:
        /// e_l = sqrt(ΔE² + ΔN² + ΔZ²) × 1000
        /// </summary>
        public double ErroLinearMm { get; init; }

        /// <summary>
        /// Erro angular de fechamento, em segundos de arco:
        /// e_a = |Az_teor − Az_med| × 3600
        /// </summary>
        public double ErroAngularSegundos { get; init; }

        /// <summary>
        /// Precisão relativa da base: e_l_m / distância_teórica (1 : P).
        /// Calculada apenas com a componente planar (ΔE, ΔN).
        /// </summary>
        public double PrecisaoRelativa { get; init; }

        // ── Tolerâncias aplicadas ─────────────────────────────────────────────────

        /// <summary>Tolerância linear admissível, em milímetros.</summary>
        public double ToleranciaLinearMm { get; init; }

        /// <summary>Tolerância angular admissível, em segundos de arco.</summary>
        public double ToleranciaAngularSegundos { get; init; }

        // ── Status de aprovação ────────────────────────────────────────────────────

        /// <summary>True se ambos e_l ≤ T_l e e_a ≤ T_a.</summary>
        public bool Aprovado { get; init; }

        /// <summary>Motivo de reprovação, caso <see cref="Aprovado"/> seja false.</summary>
        public string MotivoReprovacao { get; init; } = string.Empty;

        // ── Formatação padronizada ────────────────────────────────────────────────

        /// <summary>Formata o erro linear no padrão "+12.4 mm" ou "−3.1 mm".</summary>
        public string ErroLinearFormatado
            => $"{(ErroLinearMm >= 0 ? "+" : "")}{ErroLinearMm:F1} mm";

        /// <summary>Formata o erro angular no padrão "06.5\"" (segundos com 1 decimal).</summary>
        public string ErroAngularFormatado
            => $"{ErroAngularSegundos:F1}\"";

        /// <summary>Formata a precisão relativa no padrão "1 : 25.400".</summary>
        public string PrecisaoRelativaFormatada
        {
            get
            {
                if (PrecisaoRelativa <= 0) return "1 : ∞";
                double denominador = 1.0 / PrecisaoRelativa;
                return $"1 : {denominador:N0}";
            }
        }

        /// <summary>Formata ΔE em milímetros com sinal.</summary>
        public string DeltaEsteMmFormatado
            => $"{DeltaEsteMetros * 1000.0:+0.0;-0.0} mm";

        /// <summary>Formata ΔN em milímetros com sinal.</summary>
        public string DeltaNorteMmFormatado
            => $"{DeltaNorteMetros * 1000.0:+0.0;-0.0} mm";

        /// <summary>Formata ΔZ em milímetros com sinal.</summary>
        public string DeltaZMmFormatado
            => $"{DeltaZMetros * 1000.0:+0.0;-0.0} mm";
    }
}
