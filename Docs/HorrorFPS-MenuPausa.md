# HorrorFPS — Menu de pausa enxuto

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação ou algo estranho em Play, mande o texto do Console.

## Como ficou

**Menu de pausa** (5 botões): **Continuar · Salvar jogo · Carregar jogo · Opções · Sair**.
- *Salvar jogo* só aparece em partida (e fica apagado se não puder salvar); *Carregar jogo* fica apagado sem nenhum save.
- **Sair**, em partida, abre uma página com **Menu principal**, **Sair do jogo** e **Voltar**; cada um dos dois ainda pede confirmação ("O progresso não salvo será perdido") e **Não** volta para essa página. Fora de partida, *Sair* vai direto para a confirmação de fechar o jogo.
- A tecla de pausa volta uma página de cada vez: Controles → Opções → menu; confirmação → página de onde veio; menu → continua o jogo.

**Opções** agora tem tudo: sensibilidades, inverter Y, estilo da dica de interação, modos de arrastar e agachar, indicadores, efeitos de dano suaves, **Dicas na tela** (Desligado / Ligado / Ligado e pausa) e **Rever dicas**, **Áudio** (subpágina) e, embaixo, **Controles** (remapear) ao lado de **Voltar**. Vale também para as Opções do menu principal.

**Inventário** (Z/X ou LT/RT trocam de aba):
| Aba | Aparece quando | O que tem |
|---|---|---|
| Mochila | sempre | a grade de sempre |
| Chaves | tem itens-chave | itens de missão |
| Registros | há diário, documentos ou dicas | **Diários, Notas, Gravações, Documentos e Como jogar** (leitor ao lado) |
| Mapa | tem mapa | mapas |
| **Objetivos** (nova) | o jogo tem algum objetivo | Em andamento, Concluídos, Falharam (texto com rolagem; número amarelo = em andamento) |

Com baú aberto só a mochila aparece (como antes).

## Para mexer

- Ordem das abas: `InventoryScreen.Tabs.cs`, array `extra` em `BuildTabList` (hoje Chaves, Registros, Mapa, Objetivos).
- Para tirar a confirmação depois de escolher em **Sair** (a escolha já seria a decisão): em `PauseMenu.BuildExitPage` troque o `AskConfirm(...)` pela ação direta (`SceneDirector.ReturnToMainMenu` / `SceneDirector.Quit`).
- Quais dicas aparecem em Como jogar: o campo `In Menu` (e `Enabled`) de cada dica do `TutorialCatalog`.

## Arquivos

Novo: `UI/Inventory/ObjectivesBrowser.cs`. Alterados: `UI/PauseMenu.cs`, `UI/SettingsPanel.cs` (dicas e Controles dentro, linhas mais baixas), `UI/Inventory/RecordBrowser.cs` (categoria Como jogar), `UI/Inventory/InventoryScreen.Tabs.cs` e `InventoryScreen.cs` (aba Objetivos), `Items/InventoryTab.cs` (`Objectives = 4`, só visual), `Core/UIText.cs`.
**Removidos (apague no Unity):** `UI/InfoPanelBase.cs`, `Tutorials/HowToPlayPanel.cs`, `Objectives/ObjectivesPanel.cs` (e os `.meta`). Se esquecer, nada quebra: só ficam sem uso.

## Limites

- A página de Opções ficou comprida (11 linhas + botões); o painel de pausa subiu de 880 para 940 de altura. Não há rolagem nessa página: se algum dia ela ganhar mais linhas, vai passar do painel (aí vale dividir em subpáginas, como o Áudio).
- Em jogo dividido, Objetivos e Como jogar aparecem no inventário de cada jogador (a lista de objetivos é a mesma para todos; as dicas mostram os botões de cada um).
- As constantes antigas de `UIText` (`ObjectivesButton`, `HowToPlayButton`, `HowToPlayTitle`, `TipsOn...`) ficaram, sem uso.
