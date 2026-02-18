using System;
using System.Collections.Generic;
using System.Text;

namespace TopoGente.Core.Entities
{
    public enum SentidoAngulo
    {
        Horario,     // Direita (Padrão)
        AntiHorario  // Esquerda
    }

    public enum TipoLeitura
    {
        Re = 0,        
        Irradiacao = 1, 
        Poligonal = 2   
    }

    public enum FormatoArquivoEntrada
    {
        CsvPadrao = 0,
        Fbk = 1,
        LandXml = 2
    }

    public enum TipoCenarioPoligonal
    {
        // <summary> Poligonal enquadrada : sai de um ponto conhecido e chega em outro ponto conhecido</summary>
        Enquadrada,
        // <summary> Poligonal fechada : fechamento automático entre o último ponto e o primeiro ponto</summary>
        Fechada,
        //<summary> Poligonal aberta : sem fechamento , apenas ponto de partida e orientação inicial</summary>
        AbertaOrientada
    }
}