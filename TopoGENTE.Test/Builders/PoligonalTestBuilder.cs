using System;
using System.Collections.Generic;
using System.Linq;
using TopoGente.Core.Entities;
using TopoGente.Core.Strategies;

namespace TopoGENTE.Test.Builders;

/// <summary>
/// Test Data Builder para construção fluente de cenários topográficos (Poligonais, Estações e Visadas).
/// Elimina os smells de Setup Pollution e General Fixture nos testes de ajustamento.
/// </summary>
public class PoligonalTestBuilder
{
    private TipoCenarioPoligonal _tipoCenario = TipoCenarioPoligonal.Fechada;
    private string _nomePartida = "E0";
    private (double X, double Y, double Z) _coordPartida = (0, 0, 100);
    private string? _nomeChegada;
    private (double X, double Y, double Z)? _coordChegada;
    private double _azimutePartida = 0.0;
    private double? _azimuteChegada;
    private ParametrosTolerancia _tolerancias = ParametrosTolerancia.Padrao;

    private readonly List<Estacao> _estacoes = new();
    private readonly Dictionary<string, PontoCoordenada> _pontosConhecidos = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _sequenciaEstacoes = new();

    private Estacao? _estacaoCorrente;

    public PoligonalTestBuilder ComoFechada(
        string nomePartida = "E0", 
        double x = 0, 
        double y = 0, 
        double z = 100, 
        double azimutePartida = 0)
    {
        _tipoCenario = TipoCenarioPoligonal.Fechada;
        _nomePartida = nomePartida;
        _coordPartida = (x, y, z);
        _azimutePartida = azimutePartida;
        ComPontoConhecido(nomePartida, x, y, z);
        return this;
    }

    public PoligonalTestBuilder ComoEnquadrada(
        string nomePartida, (double X, double Y, double Z) partida,
        string nomeChegada, (double X, double Y, double Z) chegada,
        double azimutePartida = 0)
    {
        _tipoCenario = TipoCenarioPoligonal.Enquadrada;
        _nomePartida = nomePartida;
        _coordPartida = partida;
        _nomeChegada = nomeChegada;
        _coordChegada = chegada;
        _azimutePartida = azimutePartida;
        ComPontoConhecido(nomePartida, partida.X, partida.Y, partida.Z);
        ComPontoConhecido(nomeChegada, chegada.X, chegada.Y, chegada.Z);
        return this;
    }

    public PoligonalTestBuilder ComoAbertaOrientada(
        string nomePartida, 
        double x, 
        double y, 
        double z = 0, 
        double azimutePartida = 0)
    {
        _tipoCenario = TipoCenarioPoligonal.AbertaOrientada;
        _nomePartida = nomePartida;
        _coordPartida = (x, y, z);
        _azimutePartida = azimutePartida;
        ComPontoConhecido(nomePartida, x, y, z);
        return this;
    }

    public PoligonalTestBuilder ComTolerancias(ParametrosTolerancia tolerancias)
    {
        _tolerancias = tolerancias;
        return this;
    }

    public PoligonalTestBuilder ComAzimuteChegada(double azimuteChegada)
    {
        _azimuteChegada = azimuteChegada;
        return this;
    }

    public PoligonalTestBuilder ComPontoConhecido(string nome, double x, double y, double z)
    {
        _pontosConhecidos[nome] = new PontoCoordenada { Nome = nome, X = x, Y = y, Z = z, EhPontoPoligonal = true };
        return this;
    }

    public PoligonalTestBuilder ComEstacao(string nome, double hi = 1.50)
    {
        _estacaoCorrente = new Estacao { Nome = nome, AlturaInstrumento = hi };
        _estacoes.Add(_estacaoCorrente);
        if (!_sequenciaEstacoes.Contains(nome))
        {
            _sequenciaEstacoes.Add(nome);
        }
        return this;
    }

    public PoligonalTestBuilder VisandoVante(
        string pontoVisado, 
        double anguloHz, 
        double distanciaInclinada, 
        double anguloVt = 90.0, 
        double hp = 1.50)
    {
        GarantirEstacaoCorrente();
        _estacaoCorrente!.AdicionarVisada(new LeituraEstacaoTotal
        {
            PontoVisado = pontoVisado,
            Tipo = TipoLeitura.Poligonal,
            Purpose = "vante",
            AnguloHorizontal = anguloHz,
            AnguloVertical = anguloVt,
            DistanciaInclinada = distanciaInclinada,
            AlturaInstrumento = _estacaoCorrente.AlturaInstrumento,
            AlturaPrisma = hp
        });
        return this;
    }

    public PoligonalTestBuilder VisandoRe(
        string pontoVisado, 
        double anguloHz = 0.0, 
        double distanciaInclinada = 100.0, 
        double anguloVt = 90.0, 
        double hp = 1.50)
    {
        GarantirEstacaoCorrente();
        _estacaoCorrente!.AdicionarVisada(new LeituraEstacaoTotal
        {
            PontoVisado = pontoVisado,
            Tipo = TipoLeitura.Re,
            Purpose = "re",
            AnguloHorizontal = anguloHz,
            AnguloVertical = anguloVt,
            DistanciaInclinada = distanciaInclinada,
            AlturaInstrumento = _estacaoCorrente.AlturaInstrumento,
            AlturaPrisma = hp
        });
        return this;
    }

    public PoligonalTestBuilder ComIrradiacao(
        string pontoVisado, 
        double anguloHz, 
        double distanciaInclinada, 
        double anguloVt = 90.0, 
        double hp = 1.50, 
        string? descricao = null)
    {
        GarantirEstacaoCorrente();
        _estacaoCorrente!.AdicionarVisada(new LeituraEstacaoTotal
        {
            PontoVisado = pontoVisado,
            Tipo = TipoLeitura.Irradiacao,
            AnguloHorizontal = anguloHz,
            AnguloVertical = anguloVt,
            DistanciaInclinada = distanciaInclinada,
            AlturaInstrumento = _estacaoCorrente.AlturaInstrumento,
            AlturaPrisma = hp,
            Observacao = descricao ?? string.Empty
        });
        return this;
    }

    public PoligonalTestBuilder ComSequencia(params string[] nomesEstacoes)
    {
        _sequenciaEstacoes.Clear();
        _sequenciaEstacoes.AddRange(nomesEstacoes);
        return this;
    }

    public (List<SequenciaPoligonal> Sequencias, List<Estacao> Estacoes, Dictionary<string, PontoCoordenada> PontosConhecidos) Build()
    {
        var seqFinal = new List<string>(_sequenciaEstacoes);
        if (_tipoCenario == TipoCenarioPoligonal.Fechada && seqFinal.Count > 0 && seqFinal[^1] != seqFinal[0])
        {
            seqFinal.Add(seqFinal[0]);
        }

        var metadados = new MetadadosCenario
        {
            TipoCenario = _tipoCenario,
            PartidaX = _coordPartida.X,
            PartidaY = _coordPartida.Y,
            PartidaZ = _coordPartida.Z,
            AzimutePartida = _azimutePartida,
            NomeChegada = _nomeChegada,
            ChegadaX = _coordChegada?.X,
            ChegadaY = _coordChegada?.Y,
            ChegadaZ = _coordChegada?.Z,
            Tolerancias = _tolerancias,
            SequenciaEstacoesSelecionadas = seqFinal
        };

        var sequencia = new SequenciaPoligonal
        {
            Nome = "Poligonal Principal de Teste",
            EhPrincipal = true,
            Metadados = metadados,
            Estacoes = seqFinal
        };

        return (new List<SequenciaPoligonal> { sequencia }, _estacoes, _pontosConhecidos);
    }

    public List<Estacao> BuildEstacoes() => _estacoes;

    public Dictionary<string, PontoCoordenada> BuildPontosConhecidos() => _pontosConhecidos;

    public SequenciaPoligonal BuildSequencia(string nome, bool ehPrincipal, string? ancoragem = null, List<string>? estacoes = null)
    {
        var seqEstacoes = estacoes ?? new List<string>(_sequenciaEstacoes);
        var metadados = new MetadadosCenario
        {
            TipoCenario = _tipoCenario,
            PartidaX = _coordPartida.X,
            PartidaY = _coordPartida.Y,
            PartidaZ = _coordPartida.Z,
            AzimutePartida = _azimutePartida,
            NomeChegada = _nomeChegada,
            ChegadaX = _coordChegada?.X,
            ChegadaY = _coordChegada?.Y,
            ChegadaZ = _coordChegada?.Z,
            Tolerancias = _tolerancias,
            SequenciaEstacoesSelecionadas = seqEstacoes
        };

        return new SequenciaPoligonal
        {
            Nome = nome,
            EhPrincipal = ehPrincipal,
            EstacaoAncoragemNome = ancoragem,
            Metadados = metadados,
            Estacoes = seqEstacoes
        };
    }

    public CompensacaoPoligonalInputDTO BuildDto()
    {
        var (sequencias, _, _) = Build();
        var principal = sequencias[0];
        var metadados = principal.Metadados;

        var partida = new PontoCoordenada
        {
            Nome = _nomePartida,
            X = _coordPartida.X,
            Y = _coordPartida.Y,
            Z = _coordPartida.Z,
            EhPontoPoligonal = true,
            AzimuteChegada = 90.0
        };

        var chegada = _coordChegada.HasValue
            ? new PontoCoordenada { Nome = _nomeChegada ?? "Chegada", X = _coordChegada.Value.X, Y = _coordChegada.Value.Y, Z = _coordChegada.Value.Z, EhPontoPoligonal = true }
            : partida;

        var todasLeituras = _estacoes.SelectMany(e => e.Leituras).Where(l => l.Tipo == TipoLeitura.Poligonal).ToList();

        var poligonalBruta = new List<PontoCoordenada> { partida };
        double x = partida.X;
        double y = partida.Y;
        double z = partida.Z;
        foreach (var l in todasLeituras)
        {
            x += l.DistanciaInclinada;
            poligonalBruta.Add(new PontoCoordenada 
            { 
                Nome = l.PontoVisado, 
                X = x, 
                Y = y, 
                Z = z, 
                EhPontoPoligonal = true,
                AzimuteChegada = 90.0 
            });
        }

        return new CompensacaoPoligonalInputDTO
        {
            Metadados = metadados,
            PontoPartida = partida,
            PontoChegada = chegada,
            AzimuteInicial = _azimutePartida,
            AzimuteChegada = _azimuteChegada ?? _azimutePartida,
            AnguloFechamento = 0.0,
            Leituras = todasLeituras,
            PoligonalBruta = poligonalBruta,
            Tolerancias = _tolerancias
        };
    }

    private void GarantirEstacaoCorrente()
    {
        if (_estacaoCorrente == null)
        {
            ComEstacao(_nomePartida);
        }
    }
}
