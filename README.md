# Huggable Horrors

Jogo de terror em primeira pessoa feito na **Unity**, com **multiplayer local em tela dividida** (1 a 4 jogadores).
Tudo é C# puro mais o asset de input; a interface é criada por código (não há prefab de UI).

Esta pasta (`Assets/HorrorFPS`) é o framework e o jogo. Os documentos detalhados ficam em [`Docs/`](Docs/).

## Começando

**Requisitos:** Unity **2022.3 LTS** ou **Unity 6**; pacote **Input System** (`com.unity.inputsystem`) com
*Project Settings > Player > Active Input Handling* em **Input System Package (New)** (ou *Both*).

1. Abra o projeto na Unity e espere compilar.
2. Se as cenas ainda não estiverem montadas: **Tools > Horror FPS > Demo > Create Full Game Demo** (cria o prefab do jogador, materiais, as cenas, o catálogo do jogo e ajusta os *Build Settings*).
3. Abra a cena **MainMenu** e dê **Play**. Jogador 1 = teclado e mouse; para o jogador 2, aperte um botão em um **controle**.

Também dá para dar Play direto em `Level_Hall` ou `Level_Cellar`: o jogo se monta sozinho.

## Onde fica cada coisa

| Pasta | O que tem |
|---|---|
| `Core/`, `Player/`, `Input/`, `World/`, `Stats/` | base: jogador por contexto, movimento, interação, input (só `PlayerInputHandler` lê o input), status |
| `Flow/`, `Save/`, `Cutscenes/`, `UI/` | troca de cenas (`SceneDirector`, `GameCatalog`), save/load, cutscenes, menus e HUD |
| `Items/`, `Inventory/`, `Equipment/` | itens, inventário (lista/grade), mãos, armas e lanterna |
| `Sanity/`, `Body/` | sanidade por jogador, respiração, batimento, tremor, impulsos de câmera |
| `Animation/`, `Characters/`, `Clips/` | corpo animado visível aos outros jogadores, personagens, clipes do Mixamo |
| `Dialogue/`, `SmartEvents/` | diálogos em grafo e eventos com condições |
| `Props/` | objetos interativos (gaveta, esconderijo, barril, espelho, armadilha, botões coop) |
| `Stalker/` | o perseguidor: IA, sentidos, árvore de comportamento, rotas e hurtboxes |
| `Revive/` | estado "caído" cooperativo |
| `Objectives/`, `Tutorials/` | objetivos da partida (lista, faixa, página no menu) e dicas de tutorial que aparecem uma vez por jogador (página "Como jogar") |
| `Editor/` (e `SmartEvents/Editor/`) | **todo** código que usa `UnityEditor`: menus, janelas e inspetores |
| `Scenes/`, `Prefabs/`, `Materials/`, `Resources/`, `Generated/` | assets: `Core`, `MainMenu` e níveis; prefab do jogador; catálogo em `Resources/HorrorFPS` |
| `Docs/` | documentação |

## Menus do Editor (`Tools > Horror FPS`)

Os menus são agrupados por área, sem números:

| Área | Para quê |
|---|---|
| **Player** | prefab do jogador, módulos de itens/sanidade/corpo, inventário inicial, catálogo de personagens |
| **Items** | Item Studio (criar itens e fotografar o ícone) |
| **Dialogue** | editor de diálogos |
| **Animation** | rig de animação, Animator Controller, modelo humanoide (Mixamo), separar a cabeça |
| **Stalker** | perseguidor, editor da árvore de comportamento, rotas e emboscadas |
| **Hurtbox** | zonas de dano nos inimigos |
| **Revive** | perfil e diagnóstico da reanimação |
| **Objectives** | catálogo de objetivos e ações para eventos |
| **Tutorials** | catálogo de dicas, volume de dica, reiniciar dicas vistas |
| **Demo** | jogo completo de demonstração e itens/diálogos/objetivos de exemplo |

Lista completa, e a tabela "número antigo → caminho novo", em [`Docs/Menus.md`](Docs/Menus.md).

## Controles padrão

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Mover / Olhar | WASD / mouse | Analógico esq. / dir. |
| Pular / Correr / Agachar | Espaço / Shift / Ctrl | A / L3 / B |
| Interagir | E | X |
| Agarrar e arrastar objeto | Botão esquerdo do mouse (com um item que bloqueia o arrastar na mão, o botão é do item) | RT |
| Usar item da mão esquerda / direita | Botão esquerdo / direito do mouse | LT / RT |
| Os dois lados na mesma mão (duas mãos) | G (toque = item da direita, segurar = item da esquerda) | RB |
| Mirar | Q | Select |
| Recarregar / Lanterna | R / F | D-pad ↓ / ↑ |
| Mostrar objetivos | J | R3 (clique do analógico direito) |
| Pausar | Esc | Start |

Tudo é remapeável em **Controles** (menu de pausa e menu principal > Opções). Detalhes das mãos em
[`Docs/HorrorFPS-Maos-Equipamento.md`](Docs/HorrorFPS-Maos-Equipamento.md).

## Documentação

| Doc | Assunto |
|---|---|
| [`HorrorFPS-Arquitetura`](Docs/HorrorFPS-Arquitetura.md) | base: input, movimento, interação, status, split-screen, fluxo de cenas, save, cutscenes |
| [`HorrorFPS-Maos-Equipamento`](Docs/HorrorFPS-Maos-Equipamento.md) | duas mãos, escolha da mão, tecla G, golpe fraco/forte |
| [`HorrorFPS-Itens-Sanidade-Corpo`](Docs/HorrorFPS-Itens-Sanidade-Corpo.md) | itens, inventário, sanidade, corpo, personagens, Item Studio, armas e lanterna |
| [`HorrorFPS-Animacao-Corpo`](Docs/HorrorFPS-Animacao-Corpo.md) · [`Locomocao-Mixamo`](Docs/HorrorFPS-Locomocao-Mixamo.md) | corpo visível aos outros jogadores, Animator, Mixamo |
| [`HorrorFPS-Perseguidor`](Docs/HorrorFPS-Perseguidor.md) · [`ArvoreComportamento`](Docs/HorrorFPS-ArvoreComportamento.md) | IA do perseguidor e a árvore de comportamento editável |
| [`HorrorFPS-Props`](Docs/HorrorFPS-Props.md) | props interativos |
| [`HorrorFPS-Reanimacao`](Docs/HorrorFPS-Reanimacao.md) | reanimação em co-op |
| [`HorrorFPS-Contexto-Scripts`](Docs/HorrorFPS-Contexto-Scripts.md) | guia de referência do código (receitas, armadilhas, índice de classes) |
| [`HorrorFPS-Objetivos-Tutorial`](Docs/HorrorFPS-Objetivos-Tutorial.md) | objetivos (lista, faixa, eventos, save) e dicas de tutorial de uma vez só + página "Como jogar" |
| [`Menus`](Docs/Menus.md) | todos os menus do Editor |
| [`Contribuindo`](Docs/Contribuindo.md) | convenções e cuidados antes de mexer no código |

Antes de mexer no código, leia [`Docs/Contribuindo.md`](Docs/Contribuindo.md).
