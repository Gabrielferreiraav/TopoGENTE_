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
}