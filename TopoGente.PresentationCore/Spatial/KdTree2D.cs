using System;
using System.Collections.Generic;

namespace TopoGente.UI.Spatial
{
    /// <summary>
    /// Nó estrutural de valor para armazenamento contíguo.
    /// Evita overhead de objetos na Heap (Geração 2 / LOH) garantindo localidade de cache.
    /// </summary>
    public readonly struct KdNode
    {
        public readonly double X;
        public readonly double Y;
        public readonly int DomainId;

        public KdNode(double x, double y, int domainId)
        {
            X = x;
            Y = y;
            DomainId = domainId;
        }
    }

    /// <summary>
    /// Árvore KD-2D para busca de vizinho mais próximo em espaço geodésico bidimensional.
    /// Operações de busca ocorrem em tempo logarítmico estrito O(log N).
    /// </summary>
    public sealed class KdTree2D
    {
        private readonly KdNode[] _nodes;

        /// <summary>
        /// Constrói a árvore a partir de um conjunto de pontos projetados.
        /// O custo de construção é O(N log N).
        /// </summary>
        public KdTree2D(KdNode[] projectedNodes)
        {
            if (projectedNodes == null || projectedNodes.Length == 0)
            {
                _nodes = Array.Empty<KdNode>();
                return;
            }

            // A árvore toma propriedade do array e o ordena in-place
            _nodes = projectedNodes;
            BuildTree(0, _nodes.Length - 1, 0);
        }

        private void BuildTree(int left, int right, int depth)
        {
            if (left >= right) return;

            int axis = depth % 2;
            int medianIndex = left + (right - left) / 2;

            QuickSelect(left, right, medianIndex, axis);

            BuildTree(left, medianIndex - 1, depth + 1);
            BuildTree(medianIndex + 1, right, depth + 1);
        }

        /// <summary>
        /// Localiza o vértice mais próximo dentro do espaço métrico atual (Geodésico/Pixel).
        /// Requer que (targetX, targetY) esteja no mesmo sistema de coordenadas que os nós internos.
        /// </summary>
        public KdNode? FindNearest(double targetX, double targetY, double radiusTolerance)
        {
            if (_nodes.Length == 0) return null;

            KdNode? bestMatch = null;
            double bestDistSq = radiusTolerance * radiusTolerance;

            SearchNearest(0, _nodes.Length - 1, 0, targetX, targetY, ref bestMatch, ref bestDistSq);

            return bestMatch;
        }

        private void SearchNearest(int left, int right, int depth, double targetX, double targetY, ref KdNode? bestMatch, ref double bestDistSq)
        {
            if (left > right) return;

            int medianIndex = left + (right - left) / 2;
            KdNode node = _nodes[medianIndex];

            double distSq = DistanceSquared(node.X, node.Y, targetX, targetY);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestMatch = node;
            }

            int axis = depth % 2;
            double nodeAxisVal = axis == 0 ? node.X : node.Y;
            double targetAxisVal = axis == 0 ? targetX : targetY;
            double axisDist = targetAxisVal - nodeAxisVal;
            
            int firstLeft = left;
            int firstRight = medianIndex - 1;
            int secondLeft = medianIndex + 1;
            int secondRight = right;

            if (axisDist > 0)
            {
                // Alvo está à direita, procura na subárvore direita primeiro
                firstLeft = medianIndex + 1;
                firstRight = right;
                secondLeft = left;
                secondRight = medianIndex - 1;
            }

            // Procura na primeira partição
            SearchNearest(firstLeft, firstRight, depth + 1, targetX, targetY, ref bestMatch, ref bestDistSq);

            // Se o cilindro de busca interceptar o plano de corte, investiga a outra partição
            if ((axisDist * axisDist) < bestDistSq)
            {
                SearchNearest(secondLeft, secondRight, depth + 1, targetX, targetY, ref bestMatch, ref bestDistSq);
            }
        }

        private void QuickSelect(int left, int right, int k, int axis)
        {
            while (left < right)
            {
                int pivotIndex = Partition(left, right, axis);
                if (pivotIndex == k)
                {
                    return;
                }
                if (k < pivotIndex)
                {
                    right = pivotIndex - 1;
                }
                else
                {
                    left = pivotIndex + 1;
                }
            }
        }

        private int Partition(int left, int right, int axis)
        {
            int mid = left + (right - left) / 2;

            double valLeft = axis == 0 ? _nodes[left].X : _nodes[left].Y;
            double valMid = axis == 0 ? _nodes[mid].X : _nodes[mid].Y;
            double valRight = axis == 0 ? _nodes[right].X : _nodes[right].Y;

            if (valLeft > valMid)
            {
                Swap(left, mid);
                double temp = valLeft; valLeft = valMid; valMid = temp;
            }
            if (valLeft > valRight)
            {
                Swap(left, right);
                double temp = valLeft; valLeft = valRight; valRight = temp;
            }
            if (valMid > valRight)
            {
                Swap(mid, right);
                double temp = valMid; valMid = valRight; valRight = temp;
            }

            Swap(mid, right);

            KdNode pivotNode = _nodes[right];
            double pivotValue = axis == 0 ? pivotNode.X : pivotNode.Y;
            
            int i = left;
            for (int j = left; j < right; j++)
            {
                double val = axis == 0 ? _nodes[j].X : _nodes[j].Y;
                if (val < pivotValue)
                {
                    Swap(i, j);
                    i++;
                }
            }
            Swap(i, right);
            return i;
        }

        private void Swap(int i, int j)
        {
            KdNode temp = _nodes[i];
            _nodes[i] = _nodes[j];
            _nodes[j] = temp;
        }

        private static double DistanceSquared(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return dx * dx + dy * dy;
        }
    }
}
