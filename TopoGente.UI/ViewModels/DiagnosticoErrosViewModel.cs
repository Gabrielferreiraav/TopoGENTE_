using System;
using System.Linq;
using System.Collections.Generic;
using TopoGente.Core.Entities;
using TopoGente.UI.Eventing;

namespace TopoGente.UI.ViewModels
{
    public class DiagnosticoErrosViewModel : ObservableObject
    {
        private readonly ResultadoLevantamento _resultado;

        public DiagnosticoErrosViewModel(ResultadoLevantamento resultado)
        {
            _resultado = resultado ?? throw new ArgumentNullException(nameof(resultado));
        }

        public string Status => _resultado.AprovadoNorma ? "✅ APROVADO pela NBR 13.133" : "❌ REPROVADO pela NBR 13.133";
        public double ErroLinear => _resultado.ErroLinear;
        public double ErroFechamentoX => _resultado.ErroFechamentoX;
        public double ErroFechamentoY => _resultado.ErroFechamentoY;

        public string PrecisaoFormatada
        {
            get
            {
                if (_resultado.Precisao <= 0) return "1:∞";
                double denominador = 1.0 / _resultado.Precisao;
                long arredondado = (long)Math.Round(denominador, MidpointRounding.AwayFromZero);
                return $"1:{arredondado:N0}";
            }
        }

        public string ResumoFormatado => $"{Status} | Erro Linear: {ErroLinear:F4}m | Precisão {PrecisaoFormatada}\n" +
                                         $"Erros de Fechamento: ΔX = {ErroFechamentoX:F4}m, ΔY = {ErroFechamentoY:F4}m";

        public IEnumerable<object> ItensDiagnostico
        {
            get
            {
                return _resultado.Poligonal.Select(p => new
                {
                    p.Nome,
                    p.X,
                    p.Y,
                    p.XBruto,
                    p.YBruto,
                    DeltaX = p.XBruto == 0 ? 0 : Math.Round(p.X - p.XBruto, 4),
                    DeltaY = p.YBruto == 0 ? 0 : Math.Round(p.Y - p.YBruto, 4)
                }).ToList();
            }
        }
    }
}
