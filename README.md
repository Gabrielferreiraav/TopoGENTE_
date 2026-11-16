# TopoGENTE

Plataforma de processamento topográfico, modelagem digital de terreno e desenho CAD 2D inspirada no topoGRAPH 98 SE. O sistema foi construído em C# 13 e .NET 10 com Arquitetura Hexagonal, isolando o núcleo matemático de topografia e geodésia das interfaces gráficas e do armazenamento em disco.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](#)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](#)
[![WPF](https://img.shields.io/badge/UI-WPF%20Desktop-0078D7?logo=windows&logoColor=white)](#)
[![NBR 13.133](https://img.shields.io/badge/Norma-NBR%2013.133-orange)](#)

---

## Estrutura da Solução

O código segue a separação de responsabilidades da Arquitetura Hexagonal:

* **`TopoGente.Core`**: Núcleo puro de domínio. Reúne as entidades de levantamento, estratégias de ajustamento de poligonais (Bowditch), verificação de linha de base e auditoria de qualidade segundo a NBR 13.133. Não possui dependências de interface gráfica ou de bibliotecas CAD.
* **`TopoGente.Infrastructure`**: Adaptadores secundários. Implementa leitores de caderneta de campo (Autodesk Field Book `.fbk`, arquivos delimitados `.csv`/`.txt`), exportadores de desenho `.dxf`, geradores de relatórios `.txt` e o adaptador de triangulação de Delaunay (Tinfour).
* **`TopoGente.PresentationCore`**: Regras de formatação de grandezas físicas (metros, milímetros, graus decimais, graus-minutos-segundos) e oráculos de precisão numérica.
* **`TopoGente.UI`**: Adaptador primário. Aplicação desktop em WPF com padrão MVVM, mensageria desacoplada via `WeakReferenceMessenger` e motor CAD com renderização acelerada por `StreamGeometry`.
* **`TopoGENTE.Test`** e **`TopoGente.UI.E2ETests`**: Suítes com 217 testes automatizados cobrindo cálculos geodésicos, compensação de erros, restrições topológicas e contratos do ViewModel.

---

## Fluxo Geral de Operação

O diagrama abaixo descreve o percurso dos dados desde a importação do arquivo bruto até a entrega dos produtos cartográficos.

```mermaid
flowchart TD
    START(("Início")) --> ABERTURA["Tela Inicial Vazia (Estado Bruto)"]

    ABERTURA -->|"Seleciona caderneta (.fbk, .csv, .txt)"| IMPORTACAO["Janela de Importação e Pré-Visualização"]

    IMPORTACAO -.->|"Linha corrompida ou comando ignorado"| ALERTA_LINHA["Identifica linha com alerta e descarta do cálculo"]
    ALERTA_LINHA --> IMPORTACAO

    IMPORTACAO -->|"Confirma delimitadores e formato"| ESBOCO["Agrupa sessões por instrumento e gera coordenadas provisórias"]
    ESBOCO --> PLANIMETRIA["Espaço Planimetria: Poligonal Laranja Tracejada"]

    PLANIMETRIA -->|"Revisa amarração à Ré ou sequência de estações"| PLANIMETRIA
    PLANIMETRIA -->|"Clica em Calcular e Ajustar"| PROCESSAMENTO["Aplica Bowditch e audita tolerâncias da NBR 13.133"]

    PROCESSAMENTO -->|"Fechamento aprovado na classe de precisão"| COMPENSADO["Gera malha triangular e curvas de nível"]
    COMPENSADO --> APROVADO["Levantamento Compensado: Barra Verde e Poligonal Azul"]

    PROCESSAMENTO -.->|"Erro angular ou linear supera a tolerância"| REPROVADO["Alerta Normativo: Barra Vermelha e Poligonal Laranja"]
    REPROVADO -->|"Exporta laudo técnico de auditoria"| LAUDO_TXT["Arquivo de auditoria (.txt)"]
    REPROVADO -->|"Ajusta classe de precisão ou parâmetros"| RECALCULA_LAUDO["Reavalia erros com novas tolerâncias"]
    RECALCULA_LAUDO -->|"Novo clique em Calcular e Ajustar"| PROCESSAMENTO

    APROVADO -->|"Abre Modelagem MDT"| MDT["Espaço MDT: Superfície Triangular"]
    MDT -->|"Clica dois vértices com a ferramenta Breakline"| INSERE_BK["Insere linha de quebra rosa na malha"]
    INSERE_BK -.->|"Cruzamento de arestas ou vértice repetido"| REJEITA_BK["Cancela inserção e emite aviso em tela"]
    REJEITA_BK --> MDT
    INSERE_BK -->|"Recalcula malha triangular"| MDT

    APROVADO -->|"Abre Altimetria e define equidistância"| ALTIMETRIA["Espaço Altimetria: Curvas de Nível"]
    ALTIMETRIA -->|"Atualiza curvas mestras e secundárias"| ALTIMETRIA

    APROVADO -->|"Abre Planta Geral"| PRANCHA["Prancha Final de Entrega"]
    PRANCHA -->|"Aciona menu Exportar"| EXPORTACAO["Gera arquivos finais: Coordenadas (.txt), Desenho (.dxf) ou Laudo (.txt)"]
    EXPORTACAO --> FIM((("Fim")))

    FERRAMENTAS_GLOBAIS["Ferramentas ativas no mapa: Inspecionar Vértices | Medir Distâncias e Desníveis | Desfazer e Refazer"] -.->|"Acesso contínuo"| CONSULTAS["Leitura cadastral e geométrica na barra inferior"]
```

---

## Ciclo de Estados e Travas de Interface

O sistema impede a geração de produtos inválidos por meio de uma máquina de estados:

```mermaid
stateDiagram-v2
    [*] --> Bruto : Janela abre
    state "Estado do Motor" as Motor {
        Bruto --> Calculando : Clica em Calcular e Ajustar
        Calculando --> Compensado : Fechamento dentro da tolerância
        Calculando --> ReprovadoNorma : Fechamento fora da tolerância
        Compensado --> Bruto : Importa arquivo ou altera leitura
        ReprovadoNorma --> Bruto : Importa arquivo ou altera leitura
        ReprovadoNorma --> Calculando : Clica em Calcular e Ajustar
        Compensado --> Calculando : Clica em Calcular e Ajustar
    }
    state "Ferramenta Ativa no Mapa" as CAD {
        Inspecao --> Medicao : Clica em Medir
        Medicao --> Inspecao : Clica em Inspecionar
        Inspecao --> Breakline : Clica em Breakline (em estado Compensado)
        Breakline --> Inspecao : Clica em Inspecionar
    }
```

* **Estado `Bruto`**: Exportações vetoriais bloqueadas. O menu `Exportar ▾` exibe aviso de bloqueio prévio. Linhas de quebra ficam desabilitadas.
* **Estado `Calculando`**: Trava reentrâncias de cálculo e edição de tabelas durante a execução das rotinas em segundo plano.
* **Estado `Compensado`**: Libera exportação de arquivos `.dxf` em camadas, arquivos de coordenadas com memória de cálculo `.txt`, criação de linhas de quebra e ferramentas altimétricas.
* **Estado `ReprovadoNorma`**: Bloqueia exportações cartográficas e geração de superfícies. Libera a emissão do laudo técnico de não conformidade para reorientação dos trabalhos de campo.

---

## Demonstração Visual com Caso Real de Campo

As telas abaixo foram capturadas durante o processamento do levantamento `113503.FBK` (129 pontos, desnível de 1,979 m).

### 1. Importação Inteligente e Pré-Visualização

O leitor detecta separadores decimais, escala de coordenadas e delimitadores de coluna antes de transferir os dados para a memória:

| Caderneta Autodesk Field Book (`.fbk`) | Arquivo Tabular Delimitado (`.csv`) |
| :---: | :---: |
| ![Importação FBK](docs/prints_ui/03_dialogo_importacao_fbk.png) | ![Importação CSV](docs/prints_ui/04_dialogo_importacao_csv.png) |
| *Identificação de comandos especiais (`UNIT`, `PRISM`) e separação dos pontos medidos.* | *Detecção de colunas incorretas e validação numérica antes da carga.* |

### 2. Esboço Inicial e Revisão de Sessões

Após a confirmação da importação, o sistema agrupa as visadas em sessões de instrumento por altura de mira e desenha a geometria bruta em laranja tracejado:

| Esboço Inicial e Amarração de Partida | Agrupamento por Sessões de Campo |
| :---: | :---: |
| ![Esboço Inicial](docs/prints_ui/05_pos_importacao_esboco_bruto.png) | ![Aba Visadas](docs/prints_ui/06_ws1_aba1_visadas.png) |
| *Preenchimento da estação base E1, ré E4 e projeção preliminar no mapa.* | *Separação das leituras por altura do instrumento (HI).* |

### 3. Compensação de Bowditch e Auditoria de Qualidade (NBR 13.133)

Ao clicar em `Calcular e Ajustar`, o motor avalia o erro angular e o erro linear total. Se aprovado, a barra de status muda para verde e a poligonal passa a ser desenhada em azul contínuo:

![Diagnóstico Bowditch Aprovado](docs/prints_ui/08_ws1_aba3_diagnostico_bowditch.png)

Quando o fechamento supera os limites normativos (ou em verificação de linha de base com erro excessivo), a barra inferior passa para vermelho e as exportações finais são travadas:

| Reprovação em Verificação de Linha de Base | Reprovação Angular na Classe I-P |
| :---: | :---: |
| ![Linha de Base Reprovada](docs/prints_ui/18_ws1_aba3_diagnostico_linha_base.png) | ![Status Reprovado](docs/prints_ui/19_status_reprovado_norma.png) |
| *Detecção de divergência métrica entre pontos de apoio conhecidos.* | *Retorno da poligonal ao traçado laranja e emissão do laudo técnico.* |

### 4. Modelagem Digital do Terreno (MDT) e Linhas de Quebra

A superfície é calculada por triangulação de Delaunay com restrições (CDT). O operador insere linhas de quebra (cristas, talvegues, bordos de pista) clicando em vértices do terreno:

| Malha Triangular (244 elementos) | Inserção de Breakline e Recálculo da Malha |
| :---: | :---: |
| ![Modelagem MDT](docs/prints_ui/11_ws2_modelagem_mdt.png) | ![Inserção de Breakline](docs/prints_ui/15_cad_ferramenta_breakline_mdt.png) |
| *Triangulação respeitando a nuvem de 129 pontos.* | *Linha rosa obrigatória forçando a reorganização dos triângulos para 234 elementos.* |

### 5. Altimetria e Curvas de Nível

O módulo altimétrico interpola curvas mestras e secundárias sobre a malha triangular com equidistância configurável:

![Curvas de Nível](docs/prints_ui/12_ws3_altimetria_curvas.png)

### 6. Ferramentas CAD do Viewport

O motor gráfico inclui indexação espacial em memória ($K$-d Tree e BVH) para resposta imediata sem alocação de objetos no ciclo de renderização:

| Inspeção Cadastral de Ponto | Medição Vetorial com HUD em Tempo Real |
| :---: | :---: |
| ![Inspeção de Ponto](docs/prints_ui/14_cad_ferramenta_inspecionar_ponto.png) | ![Medição no Mapa](docs/prints_ui/17_cad_ferramenta_medir_hud.png) |
| *Identificação de coordenadas X, Y, Z e classificação do vértice.* | *Cálculo de distância horizontal, inclinada, desnível, inclinação e azimute.* |

### 7. Prancha de Entrega e Exportação de Entregáveis

O espaço de entrega consolida o sumário cartográfico da poligonal e libera os arquivos de saída:

| Prancha de Saída Consolidada | Menu de Exportação Explicativo |
| :---: | :---: |
| ![Planta Geral](docs/prints_ui/13_ws4_planta_geral_entrega.png) | ![Menu Exportar](docs/prints_ui/10_menu_exportar_compensado.png) |
| *Exibição simultânea de poligonal, malha, curvas e pontos irradiados.* | *Opções detalhadas: Coordenadas (.txt), Desenho (.dxf) e Laudo (.txt).* |

---

## Formulação Matemática Implementada

### 1. Tolerâncias Normativas (NBR 13.133)

* **Tolerância Angular Admissível ($T_a$):**

  $$T_a = b \cdot \sqrt{n} + C_a$$

  Onde $b$ é o coeficiente angular da classe em segundos de arco, $n$ é o número de vértices ocupados da poligonal e $C_a$ representa o termo aditivo de centragem instrumental.

* **Tolerância Linear Absoluta Admissível ($T_P$):**

  $$T_P = c + d \cdot \sqrt{L_{km}}$$

  Onde $c$ é a constante linear em metros, $d$ é o coeficiente por raiz de quilômetro e $L_{km}$ é o perímetro total em quilômetros.

* **Tolerância Linear Relativa ($T_l$):**

  $$T_l = \frac{L}{P}$$

  Onde $P$ é o denominador da precisão linear mínima da classe (ex.: $1:10.000$ para Classe I-P, $1:5.000$ para Classe II-P).

* **Tolerância Altimétrica ($T_z$):**

  $$T_z = \left(\frac{c}{1000}\right) \cdot \sqrt{L_{km}}$$

### 2. Compensação Planimétrica pelo Método de Bowditch

A distribuição do erro linear total de fechamento ($\Delta X$ e $\Delta Y$) ocorre em proporção direta à extensão de cada lance:

$$C_{X_i} = - \Delta X \cdot \frac{D_i}{\sum D}, \quad C_{Y_i} = - \Delta Y \cdot \frac{D_i}{\sum D}$$

Onde $D_i$ é a distância horizontal do alinhamento $i$ e $\sum D$ é o perímetro total da poligonal.

---

## Como Compilar e Executar

### Pré-requisitos

* Windows 10 ou 11 (64 bits).
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.
* Visual Studio 2022 (versão 17.12 ou superior) ou Visual Studio Code com extensão C# Dev Kit.

### Compilação via Linha de Comando

1. Clone o repositório:
   ```bash
   git clone https://github.com/SeuUsuario/TopoGente.git
   cd TopoGente
   ```

2. Restaure as dependências e compile a solução:
   ```bash
   dotnet build TopoGENTE_.slnx
   ```

3. Execute a aplicação gráfica:
   ```bash
   dotnet run --project TopoGente.UI/TopoGente.UI.csproj
   ```

### Execução dos Testes Automatizados

O projeto contém testes de unidade e de integração isolados de interface gráfica e testes de ponta a ponta do ViewModel:

```bash
# Executa a suíte de cálculos topográficos, regras normativas e algoritmos puros
dotnet test TopoGENTE.Test/TopoGENTE.Test.csproj

# Executa a suíte de testes de interação de telas e comandos do ViewModel
dotnet test TopoGente.UI.E2ETests/TopoGente.UI.E2ETests.csproj
```

---

## Licença e Créditos

Desenvolvido no **GENTE** (Grupo de Engenharia e Tecnologias Espaciais) da **Universidade Federal de Viçosa (UFV)** como plataforma de engenharia para cálculo e cartografia topográfica profissional. Baseado nos princípios teóricos da ABNT NBR 13.133 e na literatura clássica de Topografia e Geodésia.
