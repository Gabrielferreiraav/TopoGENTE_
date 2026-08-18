using System.Collections.Generic;
using TopoGENTE.Domain.ValueObjects;

namespace TopoGENTE.Domain.Ports;

/// <summary>
/// Porta de domínio que dita as capacidades de processamento topográfico avançado e análise contínua do MDT.
/// </summary>
public interface ITopographicAnalytics
{
    /// <summary>
    /// Processa a interpolação exata da cota (Z) de um dado ponto sobre a malha de triângulos,
    /// utilizando o método avançado dos vizinhos naturais de Sibson (Natural Neighbor Interpolation).
    /// Este método resolve singularidades em MDTs esparsos que as interpolações lineares convencionais falham em cobrir.
    /// </summary>
    /// <param name="easting">Coordenada cartesiana E (X).</param>
    /// <param name="northing">Coordenada cartesiana N (Y).</param>
    /// <returns>A elevação exata interpolada matematicamente.</returns>
    double InterpolateExactElevationUsingSibson(double easting, double northing);

    /// <summary>
    /// Varre e extrai o mapa isohípsico (curvas de nível) fatiando matematicamente o relevo.
    /// </summary>
    /// <param name="stepInterval">Equidistância requerida entre as linhas de contorno (ex: 1.0 metro).</param>
    /// <param name="anchorElevation">Cota plana de amarração base para sincronização rítmica das quebras.</param>
    /// <returns>Coleção de curvas de nível lazy-evaluated prontas para serem renderizadas ou exportadas.</returns>
    IEnumerable<Isoline> ComputeContourMap(double stepInterval, double anchorElevation);
}
