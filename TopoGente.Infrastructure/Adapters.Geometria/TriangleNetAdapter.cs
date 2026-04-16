using System;
using System.Collections.Generic;
using System.Text;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TopoGente.Core.Interfaces;

namespace TopoGente.Infrastructure.Adapters.Geometria
{
    public class TriangleNetAdapter : IMalhaTriagularService
    {
        public List<TrianguloTopografico> GerarMalha(List<PontoCoordenada> nuvemPontos)
        {
            // Implementação do método usando TriangleNet
            return new List<TrianguloTopografico>();
        }
    }
}
