using System;
using System.Runtime.InteropServices;

namespace TopoGENTE.Domain.ValueObjects;

/// <summary>
/// Representa um vértice topográfico básico.
/// Em arquitetura x64, com o alinhamento padrão ou explícito de 8 bytes, a struct
/// ocupa 24 bytes para as três variáveis double (X, Y, Z) e 4 bytes para o Id,
/// totalizando 28 bytes de payload e completando 32 bytes de footprint com o padding.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct TerrainVertex(double X, double Y, double Z, int Id);
