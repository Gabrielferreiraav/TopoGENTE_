using System.Globalization;
using System.IO;
using System.Text;
using TopoGente.Core.Entities;
using TopoGente.Core.Interfaces;

namespace TopoGente.Infrastructure.Adapters.Exportadores
{
    public class ExportarTxtService : IExportarTxtService
    {
        public void ExportarCoordenadasGestor(ResultadoLevantamento resultado, string caminhoArquivo)
        {
            var ic = CultureInfo.InvariantCulture;
            using var writer = new StreamWriter(caminhoArquivo, append: false, encoding: new UTF8Encoding(false));

            writer.WriteLine("Nome Ponto;X (E) ;Y (N) ;Z (Cota);Descricao");

            foreach (var ponto in resultado.TodosOsPontos)
            {
                var linha = $"{ponto.Nome};{ponto.X.ToString("F3", ic)};{ponto.Y.ToString("F3", ic)};{ponto.Z.ToString("F3", ic)};{ponto.TipoDescricao}";
                writer.WriteLine(linha);
            }
        }

        public void ExportarMemoriaCalculo(ResultadoLevantamento resultado, string caminhoArquivo)
        {
            var ic = CultureInfo.InvariantCulture;

            using var writer = new StreamWriter(caminhoArquivo, append: false, encoding: new UTF8Encoding(false));

            writer.WriteLine("        MEMORIA CALCULO LEVANTAMENTO TOPOGRAFICO        ");
            writer.WriteLine("---------------------------------------------------------");

            writer.WriteLine($"Cenario Processamneto : {resultado.TipoCenario}");
            writer.WriteLine("Perimetro Total Calculado : " + resultado.Perimetro.ToString("F3", ic) + " m");

            writer.WriteLine("ERROS DE FECHAMENTO : ");
            writer.WriteLine($"Erro Angular (eA) : {resultado.ErroAngular.ToString("F4", ic)}º");
            writer.WriteLine($"Erro Linear X (fX) : {resultado.ErroFechamentoX.ToString("F4", ic)}m");
            writer.WriteLine($"Erro Linear Y (fY) : {resultado.ErroFechamentoY.ToString("F4", ic)}m");
            writer.WriteLine($"Erro Altimetrico Z : {resultado.ErroFechamentoZ.ToString("F4", ic)}m");
            writer.WriteLine($"Erro Linear Total : {resultado.ErroLinear.ToString("F4", ic)}m");

            string precisao = resultado.Precisao > 0 ? (1 / resultado.Precisao).ToString("F0", ic) : "∞";
            writer.WriteLine($"Precisao Relativa : 1:{precisao}\n");

            writer.WriteLine("COORDENADAS FINAIS (POLIGONAL )  :  ");
            writer.WriteLine("Nome Ponto;X (E) ;Y (N) ;Z (Cota);Azimute Chegada");

            foreach (var ponto in resultado.Poligonal)
            {
                var linha = $"{ponto.Nome};{ponto.X.ToString("F3", ic)};{ponto.Y.ToString("F3", ic)};{ponto.Z.ToString("F3", ic)};{ponto.AzimuteChegada.ToString("F4", ic)}";
                writer.WriteLine(linha);
            }
        }
    }
}
