using System;
using System.Collections.Generic;
using TopoGente.Core.Entities;

namespace TopoGente.PresentationCore.Tolerances;

/// <summary>
/// Provedor centralizado de metadados, descrições técnicas e aplicações profissionais
/// para as classes da NBR 13.133 e modos de avaliação de fechamento linear.
/// </summary>
public static class ToleranciaInfoHelper
{
    private static readonly Dictionary<PresetToleranciaEnum, ToleranciaPresetInfo> PresetsInfo = new()
    {
        [PresetToleranciaEnum.ClasseIP] = new(
            Preset: PresetToleranciaEnum.ClasseIP,
            Titulo: "Classe I P — Alta Precisão (Adensamento Geodésico e Monitoramento)",
            NomeExibicao: "Classe I P (Alta Precisão)",
            ParametrosResumo: "b = 6'' | 1/P = 1:10.000 | c = 1,5 mm | d = 0,10 m/√km | σa = 3,0''",
            Descricao: "Poligonal planimétrica de precisão mais rigorosa segundo a ABNT NBR 13.133. Aplica tolerância angular máxima Ta = 6''·√n + Ca, razão de precisão linear mínima de 1:10.000 e tolerância linear absoluta TP = 1,5 mm + 0,10 m/√L(km).",
            Aplicacoes: "• Apoio topográfico fundamental e adensamento da rede geodésica municipal/estadual (SGL).\n• Obras de grande porte e alta precisão (usinas hidrelétricas, barragens, túneis, pontes estaiadas, vias férreas de alta velocidade).\n• Monitoramento geodinâmico de deformações de taludes e recalque de fundações.\n• Perícias judiciais demarcatórias de alta complexidade técnica."
        ),

        [PresetToleranciaEnum.ClasseIIP] = new(
            Preset: PresetToleranciaEnum.ClasseIIP,
            Titulo: "Classe II P — Média-Alta Precisão (Padrão NBR para Projetos Básicos/Executivos)",
            NomeExibicao: "Classe II P (Média-Alta Precisão)",
            ParametrosResumo: "b = 15'' | 1/P = 1:5.000 | c = 3,0 mm | d = 0,30 m/√km | σa = 7,5''",
            Descricao: "Classe de referência recomendada para a grande maioria dos projetos executivos de engenharia civil e infraestrutura. Aplica tolerância angular Ta = 15''·√n + Ca (com termo de centragem Ca = 10'' recomendado em áreas urbanas), precisão mínima de 1:5.000 e limite absoluto TP = 3,0 mm + 0,30 m/√L(km).",
            Aplicacoes: "• Poligonais principais para projetos básicos e executivos de engenharia civil e viária (rodovias, ferrovias, canais, saneamento).\n• Levantamentos cadastrais urbanos detalhados e demarcação de perímetros urbanos.\n• Loteamentos de grande porte, parcelamento de solo e regularização fundiária (REURB).\n• Apoio terrestre para aerofotogrametria e perfilamento laser (LIDAR)."
        ),

        [PresetToleranciaEnum.ClasseIIIP] = new(
            Preset: PresetToleranciaEnum.ClasseIIIP,
            Titulo: "Classe III P — Média Precisão (Cadastral Urbano e Poligonais Secundárias)",
            NomeExibicao: "Classe III P (Média Precisão)",
            ParametrosResumo: "b = 30'' | 1/P = 1:3.000 | c = 5,0 mm | d = 0,42 m/√km | σa = 15,0''",
            Descricao: "Poligonal secundária e cadastral com requisitos intermediários. Aplica tolerância angular Ta = 30''·√n + Ca, fechamento relativo mínimo de 1:3.000 e limite linear absoluto TP = 5,0 mm + 0,42 m/√L(km).",
            Aplicacoes: "• Poligonais secundárias e de detalhamento vinculadas à poligonal principal.\n• Levantamentos planialtimétricos cadastrais de lotes, quadras e glebas urbanas médias e pequenas.\n• Cadastro imobiliário multifinalitário, faixas de servidão e estudos de desapropriação.\n• Redes de drenagem pluvial, redes de água, esgoto e redes de eletrificação/telecom."
        ),

        [PresetToleranciaEnum.ClasseIVP] = new(
            Preset: PresetToleranciaEnum.ClasseIVP,
            Titulo: "Classe IV P — Baixa Precisão (Rural, Terciária e Reconhecimento)",
            NomeExibicao: "Classe IV P (Baixa Precisão)",
            ParametrosResumo: "b = 60'' (1') | 1/P = 1:1.000 | c = 10,0 mm | d = 0,50 m/√km | σa = 30,0''",
            Descricao: "Poligonal com tolerâncias atenuadas para situações com restrições de visibilidade ou relevo acidentado. Aplica tolerância angular Ta = 60''·√n + Ca, fechamento relativo de 1:1.000 e limite linear absoluto TP = 10,0 mm + 0,50 m/√L(km).",
            Aplicacoes: "• Poligonais terciárias e de preenchimento em terrenos com relevo movimentado ou mata fechada.\n• Estudos preliminares de viabilidade de traçado viário e reconhecimento de campo.\n• Delimitação preliminar de propriedades rurais de grande extensão, áreas de preservação (APP) e reservas florestais.\n• Levantamentos para terraplenagem preliminar e abertura de estradas vicinais."
        ),

        [PresetToleranciaEnum.ClasseVP] = new(
            Preset: PresetToleranciaEnum.ClasseVP,
            Titulo: "Classe V P — Aproximada (Expedita e Pré-Viabilidade)",
            NomeExibicao: "Classe V P (Aproximada)",
            ParametrosResumo: "b = 180'' (3') | 1/P = 1:500 | c = 20,0 mm | d = 1,00 m/√km | σa = 90,0''",
            Descricao: "Poligonal expedita com tolerância aberta para reconhecimento rápido ou levantamentos com bússola/trena. Aplica tolerância angular Ta = 180''·√n + Ca, precisão relativa de 1:500 e limite linear absoluto TP = 20,0 mm + 1,00 m/√L(km).",
            Aplicacoes: "• Estudos de anteprojeto, viabilidade locacional preliminar e reconhecimento de acessos.\n• Delimitação expedita para linhas provisórias de transmissão, cercas rurais e canteiros de obra.\n• Esboços e croquis de campo para planejamento logístico e alocação de equipes."
        ),

        [PresetToleranciaEnum.Personalizado] = new(
            Preset: PresetToleranciaEnum.Personalizado,
            Titulo: "Personalizado — Órgãos Reguladores, Editais e Contratos Especiais",
            NomeExibicao: "Personalizado (Editais / INCRA / DER)",
            ParametrosResumo: "Parâmetros livres: b, k, σa, Ca, 1/P, c, d e tolerâncias de controle editáveis",
            Descricao: "Permite ao engenheiro configurar manualmente cada parâmetro de tolerância linear e angular, adaptando o software a instruções normativas específicas de órgãos reguladores, concessionárias ou termos de referência contratuais.",
            Aplicacoes: "• Georreferenciamento de imóveis rurais (Manuais Técnicos de Posicionamento do INCRA).\n• Cadernos de encargos rodoviários estaduais e federais (DER/SP, DNIT, ARTESP).\n• Normas corporativas de saneamento, mineração e óleo & gás (SABESP, COPASA, VALE, Petrobras).\n• Perícias judiciais com tolerâncias pactuadas em despacho do juízo."
        )
    };

    private static readonly Dictionary<ModoAvaliacaoLinearEnum, ModoAvaliacaoLinearInfo> ModosInfo = new()
    {
        [ModoAvaliacaoLinearEnum.AmbosRigorosoNBR] = new(
            Modo: ModoAvaliacaoLinearEnum.AmbosRigorosoNBR,
            Titulo: "Ambos (Rigoroso NBR 13.133) — Padrão Compulsório",
            NomeExibicao: "Ambos (Rigoroso NBR 13.133)",
            RegraMatematica: "Aprovado se: (P_obtido ≥ P_min) E (el ≤ TP = c + d·√L_km)",
            Descricao: "Critério oficial da ABNT NBR 13.133. O levantamento é reprovado se falhar em qualquer um dos dois requisitos: deve satisfazer tanto a precisão relativa fracionária mínima (1/P) quanto o limite de erro linear absoluto em milímetros (TP).",
            Aplicacoes: "• Fiscalização de obras públicas e projetos com exigência estrita da NBR 13.133.\n• Laudos periciais judiciais demarcatórios e reintegração de posse.\n• Implantação de marcos geodésicos e aprovação de loteamentos em prefeituras.\n• Contratos de engenharia onde a conformidade com a NBR 13.133 é cláusula vinculante com ART/RRT."
        ),

        [ModoAvaliacaoLinearEnum.ApenasRelativo] = new(
            Modo: ModoAvaliacaoLinearEnum.ApenasRelativo,
            Titulo: "Apenas Relativo (1/P) — Fração de Precisão Linear",
            NomeExibicao: "Apenas Relativo (1/P)",
            RegraMatematica: "Aprovado se: P_obtido ≥ P_min (el ≤ Perímetro / P)",
            Descricao: "Avalia a conformidade linear exclusivamente pela razão proporcional obtida (ex: 1:5.000, 1:10.000). Não reprova pelo erro linear absoluto em milímetros.",
            Aplicacoes: "• Poligonais extensas com perímetros superiores a 1,5 km ou 2,0 km (rodovias, ferrovias, adutoras, canais, linhas de transmissão).\n• Contratos e especificações técnicas tradicionais que definem a precisão linear unicamente por fração (1/P)."
        ),

        [ModoAvaliacaoLinearEnum.ApenasAbsoluto] = new(
            Modo: ModoAvaliacaoLinearEnum.ApenasAbsoluto,
            Titulo: "Apenas Absoluto (TP = c + d·√L) — Limite Milimétrico Planar",
            NomeExibicao: "Apenas Absoluto (TP = c + d·√L)",
            RegraMatematica: "Aprovado se: el ≤ TP = c + d·√L_km (em metros/milímetros)",
            Descricao: "Avalia o erro linear de fechamento diretamente contra o limite absoluto em milímetros previsto pela NBR 13.133. Não reprova pela fração proporcional 1/P.",
            Aplicacoes: "• Levantamentos urbanos com lances curtos e perímetros pequenos (como o caso 113503.FBK, L = 97 m, onde o erro real de 2,27 mm é impecável, mas a razão 1/P seria artificialmente severa).\n• Loteamentos urbanos densos, quadras centrais e implantação predial onde o erro milimétrico real é minúsculo (2 a 10 mm), evitando falsos negativos por perímetro reduzido."
        )
    };

    public static ToleranciaPresetInfo ObterInfo(PresetToleranciaEnum preset)
    {
        return PresetsInfo.TryGetValue(preset, out var info) ? info : PresetsInfo[PresetToleranciaEnum.ClasseIIP];
    }

    public static ModoAvaliacaoLinearInfo ObterInfoModo(ModoAvaliacaoLinearEnum modo)
    {
        return ModosInfo.TryGetValue(modo, out var info) ? info : ModosInfo[ModoAvaliacaoLinearEnum.AmbosRigorosoNBR];
    }
}
