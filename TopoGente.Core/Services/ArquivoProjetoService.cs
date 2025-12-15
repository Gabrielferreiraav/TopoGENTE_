using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TopoGente.Core.Entities;


namespace TopoGente.Core.Services
{
    public class ArquivoProjetoService
    {
        public void SalvarProjeto(ProjetoTopo projeto, string caminhoArquivo)
        {
            var opcoesJson = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string jsonString = JsonSerializer.Serialize(projeto, opcoesJson);
            File.WriteAllText(caminhoArquivo, jsonString);
        }

        public ProjetoTopo CarregarProjeto(string caminhoArquivo)
        {
            if (!File.Exists(caminhoArquivo))
            {
                throw new FileNotFoundException("O arquivo de projeto não foi encontrado.", caminhoArquivo);
            }
            string jsonString = File.ReadAllText(caminhoArquivo);
            var projetoCarregado = JsonSerializer.Deserialize<ProjetoTopo>(jsonString);

            if (projetoCarregado == null)
            {
                throw new InvalidOperationException("Falha ao desserializar o arquivo de projeto.");
            }
            return projetoCarregado;
        }
    }
}
