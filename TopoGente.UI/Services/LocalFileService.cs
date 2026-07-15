using System.IO;

namespace TopoGente.UI.Services
{
    public class LocalFileService : IFileService
    {
        public string[] LerLinhas(string caminho)
        {
            return File.ReadAllLines(caminho);
        }
    }
}
