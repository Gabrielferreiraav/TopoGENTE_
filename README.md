**Versão:** 1.0 
**Ultima atualização: 2025-12-10**

## Sumário
- [O que é o TopoGente?](#o-que-é-o-topogente)
- [Por que foi criado?](#por-que-foi-criado)
- [Comparação com topoGRAPH 98 SE](#comparação-com-topograph-98-se)
- [Funcionalidades principais](#funcionalidades-principais)
- [Benefícios para o trabalho de topografia](#benefícios-para-o-trabalho-de-topografia)
- [Requisitos mínimos](#requisitos-mínimos)
- [Guia prático de uso](#guia-prático-de-uso)
- [Integração operacional](#integração-operacional)
- [FAQ (perguntas frequentes)](#faq)
- [Glossário rápido](#glossário-rápido)
- [Suporte e contato](#suporte-e-contato)

## O que é o TopoGente? 

Um aplicativo desktop leve que processa dados de levantamento topográfico (poligonais e pontos irradiados) e desenha o resultado. Focado em ensino/uso acadêmico: transparente, sem custos, fácil de entender e adaptar.

## Por que foi criado? 

Reproduzir funcionalidades essenciais de programas clássicos (ex.: topoGRAPH 98 SE) de forma leve e didática. Permitir que equipes de topografia processem cadernetas de campo rapidamente e visualizem coordenadas e desenhos básicos. Facilitar ensino e validação de conceitos topográficos (azimutes, projeções, fechos).

## Comparação com topoGRAPH 98 SE  

Similaridades: Processa leituras de estação (ângulos, distâncias). Gera poligonais e calcula pontos irradiados. Exibe resultados numéricos e desenho básico. 

Diferenças: TopoGente é mais leve, código aberto/educacional e sem todas as opções avançadas de CAD/plot. Menos integrações e formatos de exportação inicialmente. 

Objetivo principal: aprendizado, análise e processamento rápido, não substituição completa de suítes comerciais.

## Funcionalidades principais 

Importar caderneta de campo (.txt ou .csv): Arquivo com linhas que descrevem leituras; o sistema interpreta automaticamente (separador vírgula ou ponto-e-vírgula). 

Caderneta de Campo Editável: Permite alterar a classificação dos pontos (Ré, Vante, Irradiação) diretamente na interface antes do cálculo, facilitando correções sem necessidade de editar o arquivo original. 

Calcular poligonal: A partir de um ponto inicial (M1) com coordenadas e um azimute inicial é calculada a sequência de estações (M2, M3...). 

Calcular pontos irradiados: Pontos de detalhe (poste, árvore, elementos) são calculados a partir da estação com ângulo e distância lida. 

Mostrar resultados: Tabela com todos os pontos (nome, X, Y, Z), perímetro da poligonal, erro de fechamento (se houver) e precisão. 

Desenho simples: Exibe a poligonal e pontos irradiados em um Canvas com legendas e tooltips. Conseguimos aumentar ou diminuir o zoom, além de poder movimentar o desenho no visualizador. 

Relatórios básicos: Listagem de pontos calculados (pode ser copiada/exportada no futuro).

## Benefícios para o trabalho de topografia

Agilidade: processamento rápido de cadernetas em campo ou escritório. 

Visibilidade: ver imediatamente se a poligonal fechou e qual o erro. 

Economia: ferramenta sem custo e leve para máquinas simples. 

Ensino: excelente para treinamento de instrumentistas e estagiários, mostrando passo-a-passo os cálculos.

## Requisitos mínimos

SO recomendado: Windows 10/11 (WPF nativo). Possível executar em outras plataformas apenas para testes do Core. 

RAM: 4 GB (8 GB recomendado). 

Espaço em disco: < 200 MB para instalação base. .NET SDK/Runtime 10 (para rodar/compilar). Usuário final precisa apenas do runtime (se houver instalador). 

Tela: resolução mínima 1024x768 para conforto.

## Guia prático de uso

Abrir o aplicativo (TopoGente.UI). 

Carregar caderneta: Botão "Carregar Arquivo" → selecionar arquivo .txt/.csv. O sistema lista as leituras na grade. 

Inserir ponto de partida: Preencha X, Y, Z do marco inicial (M1) e azimute inicial. 

Processar: Clicar em "Processar". O programa calcula automaticamente a poligonal, pontos irradiados, perímetro e erro. 

Visualizar: Aba de resultados mostra tabela e desenho. Tooltip nos pontos exibe coordenadas e nome. 

Corrigir input: Se algo estiver errado, editar a grade (seção das leituras) e reprocessar.

## Integração operacional

Instrumentista: gera caderneta no campo e passa arquivo ao técnico. 

Técnico/engenheiro: altera o arquivo para quie esteja no padrão de entrada , roda o TopoGente, verifica fecho e erros; corrige ou reprocessa se necessário. 

Documentação: exporta ou registra coordenadas para planta ou cadastro.

## FAQ

Q : Que formatos de arquivo são suportados? 

A: Arquivos de texto .txt e .csv com separador , ou ;. Cabeçalhos com "Estação" ou linhas começando com # são ignorados. 

Q: O que significa "Erro de Fechamento"? 

A: É a distância entre o ponto final calculado e o ponto onde deveria fechar (normalmente o ponto inicial). Indica precisão do levantamento. 

Q: Posso usar em campo com um laptop simples? 

A: Sim — aplicação é leve. Evite telas muito pequenas; recomenda-se pelo menos 4 GB RAM. 

Q: Ele gera arquivos para CAD (DXF) ou GIS? 

A: Agora não; exportação é um recurso futuro planejado.

## Glossário rápido

Poligonal: sequência de estações ligadas por distâncias e ângulos. 

Irradiado: ponto de detalhe medido a partir de uma estação (poste, árvore). 

Azimute: direção medida a partir do Norte (em graus). 

Ré / Vante: Ré = direção de chegada (do ponto anterior); Vante = direção de saída (próximo ponto). 

Erro de fechamento: a discrepância entre ponto esperado e ponto calculado no final da poligonal.

## Suporte e contato

Entrar em contato **gabriel.f.viana@ufv.br** para detalhes.
