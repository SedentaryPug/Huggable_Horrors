# Objetivos e dicas de tutorial

Dois sistemas que ajudam o jogador a saber **o que fazer** (objetivos) e **como fazer** (dicas de tutorial). Os dois são criados sozinhos ao iniciar o jogo: não precisam estar em cena. Código em `Objectives/` e `Tutorials/` (namespace `HorrorFPS`).

> Status: escritos e revisados no código, **ainda não compilados nem testados na Unity** (o ambiente onde foram feitos não tem a Unity). Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Objetivos

### O que o jogador vê

- **Faixa no topo** quando um objetivo começa, termina ou falha: `NOVO OBJETIVO` (com a descrição), `OBJETIVO CONCLUÍDO`, `OBJETIVO FALHOU`.
- **Lista no canto superior esquerdo** com os objetivos ativos e o progresso dos contadores (`Juntar pilhas (2/3)`). Ela aparece por alguns segundos quando algo muda e some sozinha, para não poluir a tela. **J** (teclado) ou o **clique do analógico direito / R3** (controle) mostra de novo na hora; apertar outra vez esconde. Escondido num esconderijo, o R3 só segura o fôlego (não mostra a lista). A lista some com o inventário aberto.
- **Pausa > Objetivos**: o que está em andamento (com a descrição), o que foi concluído e o que falhou. O botão só aparece se houver objetivos cadastrados.
- Vale para **todos os jogadores** (a lista é do grupo). Cada jogador vê na tela dele.

### Como definir os objetivos

Duas formas, que se misturam:

1. **Catálogo** (recomendado): `Tools > Horror FPS > Objectives > Create Objective Catalog` cria `Resources/HorrorFPS/ObjectiveCatalog.asset`. Cada objetivo tem:

| Campo | Para quê |
|---|---|
| `Id` | identificador único sem espaços (`achar_chave`). É por ele que eventos, scripts e o save se referem ao objetivo |
| `Title` / `Description` | texto da lista / dica longa (página Objetivos do menu de pausa e faixa de "novo objetivo") |
| `Auto Start` | começa sozinho quando a partida começa |
| `Start After` | começa sozinho quando **todos** os objetivos listados (ids) terminarem: forma de encadear |
| `Complete Variable` | termina sozinho quando esta variável do jogo ficar verdadeira (a mesma de Pickup, Door, SmartEvent, Set Variable) |
| `Counter Variable` + `Target` | termina sozinho quando o contador (variável inteira) chegar ao alvo, e mostra o progresso `(2/3)` |
| `Optional` | aparece marcado como opcional e depois dos obrigatórios |
| `Notify` | mostra a faixa ao começar/terminar/falhar |

2. **Código**: `Objectives.Register(new ObjectiveEntry { id = "...", title = "..." })`. Um objetivo registrado por código com o mesmo id de um do catálogo o substitui.

Sem catálogo nem código o sistema fica vazio e invisível (sem botão no menu de pausa).

### Como usar em cena (sem programar)

- **Por variável** (o jeito mais simples): preencha `Complete Variable` ou `Counter Variable` no catálogo. Tudo que já mexe em variáveis do jogo conclui o objetivo sozinho: `Pickup` ("pegou a chave"), `Door`, `SmartEvent` com variável, passo de cutscene `Set Variable`. Para um contador, some 1 cada vez (`GameSession.AddInt("geradores", 1)` ou `Set Variable > Add Int`).
- **Componente `Objective Actions`** (`Tools > Horror FPS > Objectives > Add Objective Actions To Selected`): expõe `StartObjective`, `CompleteObjective`, `FailObjective`, `AddProgress` e `ResetObjective` (usam o `Objective Id` do componente) e as versões `...ById(string)` (o id é escrito no evento). Ligue num UnityEvent: `SmartEventTrigger` (volume "entrou na sala" com **Once** e **Save**), botão, `Interactable`, fim de diálogo...
- **Passo de cutscene `Objective`** (`HorrorFPS/Cutscene/Steps/Objective`): começa, termina, falha, soma progresso ou reinicia um objetivo. Se o jogador pular a cutscene, o resultado é aplicado do mesmo jeito.
- **Condição de SmartEvent `Objetivos/Estado do objetivo`**: deixa um evento rodar só se o objetivo está Não começou / Ativo / Feito / Falhou.

### Como usar em código

```csharp
Objectives.Start("achar_chave");              // aparece na lista
Objectives.AddProgress("geradores");          // +1 no contador (termina sozinho ao chegar no alvo)
Objectives.Complete("achar_chave");           // termina e libera os que têm Start After = achar_chave
Objectives.Fail("salvar_o_cachorro");
bool feito = Objectives.IsDone("achar_chave");
Objectives.Started += e => ...;               // também Completed, Failed e Changed
```

### Como funciona por dentro

- O estado de cada objetivo fica nas **variáveis do save** (`GameSession`): `objective.<id>` = 1 ativo, 2 feito, 3 falhou. Por isso **salvar, carregar e Novo Jogo funcionam sozinhos**, sem código de save.
- `ObjectiveRunner` (criado sozinho) reavalia tudo quando uma variável muda e a cada meio segundo; começa os `Auto Start` uma vez por partida, assim que há jogadores em cena (espera ~1 s para a tela deles existir).
- Eventos (`Started`, `Completed`, `Failed`) só disparam quando algo muda de verdade; carregar um save não repete a faixa de "novo objetivo".

### Objetivos de exemplo

`Tools > Horror FPS > Demo > Create Sample Objectives` acrescenta 4 objetivos de exemplo ao catálogo (explorar → juntar 3 pilhas → fugir; mais um opcional). Eles começam sozinhos: **apague-os do catálogo depois de testar**. Para testar sem montar cena, num script de teste: `GameSession.SetBool("sample.explored", true)` e `GameSession.AddInt("sample.batteries", 1)`.

---

## 2. Dicas de tutorial

### O que o jogador vê

Um cartão no lado esquerdo da tela explicando **como executar uma ação**, com o botão que ele realmente usa:

> **DICA · AGACHAR** — Use **Ctrl** para agachar e passar por vãos baixos.

- **Aparece uma única vez para cada jogador** (no co-op local, cada um vê as dicas dele, no dispositivo que ele usa: teclado mostra `Espaço`, controle mostra `A`). O "já vi" fica no perfil do computador (`PlayerPrefs`), vale para qualquer save: um Novo Jogo não repete.
- Uma dica por vez, na ordem pedida. Fica parada com o jogo pausado, o inventário aberto ou em cutscene. Fecha logo depois de o jogador fazer a ação ensinada (`Dismiss Action`).
- **Pausa > Como jogar**: a lista de todos os comandos, com os botões do jogador que abriu o menu. É onde ele relê o que a tela mostrou uma vez. Na mesma página: **Dicas na tela: Ligado/Desligado** (para quem já conhece o jogo) e **Rever dicas** (esquece o que já foi visto).
- O texto troca `{Nome}` pelo botão. Nomes: `Move`, `Look`, `Jump`, `Sprint`, `Crouch`, `Interact`, `Grab`, `GrabDistance`, `Pause`, `Inventory`, `Objectives`, `Reload`, `Flashlight`, `Aim`, `HoldBreath`, `TwoHand`, `Inspect`, `HandLeft`, `HandRight`, `Hotbar`. Os botões respeitam o remapeamento (as ações do jogo base). As ações do inventário/mãos (`ItemInput`) ainda não são remapeáveis; o texto mostra o botão fixo delas.

### Dicas padrão

Valem sem configurar nada (`Tutorials/TutorialDefaults.cs`):

| Id | Quando aparece |
|---|---|
| `move` | 2,5 s depois de o jogador aparecer (some ao se mexer) |
| `sprint`, `jump`, `crouch` | depois de andar 8 s, 18 s e 30 s no total (parado não conta) |
| `interact`, `grab` | a primeira vez que aparece uma dica de interação dessa ação |
| `ladder` | **manual** (volume `Tutorial Trigger` perto da escada) |
| `hide` | ao entrar num esconderijo |
| `inventory`, `hotbar` | ao pegar o primeiro item (a segunda ~16 s depois) |
| `flashlight`, `melee`, `firearm` | ao equipar uma lanterna / arma branca / arma de fogo |
| `objectives` | quando o primeiro objetivo começa |
| `pause` | 2 minutos de jogo |
| `hurt`, `low_stamina`, `low_sanity` | primeiro dano / fôlego abaixo de 20% / sanidade abaixo de 40% (avisos: não aparecem na lista Como jogar) |

### Mudar textos, gatilhos ou acrescentar dicas

`Tools > Horror FPS > Tutorials > Create Tutorial Catalog` cria `Resources/HorrorFPS/TutorialCatalog.asset` **já com as dicas padrão** para editar. Uma dica com o mesmo `Id` de uma padrão a **substitui** (desmarque `Enabled` para desligar uma); `Id` novo **acrescenta**. Campos principais: `Title`, `Text`, `When` (gatilho), `Filter`, `Delay`, `Threshold`, `Seconds`, `Dismiss Action`, `In Menu`.

| `When` | Dispara | `Filter` |
|---|---|---|
| `Manual` | só quando chamada | — |
| `GameStart` | `Delay` s depois de o jogador aparecer | — |
| `Walking` | depois de andar `Delay` s (somando) | — |
| `PromptShown` | primeira dica de interação | ação (`Interact`, `Grab`...) ou vazio |
| `ItemPicked` | pegou um item | — |
| `ItemEquipped` | equipou um item | `Flashlight`, `Melee`, `Firearm` ou vazio |
| `Damaged` | levou dano | — |
| `LowStat` | status abaixo de `Threshold` (0 a 1) | `Health`, `Stamina`, `Sanity`... |
| `Hid` | entrou num esconderijo | — |
| `ObjectiveStarted` | um objetivo começou | — |

Para eventos, `Delay` é a espera depois do evento.

### Mostrar uma dica por cena ou por código

- **Volume `Tutorial Trigger`** (`Tools > Horror FPS > Tutorials > Create Tutorial Trigger Volume`): escreva o `Tutorial Id`; ao entrar, o jogador vê a dica (se ainda não viu). `All Players` mostra para todos quando qualquer um entra.
- **Passo de cutscene `Tutorial Tip`**: pede a dica para todos; ela espera a cutscene acabar.
- **Código**: `Tutorials.Show(jogador, "ladder")`, `Tutorials.ShowAll("hide")`, `Tutorials.Register(new TutorialEntry { ... })`.
- `Tools > Horror FPS > Tutorials > Reset Tips Seen` esquece as dicas vistas (para testar de novo).

---

## 3. Arquivos

| Arquivo | O que faz |
|---|---|
| `Objectives/ObjectiveTypes.cs`, `ObjectiveCatalog.cs` | dados: estado, entrada de objetivo, catálogo (ScriptableObject) |
| `Objectives/Objectives.cs` | API estática, regras de início/término, eventos |
| `Objectives/ObjectiveRunner.cs` | mantém o sistema vivo (variáveis, `Auto Start`) |
| `Objectives/ObjectivesHUD.cs` | módulo do jogador: lista + faixa na tela dele, tecla J / R3 |
| `Objectives/ObjectiveActions.cs`, `ObjectiveStep.cs` | uso em cena: componente para UnityEvent, passo de cutscene |
| `Objectives/ObjectivesPanel.cs` | página "Objetivos" do menu de pausa |
| `Tutorials/TutorialTypes.cs`, `TutorialCatalog.cs`, `TutorialDefaults.cs` | dados: dica, catálogo, dicas padrão |
| `Tutorials/Tutorials.cs` | API estática, regra "uma vez por jogador", pedido de dica |
| `Tutorials/TutorialText.cs` | troca `{Nome}` pelo botão do jogador (teclado/controle) |
| `Tutorials/TutorialHUD.cs`, `TutorialDirector.cs` | módulos do jogador: cartão na tela, gatilhos automáticos |
| `Tutorials/TutorialTrigger.cs`, `TutorialStep.cs` | uso em cena: volume e passo de cutscene |
| `Tutorials/HowToPlayPanel.cs`, `UI/InfoPanelBase.cs` | página "Como jogar" do menu de pausa (e a base das páginas de leitura) |
| `Editor/Objectives/ObjectivesMenu.cs`, `Editor/Tutorials/TutorialsMenu.cs` | menus do Editor |

Alterados: `Core/PlayerModules.cs` (instala `ObjectivesHUD`, `TutorialHUD`, `TutorialDirector` em cada jogador), `Input/ItemInput.cs` (ação `Objectives`), `Input/InputGlyphs.cs` (texto de botão por dispositivo), `Core/GameSettings.cs` (`TipsEnabled`), `Core/UIText.cs` (textos), `UI/PauseMenu.cs` (botões e páginas; os botões da página principal ficaram mais baixos, 56 em vez de 64, para caberem 9), `SmartEvents/HorrorFPSConditions.cs` (condição de objetivo), `Editor/HorrorFPSMenu.cs` (áreas `Objectives` e `Tutorials`).

## 4. Limites conhecidos

- O **menu principal** (antes de começar a partida) não tem a página "Como jogar": as dicas existem dentro do jogo, no menu de pausa.
- A tecla de objetivos (**J / R3**) vive no `ItemInput` e, como as outras ações dele, não aparece no painel de remapeamento (para mudar, edite os caminhos em `ItemInput.Build()`).
- Se o jogador trocar de teclado para controle no meio de uma dica, o cartão já aberto não muda de texto (a lista Como jogar, ao ser aberta, já mostra os botões do dispositivo atual).
- Um diário/caderno de notas (reler documentos achados) é um passo futuro e ainda não existe.
