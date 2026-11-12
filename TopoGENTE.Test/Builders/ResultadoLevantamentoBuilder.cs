using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;

namespace TopoGente.Test.Builders;

/// <summary>
/// Test Data Builder para construção fluente de instâncias de <see cref="ResultadoLevantamento"/>.
/// Elimina Setup Pollution e Mystery Guest nos testes de integridade e desempenho (Nat Pryce / GOOS).
/// </summary>
public sealed class ResultadoLevantamentoBuilder
{
    private readonly List<PontoCoordenada> _poligonal = new();
    private readonly List<PontoCoordenada> _irradiacoes = new();
    private bool _aprovadoNorma = true;

    public ResultadoLevantamentoBuilder ComPoligonal(params string[] nomes)
    {
        for (int i = 0; i < nomes.Length; i++)
        {
            _poligonal.Add(new PontoCoordenada
            {
                Nome = nomes[i],
                X = 1000.0 + (i * 50.0),
                Y = 2000.0 + (i * 50.0),
                Z = 500.0 + (i * 0.5),
                EhPontoPoligonal = true,
                Descricao = "V"
            });
        }
        return this;
    }

    public ResultadoLevantamentoBuilder ComIrradiacao(params string[] nomes)
    {
        for (int i = 0; i < nomes.Length; i++)
        {
            _irradiacoes.Add(new PontoCoordenada
            {
                Nome = nomes[i],
                X = 1050.0 + (i * 10.0),
                Y = 2050.0 + (i * 10.0),
                Z = 502.0 + (i * 0.2),
                EhPontoPoligonal = false,
                Descricao = "MF"
            });
        }
        return this;
    }

    public ResultadoLevantamentoBuilder ComMalhaSintetica(int total)
    {
        for (int i = 0; i < total; i++)
        {
            _poligonal.Add(new PontoCoordenada
            {
                Nome = $"V{i}",
                X = 100.0 + i,
                Y = 200.0 + i,
                Z = 10.0 + (i * 0.1),
                EhPontoPoligonal = true
            });
        }
        return this;
    }

    public ResultadoLevantamentoBuilder ComStatusAprovacao(bool aprovado)
    {
        _aprovadoNorma = aprovado;
        return this;
    }

    public ResultadoLevantamento Build() => new()
    {
        Poligonal = _poligonal,
        Irradiacoes = _irradiacoes,
        AprovadoNorma = _aprovadoNorma
    };
}
