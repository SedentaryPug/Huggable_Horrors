# Horror FPS Framework (Unity) — input, movimento, interação, status, split-screen, fluxo de cenas, save/load, menu principal e cutscenes

Base para um jogo de terror em primeira pessoa com **multiplayer local / split-screen** (1 a 4 jogadores).
Tudo é C# puro + o asset de input; a UI é criada por código (nenhum prefab de interface necessário).

## Requisitos

- Unity **2022.3 LTS** ou **Unity 6** (usa `#if` onde a API mudou).
- Pacote **Input System** (`com.unity.inputsystem`) e **Project Settings > Player > Active Input Handling = Input System Package (New)** (ou *Both*).
- uGUI (vem por padrão). Texto usa `UnityEngine.UI.Text` com a fonte embutida; para usar TextMeshPro/sua fonte, troque em `UIFactory`.

## Instalação (3 passos)

1. Copie a pasta `Assets/HorrorFPS` para o `Assets` do seu projeto e espere compilar.
2. Menu **Tools > Horror FPS > 3. Create Full Game Demo** — cria o prefab do jogador, materiais, **5 cenas** (MainMenu, Core, Salão, Porão, Anexo),
   o catálogo do jogo e já arruma os *Build Settings*. (O item **2. Create Demo Scene** continua existindo: é só a sala de testes de movimento/escadas/arrastar, sem menu nem save.)
3. Abra a cena **MainMenu** e dê **Play**. Jogador 1 = teclado e mouse. Para o jogador 2, aperte um botão em um **controle**: a tela divide sozinha.

Roteiro da demo: *Novo jogo* → a **cutscene de introdução** mostra a mesa → pegue a **chave** → abra as portas (a de ferro só destranca com a chave) →
a **porta do porão** leva ao segundo nível, onde uma **cutscene de susto** dispara no meio da sala → suba de volta. Mova a caixa/barril, salve no **gravador**
(ou pelo menu de pausa), vá ao menu principal e use **Carregar**: tudo volta como você deixou. O **Anexo** (leste do Salão) é uma cena aditiva carregada por proximidade.
Também dá para dar Play direto em `Level_Hall` ou `Level_Cellar`: o jogo se monta sozinho (carrega o Core e posiciona os jogadores).

## Controles padrão

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Mover / Olhar | WASD / mouse | Analógico esq. / dir. |
| Pular | Espaço | A |
| Correr (gasta fôlego) | Shift | L3 |
| Agachar | Ctrl | B |
| Interagir / soltar escada | E | X |
| **Pular cutscene** | **Segurar** Interagir (E / X) | |
| Agarrar e arrastar objeto | Botão esquerdo do mouse | RT |
| Aproximar / afastar objeto | Scroll | D-pad ↑ / ↓ |
| Pausar | Esc | Start |

Tudo é remapeável em **Controles** (menu de pausa e menu principal > Opções).

## Arquitetura

```
Assets/HorrorFPS
├─ Input/      HorrorControls.inputactions   (mapa "Gameplay", esquemas Keyboard&Mouse e Gamepad)
│              InputNames · PlayerInputHandler (ÚNICA ponte PlayerInput -> gameplay) · InputBindingStore · InputGlyphs/InputGlyphSet
├─ Player/     PlayerContext (hub por jogador, travas de controle), FirstPersonController, PlayerClimber, PlayerInteractor, PlayerDragger
├─ World/      Interactable (base), Ladder, Draggable, Door, UnityEventInteractable, Pickup, SavePoint, Checkpoint
├─ Stats/      StatsComponent + Stat + StatusEffectData/Asset   (jogador, NPCs e objetos)
├─ Flow/       SceneDirector (troca de cenas), GameCatalog (mapa do jogo), SpawnPoint, LevelDoor, LevelTrigger, SceneStreamer, LoadingScreen
├─ Save/       GameSession (partida em andamento), SaveSystem (slots em disco), SaveableEntity + SaveableTransform/Active/Stats, SaveData
├─ Cutscenes/  CutsceneSequence, CutsceneStep (+ 16 passos prontos), CutsceneTrigger, CutsceneDirector, CutsceneOverlay
├─ UI/         MainMenu, PauseMenu, SaveSlotsPanel, SettingsPanel, ControlsPanel, GameOverScreen, PlayerHUD, Toast, UIFactory, UISprites
├─ Core/       LocalPlayerManager (split-screen), PauseManager, GameSettings, GameRuntime, UIText
└─ Editor/     Tools > Horror FPS (prefab do jogador, cena demo simples e jogo completo)
```

Regra de ouro: **gameplay nunca lê `InputAction` direto** — só `PlayerInputHandler`. Por isso pausar, travar controles
(`GameplayEnabled = false`, `AddControlLock`) e remapear funcionam para todo o jogo sem mexer nos sistemas.

Há objetos que o jogo cria sozinho (não precisam estar em cena): `[HorrorFPS] Runtime` (corrotinas e tempo de jogo), menu de pausa, tela de game over,
tela de loading, avisos (`Toast`) e o diretor de cutscenes.

---

## Estrutura de cenas e troca de nível (`SceneDirector`, `GameCatalog`)

```
MainMenu   (câmera + componente MainMenu; sem jogadores)
   │  Novo jogo / Continuar / Carregar
   ▼
Core       (LocalPlayerManager + PlayerInputManager; FICA carregada a partida inteira e guarda os jogadores)
 + Nível   (1 ou mais cenas carregadas ADITIVAMENTE por cima do Core; a primeira é a cena ativa: névoa, skybox, luz ambiente)
 + Streaming (cenas aditivas extras que entram e saem durante o nível, via SceneStreamer)
```

- Os jogadores moram no **Core**, então atravessam qualquer troca sem serem recriados (vida, efeitos e divisão de tela intactos).
- Toda troca faz: **trava os jogadores → fade para preto → captura o estado da cena que sai → descarrega → carrega a nova → reaplica o estado → posiciona os jogadores no `SpawnPoint` → fade de volta → destrava**.
  Depois de `GoToLevel` há um autosave (`SceneDirector.AutosaveOnLevelChange`, ligado por padrão), já com a tela clara. Ele é ignorado se uma cutscene já estiver tocando
  (`SaveSystem.CanSave`); por isso o trigger de início de nível da demo usa `Start Delay` de 0,8 s.
- **`GameCatalog`** (`Resources/HorrorFPS/GameCatalog.asset`, criado pelo wizard ou por *Create > HorrorFPS > Game Catalog*): nome das cenas de menu e Core, título do jogo,
  `firstLevel`, a lista de **níveis** (`id`, nome para o menu, **lista de cenas**, spawn padrão, dica de loading) e as dicas da tela de loading.
  Um nível pode ter várias cenas (geometria, luz, áudio, inimigos...). Sem catálogo, o "id do nível" é simplesmente o nome de uma cena.
- Todas as cenas precisam estar nos **Build Settings** (o wizard já faz isso).

API (tudo estático, pode chamar de qualquer script):

```csharp
SceneDirector.StartNewGame();                       // zera a partida e abre o primeiro nível
SceneDirector.LoadGame(slot);                       // carrega um save
SceneDirector.GoToLevel("cellar", "from_hall");     // id do nível + grupo de spawn (opcional)
SceneDirector.ReturnToMainMenu();  SceneDirector.Quit();
SceneDirector.LoadAdditive("Level_Annex");  SceneDirector.UnloadAdditive("Level_Annex");  // streaming manual
bool ocupado = SceneDirector.IsBusy;                // true durante qualquer troca

SceneDirector.TransitionStarted += ...;             // a troca começou
SceneDirector.LevelLoaded += id => ...;             // nível montado, jogadores no lugar, tela ainda PRETA (bom para cutscenes)
SceneDirector.LevelReady  += id => ...;             // tela clara, jogadores com controle
```

Componentes de cena (todos em *Add Component > HorrorFPS*):

| Componente | Para quê |
|---|---|
| `SpawnPoint` | Onde os jogadores aparecem. Vários pontos com o mesmo **Spawn Id** formam um grupo; **Slot** 0 = jogador 1, 1 = jogador 2... Id `start` é o padrão do nível. |
| `LevelDoor` | Interagível que troca de nível (`levelId`, `spawnId`, e `requiredVariable` opcional: só abre com aquela variável ligada, ex.: `tem_chave`). |
| `LevelTrigger` | Volume que troca de nível ao entrar; `Require All Players` espera todos os jogadores vivos. |
| `SceneStreamer` | Volume que **carrega** uma cena aditiva quando alguém chega e **descarrega** `unloadDelay` segundos depois que todos saem. O volume deve cobrir também o interior da cena streamada. |

## Save e load (`GameSession`, `SaveSystem`, `SaveableEntity`)

**O que é salvo**: variáveis do jogo, nível atual e spawn, tempo de jogo, os jogadores (posição, olhar, vida/stamina e efeitos) e o **estado de todas as cenas já visitadas**,
inclusive as que já foram descarregadas. É isso que faz uma caixa mexida no Porão continuar no mesmo lugar quando você sai e volta, e depois de carregar um save.

**Slots**: 0 = salvamento automático, 1 a 5 = manuais. Arquivos JSON em `Application.persistentDataPath/HorrorFPS/Saves` (`slot_N.json`, miniatura `slot_N.png`),
com escrita segura (arquivo temporário + cópia `.bak`) e leitura que tolera arquivo corrompido (aparece como "Save corrompido"). O autosave acontece em **checkpoints** e em **trocas de nível**.
`SaveSystem.CanSave` é falso durante trocas de cena, cutscenes, com alguém morto ou fora de partida.

### Variáveis (flags, contadores, textos)
```csharp
GameSession.SetBool("tem_chave", true);   bool ok = GameSession.GetBool("tem_chave");
GameSession.AddInt("pilhas", 1);          int n   = GameSession.GetInt("pilhas");
GameSession.SetString("nome", "Ana");     GameSession.SetFloat("x", 1.5f);
GameSession.VariableChanged += chave => ...;
```
Tudo isso entra no save sozinho. `Pickup`, `Door.unlockVariable`, `LevelDoor.requiredVariable` e os passos de cutscene usam essas variáveis.

### Objetos da cena: `SaveableEntity` + `ISaveable`
Para um objeto da cena entrar no save, coloque **`SaveableEntity`** nele (o id único é gerado sozinho; **não copie** o componente de um objeto para outro — duplicar o objeto no Editor gera um id novo) e
adicione os componentes que dizem *o que* guardar:

| Componente | Guarda |
|---|---|
| `SaveableTransform` | posição, rotação (e escala, opcional) — caixas, armários, itens soltos |
| `SaveableActive` | ligado/desligado — item escondido, luz apagada, inimigo desativado |
| `SaveableStats` | vida, stamina e efeitos de um `StatsComponent` (NPC, objeto destrutível) |
| `Door`, `UnityEventInteractable` | já implementam `ISaveable`: porta aberta/trancada, interação já usada (`oneShot`) |

Se o objeto for **destruído** (ex.: `Pickup` pego, porta quebrada) ele continua destruído quando você voltar à cena.
Só entra no save o que **já existia quando a cena abriu**: objetos instanciados em tempo de execução não são recriados pelo load — guarde os dados deles em variáveis
e recrie em `SaveSystem.AfterLoad`.

Estado próprio em 15 linhas:
```csharp
public class Gerador : MonoBehaviour, ISaveable
{
    [System.Serializable] class Estado { public bool ligado; public float combustivel; }
    bool ligado; float combustivel = 100f;

    public string SaveKey { get { return "gerador"; } }   // único dentro do objeto
    public string CaptureState() { return JsonUtility.ToJson(new Estado { ligado = ligado, combustivel = combustivel }); }
    public void RestoreState(string json) { Estado e = JsonUtility.FromJson<Estado>(json); ligado = e.ligado; combustivel = e.combustivel; }
}
// + SaveableEntity no mesmo objeto
```
Dados globais (inventário...): escreva em `GameSession` dentro de `SaveSystem.BeforeSave` e leia em `SaveSystem.AfterLoad`.

### Pontos de save no mundo
- **`SavePoint`** (gravador, máquina de escrever): interagir abre a tela de slots (`Open Slot Menu`) ou salva direto no autosave.
- **`Checkpoint`**: volume que faz autosave quando alguém entra e, se `Set Spawn Id` estiver preenchido, vira o ponto de retorno.
- **`Pickup`**: item coletável; liga uma variável (ou soma num contador) e some para sempre. Combine com `SaveableEntity`.
- Também dá pelo código: `SaveSystem.SaveToSlot(slot)`, `SaveSystem.Autosave()`, `SaveSystem.GetAllInfo()`, `SaveSystem.GetMostRecentSlot()`, `SaveSystem.Delete(slot)`.

## Menus

- **Menu principal** (`MainMenu`, na cena MainMenu): *Continuar* (aparece se houver save), *Novo jogo*, *Carregar*, *Opções* (inclui **Controles**) e *Sair* (com confirmação).
  O título vem do `GameCatalog` (ou do nome do produto) e pisca como lâmpada ruim. A câmera/cenário de fundo é da própria cena: edite à vontade.
- **Pausa** (`PauseMenu`): Continuar, **Salvar jogo**, **Carregar jogo**, Controles, Opções, **Menu principal** e **Sair** (os dois com confirmação). Quem pausou é o dono do menu.
- **Slots** (`SaveSlotsPanel`): miniatura da hora do save, nome do nível, data, tempo de jogo e nº de jogadores; sobrescrever e apagar pedem um segundo clique.
- **Game over** (`GameOverScreen`): aparece quando **todos** os jogadores morrem — *Carregar último save* ou *Menu principal*.

---

## Cutscenes (`CutsceneSequence`, `CutsceneStep`, `CutsceneTrigger`)

Uma cutscene é um objeto de cena com **`CutsceneSequence`** e **passos (`CutsceneStep`) como filhos, que rodam na ordem da hierarquia**. Para tocar: um **`CutsceneTrigger`**
(entrar num volume / começo do nível / manual), um `UnityEvent` chamando `Play()` ou o código `sequence.Play()`.

Opções da sequência: `Id` (o save lembra que ela já passou), `Play Once`, `Lock Players` (trava movimento, olhar e interação), `Use Cutscene Camera`
(câmera própria em **tela cheia**, esconde o split-screen e o HUD; desligado, mantém a visão dos jogadores — bom para "todos viram a cabeça"), `Custom Camera` (URP/HDRP/Cinemachine),
`Letterbox` (barras pretas), `Allow Skip` + `Skip Hold Seconds` (**segurar Interagir** pula; o resultado final de cada passo restante é aplicado na hora).

Em cada passo: **`Delay`** (espera antes de começar) e **`Run With Next`** (começa e já segue para o próximo, em paralelo — ex.: câmera andando enquanto a legenda aparece).

| Passo | O que faz |
|---|---|
| `CameraMoveStep` | Move a câmera entre dois marcadores (`from` → `to`), com curva, olhar para um alvo e FOV |
| `CameraShakeStep` | Treme a câmera (explosão, passos) |
| `SubtitleStep` | Legenda (fala opcional, tempo manual ou automático) |
| `PlayerLookStep` | Vira a visão de todos os jogadores para um alvo (sem câmera de cutscene) |
| `FlickerLightsStep` | Luzes piscam e terminam acesas ou apagadas |
| `ActivateStep` | Liga/desliga objetos (o monstro aparece) |
| `MoveObjectStep` | Move/gira um objeto até outro ponto |
| `AnimatorStep` | Trigger, bool, float, int ou estado de um `Animator` |
| `AudioStep` | Toca um `AudioSource` da cena ou um clipe solto |
| `FadeStep` | Fade para preto / de volta |
| `WaitStep` | Espera N segundos (tempo de jogo: pausa junto) |
| `SetVariableStep` | Liga/desliga flag, soma contador, guarda texto (vai para o save) |
| `EventStep` | Dispara um `UnityEvent` (qualquer método de qualquer script) |
| `PlacePlayersStep` | Leva os jogadores para um grupo de `SpawnPoint` (acordar na cama) |
| `GoToLevelStep` | Troca de nível no fim da cutscene — **coloque como ÚLTIMO passo** |
| `TimelineStep` | Toca um `PlayableDirector` (só existe com o símbolo `HORRORFPS_TIMELINE` em *Scripting Define Symbols* e o pacote Timeline) |

Boas práticas e detalhes:
- A cutscene respeita a pausa (usa tempo de jogo). *Novo jogo*, *Carregar* e *Menu principal* **abortam** a cutscene na hora; trocar de nível (`GoToLevelStep`, `LevelDoor`) **não** aborta,
  e no caso do `GoToLevelStep` a câmera da cutscene fica até a tela escurecer, então a próxima cena começa limpa.
- **Persistência**: o que a cutscene muda e deve valer no save (luz que ficou apagada, monstro que sumiu) precisa de `SaveableEntity` + `SaveableActive`/`SaveableTransform` no objeto afetado.
- Com URP/HDRP, se a câmera gerada (cópia da câmera do jogador 1) perder efeitos, crie uma câmera na cena com seu volume/Cinemachine e arraste em **Custom Camera**.

**Passo novo** (um arquivo por classe, com o mesmo nome — regra do Unity):

```csharp
using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    public class BlackoutStep : CutsceneStep
    {
        [SerializeField] Light[] lights;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
            yield return CutsceneContext.Wait(1.5f);    // só 'yield return null' ou Wait(...) aqui dentro
        }

        public override void OnSkip(CutsceneContext ctx)  // jogador pulou: aplique o resultado final na hora
        {
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = false;
        }
    }
}
```
`CutsceneContext` oferece a câmera da cutscene (`CameraPosition/Rotation/Fov/Shake`), `ShowSubtitle/HideSubtitle` e `Wait`.

---

## Sistemas anteriores

### Input e remapeamento
- Cada jogador tem sua própria cópia do asset (o `PlayerInput` clona), então mouse/teclado e controles não se misturam.
- Remapeamento por captura interativa, **só aceita dispositivos do esquema editado**, troca automática quando o botão já está em uso
  e salva por ID de binding (vale para todos os jogadores). Botão "Restaurar padrão" incluso. Também funciona no menu principal.
- Para **adicionar uma action**: (1) crie no `.inputactions` (mapa Gameplay), (2) constante em `InputNames`, (3) `FindAction` + propriedade em `PlayerInputHandler`,
  (4) se for remapeável, inclua em `InputNames.Rebindable` e dê nome em `UIText.ActionName`.

### Movimento (`FirstPersonController`)
Andar/correr (gasta **Stamina**), agachar com checagem de teto, pular com *coyote time*, dano de queda, head bob, eventos
`Footstep(intensidade)`, `Jumped`, `Landed` (ótimos para áudio e para monstros "ouvirem" o jogador).
Multiplicadores nomeados: `SetSpeedModifier(chave, mult)`. Velocidade também respeita o stat `MoveSpeed`.

### Escadas (`Ladder`) — dois modos
Pivô do objeto no **centro da base**, eixo **Z (azul) apontando para o lado de onde o jogador sobe**, eixo Y = "para cima" da superfície
(funciona em paredes inclinadas e tetos). Mantenha a escala em (1,1,1) e coloque um **BoxCollider em Trigger** na frente para o jogador conseguir mirar.
Selecione o objeto para ver o gizmo (retângulo, direção e ponto de saída).

- **Fixed (escada fixa)**: trilho vertical. W/S sobem/descem, **só sobe enquanto o corpo está virado para a escada** (`lookAngle`).
  Se o jogador olhar para o lado, a dica mostra "Olhe para a escada para subir". A câmera fica limitada a `maxLookYaw` graus ao redor da escada.
- **Free (escalada livre)**: anda pela superfície em **todas as direções** (W/S = cima/baixo, A/D = esquerda/direita, diagonais),
  com a câmera livre. A largura/altura (`size`) delimita a área.
- Comum aos dois: **E** solta, **Pular** salta para trás, passar do topo faz a saída automática (`topExit` ou um ponto à frente do topo) e,
  na base, descer solta. A escada é um `Interactable`, então a dica de interação aparece sozinha.

### Interação e feedback visual
`PlayerInteractor` mira do centro da câmera (SphereCast fino, enxerga triggers e ignora o próprio corpo) e publica um `PromptData`.
O `PlayerHUD` desenha, na metade de tela do jogador: mira que cresce/avermelha ao mirar algo interagível + dica.

- **Botão + texto**: caixa com o botão atual daquela action (muda com remapeamento e com teclado/controle) + "Abrir porta".
- **Somente ícone**: só o ícone da ação (sem texto). Se estiver bloqueado (ex.: "Trancado") o texto aparece junto, porque ícone sozinho não explica.
- Escolha **por objeto** (`Prompt Mode` no `Interactable`) ou deixe **seguir a opção do jogador** (Opções > Dica de interação).
- Ícones: sprite próprio no `Interactable.icon`, ou os embutidos (`IconKind`: Generic, Grab, Climb, Door) gerados por código. Para glifos de controle
  (A/B/X/Y...), crie um `InputGlyphSet` (Create > HorrorFPS) e atribua no `PlayerHUD`.
- Segurar para interagir: `holdDuration > 0` mostra a barra de progresso.

Novo interagível em 10 linhas:

```csharp
public class Lever : Interactable
{
    protected override string DefaultVerb { get { return "Puxar"; } }
    public override bool CanInteract(PlayerContext p) { return !pulled; }
    public override string GetBlockedReason(PlayerContext p) { return "Já puxada"; }
    public override void Interact(PlayerContext p) { pulled = true; /* ... */ }
    bool pulled;
}
```
Sem código: `UnityEventInteractable` (eventos no Inspector). Exemplos com estado salvo: `Door` (abre/fecha/tranca, destranca por variável) e `Pickup`.

### Arrastar objetos (`Draggable` + `PlayerDragger`)
Objeto com `Rigidbody` + `Draggable`. O ponto da mira é puxado até um alvo na frente da câmera **por força** (mola + amortecimento): objetos pesados aceleram
menos, o jogador anda mais devagar, o objeto balança ao ser puxado e é solto sozinho se ficar preso. Scroll/D-pad aproximam e afastam.
- `Carry`: segue a mira em 3D. `Slide`: só desliza no chão (móveis pesados).
- Acima de `maxMass` (padrão 60 kg) aparece "Muito pesado". Modo **segurar** ou **alternar** em Opções > Arrastar objetos.
- Para o objeto lembrar onde foi deixado: `SaveableEntity` + `SaveableTransform`.

### Status (`StatsComponent`) — jogador, NPCs e objetos
Cada entidade escolhe seus `Stat`s: *recursos* (Vida, Stamina, Sanidade, Integridade: valor atual com regeneração e atraso) ou *atributos* (MoveSpeed, Armor...).
Presets pelo menu de contexto do componente: **Jogador / Monstro / Objeto**. O "stat vital" recebe dano e define a morte
(`Health` para seres vivos, `Durability` para objetos; opção de destruir ao morrer).

```csharp
// dano em qualquer coisa que tenha StatsComponent (jogador, monstro, caixa...)
target.GetComponent<IDamageable>().ApplyDamage(new DamageInfo(25f, DamageType.Physical, gameObject));

// efeito por código: lento (MoveSpeed x0.6) e perde sanidade por 8 s
var slow = new StatusEffectData
{
    id = "slow", displayName = "Lento", duration = 8f,
    modifiers = new[] { new StatusEffectData.StatChange { stat = StatType.MoveSpeed, op = ModifierOp.Multiply, value = 0.6f } },
    overTime  = new[] { new StatusEffectData.StatTick   { stat = StatType.Sanity, perSecond = -1f } }
};
stats.ApplyEffect(slow, gameObject);

// ou crie um asset (Create > HorrorFPS > Status Effect) e use stats.ApplyEffect(asset)
```
Eventos: `StatChanged`, `Damaged`, `Died`, `EffectChanged` (+ `UnityEvent`s no Inspector). O HUD mostra barras que só aparecem quando mudam ou não estão cheias, e um flash vermelho ao tomar dano.

### Pausa (`PauseMenu` + `PauseManager`)
Qualquer jogador pausa; **quem pausou é o dono** e navega/remapeia com o dispositivo dele (mouse também funciona). `Time.timeScale = 0`, áudio pausado, gameplay de todos desligado.
Se um controle desconecta, o jogo pausa. A pausa fica separada das **travas de controle** (cutscenes e trocas de nível travam os jogadores sem pausar o jogo).

### Split-screen (`LocalPlayerManager`)
Usa o `PlayerInputManager` do Unity: cada jogador tem câmera, `AudioListener` (só o 1º fica ativo), HUD no próprio viewport e input próprio.
Quem entra depois usa um dispositivo livre (aperte um botão). No jogo completo os jogadores nascem nos `SpawnPoint` do nível (grupo + slot).
- `Max Players` (padrão 4): quando lota, a entrada fecha sozinha e reabre se alguém sair.
- `Join By Button Press`: ligado = qualquer botão de um dispositivo livre cria um novo jogador; desligado = você chama `PlayerInputManager.JoinPlayer(...)` quando quiser.
- `First Player Prefers Gamepad`: o jogador 1 já nasce no controle se houver um conectado.
- Os esquemas de controle ficam travados por jogador (`neverAutoSwitchControlSchemes`) enquanto a entrada por botão estiver ligada ou houver 2+ jogadores,
  senão o jogador 1 "roubaria" o controle que o jogador 2 acabou de usar. Para um jogo só solo com troca teclado ↔ controle ao vivo, desligue `Join By Button Press`.

---

## Montando o seu jogo (receita)

1. **Cenas**: `MainMenu` (câmera + objeto com `MainMenu`), `Core` (objeto com `PlayerInputManager` + `LocalPlayerManager` com o `Player.prefab`) e uma cena por nível. Todas nos Build Settings.
2. **Catálogo**: *Create > HorrorFPS > Game Catalog* dentro de uma pasta `Resources/HorrorFPS/` (nome do arquivo `GameCatalog`). Preencha título, `firstLevel` e os níveis.
3. **Em cada nível**: um grupo de `SpawnPoint` com id `start` (um ponto por jogador, `slot` 0, 1, 2, 3), `LevelDoor`/`LevelTrigger` para os outros níveis
   (e um grupo de spawn com o id que eles apontam) e `SaveableEntity` em tudo que muda de estado.
4. **Save points**: `SavePoint` em objetos do mundo e `Checkpoint` em corredores entre salas importantes.
5. **Cutscenes**: `CutsceneSequence` + passos filhos + `CutsceneTrigger`.
6. Variáveis: escolha nomes (`tem_chave`) e reuse os mesmos em `Pickup`, `Door.unlockVariable`, `LevelDoor.requiredVariable` e `SetVariableStep`.

## Adaptando ao seu projeto
- **Textos**: `Core/UIText.cs` (PT-BR; troque ou ligue à sua localização).
- **Visual da UI**: `UIFactory` (cores, fonte) e `UISprites`. O fundo do menu principal é a própria cena MainMenu.
- **Camadas/máscaras**: `PlayerInteractor.mask`, `FirstPersonController.obstructionMask`.
- **Duração do fade**: constante `FadeSeconds` em `SceneDirector`; **formato do save**: `Save/SaveData.cs` (classes) e `FormatVersion` em `SaveSystem`.
- Sem `.asmdef` de propósito (para não quebrar referências). Se usar assemblies, crie um para `HorrorFPS` referenciando `Unity.InputSystem` e `UnityEngine.UI`, e outro só-Editor.

## Observações importantes
- **Código escrito sem compilar no Unity.** Foi revisado por scripts (delimitadores, referências cruzadas, número de argumentos das chamadas, um componente por arquivo, usings)
  e por leitura, mas se aparecer algum erro de compilação na primeira importação, envie a mensagem que eu corrijo.
- **Um componente por arquivo**: toda classe `MonoBehaviour`/`ScriptableObject` está num arquivo com o mesmo nome (exigência do Unity para anexar ao objeto). Siga isso nos seus scripts.
- A cena **HorrorDemo** (item 2 do menu) não tem sessão de jogo nem save; use o jogo completo (item 3) para testar fluxo de cenas, save e cutscenes.
- Os `.meta` não vão no pacote: o Unity gera na importação. Se trocar uma cena de nome, atualize o `GameCatalog` e os `SceneStreamer`.
- Cenas com **URP + Camera Stack**: o HUD usa Canvas *Screen Space - Camera* na câmera do jogador; funciona com câmera base única.
