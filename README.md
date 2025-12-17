**Versão:** 1.0 
**Ultima atualização: 2025-12-16**

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

Importar caderneta de campo (.fbk, .txt ou .csv): Suporte nativo ao formato Autodesk Field Book (.fbk) e arquivos de texto delimitados. O sistema identifica automaticamente o formato, agrupa estações duplicadas e organiza o caminhamento.

Identificação Automática de Coordenadas: Ao ler arquivos como o .fbk, o sistema detecta se existem coordenadas de partida (NEZ) e preenche automaticamente os campos de configuração inicial.

Visualização e Edição por Estação: Interface organizada hierarquicamente. O usuário seleciona a estação desejada em uma lista para visualizar apenas as leituras daquele ponto, facilitando a conferência.

Persistência de Projeto (.topo): Permite salvar todo o trabalho (dados importados, coordenadas configuradas e cálculos) em um formato próprio (.topo) para continuar depois, sem perder a organização das estações.

Exportação DXF: Gera arquivos universais (.dxf) compatíveis com AutoCAD (R12/2000 em diante), Civil 3D e QGIS. Exporta camadas separadas para poligonal, irradiações, nomes de pontos e cotas (Z real).

Calcular poligonal e irradiados: Processamento inteligente que identifica a sequência da poligonal (caminhamento por vante) e calcula as coordenadas X, Y, Z de todos os pontos irradiados.

Resultados e Desenho: Tabela completa com coordenadas e desenho gráfico interativo (zoom, pan) da poligonal e pontos.

## Benefícios para o trabalho de topografia

Interoperabilidade: aceita formatos de equipamentos (FBK) e entrega formatos de engenharia (DXF).

Segurança: possibilidade de salvar o estado do projeto e continuar posteriormente.

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

Carregar caderneta: Botão "Importar Caderneta" -> selecionar arquivo .fbk, .txt ou .csv. O sistema carregará as estações.

Verificar Estações: Utilize a caixa de seleção para navegar entre as estações carregadas e conferir os dados brutos.

Configurar Partida: Se o arquivo tiver coordenadas (NEZ), elas aparecerão automaticamente nos campos X, Y, Z e Azimute. Caso contrário, digite manualmente.

Calcular: Clicar em "CALCULAR". O programa processa a poligonal e gera os resultados na grade e no desenho.

Exportar ou Salvar: Utilize "Exportar DXF" para gerar o arquivo para CAD, ou "Salvar (.topo)" para guardar o projeto para edição futura.

Abrir Projeto: Utilize "Abrir (.topo)" para restaurar um trabalho anterior exatamente como foi salvo.

## Integração operacional

Instrumentista: exporta a caderneta da estação total (formato FBK ou CSV/TXT).

Técnico/engenheiro: importa no TopoGente, verifica a geometria, fecha a poligonal e gera o DXF.

Projetista: abre o DXF no AutoCAD/Civil 3D para desenhar a planta final com camadas já separadas.

## FAQ

Q: O que significa "Erro de Fechamento"? 

A: É a distância entre o ponto final calculado e o ponto onde deveria fechar (normalmente o ponto inicial). Indica precisão do levantamento. 

Q: Posso usar em campo com um laptop simples? 

A: Sim — aplicação é leve. Evite telas muito pequenas; recomenda-se pelo menos 4 GB RAM. 

Q: Que formatos de arquivo são suportados?

A: Autodesk Field Book (.fbk) e arquivos de texto/CSV padronizados (separador vírgula ou ponto-e-vírgula).

Q: O programa corrige erros de sequência no arquivo?

A: Sim. O sistema possui um organizador de caminhamento que conecta as estações pela leitura de Vante, mesmo que estejam gravadas fora de ordem no arquivo.

Q: Ele gera arquivos para CAD (DXF)?

A: Sim. Gera arquivos DXF compatíveis com versões antigas e novas do AutoCAD, com elevações (Z) corretas nos textos e geometrias.

Q: Posso salvar meu trabalho para continuar depois?

A: Sim, através da funcionalidade "Salvar Projeto" que cria arquivos .topo.


## Glossário rápido

Poligonal: sequência de estações ligadas por distâncias e ângulos. 

Irradiado: ponto de detalhe medido a partir de uma estação (poste, árvore). 

Azimute: direção medida a partir do Norte (em graus). 

Ré / Vante: Ré = direção de chegada (do ponto anterior); Vante = direção de saída (próximo ponto). 

Erro de fechamento: a discrepância entre ponto esperado e ponto calculado no final da poligonal.

FBK: formato de arquivo de caderneta de campo da Autodesk.

DXF: formato de intercâmbio de desenho vetorial (CAD).

## Suporte e contato

Entrar em contato com **gabriel.f.viana@ufv.br** para detalhes.
