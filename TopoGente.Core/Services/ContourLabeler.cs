using System;
using System.Globalization;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGente.Core.Services;

/// <summary>
/// Serviço de domínio responsável por analisar o traçado de isolinhas (curvas de nível)
/// e determinar a posição de inserção, o ângulo de tangência e a formatação do rótulo altimétrico.
/// </summary>
public static class ContourLabeler
{
    private const double Epsilon = 1e-6;

    /// <summary>
    /// Calcula a posição espacial e a rotação ideal para rotulagem altimétrica de uma curva de nível mestra.
    /// Seleciona o segmento reto mais longo para ancorar o rótulo no seu ponto médio,
    /// alinhando a rotação tangencial no intervalo [-90°, +90°] para garantir leitura direta no CAD.
    /// </summary>
    /// <param name="isoline">Isolinha a ser rotulada.</param>
    /// <returns>Tupla contendo as coordenadas (X, Y, Z), o ângulo de rotação em graus e o texto formatado (F2), ou null caso degenerada.</returns>
    public static (double X, double Y, double Z, double RotationAngle, string Text)? ObterRotuloCurvaMestra(Isoline isoline)
    {
        var vertices = isoline.Vertices.Span;
        if (vertices.Length < 2)
            return null;

        double maxDistSq = -1.0;
        int melhorIndice = -1;

        for (int i = 0; i < vertices.Length - 1; i++)
        {
            double dx = vertices[i + 1].X - vertices[i].X;
            double dy = vertices[i + 1].Y - vertices[i].Y;
            double distSq = dx * dx + dy * dy;

            if (distSq > maxDistSq)
            {
                maxDistSq = distSq;
                melhorIndice = i;
            }
        }

        if (melhorIndice == -1 || maxDistSq < Epsilon)
            return null;

        var p1 = vertices[melhorIndice];
        var p2 = vertices[melhorIndice + 1];

        // 1. Ponto médio do segmento retilíneo de maior comprimento
        double midX = (p1.X + p2.X) * 0.5;
        double midY = (p1.Y + p2.Y) * 0.5;
        double midZ = isoline.Elevation;

        // 2. Azimute do vetor tangente no plano XY
        double deltaX = p2.X - p1.X;
        double deltaY = p2.Y - p1.Y;
        double thetaRad = Math.Atan2(deltaY, deltaX);
        double thetaGraus = thetaRad * (180.0 / Math.PI);

        // 3. Normalização no intervalo [-90°, +90°] (impede texto de cabeça para baixo segundo a NBR 13133)
        while (thetaGraus > 90.0)
            thetaGraus -= 180.0;
        while (thetaGraus < -90.0)
            thetaGraus += 180.0;

        // 4. Formatação decimal universal
        string texto = isoline.Elevation.ToString("F2", CultureInfo.InvariantCulture);

        return (midX, midY, midZ, thetaGraus, texto);
    }
}
