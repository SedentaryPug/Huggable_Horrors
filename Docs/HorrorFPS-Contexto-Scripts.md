# HorrorFPS — Contexto para editar e criar scripts

> **Para quê:** guia de trabalho do Claude no projeto **Huggable Horrors** (terror em primeira pessoa, Unity, multiplayer local em tela dividida). Leia a seção 0 antes de qualquer tarefa de script; o resto é consulta.
> **Origem:** gerado em 09/11/2026 a partir do código real do repositório `SedentaryPug/Huggable_Horrors` (commit `9ccdc01`: 301 arquivos `.cs`, 49.309 linhas), cruzado com os 9 docs do Projeto.
> **Atualização (mesmo dia):** o **pacote A** (ajustes + cena completa, menu 27) soma 3 arquivos novos (`Editor/ProjectPaths.cs`, `Editor/HorrorFPSSetup.Showcase.cs`, `Editor/HorrorFPSSetup.Showcase.Zones.cs`) e altera 13 (12 em `Editor/` + `Revive/ReviveDownable.cs`): 304 arquivos, 50.703 linhas. As partes do pacote A estão nas seções 2, 6 (R10 e **R12**), 7.6, 8, 9 e 11 e no Apêndice A. Se o pacote A ainda não foi aplicado ao seu projeto, esses trechos descrevem o que ele traz, não o que o seu projeto já tem. O **pacote B** (opcional) remove os menus 2, 6, 13, 17 e 18 (seção 8, item 8).
> **Legenda:** **[V]** conferido no código-fonte (trecho lido ou assinatura listada) · **[D]** vem dos docs do Projeto, não conferido linha a linha · **[?]** suposição ou ponto em aberto · **[Unity]** comportamento geral do Unity, não específico do projeto.
> **Aviso:** nada aqui foi compilado no Unity. "Conferido" = lido no código + checagem estática de referências (3.610 referências `Tipo.Membro` no commit `9ccdc01`, 3.634 com o pacote A; 0 suspeitas nas duas). Detalhes na seção 11.
> **Manutenção:** se o código mudar, atualize as seções afetadas e a linha de commit acima. Este arquivo é um mapa, não a fonte da verdade: em caso de conflito, o código vence.
> **Mapa do arquivo:** 0 checklist · 1 resumo e "Pedido → onde mexer" · 2 layout do repositório · 3 convenções · 4 arquitetura e regras de ouro · 5 API conferida · 6 receitas R1–R12 · 7 armadilhas · 8 pendências · 9 cenas, prefabs e assets · 10 docs do Projeto · 11 verificação · Apêndice A índice de classes.

---

## 0. Checklist antes de escrever qualquer script

1. **Onde mexer?** Use a tabela "Pedido → onde mexer" (seção 1). Para detalhes de um sistema, abra o doc dele (seção 10).
2. **Editar:** abra o arquivo real, mude o mínimo e siga o estilo ao redor. Não reescreva nem reformate o arquivo inteiro. Em módulo que tem ponte (`*Bridge`), siga a ponte que o módulo já usa.
3. **Criar:** um MonoBehaviour/ScriptableObject por arquivo, nome do arquivo = nome da classe, `namespace HorrorFPS`, 4 espaços, chaves na linha de baixo, LF; comentários, `Tooltip` e textos em PT-BR (seção 3).
4. **Estado que precisa sobreviver** a salvar ou trocar de nível → receita R11. Nunca em campo estático.
5. **Estático novo** → zere em `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics()` (58 arquivos já fazem isso).
6. **Entrada:** nunca `Keyboard.current`/`Gamepad.current` solto em código de gameplay — veja 5.6.
7. **Travar o jogador:** `AddControlLock(chave)` / `RemoveControlLock(chave)` com chave própria (`static readonly object`); sempre destravar ao terminar e em `OnDisable`.
8. **Tudo é por jogador** (1 a 4 em split-screen): use o `PlayerContext` recebido por parâmetro/evento ou `PlayerRef.ContextOf(this)`; nunca assuma "o jogador".
9. **Textos de interface** em `UIText` (constantes PT-BR), não espalhados em strings.
10. **Arquivo novo = `.meta` novo:** o Unity cria o `.meta` ao importar; o usuário precisa commitar os dois. Não renomeie nem mova `.cs` sem o `.meta` (cenas e prefabs referenciam scripts por GUID).
11. **Ao entregar:** diga o que mudou, o que **não** foi testado (nada compila no sandbox) e o que o usuário precisa configurar no Inspector.

---

## 1. O projeto em resumo

- Jogo de terror em primeira pessoa, **Unity**, multiplayer **local**: split-screen, 1 a 4 jogadores (padrão `maxPlayers = 4` do `LocalPlayerManager`); quem entra depois aperta um botão em um controle livre. [V]
- Título no catálogo: "Huggable Horrors"; o código, os menus e os namespaces se chamam **HorrorFPS**. [V]
- Unity: os docs falam em 2022.3 LTS ou Unity 6; o código tem `#if UNITY_2022_2_OR_NEWER / UNITY_2023_1_OR_NEWER / UNITY_6000_0_OR_NEWER`. A pasta `ProjectSettings` não está no repositório (a raiz do repo é `Assets/HorrorFPS`), então a versão exata é **[?]**.
- Pacotes: **Input System** (novo), **uGUI legado** (`UnityEngine.UI.Text`, sem TextMeshPro — nenhuma referência a TMPro), `NavMeshAgent` no perseguidor, Timeline opcional (símbolo `HORRORFPS_TIMELINE`). [V]
- A interface é construída **por código** (`UIFactory`), sem prefabs de UI. [V]
- Idioma: **PT-BR** (comentários, tooltips, textos de interface, nomes de níveis). [V]
- Níveis no catálogo: `hall` ("Salão", cena `Level_Hall`, primeiro nível) e `cellar` ("Porão", `Level_Cellar`). Cenas extras: `Level_Annex`, `HorrorDemo`, `Item Tests`. O menu 27 (pacote A) gera a cena `Level_Showcase` e a registra como o nível `showcase` (seção 9). [V]

### Pedido → onde mexer

| Quero… | Mexer em | Receita / seção |
|---|---|---|
| Objeto interativo (porta, alavanca, gaveta, botão) | herdar `Interactable` (`World/`, `Props/`) | R1, 5.3 |
| Comportamento por jogador (habilidade, HUD, efeito) | MonoBehaviour no jogador + `PlayerModules.Install` | R2, 5.1 |
| Trecho de cutscene | herdar `CutsceneStep` (`Cutscenes/`) | R3, 5.9 |
| Efeito de item, arma, lanterna | `Items/ItemEffect.cs`, `Items/ItemEquip.cs`, `Equipment/EquipmentController.cs` (itens são assets: Item Studio, menu 10) | R4, 5.10 |
| Ação/condição própria em diálogo | `DialogueActions` / `DialogueConditions` (`Dialogue/`) | R4, 5.13 |
| Comportamento ou poder de monstro | `StalkerTask` / `StalkerCondition` / `StalkerModule` (`Stalker/`) | R5, 5.15 |
| Condição de SmartEvent | herdar `SmartCondition` (`SmartEvents/`) | R6, 5.17 |
| Efeito de câmera, sanidade, ouvir barulho | `ICameraModifier`, `SanityAware`/`PerViewerEffect`, `INoiseHearer` | R7, 5.7, 5.11, 5.12 |
| Nova tecla ou ação de input | `HorrorControls.inputactions` ou `ItemInput.Build()` | R8, 5.6 |
| Novo nível, spawn, porta entre níveis | `GameCatalog.asset`, `SpawnPoint`, `LevelDoor`, `LevelTrigger` | R9, 5.5 |
| Ferramenta de Editor / menu | `Editor/` + `[MenuItem]` | R10 |
| Cena de teste/demonstração montada por código (nova zona, nova bancada) | `Editor/HorrorFPSSetup.Showcase*.cs` + os construtores públicos de bancada | R12, 9 |
| Salvar algo novo | `GameSession` / `ISaveable` | R11, 5.4 |
| Menu, HUD, tela nova | `UI/` + `UIFactory` + `UIText` | 5.18 |
| Vida, dano, status do jogador | `Stats/` | 5.2 |
| Props (barril, esconderijo, espelho, armadilha) | `Props/` + `PropsBridge` | 5.14 |
| Jogador caído / reanimar | `Revive/` + `ReviveApi` | 5.16 |
| Trocar de personagem | `Characters/` + `CharacterManager` | 5.18 |

---

## 2. Layout do repositório

A **raiz do repositório é o conteúdo da pasta do framework** (`Assets/HorrorFPS` no pacote original; o `HorrorFPSSetup` do commit `9ccdc01` usava `Assets/Huggable Horrors`, que provavelmente é o nome atual no projeto do usuário [?]; `ProjectPaths` descobre o nome sozinho). As pastas abaixo estão na raiz do repo. Não há `.asmdef`: tudo compila no `Assembly-CSharp` (e no de Editor, para `Editor/`). [V]

| Pasta | O que tem | Namespace |
|---|---|---|
| `Core/` | `FrameworkBridge`, `HorrorUtil` + `PlayerRef`, `PlayerModules`, `CameraFx`/`CameraHook`, `PauseManager`, `GameRuntime`, `GameSettings`, `LocalPlayerManager`, `UIText` | `HorrorFPS` |
| `Player/` | `PlayerContext`, `FirstPersonController`, `PlayerInteractor`, `PlayerDragger`, `PlayerClimber` | `HorrorFPS` |
| `Stats/` | `StatsComponent`, `StatTypes` (`StatType`, `DamageInfo`, `IDamageable`…), `Stat`, `StatusEffectAsset` | `HorrorFPS` |
| `Input/` | `PlayerInputHandler`, `InputNames`, `ItemInput`, `InputBindingStore`, `InputGlyphs`, `HorrorControls.inputactions` | `HorrorFPS` |
| `World/` | `Interactable` (base), `Door`, `Draggable`, `Ladder`, `Pickup`, `SavePoint`, `Checkpoint`, `UnityEventInteractable` | `HorrorFPS` |
| `Flow/` | `SceneDirector`, `GameCatalog`, `SpawnPoint`, `LevelDoor`, `LevelTrigger`, `SceneStreamer`, `LoadingScreen` | `HorrorFPS` |
| `Save/` | `GameSession`, `SaveSystem`, `SaveData`, `SaveableEntity` (+`ISaveable`), `SaveableTransform`/`Active`/`Stats`, `StatsSnapshot` | `HorrorFPS` |
| `Cutscenes/` | `CutsceneSequence`/`Director`/`Context`/`Step` + 16 passos prontos | `HorrorFPS` |
| `Items/`, `Inventory/`, `Equipment/` | dados e lógica de itens, grade/listas, equipar, lanterna/arma | `HorrorFPS` |
| `Sanity/`, `Body/` | sanidade por jogador; coração, respiração, head bob, ruído | `HorrorFPS` |
| `Props/` | barris, gavetas, esconderijos, espelho, coop, armadilha + `PropsBridge` | `HorrorFPS` |
| `Dialogue/` | grafo de diálogo, gerente, visual, ações/condições | `HorrorFPS` |
| `Stalker/` (+`Tree/`) | IA do perseguidor: cérebro, árvore de comportamento editável, diretor, memória | `HorrorFPS` |
| `Revive/` | estado "caído" cooperativo | `HorrorFPS` |
| `SmartEvents/` (+`Editor/`) | eventos com condições | **`SmartEvents`** (campos `_camelCase`) |
| `Characters/`, `Animation/` | personagens selecionáveis; corpo/animação visível aos outros jogadores | `HorrorFPS` |
| `UI/` (+`Inventory/`) | menus, HUD, inventário (tudo por código) | `HorrorFPS` |
| `Editor/` (raiz) | ferramentas de itens/personagens/jogador e `ProjectPaths` (17 arquivos) e assistente de setup (5 `HorrorFPSSetup*`; os dois `.Showcase*` são a cena completa, menu 27) | `HorrorFPS.ItemsEditor` / `HorrorFPS.EditorTools` (`ProjectPaths`: `HorrorFPS`) |
| `Editor/Animation`, `Dialogue`, `Props`, `Revive`, `Stalker` | ferramentas por sistema | `HorrorFPS` |
| `Prefabs/`, `Scenes/`, `Materials/`, `Resources/HorrorFPS/`, `Generated/` | assets (seção 9) | — |
| `Docs/`, `Explicações de como usar/`, `README.md` | cópias dos docs (o `README.md` é o doc de arquitetura) | — |

Menus do Editor (todos em `Tools > Horror FPS > N. …`; o próximo número livre é **28**): 1 Create Player Prefab · 2 Create Demo Scene · 3 Create Full Game Demo · 4 Add Item, Sanity and Body modules to Player Prefab · 5 Create Sample Items · 6 Create Demo Props In Open Scene · 7 Player Starting Inventory · 8 Character Catalog · 9 Add Selected Player Prefab(s) as Characters · 10 Item Studio · 11 Dialogue Editor · 12 Create Sample Dialogues · 13 Create Dialogue Demo Props In Open Scene · 14 Add Animation Rig To Player Prefab · 15 Create Player Animator Controller · 16 Setup Selected Model As Player Body · 17 Create Props Demo In Open Scene · 18 Create Stalker Demo In Open Scene · 19 Add Stalker To Selected Object · 20 Create Stalker Definition From Selected · 21 Create Revive Profile · 22 Toggle Revive Debug Keys · 23 Revive Diagnostics (Play Mode) · 24 Stalker Tree Editor · 25 Create Stalker Tree (Default) · 26 Create Stalker Tree (Example: Roar On Spot) · **27 Create Showcase Scene (Everything)** (pacote A: cria `Level_Showcase`, a cena com todos os sistemas, seção 9). Mais: `Assets > Create > HorrorFPS > Stalker Node Script (Action | Condition)`. Se o **pacote B** (opcional) for aplicado, somem os menus 2, 6, 13, 17 e 18 e os números **não** são reaproveitados; os construtores públicos que eles chamavam continuam (R12). [V]

Arquivos de projeto Unity: `.meta` versionados (367); cenas e prefabs são YAML (não edite à mão); `.gitattributes` = `* text=auto`; fim de linha LF. [V]

---

## 3. Convenções de código (medidas nos 301 arquivos do commit `9ccdc01`; os 3 arquivos do pacote A seguem as mesmas)

- **Formato [V]:** LF (301/301), 4 espaços (nenhuma linha com tab), chaves **sempre** na linha de baixo (Allman, 5.091 ocorrências), `namespace HorrorFPS { … }` com tipos a 4 espaços.
- **Campos [V]:** sem prefixo (`float valor;`), exceto o namespace `SmartEvents` (`_camelCase`, 42 campos). Campos de Inspector: `[SerializeField] tipo nome;` privado + propriedade pública separada (`public float Valor { get { return valor; } }`).
- **Estilo C# [V]:** código "antigo". Quase não há `var` (6 linhas), `$"..."` (0), `nameof` (0), `async` (0); `=>` aparece em 54 linhas e `?.` em 7 (`PlayerInputHandler` usa `?.Invoke`); `string.Format` 12×. **Siga o estilo existente:** declare o tipo, use `+` ou `string.Format`, propriedades `{ get { return x; } }`.
- **Atributos [V]:** `[AddComponentMenu("HorrorFPS/Grupo/Nome")]` em todo componente público; `[CreateAssetMenu(menuName = "HorrorFPS/Nome", fileName = "...")]` nos ScriptableObjects; `[RequireComponent]`, `[DisallowMultipleComponent]`; `[DefaultExecutionOrder(n)]` só onde a ordem importa (`PlayerInputHandler` −100, `PlayerContext` −90, `ReviveManager` −40, `ReviveDownable` −30, `CutsceneDirector` 100, `PlayerAnimDriver` 100, corpo/`PlayerBodyPresenter` 200, `InventoryScreen` 500). `[PlayerModule("Grupo", ordem)]` só organiza a lista no Inspector do `PlayerContext` (é lido apenas por `Editor/PlayerContextEditor.cs`); **não instala nada**.
- **Documentação [V]:** `/// <summary>` em PT-BR nas classes e membros públicos; `[Header]` e `[Tooltip]` em PT-BR em cada campo do Inspector. Uma classe (MonoBehaviour/ScriptableObject) por arquivo, com o mesmo nome; helpers `[Serializable]` pequenos podem ficar no arquivo do dono (ex.: `ItemAction` em `ItemData.cs`).
- **Logs [V]:** `Debug.Log/Warning/Error` com prefixo do módulo entre colchetes (`[HorrorFPS]` 39×, `[Anim]` 27×, `[Stalker]` 15×, `[Dialogue]` 14×, `[Revive]` 12×, `[Props]` 8×). Configuração faltando = `LogWarning`/`LogError` com mensagem em PT-BR dizendo o que fazer.
- **Estado estático [V]:** todo `static` mutável é zerado em `ResetStatics()` com `RuntimeInitializeLoadType.SubsystemRegistration`.
- **Textos [V]:** strings de interface em `UIText` (`public const string`), mostradas por `Toast.Show(UIText.X)`; textos de itens/diálogos vivem nos assets (`ItemTextSet`, `DialogueNode`).
- **Reflexão [V]:** só nas pontes e afins (15 arquivos importam `System.Reflection`, lista na seção 7): procuram membros **por nome**, com listas de nomes candidatos (`"Health"`, `"Life"`, `"HP"`…). Não introduza reflexão em código novo.

Modelo mínimo de componente:

```csharp
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>O que este componente faz, em uma frase (PT-BR).</summary>
    [AddComponentMenu("HorrorFPS/Grupo/Nome")]
    public class MeuComponente : MonoBehaviour
    {
        [Header("Configuração")]
        [Tooltip("Explique o campo para quem for configurar no Inspector.")]
        [SerializeField] float valor = 1f;

        public float Valor { get { return valor; } }
    }
}
```

Quando a classe tem estado estático:

```csharp
static MeuSistema instance;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() { instance = null; }
```

---

## 4. Arquitetura e regras de ouro

**Ciclo de vida [V].** `MainMenu` (cena) → `SceneDirector.StartNewGame()` carrega **Core** (persistente: `LocalPlayerManager` + `PlayerInputManager`) e o primeiro nível do `GameCatalog` (cena aditiva). Os jogadores moram na cena do `LocalPlayerManager` (Core) e **sobrevivem à troca de nível**; níveis são cenas aditivas e podem ter cenas extras em streaming (`SceneStreamer`). Troca de nível (`GoToLevel`): `BeginTransition` (`IsBusy = true`, aborta cutscene, força despausar, trava jogadores, dispara `TransitionStarted`) → fade para preto → **captura o estado** da cena que sai (`GameSession.CaptureScene`) e descarrega → carrega a nova (`GameRuntime` reaplica o estado salvo em `sceneLoaded`, antes de qualquer `Start`) → posiciona jogadores nos `SpawnPoint` → `LevelLoaded` (tela ainda preta: bom momento para cutscene) → fade in, destrava, `IsBusy = false` → `LevelReady` → autosave (se `AutosaveOnLevelChange`).

**Regras de ouro**

1. **Tudo por jogador.** `PlayerContext.All` tem 1–4 jogadores; cada um tem câmera, HUD, input, inventário, sanidade próprios. Interações recebem o `PlayerContext` de quem agiu. Para achar o dono de um componente: `PlayerRef.ContextOf(this)`; para achar um módulo do mesmo jogador: `PlayerRef.Find<T>(this)`.
2. **Interação = `Interactable`.** O `PlayerInteractor` de cada jogador cuida de foco, dica e HUD; seu script só implementa `Interact`/`CanInteract`/`GetBlockedReason`.
3. **Controle do jogador = travas por chave** (`AddControlLock`). Cutscene, troca de cena, diálogo e esconderijo usam chaves próprias; nunca desligue `Input.GameplayEnabled` à mão (é da pausa).
4. **Estado persistente = `GameSession` (variáveis) ou `ISaveable` + `SaveableEntity`** (R11). Estado global de um sistema por jogador (sanidade) usa variável JSON gravada em `FrameworkBridge.BeforeSave` e lida em `AfterLoad`.
5. **Câmera = `ICameraModifier`.** Ninguém mexe na `Camera` do jogador direto (5.7).
6. **Barulho de gameplay = `NoiseSystem.Emit`.** Monstros e outros ouvintes (`INoiseHearer`) reagem; independente do áudio tocado (5.12).
7. **Dados são assets** (`ItemData`, `DialogueGraph`, `StalkerDefinition`, `SanityProfile`…) criados pelos menus do Editor; vários vêm de `Resources` (seção 7).
8. **Módulos por jogador** são instalados automaticamente por `PlayerModules.Install` (lista fixa, 5.1/R2) ou ficam no prefab `Player.prefab`.
9. **Pontes (`*Bridge`)** concentram chamadas ao framework base e a outros módulos: `FrameworkBridge` (eventos de save/nível, variáveis, travas), `PropsBridge`, `DialogueBridge`, `StalkerBridge`, `ReviveBridge`, `PlayerAnimBridge`. Elas foram escritas quando o código-fonte do framework base não estava disponível (por isso 15 arquivos importam `System.Reflection` e procuram membros por nome). Hoje o código real está no repositório: em **código novo**, chame a API real conferida na seção 5; ao **editar** um módulo, mantenha o estilo da ponte dele.
10. **Buscas de objetos:** prefira `HorrorUtil.FindAll<T>()` / `FindFirst<T>()` (escolhem a API certa por versão do Unity) a `FindObjectOfType` direto. Alguns arquivos (pontes, `StalkerManager`/`StalkerDirector`) chamam `FindObjectsByType`/`FindFirstObjectByType` direto; em código novo use `HorrorUtil`.
11. **Corrotinas de sistemas estáticos:** `GameRuntime.Run(IEnumerator)` (sobrevive à troca de cenas).
12. **Pausa é `Time.timeScale = 0`:** UI e temporizadores que rodam pausados usam `unscaledDeltaTime`/`unscaledTime`.

---

## 5. API conferida do núcleo

Tudo desta seção é **[V]** (assinaturas lidas no código), salvo onde marcado. Assinaturas abreviadas, em pseudo-C#.

### 5.1 Jogador — `Player/`, `Core/HorrorUtil.cs`, `Core/PlayerModules.cs`

```
PlayerContext : MonoBehaviour   [DisallowMultipleComponent, DefaultExecutionOrder(-90)]
  static List<PlayerContext> All;   static event Action<PlayerContext> PlayerSpawned, PlayerRemoved;
  PlayerInputHandler Input; FirstPersonController Controller; PlayerClimber Climber; PlayerInteractor Interactor;
  PlayerDragger Dragger; StatsComponent Stats; PlayerHUD HUD; Camera Camera;
  int PlayerIndex;  bool IsDead { get; private set; };  bool IsControlLocked;  event Action<PlayerContext> Died;
  void AddControlLock(object key);   void RemoveControlLock(object key);
  void TeleportTo(Vector3 position, float yaw, float? pitch = null);   // solta escada e objeto arrastado
  void Revive(float healthPercent = 1f);
  static void SetAudioListenersSuppressed(bool suppressed);

static class PlayerRef  (Core/HorrorUtil.cs)
  PlayerContext ContextOf(Component c);  PlayerInput InputOf(Component c);  int IndexOf(Component c);
  T Find<T>(Component c) where T : Component;   // procura nos pais; depois em qualquer filho da raiz do jogador
  Camera CameraOf(Component c);  List<PlayerContext> AllPlayers();
static class HorrorUtil
  T[] FindAll<T>();  T FindFirst<T>();  float Remap01(v, a, b);  float Smooth01(e0, e1, x);  float Hash01(string, int);  string NewId();

FirstPersonController
  enum MovementState { Idle, Walking, Sprinting, Crouching, Airborne, Climbing }
  bool IsGrounded, IsCrouching, IsSprinting;  bool MovementLocked { get; set; };  bool ExternallyLocked { get; set; };
  float Yaw, Pitch;  CharacterController Body;  Vector3 Velocity;  MovementState State;  float StatSpeedMultiplier;
  event Action Jumped;  event Action<float> Landed (m/s de impacto);  event Action<float> Footstep (0..1)
  void SetYaw(float);  SetYawLimit(float center, float range);  ClearYawLimit();  SetLook(float yaw, float pitch);
  static float YawFromDirection(Vector3);  void SetSpeedModifier(object key, float multiplier);
  void StopMotion();  void Launch(Vector3);  void Teleport(Vector3 position, float? newYaw = null)

PlayerInteractor: Interactable Focus; PromptData Prompt; bool Suspended {get;set;}; events PromptChanged(PromptData), FocusChanged(old,new)
PlayerDragger:    bool IsDragging; Draggable Held; bool CanDrag(d); bool Begin(d); void Release(); events Grabbed/Released(Draggable)
PlayerClimber:    Ladder Current; bool IsClimbing, CanGrab; bool BeginClimb(Ladder); void Release(); events ClimbStarted/ClimbEnded(Ladder)
```

**Módulos instalados automaticamente** (`PlayerModules.Install`; o `PlayerModulesRunner`, criado sozinho, varre os `PlayerContext` a cada 0,5 s e instala **uma vez** por jogador; desligue com `PlayerModules.AutoInstall = false` ou o símbolo `HORRORFPS_NO_AUTOINSTALL`; só adiciona o que **não** existe, procurando também nos filhos): `ItemInput`, `PlayerOverlay`, `InventoryOwner` (+ `GridContainer` "bag" e listas Chaves/Registros/Mapas), `Hotbar`, `HotbarHUD`, `InventoryScreen`, `EquipmentController`, `EquipmentHUD`, `PlayerMotionProbe`, `BreathingComponent`, `HeartbeatComponent`, `DynamicHeadBob`, `DamageReaction`, `SanityComponent`, `SanityFX`, `SanityEffects`, `SanityAudio`, `SanitySubtitles`. O resto do jogador vem do prefab (seção 9).

### 5.2 Stats e dano — `Stats/`

```
enum StatType { Health, Stamina, Sanity, Fear, Durability, Armor, MoveSpeed, Noise, Awareness }
enum DamageType { Generic, Physical, Fire, Fall, Supernatural }      enum ModifierOp { Add, Multiply }
struct DamageInfo { float amount; DamageType type; GameObject source; Vector3 point, direction;
                    DamageInfo(float amount, DamageType type = Generic, GameObject source = null, Vector3 point = default, Vector3 direction = default) }
interface IDamageable { bool IsAlive; float ApplyDamage(DamageInfo info) /*devolve o aplicado*/; float Heal(float amount); }

StatsComponent : MonoBehaviour, IDamageable          enum StatPreset { Player, Monster, Object }
  events C#: Damaged(StatsComponent, DamageInfo, float aplicado)   Died(StatsComponent, DamageInfo)
             StatChanged(StatsComponent, Stat, float)              EffectChanged(StatsComponent, ActiveEffect, bool)
  UnityEvents: onDamaged(float), onDied
  bool IsAlive, Invulnerable{get;set;}; StatType VitalStat; IReadOnlyList<ActiveEffect> ActiveEffects
  bool TryGet(StatType, out Stat); bool Has(type); float GetCurrent(type, fb=0) / GetMax(type, fb=0) / GetNormalized(type, fb=1) / GetValue(type, fb=1)
  float Modify(type, delta); bool TryConsume(type, amount); float ApplyDamage(info); float Heal(amount)
  void Kill(GameObject source = null); void Revive(float healthPercent = 1f); void ConfigurePreset(StatPreset)
  ActiveEffect ApplyEffect(StatusEffectData | StatusEffectAsset, GameObject source = null); bool HasEffect(string id); bool RemoveEffect(string id)
StatusEffectData [Serializable]: id, displayName, duration (<=0 = permanente), icon, StatChange[] modifiers {stat, op, value}, StatTick[] overTime {stat, perSecond}
```

- **Morte:** `Die` (privado) → `IsAlive = false`, remove efeitos, dispara `Died` e `onDied` e, se configurado, destrói o objeto. `PlayerContext` ouve `Stats.Died`: `IsDead = true` e `Input.GameplayEnabled = false`. `GameOverScreen` espera um atraso e só aparece se **todos** os jogadores estiverem `IsDead`.
- **Armadura:** `dano *= 1 − clamp01(Armor/100)`.

### 5.3 Interactable — `World/Interactable.cs`

Base de tudo que o jogador "olha e usa" (o collider pode ser trigger ou sólido).

```
abstract class Interactable : MonoBehaviour
  [SerializeField] verb, objectName, maxDistance = 2.5f, holdDuration = 0 (0 = apertar), interactionEnabled, promptMode, icon, iconKind
  UnityEvent onFocused, onUnfocused
  float MaxDistance, HoldDuration;  bool InteractionEnabled {get;set;}
  virtual string ActionName => InputNames.Interact;    protected virtual string DefaultVerb => UIText.Interact
  virtual bool IsFocusable(PlayerContext p)             // isActiveAndEnabled && interactionEnabled
  virtual bool CanInteract(PlayerContext p)             // true
  virtual string GetBlockedReason(PlayerContext p)      // texto do "trancado"
  virtual string GetLabel(PlayerContext p)              // verbo + objectName
  abstract void Interact(PlayerContext p)
  virtual void OnInteractBlocked(PlayerContext p)       // apertou, mas CanInteract é falso
  virtual void OnFocusEnter/OnFocusExit(PlayerContext p)
  virtual PromptData BuildPrompt(PlayerContext p)       // visible, label, actionName, icon, iconKind, mode, progress, blocked, hintOnly
  protected void SetIcon(Sprite, IconKind)              // enum IconKind { Generic, Grab, Climb, Door }
```

Exemplos para copiar (todos herdam de `Interactable`): `Door`, `Drawer`, `LightSwitch`, `UnityEventInteractable`, `Pickup`, `SavePoint`, `HidingSpot`, `CoopButton`, `WorldItem`, `ContainerInteractable`, `DialogueInteractable`, `SmartEventInteractable`.

### 5.4 Save e estado — `Save/`

```
static class GameSession
  bool IsActive; float PlayTime; int CurrentSlot; string CurrentLevelId, CurrentSpawnId; List<string> StreamedScenes
  event Action<string> VariableChanged
  void StartNew(string levelId); void End();  bool Has(key); void Remove(key)
  string GetString(key, fb="") / SetString(key, v);  bool GetBool(key, fb=false) / SetBool(key, v)   // bool = "1"/"0"
  int GetInt(key, fb=0) / SetInt(key, v) / int AddInt(key, delta);  float GetFloat(key, fb=0) / SetFloat(key, v)
  bool IsTrackedScene(Scene); void CaptureScene(Scene); void ApplyScene(Scene)      // do SceneDirector/GameRuntime: não chame à mão

interface ISaveable { string SaveKey { get; } string CaptureState(); void RestoreState(string json); }   // use JsonUtility
class SaveableEntity : MonoBehaviour   // [DisallowMultipleComponent]
  string Id; bool IsMarkedDestroyed; void MarkDestroyed(); void AssignNewId(); EntityRecord Capture(); void Restore(EntityRecord); static List<SaveableEntity> Collect(Scene)
Prontos: SaveableTransform (posição/rotação, escala opcional, zera a velocidade do Rigidbody; chave "transform"),
         SaveableActive (ativo/inativo; "active"), SaveableStats (vida, stamina e efeitos de um StatsComponent; "stats")

static class SaveSystem
  const int AutosaveSlot = 0, ManualSlots = 5, SlotCount = 6        // slots 1–5 manuais
  events: BeforeSave, AfterLoad (Action); Saved (Action<int>)       // arquivos: <persistentDataPath>/HorrorFPS/Saves/slot_N.json (+ .png)
  bool CanSave   // falso em: sem partida, troca de cena, cutscene, salvando, ou QUALQUER jogador morto
  SaveSlotInfo GetInfo(int); List<SaveSlotInfo> GetAllInfo(); int GetMostRecentSlot(); bool HasAnySave(); Texture2D LoadThumbnail(int)
  bool TryRead(int slot, out GameSave save); void Delete(int); void SaveToSlot(int slot, Action<bool> onDone = null); void Autosave()
  PlayerRecord FindPlayerRecord(GameSave, int playerIndex); void RestorePlayer(PlayerContext, PlayerRecord)
```

Como o save de mundo funciona (`GameSession.CaptureScene/ApplyScene`):
- Só cenas que **não** são `MainMenu`/`Core` guardam estado de mundo.
- Ao abrir uma cena, anota-se o conjunto de ids de `SaveableEntity` presentes. Na captura entram **só esses** ids; "destruído" = estava no conjunto e sumiu (ou `MarkDestroyed()`).
- Ao reabrir (evento `sceneLoaded`, em `GameRuntime`), as entidades destruídas são destruídas de novo e as demais recebem `Restore` por id → chamam `RestoreState(json)` no `ISaveable` cuja `SaveKey` bate.

### 5.5 Fluxo de níveis — `Flow/`

```
static class SceneDirector
  object LockKey;  bool IsBusy;  bool AutosaveOnLevelChange {get;set;}  string MainMenuScene, CoreScene
  events: TransitionStarted (Action); LevelLoaded (Action<string>: carregou e jogadores no lugar, tela ainda PRETA);
          LevelReady (Action<string>: tela clareou e jogadores têm controle)
  void StartNewGame(); void LoadGame(int slot); void GoToLevel(string levelId, string spawnId = null); void ReturnToMainMenu()
  void LoadAdditive(string scene); void UnloadAdditive(string scene); bool IsLoaded(string scene); void Quit()
GameCatalog : ScriptableObject (Resources "HorrorFPS/GameCatalog")
  mainMenuScene, coreScene, gameTitle, firstLevel, LevelEntry[] levels {id, displayName, scenes[], defaultSpawn, loadingTip}, loadingTips[]
SpawnPoint (spawnId = DefaultId "start", slot)   static SpawnPoint Find(string spawnId, int playerIndex)
   // pontos com o mesmo id formam um grupo; o jogador 1 usa o de menor "slot", o 2 o seguinte...; sobrando jogadores, os pontos se repetem
   // sem o grupo pedido, tenta "start"; sem nada, devolve null (o LocalPlayerManager usa seus próprios spawnPoints)
LevelDoor (levelId, spawnId, requiredVariable)   LevelTrigger (levelId, spawnId, requireAllPlayers)
Checkpoint (once, setSpawnId)   SavePoint (openSlotMenu)
SceneStreamer (sceneName, unloadWhenEmpty, unloadDelay)   // Collider trigger: carrega a cena aditiva quando um jogador entra e descarrega quando todos saem
```

### 5.6 Entrada — `Input/`

Existem **três caminhos**, todos presos aos dispositivos **do próprio jogador** (sem `Keyboard.current` solto; exceções e fallbacks em 7.2):

1. **Ações base** — `PlayerInputHandler` (um por jogador) lê o `InputActionAsset` do `PlayerInput` (`Input/HorrorControls.inputactions`, mapa `Gameplay`: `Move, Look, Jump, Sprint, Crouch, Interact, Grab, Pause, GrabDistance`; esquemas `Keyboard&Mouse` e `Gamepad`). Leitura: `MoveInput`, `LookDelta`, `JumpPressed`, `SprintHeld`, `InteractPressed`, `GrabPressed/Held/Released`, `GrabDistanceInput`, `CrouchHeld`, `PausePressed`; genéricos `GetAction(name)`, `WasPressed(name)`, `IsHeld(name)`, `IsHeldIgnoringLock(name)`. Devolve zero se `GameplayEnabled == false` (pausa/menu) ou `ExternallyLocked` (travas de controle). Nomes em `InputNames` (`Rebindable` = 7 ações que aparecem no painel de controles). Eventos `ControlsChanged`, `DeviceLost`, `DeviceRegained`; `CurrentScheme`, `IsGamepad`.
2. **Ações de módulos** — `ItemInput` (um por jogador) cria **em código** um `InputActionAsset` "Items" com mais de 25 ações (inventário, cursor virtual, hotbar `Hotbar1..N`, `Reload`, `ToggleLight`, `UseEquipped`, `HoldBreath`, abas…), presas aos dispositivos do jogador (`SyncDevices`). Helpers estáticos: `ItemInput.Pressed(action)`, `Released`, `Held`, `Vec`; `Paused` (= `timeScale <= 0`); `UsesMouse`. **Não** entram no painel de remapeamento.
3. **Leitura direta dos dispositivos do jogador** — `PropsInput` (E/X confirma, Backspace/B cancela) e `DialogueInput` leem `PlayerInput.devices`. Também fora do remapeamento.

### 5.7 Câmera — `Core/CameraFx.cs`, `Core/CameraHook.cs`

```
struct CameraDelta { Vector3 position /*espaço da câmera, m*/; Vector3 euler /*graus*/; float fov /*graus somados*/; bool IsZero }
interface ICameraModifier { void Contribute(Camera cam, ref CameraDelta delta); }   // some em delta; AJA SÓ se cam for a câmera do SEU jogador
static class CameraFx { void Register(ICameraModifier); void Unregister(ICameraModifier); }
static class CameraHook { event Action<Camera> PreRender, PostRender; }              // para efeitos por câmera/espectador
```
Em `PreRender` todos os modificadores somam num único `CameraDelta`, que é aplicado a `localPosition`/`localRotation`/`fieldOfView` (FOV limitado a 20–140) e **desfeito** em `PostRender`: o gameplay (mira, interação, física) enxerga a câmera "limpa".

### 5.8 Pausa, tempo e opções — `Core/`

```
static class PauseManager  bool IsPaused; PlayerContext Owner; event Action<bool> PauseChanged
  void Pause(PlayerContext); Resume(); ForceResume(); RequestFreeCursor(object key); ReleaseFreeCursor(object key); EnforceCursor()
  // Pausar = Time.timeScale 0 + áudio silenciado + GameplayEnabled false
GameRuntime  static GameRuntime Ensure(); static Coroutine Run(IEnumerator)     // host de corrotinas que sobrevive às cenas; soma PlayTime
GameSettings (PlayerPrefs)  MouseSensitivity, GamepadSensitivity, InvertY, PromptStyle (PromptMode), GrabMode/CrouchMode (HoldMode); event Changed; Flush()
```
Tela que precisa de cursor livre: `PauseManager.RequestFreeCursor(this)` ao abrir e `ReleaseFreeCursor(this)` ao fechar (modelo: `GameOverScreen`).

### 5.9 Cutscenes — `Cutscenes/`

```
CutsceneSequence : MonoBehaviour   // coloque no objeto; passos = CutsceneStep no mesmo objeto/filhos, rodam na ordem da hierarquia
  id (vazio = nome do objeto), playOnce, lockPlayers, useCutsceneCamera, customCamera, letterbox, allowSkip, skipHoldSeconds; UnityEvents onStarted/onFinished
  bool Play(); bool PlayAgain(); bool HasPlayed; static string PlayedKey(id) => "cutscene." + id
static class CutsceneDirector  Play(sequence, force=false); Skip(); Abort(); bool IsPlaying; string CurrentId; events Started/Finished(string id); object LockKey
CutsceneTrigger (sequence, playWhen = PlayerEntersTrigger | LevelStart | Manual, requireAllPlayers, startDelay)   // LevelStart ouve SceneDirector.LevelLoaded; Manual = UnityEvent chama o método público de tocar
abstract class CutsceneStep : MonoBehaviour  [SerializeField] delay, runWithNext
  abstract IEnumerator Run(CutsceneContext ctx);   virtual void OnSkip(CutsceneContext ctx)   // OnSkip: aplique o resultado final, sem animar
sealed class CutsceneContext  Sequence; Camera Camera (null = visão dos jogadores); bool HasCamera
  Vector3 CameraPosition; Quaternion CameraRotation; float CameraFov; Vector3 CameraShake   // escreva aqui; o Director aplica no fim do quadro
  void ShowSubtitle(string text, float seconds); void HideSubtitle(); static IEnumerator Wait(float seconds)
```
Passos prontos: `Activate`, `Animator`, `Audio`, `CameraMove`, `CameraShake`, `Event`, `Fade`, `FlickerLights`, `GoToLevel`, `MoveObject`, `PlacePlayers`, `PlayerLook`, `SetVariable`, `Subtitle`, `Timeline`, `Wait`, mais `DialogueStep` e `SanityDialogueStep`. Dentro de `Run` use apenas `yield return null` ou `yield return CutsceneContext.Wait(s)` (pausa junto com o jogo).

### 5.10 Itens, inventário e equipamento — `Items/`, `Inventory/`, `Equipment/`

- **`ItemData`** (ScriptableObject, menu "HorrorFPS/Item"): `id` (vazio = nome do asset), `displayName`/`description`/`readText` (`ItemTextSet`: texto + variantes condicionais), `category` (flags `ItemCategory`), `icon`, `color`, `shape` (`ItemShape`, grade estilo RE4), `allowRotation`, `listSlots`, `stackable`/`maxStack`, `tabRule`, `map`, `worldPrefab`, `inspectPrefab`, `canDrop`, `canInspect`, `equip` (`ItemEquip`), `equipSlot`, `actions` (`ItemAction[]`: `id`, `label`, `consume`, `requires`, `blockedReason`, `effects`, `sound`).
- **Efeitos** (`ItemEffect`, `kind`): `RestoreStat`, `StatModifier`, `SetVariable`, `AddVariable`, `Sanity`, `HeartRate`, `Noise`, `CameraShake`, `SetItemFlag`, `Message` (chama `IItemUseHandler.OnItemMessage(ctx, key)`), `Recoil`. Contexto: `ItemUseContext { owner, item, action; UserObject, Player, Stats, Sanity, Heart, HeadBob }`. `IItemUseHandler { bool OnBeforeUse(ItemUseContext ctx) /*true cancela*/; void OnItemMessage(ItemUseContext ctx, string key); }` — implemente em qualquer script do jogador ou de filhos.
- **`ItemEquip`**: `kind` (`EquipKind`: `None`, `Flashlight`, `Melee`, `Firearm`), `heldPrefab/heldPosition/heldEuler/heldScale`, dano/alcance/ruído (corpo a corpo), luz e bateria (lanterna), tiros/s, `automatic`, `pellets`, `spread`, `magazineSize`, `ammoItem`, recarga (arma). Estado por instância: `EquipState` (`Ready`, `Lit`, `Battery`, `Ammo`, `Durability`; `AmmoOf`, `BatteryOf`, `DurabilityOf`, `Status`).
- **`ItemInstance`** (um item no mundo/mochila): `uid`, `data`, `stack`, `rotation`, `gridPos`, `hotkey`, `container`; flags (`HasFlag/SetFlag`) e contadores (`GetCounter/SetCounter/AddCounter`); `Clone`, `ToSave/FromSave`; `GetName/GetDescription/GetReadText(Component viewer)` (texto depende de quem olha: sanidade/flags).
- **`ItemDatabase.Find(id)`** — carrega todos os `ItemData` de qualquer pasta `Resources` (`Resources.LoadAll<ItemData>("")`). **O asset precisa estar numa pasta `Resources`.**
- **`InventoryOwner`** (um por jogador; também em baús/gavetas; `ISaveable`, chave `inventory_owner`):
  ```
  Containers; bool IsPlayer; GetContainer(id); HasTab(tab); FindTabContainer(tab); FirstGrid(); FirstList()
  int CountOf(itemId); bool Has(itemId, count = 1); bool CanFitAny(ItemData); ItemInstance FindFirst(itemId) / FindByUid(uid); GetAllItems(List<ItemInstance>)
  int AddItem(ItemData, count = 1, preferContainerId = null); int AddInstance(ItemInstance, preferContainerId = null); int RemoveItem(itemId, count = 1)
  UseResult UseItem(ItemInstance, ItemAction = null) {ok, message}; UseResult UseEquipped(slot = null)
  ItemInstance GetEquipped(slot); bool IsEquipped(item); void ToggleEquip(item); WorldItem Drop(item, wholeStack = true, amount = 0)
  events: Changed, Restored, Picked, Dropped, Equipped, Unequipped, Used(ItemUseContext), EquippedActivated, InspectRequested
  string ExportTransfer(); void ImportTransfer(string json, bool clearFirst = true)      // troca de personagem
  ```
  Contêineres: `ItemContainer` (abstrato; `GridContainer`, `ListContainer`), `ContainerId`, `Tab` (`InventoryTab`: Keys, Records, Maps…), `TryAdd`, `CanFit`, eventos `Changed/ItemAdded/ItemRemoved/Overflowed`. Itens largados: `DropRegistry` (chave `drops_v1`).
- **`Hotbar`**, `EquipmentController` (`[PlayerModule("Equipamento", 0)]`, implementa `IItemUseHandler`), `HitTarget` (alvo de golpe), `IItemHittable` (`ItemHit.cs`).

### 5.11 Sanidade — `Sanity/`

Por jogador e **por espectador**: efeitos visuais/sonoros dependem da sanidade de quem está olhando.

```
enum SanityStage { Stable=0, Uneasy=1, Disturbed=2, Fractured=3, Broken=4 }      // quanto maior, pior
SanityComponent : MonoBehaviour   (módulo "Sanidade", ordem 0; ScriptableObject SanityProfile)
  static IReadOnlyList<SanityComponent> All;  static SanityComponent Of(Component); static float NormalizedOf(Component); static SanityComponent ForCamera(Camera)
  float Points, Max, Normalized (1 = lúcido), Insanity (1 − Normalized); SanityStage Stage; bool IsBroken; int PlayerIndex; Camera Camera; Vector3 EyePosition
  void Add(float amount, string reason = null); Set(points, reason); SetNormalized(n, reason); SetDrain(string source, float perSecond); ClearDrain(source); float GetDrain(source)
  bool CanSee(Vector3 point);  events Changed(SanityChange), StageChanged(SanityComponent, from, to)
struct SanityChange { SanityComponent who; float normalized; float delta; SanityStage stage; string reason }
interface ISanityListener { void OnSanityChanged(SanityChange c); void OnSanityStageChanged(SanityComponent who, SanityStage from, SanityStage to); }
abstract class SanityAware : MonoBehaviour, ISanityListener   // base pronta; se sobrescrever OnEnable/OnDisable chame base.*
static class SanityEvents  events Changed, StageChanged; Subscribe(ISanityListener); Unsubscribe(l);
  static int Shock(Vector3 position, float radius, float points, string reason = "shock", bool requireSight = false);  static void AddAll(float points, string reason = "event")
abstract class PerViewerEffect : MonoBehaviour   // efeito por câmera: protected abstract void Apply(Camera cam, float sanity01); protected abstract void Revert();
```
Componentes de cena: `SanityZone` (volume Trigger que drena ou cura a sanidade de **quem está dentro**, em pontos/s), `SanityLight` (luz que muda por jogador), `SanityVisibility` (mostra/esconde/troca material só para a faixa de sanidade certa), `SanityExaminable` (Interactable cuja descrição depende da sanidade de quem interage), `SanityText.Glitch(...)`/`SanityString` (texto que "mente" conforme a sanidade). Salvamento: `sanity_player_<índice>` (JSON `SanitySave`) via `FrameworkBridge.BeforeSave/AfterLoad`.

### 5.12 Ruído — `Body/NoiseSystem.cs`

```
enum NoiseKind { Footstep, Land, Heartbeat, Breath, Gasp, Item, Impact, Explosion, Voice, Custom, Gunshot }
struct NoiseEvent { Vector3 position; float radius; NoiseKind kind; GameObject source; float time; bool IsFrom(Transform) }
interface INoiseHearer { void OnNoiseHeard(NoiseEvent noise); }   // no mesmo objeto (ou pai) de um Collider dentro do raio; ou use NoiseListener
static class NoiseSystem
  void Emit(Vector3 position, float radius, NoiseKind kind = Custom, GameObject source = null)
  event Action<NoiseEvent> Emitted;  float GlobalScale; LayerMask HearingMask; float SearchMultiplier; bool DebugDraw
  void Register(NoiseListener) / Unregister(NoiseListener)
  float LoudnessAt(NoiseEvent e, Vector3 earPosition, float hearingMultiplier = 1f, float occlusionFactor = 0.6f, int occlusionMask = ~0)
```
Barulho e áudio são coisas separadas; para tocar som **e** fazer barulho use `SoundEmitter`.

### 5.13 Diálogo — `Dialogue/`

```
DialogueGraph : ScriptableObject ("HorrorFPS/Dialogue/Dialogue Graph"; editor: Tools > Horror FPS > 11. Dialogue Editor)
enum DialogueNodeKind { Line, Branch, RandomPick, Action, Jump, End }
static class DialogueManager  DialogueSession Play(DialogueGraph graph, PlayerContext player [, DialogueActor speaker | DialogueOptions options]);
  bool IsInDialogue(PlayerContext); DialogueSession SessionOf(PlayerContext); Stop(session); StopAll(); bool AnyActive; DialogueActor ResolveActor(session, id)
  events Started, Ended (Action<DialogueSession>); NodeEntered(session, node); ChoiceMade(session, node, choice)
static class DialogueEvents  On(string id, Action<DialogueContext, string> handler); Off(...); Raise(id) / Raise(id, argument, ctx); event Raised
Ações: enum DialogueActionKind { SetVariable, SetNumber, AddNumber, SetText, GiveItem, RemoveItem, Event, Animation, PlaySound, Sanity, SanityAll, Damage, CameraShake, Scare, Wait, GoToLevel, StartDialogue, Autosave, Custom }
  DialogueActions.Register(string id, Action<DialogueContext, string> fn)          // kind = Custom
Condições: enum DialogueConditionKind { Variable, Number, Text, HasItem, Sanity, NodeVisited, GraphCompleted, Chance, PlayerCount, Custom }
  DialogueConditions.Register(string id, Func<DialogueContext, string, bool> fn)   // kind = Custom
Ids especiais de quem fala: DialogueIds.Player = "@player", DialogueIds.Narrator = "@narrator"
```
Componentes de cena: `DialogueActor`, `DialogueInteractable`, `DialogueTrigger`, `DialogueEventRelay` (UnityEvent por id de evento), `DialogueStep` (cutscene). `DialogueBridge` expõe helpers (variáveis, `SetLock`, `GiveItem`, `AddSanity`, `Damage`, `Shake`, `Alert`, `GoToLevel`, `Autosave`). Chaves de save: `dlg.<grafo>.v.<nó>` (nó visitado), `dlgtrigger.<id>`.

### 5.14 Props — `Props/`

```
abstract class PropHealth : MonoBehaviour, IItemHittable   // MaxHealth, Health, Health01, IsDead, Invulnerable
  protected abstract void OnZeroHealth(Vector3 point, Vector3 direction, GameObject source);  protected virtual void OnDamaged(...)
  void TakeDamage(float amount, Vector3 point, Vector3 direction, GameObject source);  void OnItemHit(ItemHit hit);  protected void SetHealth(float)
interface IBlastReceiver { void OnBlast(Vector3 center, float distance, float delay, GameObject source); }
static class PropBlast { void Detonate(Vector3 center, BlastSettings s, GameObject source) };  struct BlastSettings { radius, damage, velocity, upwards, playerDamageMultiplier, occlusion, mask, noiseRadius, cameraPower, cameraRadius, sanityLoss, chainDelayPerMeter; static Default }
```
Props prontos: `Breakable`, `ExplosiveBarrel` (herdam `PropHealth`), `Drawer`, `HidingSpot`/`HidingOccupant`, `LightSwitch`, `SwitchableLights`, `MirrorSurface`/`MirrorPhantom`, `OpenablePart`, `CoopButton`, `CoopLock`, `BearTrap`. **`PropsBridge`** (`Props/PropsBridge.cs`) reúne helpers prontos: `SetLock(p, key, on)`, `Teleport`, `SetHiding`, `Scare`, `SanityHit`, `Hint(p, text, seconds)`, `Noise`, `Sfx`, `Shake`, `ExplosionCamera`, `GetBool/SetBool`, `CountItem/GiveItem/RemoveItem/ItemName`, `Immobilize`, `Damage(Collider, …)`, `Players`, `IsAlive`, `AliveCount`, `Sanity01`, `SanityDrain`, `Glow`, `Tint`.

### 5.15 Perseguidor (Stalker) — `Stalker/`

- **Objetos de dados:** `StalkerDefinition` (carregadas por `Resources.LoadAll<StalkerDefinition>("HorrorFPS/Stalkers")`), `StalkerProfile` (sentidos, `AttentionTuning`), `StalkerTreeAsset` (árvore editável; `Tools > Horror FPS > 24. Stalker Tree Editor`).
- **Cérebro:** `StalkerBrain` (+`NavMeshAgent`) com `Knowledge` (`StalkerKnowledge`: `focus`, `hasVisual`, `visualPos`, `hasLead`, `leadFresh`, `leadPos`, `leadRadius`, `leadConfidence`…), `Blackboard` (`GetFloat/GetBool/SetFloat/SetBool/Add/Remove/Clear`), `Mode` (`StalkerMode`: Patrol, Investigate, Search, CheckHiding, Chase, Attack), `Tune` (`AttentionTuning`), `Home`, `Memory`, `Senses`, `Agent`, `Modules`; ações `MoveTo(dest, speed)`, `Stop()`, `Arrived(dist)`, `FaceToward(point, degPerSecond)`, `Stimulate(amount)`, `ForceAttention(value)`, `Freeze(frozen, disableAgent)`, `Warp(pos, rot)`, `ReceiveHint(h)`, `SetTree(asset)`/`RebuildTree()`; eventos `ModeChanged`, `AttentionChanged`, `Spotted`, `HintReceived`. Enums: `StalkerAttention { Calm, Alert, Hunting, Frenzy }`, `HintSource { Seen, Glimpse, Heard, DirectorPressure, DirectorHiding, Ping, Shared }`.
- **Ganchos de extensão:** nós da árvore (`StalkerTask`, `StalkerCondition`, `StalkerNode` — R5) e **módulos/traços** (`StalkerModule`: `Order`, `OnAttach`, `BuildBranches(StalkerTreeBuilder)`, `ModifyTuning(ref AttentionTuning)`, `ModifyVisibility(target, visibility)`, `FilterNoise(ref StalkerNoise)`, `OnSpotted`, `OnLostSight`, `OnHint`, `OnModeChanged`, `OnAttentionChanged`, `DecideAttack(target, ref damage)`, `OnArrived(reason)`, `BrainTick(dt)`, `SaveState()/LoadState(string)`). `StalkerTraitSprint` e `StalkerFinisher` são os exemplos.
- **Jogadores vistos pelo monstro:** `StalkerTarget` (`player`, `root`, `alive`, `hidden`, `sanity01`, `tension`, `speed`, `isolation`, `exposure`) via `StalkerBridge` (`TargetOf`, `Players`, `IsAlive`, `IsHidden`, `Damage`, `Kill`, `LoseSanity`, `EmitNoise`, `SpotsNear`…). Sistema em volta: `StalkerManager` (singleton), `StalkerDirector` (pressão/ritmo), `StalkerRoster` (persiste entre cenas), `StalkerMemory` (aprende iscas), `StalkerSpawner`/`StalkerSpawnPoint`/`StalkerSummonTrigger`, `PatrolPoint`, `StalkerNoiseTag`. Chaves de save: `stalker_memory_v2_<chave>`, `stalker_roster_v1`, `stalker_trigger_<id>`.

### 5.16 Reanimação (Revive) — `Revive/`

`ReviveApi` (estático): `IsDowned(who)`, `StateOf(who)` (`DownState`), `BleedLeft(who)`, `IsInjured(who)`, `Down(who)`, `Revive(who, ReviveKind)`, `ReviveBy(victim, reviver)`, `AllowDeath(who, seconds)`, `KillFinal(who)`, `CureInjury(who)`, `TryIntercept(who)` (**não está ligado**, veja 8). Perfil: `ReviveProfile` (`Resources/HorrorFPS/ReviveProfile`). Componentes: `ReviveManager`, `ReviveDownable`, `ReviveInteractable`, `ReviveHud`. Chaves de save com prefixo `revive.`.

### 5.17 SmartEvents — `SmartEvents/` (namespace `SmartEvents`, `using HorrorFPS;`)

```
[Serializable] class SmartEvent            // campo de Inspector dos gatilhos: condições + limite de disparos + variáveis + UnityEvents OnTriggered / OnBlocked / OnRestored
  bool CanInvoke() / CanInvoke(SmartEventContext); TriggerResult Invoke() / Invoke(GameObject instigator) / Invoke(SmartEventContext)
  void SetId(string); void AddCondition(SmartCondition); bool Restore(); void ResetProgress(); bool IsExhausted; bool PersistsToSave
readonly struct SmartEventContext { UnityEngine.Object Owner; GameObject Instigator; }
abstract class SmartCondition [Serializable]  bool Invert; bool Evaluate(ctx) (captura exceções → false); protected abstract bool Check(SmartEventContext ctx)
[ConditionMenu("Grupo/Nome")] define o caminho no menu "+ Adicionar" do Inspector
enum CompareOp { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual } + extensão Compare(left, right)
SmartEventTrigger.Activation (flags): Start, OnEnable, TriggerEnter, CollisionEnter;   SmartEventInteractable (Interactable)
```
Condições prontas: `InstigatorTag`, `InstigatorLayer`, `GameBool`, `GameInt`, `EventTriggered`, `GameObjectActive`, `RandomChance` e (HorrorFPS) `InstigatorIsPlayer`. Save: `smartevent.<ID>`.

### 5.18 Personagens e UI

- **Personagens:** `CharacterDefinition` (ScriptableObject), `CharacterCatalog` (`Resources/HorrorFPS/CharacterCatalog`), `CharacterIdentity` (módulo), `CharacterManager` estático (`MaxSlots = 8`): `Select(playerIndex, id, applyNow = true)`, `SelectAll`, `SelectedId`, `GetSelected`, `CurrentOf(player)`, `Switch(player, def|id)`, `SwitchAll(id)`, `ResetBag`, `AutoApply`, evento `CharacterChanged`. Save: `character_p<n>`.
- **UI por código:** `UIFactory` (`NewRect`, `Stretch`, `Anchored`, `SetLayout`, `AddImage`, `AddText`, `Label`, `CreateButton`, `MenuButton`, `CreateSlider`, `CreateScrollList`, `VerticalColumn`, `CreateOverlayCanvas(name, parent, sortingOrder)`, `EnsureEventSystem()`, `GetFont()`/`CustomFont`; cores `PanelColor`, `TextColor`, `DimTextColor`, `AccentColor`, `ButtonColor`), `UISprites`, `InvUI`, `Toast.Show(msg, seconds = 2.5f)`. Telas: `MainMenu`, `PauseMenu`, `SettingsPanel`, `ControlsPanel`, `SaveSlotsPanel`, `GameOverScreen`, `PlayerHUD` (por jogador), `InventoryScreen`.

---

## 6. Receitas (modelos de código)

> Os modelos usam só membros conferidos na seção 5. Troque os nomes de exemplo; confirme que não colidem com classes existentes (Apêndice A).

### R1. Objeto interativo com estado salvo (modelo: `Door`, `UnityEventInteractable`)

```csharp
using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Alavanca que liga/desliga e lembra o estado no save.</summary>
    [AddComponentMenu("HorrorFPS/Props/Alavanca")]
    public class Alavanca : Interactable, ISaveable
    {
        [Header("Alavanca")]
        [Tooltip("Variável do jogo (GameSession) ligada enquanto a alavanca estiver acionada. Vazio = não grava variável.")]
        [SerializeField] string variavel = "";

        [Serializable]
        class State
        {
            public bool on;
        }

        bool on;

        public bool IsOn { get { return on; } }

        public override void Interact(PlayerContext player)
        {
            SetOn(!on);
        }

        public void SetOn(bool value)
        {
            on = value;
            if (!string.IsNullOrEmpty(variavel)) GameSession.SetBool(variavel, on);
        }

        // Só entra no save com um SaveableEntity no MESMO objeto.
        public string SaveKey { get { return "alavanca"; } }

        public string CaptureState()
        {
            State s = new State();
            s.on = on;
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            State s = JsonUtility.FromJson<State>(json);
            on = s.on;
        }
    }
}
```

Notas: o verbo e o nome da dica vêm dos campos `verb`/`objectName` do `Interactable` (ou sobrescreva `DefaultVerb` com uma constante de `UIText`). `RestoreState` chega depois do `Awake` da cena, **mas** em objeto inicialmente desativado o `Awake` ainda não rodou: se o seu estado depende de algo criado no `Awake`, guarde o JSON em um campo `pending` e aplique no `Awake` (veja `Door`). Para bloquear a interação: `CanInteract` + `GetBlockedReason` (+ `OnInteractBlocked` para tocar o som de "trancado").

### R2. Módulo por jogador (modelo: `DamageReaction`, `ItemInput`)

```csharp
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Reage ao dano do próprio jogador.</summary>
    [AddComponentMenu("HorrorFPS/Corpo/Meu Módulo")]
    [PlayerModule("Corpo", 5)]
    public class MeuModulo : MonoBehaviour
    {
        StatsComponent stats;

        void OnEnable()
        {
            stats = PlayerRef.Find<StatsComponent>(this);
            if (stats != null) stats.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (stats != null) stats.Damaged -= OnDamaged;
            stats = null;
        }

        void OnDamaged(StatsComponent who, DamageInfo info, float applied)
        {
            // reaja aqui (applied = dano realmente aplicado)
        }
    }
}
```

Para ele existir em **todo** jogador: (a) adicione `Ensure<MeuModulo>(player);` em `PlayerModules.Install` (`Core/PlayerModules.cs`) — instala sozinho em cada `PlayerContext` novo; ou (b) coloque o componente em `Prefabs/Player.prefab` (ou use o menu 4). O atributo `[PlayerModule]` só organiza o Inspector. Se o módulo precisa de outro que é instalado depois, use `Update` com `nextBind` (como `DamageReaction.TryBind`) ou busque tudo em `Start`.

### R3. Passo de cutscene (modelo: `FadeStep`, `SubtitleStep`)

```csharp
using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Acende uma luz por alguns segundos e apaga.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Pulse Light")]
    public class PulseLightStep : CutsceneStep
    {
        [Header("Luz")]
        [SerializeField] Light alvo;
        [SerializeField] float seconds = 1f;

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (alvo == null) yield break;
            alvo.enabled = true;
            yield return CutsceneContext.Wait(seconds);
            alvo.enabled = false;
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (alvo != null) alvo.enabled = false;   // resultado final, sem animar
        }
    }
}
```

`delay` e `runWithNext` já vêm da base. Para mexer na câmera da cutscene, escreva em `ctx.CameraPosition/CameraRotation/CameraFov/CameraShake` (veja `CameraMoveStep`); nunca na `Camera` direto.

### R4. Efeito de item, uso de item, ação/condição de diálogo

- **Efeito novo sem mexer no framework:** no `ItemData`, ação com efeito `kind = Message` e `key = "minha_chave"`; no jogador, um script implementa `IItemUseHandler`:

  ```csharp
  public class MeuUsoDeItem : MonoBehaviour, IItemUseHandler
  {
      public bool OnBeforeUse(ItemUseContext ctx) { return false; }   // true = cancela o uso

      public void OnItemMessage(ItemUseContext ctx, string key)
      {
          if (key == "minha_chave") { /* ctx.Player, ctx.Stats, ctx.Sanity, ctx.item ... */ }
      }
  }
  ```
- **Efeito novo no framework:** acrescente o valor **no fim** de `ItemEffectKind` e um `case` em `ItemEffect.Execute` (`Items/ItemEffect.cs`).
- **Ação e condição de diálogo por código** (nó com `kind = Custom` e o id escolhido; o método vai em qualquer classe do projeto, e `Register` substitui o id, então registrar de novo é seguro):

  ```csharp
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void RegistrarDialogo()
  {
      DialogueActions.Register("abrir_cofre", (ctx, argumento) => { GameSession.SetBool("cofre_aberto", true); });
      DialogueConditions.Register("cofre_aberto", (ctx, argumento) => GameSession.GetBool("cofre_aberto"));
  }
  ```

### R5. Nó, condição e traço do perseguidor (modelos: `StalkerNodeExamples.cs`, `StalkerTraitSprint.cs`)

Basta uma classe `[Serializable]` em qualquer arquivo; ela aparece sozinha no menu "Adicionar" da janela da árvore. Campos **públicos** viram parâmetros editáveis; campos **privados** são o estado daquele monstro. Atalho: `Assets > Create > HorrorFPS > Stalker Node Script (Action | Condition)`.

```csharp
using System;
using UnityEngine;

namespace HorrorFPS
{
    [Serializable]
    [StalkerNodeMenu("Exemplos/Encarar e esperar", "Para, encara a pista e espera alguns segundos.")]
    public class StalkerEncararEsperar : StalkerTask
    {
        public float seconds = 1.5f;

        float left;

        public override string Summary() { return seconds.ToString("0.##") + " s"; }

        public override bool CanFail { get { return false; } }

        protected override void OnEnter()
        {
            left = seconds;
            Brain.Stop();
        }

        protected override BTStatus OnTick()
        {
            if (Brain.Knowledge.hasLead) Brain.FaceToward(Brain.Knowledge.leadPos, 240f);
            left -= Time.deltaTime;
            return left > 0f ? BTStatus.Running : BTStatus.Success;
        }

        protected override void OnAbort() { left = 0f; }   // interrompido por um nó mais prioritário
    }

    [Serializable]
    [StalkerNodeMenu("Exemplos/Perto de casa", "Verdadeiro quando está a menos de N metros do lar.")]
    public class StalkerPertoDeCasa : StalkerCondition
    {
        public float meters = 12f;

        public override string Summary() { return "< " + meters.ToString("0.#") + " m"; }

        protected override bool Check()
        {
            return (Brain.transform.position - Brain.Home).sqrMagnitude < meters * meters;
        }
    }
}
```

**Traço (poder exclusivo):** componente que herda de `StalkerModule` no **mesmo prefab** do `StalkerBrain` (`[RequireComponent]` já vem da base); sobrescreva só os ganchos que precisa (lista em 5.15). `StalkerTraitSprint` usa `OnSpotted` + `ModifyTuning` + `BrainTick`. Nada no cérebro precisa mudar.

### R6. Condição de SmartEvent (namespace `SmartEvents`, campos `_camelCase`)

```csharp
using System;
using UnityEngine;
using HorrorFPS;

namespace SmartEvents
{
    [Serializable, ConditionMenu("Meu Jogo/Jogador tem item")]
    public class JogadorTemItemCondition : SmartCondition
    {
        [SerializeField] private string _itemId = "";
        [SerializeField] private int _quantidade = 1;

        protected override bool Check(SmartEventContext context)
        {
            if (context.Instigator == null) return false;
            PlayerContext player = context.Instigator.GetComponentInParent<PlayerContext>();
            if (player == null) return false;
            InventoryOwner inventario = PlayerRef.Find<InventoryOwner>(player);
            return inventario != null && inventario.Has(_itemId, _quantidade);
        }
    }
}
```
Requisitos: `[Serializable]`, construtor público sem parâmetros (o padrão basta) e campos serializáveis; o botão NOT do Inspector é a propriedade `Invert` da base.

### R7. Câmera, sanidade e barulho

```csharp
// Efeito de câmera do jogador dono (ICameraModifier)
public class MeuTremor : MonoBehaviour, ICameraModifier
{
    void OnEnable() { CameraFx.Register(this); }
    void OnDisable() { CameraFx.Unregister(this); }

    public void Contribute(Camera cam, ref CameraDelta delta)
    {
        if (cam != PlayerRef.CameraOf(this)) return;   // só a câmera do SEU jogador
        delta.euler.z += 2f;                           // somar, nunca atribuir
    }
}

// Reagir à sanidade de qualquer jogador (base pronta: SanityAware)
public class LuzNervosa : SanityAware
{
    public override void OnSanityStageChanged(SanityComponent who, SanityStage from, SanityStage to)
    {
        if (to >= SanityStage.Disturbed) { /* piscar, mudar, mentir... */ }
    }
}

// Ouvir barulhos (precisa estar no mesmo objeto, ou num pai, de um Collider; ou use NoiseListener)
public class AlarmeSonoro : MonoBehaviour, INoiseHearer
{
    [SerializeField] float sensibilidade = 1f;

    public void OnNoiseHeard(NoiseEvent noise)
    {
        float alto = NoiseSystem.LoudnessAt(noise, transform.position, sensibilidade);
        if (alto > 0.3f) { /* dispara o alarme */ }
    }
}
```
Efeitos que dependem de quem olha (luz, visibilidade, imagem): herde `PerViewerEffect` (modelos: `SanityLight`, `SanityVisibility`). Emitir barulho: `NoiseSystem.Emit(posição, raio, NoiseKind.Item, gameObject)`.

### R8. Nova ação de input

1. **Ação base** (remapeável, aparece no painel de controles): adicione a ação ao mapa `Gameplay` de `Input/HorrorControls.inputactions` pelo **editor de Input Actions do Unity** (o JSON usa ids GUID; evite editar à mão), declare a constante em `InputNames`, inclua em `InputNames.Rebindable` se for remapeável e leia por `PlayerInputHandler.GetAction(nome)` / `WasPressed(nome)` / `IsHeld(nome)` (ou crie uma propriedade como `JumpPressed`).
2. **Ação de módulo** (recomendado para recursos novos de inventário/equipamento/esconderijo): em `Input/ItemInput.cs`, acrescente a propriedade e a linha em `Build()`:
   ```csharp
   public InputAction MinhaAcao { get; private set; }
   // em Build():
   MinhaAcao = Button("MinhaAcao", "<Keyboard>/l", "<Gamepad>/dpad/up");
   ```
   Leitura em qualquer módulo do jogador:
   ```csharp
   ItemInput input = PlayerRef.Find<ItemInput>(this);
   if (input != null && !input.Paused && ItemInput.Pressed(input.MinhaAcao)) { /* ... */ }
   ```
   Fica fora do painel de remapeamento (como as demais ações do `ItemInput`).
3. **Leitura direta do dispositivo do jogador** (modelo: `PropsInput`): `PlayerInput.devices` do próprio jogador; nunca `Keyboard.current` sem filtrar pelo jogador.

### R9. Novo nível

1. Crie a cena `Level_X` em `Scenes/` e adicione-a ao **Build Settings** (também `Core` e `MainMenu`).
2. Em `Resources/HorrorFPS/GameCatalog.asset`, acrescente um item em `levels`: `id`, `displayName`, `scenes[]`, `defaultSpawn`, `loadingTip`.
3. Na cena, `SpawnPoint` com o `spawnId` (padrão `SpawnPoint.DefaultId`; o campo `slot` escolhe o ponto de cada jogador, por índice).
4. Passagem entre níveis: `LevelDoor` (`levelId`, `spawnId`, `requiredVariable`), `LevelTrigger` (`requireAllPlayers`), passo de cutscene `GoToLevelStep`, ou `SceneDirector.GoToLevel(id, spawnId)`.
5. Objetos que mudam de estado: `SaveableEntity` + `SaveableTransform`/`SaveableActive`/`ISaveable` no mesmo objeto; objetos que sumirão (item pego): `Pickup` já chama `MarkDestroyed()`.
6. Cenas extras durante o nível: `SceneStreamer`/`SceneDirector.LoadAdditive`.

### R10. Ferramenta de Editor

- Fica em `Editor/` (pasta especial do Unity; sem `#if UNITY_EDITOR`), `using UnityEditor;`. Namespace: nas subpastas (`Editor/Stalker`, `Dialogue`…) use `HorrorFPS`; na raiz de `Editor/` o código usa `HorrorFPS.ItemsEditor` (e `HorrorFPS.EditorTools` no assistente `HorrorFPSSetup`).
- Menu: `[MenuItem("Tools/Horror FPS/28. Nome")]` (próximo número livre), com `[MenuItem("...", true)]` para validação quando precisar. Assets: `Assets/Create/HorrorFPS/...`.
- **Onde gravar assets:** use `ProjectPaths.Root` (a pasta-raiz do framework, descoberta pelo lugar do script `Editor/ProjectPaths.cs`, então continua certa se a pasta for renomeada) e `ProjectPaths.Folder("Generated/Dialogues")` (pasta relativa à raiz; se a raiz atual ainda não tem a pasta mas a antiga `Assets/HorrorFPS/...` tem, usa a antiga). **Não** escreva `"Assets/HorrorFPS/..."` nem `"Assets/Huggable Horrors/..."` fixos: o código antigo tinha duas raízes fixas diferentes e podia espalhar os assets gerados em duas pastas (seção 8, item 9). No `HorrorFPSSetup`, `Root`, `InputPath`, `PrefabPath`, `ScenePath` (some com o pacote B), `CoreScenePath`, `HallScenePath`, `CellarScenePath`, `AnnexScenePath`, `CatalogPath` e `ShowcaseScenePath` são propriedades calculadas em cima disso. [V]
- **Antes de sobrescrever** algo que o usuário pode ter editado (cena, prefab, catálogo), confirme com `EditorUtility.DisplayDialog` (menus 1, 2, 3 e 27 fazem isso) e use `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` antes de trocar de cena. Nunca apague o que o usuário criou. [V]
- Inspector próprio: `[CustomEditor(typeof(X))]` (modelo: `CharacterCatalogEditor`); drawer de campo: `[CustomPropertyDrawer(typeof(X))]` (modelo: `ItemShapeDrawer`). Utilidades: `WizardUtil`, `AnimationEditorUtil`, `DialogueEditorUtil`.
- Código de Editor dentro de script de runtime só com `#if UNITY_EDITOR` (5 usos hoje; ex.: `SaveableEntity.Reset/OnValidate`).

### R11. Persistir algo novo — qual mecanismo?

| Quero guardar… | Use | Chave / padrão |
|---|---|---|
| Flag, contador ou texto do jogo ("tem_chave", progresso) | `GameSession.SetBool/SetInt/AddInt/SetString` | chave em string; **não** colidir com prefixos abaixo |
| Estado de um objeto da cena (porta, gaveta, alavanca) | `ISaveable` + `SaveableEntity` no mesmo objeto | `SaveKey` único dentro do objeto (`"door"`, `"event"`…) |
| Posição de um objeto movido (caixa) | `SaveableTransform` + `SaveableEntity` | — |
| Objeto ativo/inativo | `SaveableActive` | — |
| Vida/status de algo com `StatsComponent` | `SaveableStats` | — |
| Estado por jogador de um sistema global (sanidade, personagem) | JSON em variável de sessão, gravado em `FrameworkBridge.BeforeSave` e lido em `AfterLoad` | `sanity_player_<n>`, `character_p<n>` |
| Item largado no mundo | `DropRegistry` | `drops_v1` |
| Cutscene já vista | automático (`CutsceneSequence.PlayOnce`) | `cutscene.<id>` |

Prefixos de chave **já usados** (não reutilize): `sanity_player_<n>`, `container_<id>`, `drops_v1`, `character_p<n>`, `cutscene.<id>`, `dlg.<grafo>.v.<nó>`, `dlgtrigger.<id>`, `smartevent.<ID>`, `stalker_memory_v2_<chave>`, `stalker_roster_v1`, `stalker_trigger_<id>`, `revive.*` e `revive_*`. Padrão de código para estado global por jogador (de `SanityComponent`):

```csharp
void OnEnable()
{
    FrameworkBridge.BeforeSave += OnBeforeSave;
    FrameworkBridge.AfterLoad += OnAfterLoad;
}

void OnDisable()
{
    FrameworkBridge.BeforeSave -= OnBeforeSave;
    FrameworkBridge.AfterLoad -= OnAfterLoad;
}

string SessionKey { get { return "meu_sistema_player_" + PlayerRef.IndexOf(this); } }

void OnBeforeSave() { FrameworkBridge.SetString(SessionKey, JsonUtility.ToJson(meuEstado)); }

void OnAfterLoad()
{
    string json = FrameworkBridge.GetString(SessionKey);
    if (!string.IsNullOrEmpty(json)) meuEstado = JsonUtility.FromJson<MeuEstado>(json);
}
```

### R12. Montar uma cena de teste por código (modelo: menu 27, `HorrorFPSSetup.Showcase*.cs`)

Os construtores abaixo são `public static` e **não** abrem diálogo, não selecionam nada e não registram undo: quem chama decide. Use-os em vez de copiar a montagem. [V]

| Construtor | O que monta | Observações |
|---|---|---|
| `SampleItems.EnsureAll(out int created)` → `Dictionary<string, ItemData>` | cria só os itens de exemplo que faltam (reaproveita o que já existe no projeto, por `id`) e devolve todos por id | alimenta `DemoProps.Build` |
| `DemoProps.Build(items, origin, facing, ensureGround)` → `GameObject` | bancada de itens, sanidade e corpo (`"[Demo] Itens, Sanidade e Corpo"`) | `ensureGround` só põe chão se a física não achar nenhum embaixo |
| `PropsDemoBuilder.BuildAt(origin, withFloor)` → `GameObject` | bancada de props (`"[Props Demo]"`, faixa de ~70 m no eixo X, frente = +Z) | `withFloor` desenha o chão da bancada |
| `DialogueMenus.EnsureSamples(out zelador, out pensamento)` → `bool` e `DialogueMenus.BuildDemoProps(origin, zelador, pensamento)` → `GameObject` | os dois grafos de exemplo (cria só o que falta) e o NPC, a barreira, a luz de alarme e o gatilho já ligados a eles | — |
| `StalkerMenu.BuildDemo(origin, embedded)` → `StalkerMenu.DemoResult` (`root`, `monster`, `baked`, `finisherNote`) | arena 40×40 com 4 salas, ronda, monstro com finalizador e rádio isca (`"[Stalker Demo]"`) | `embedded = true`: abre porta de 7 m na parede sul, NavMesh só da arena, painel de debug do monstro desligado. `baked == false` → avise o usuário para fazer o Bake |

**Como o menu 27 se organiza** (`HorrorFPSSetup` é `partial`; `Root`, `Mat`, `Prim`, `MakeLight`, `MakeSpawnGroup`, `SetupAtmosphere`, `UpsertLevel`, `AddToBuildSettings`, `EnsurePlayerPrefab`, `BuildCoreScene` vêm dos outros arquivos da classe):

1. `CreateShowcaseMenu` (menu 27) → confirma antes de recriar `Level_Showcase`, salva a cena atual, cria o Core só se faltar, chama `BuildShowcaseScene`, registra o nível `showcase` no catálogo **só se o catálogo existe e ainda não conhece o nível** (`RegisterShowcaseLevel`; não mexe nas outras entradas) e põe Core e `Level_Showcase` nos Build Settings. Nada que o menu 3 criou é recriado.
2. `BuildShowcaseScene` cria uma cena vazia, salva-a primeiro (os assets gerados, como o NavMesh, ficam ao lado dela) e roda cada zona com `RunZone(failures, "nome", método)`: try/catch, e tudo que a zona criou na raiz da cena vira filho de `"[Zona] nome"` (`SetParent(..., true)`). Se uma zona lançar exceção, só ela é pulada (o Console tem o erro; o diálogo final lista as zonas puladas).
3. **Zona nova:** (a) escolha uma origem livre e acrescente um `static readonly Vector3 ScNova` junto de `ScMovement`/`ScItems`/… (mantenha ≥ 12 m de folga das outras; o mundo vai de −130 a +130 m e tem limites invisíveis); (b) acrescente um `NewZoneInfo(título, nomeCurto, nomeDaCor, cor, saída, ladoPraça, ladoPortal, pontosDaEstrada, textoDaPlaca, largura, altura)` em `ShowcaseZones()`: a praça e a entrada da zona (estrada, portal, farol, placa de direção e placa de entrada) saem sozinhas desses dados; (c) escreva `ShowcaseNova()` em `HorrorFPSSetup.Showcase.Zones.cs`; (d) chame-a em `BuildShowcaseScene` com `RunZone`.
4. **Ajudantes de cenário** (em `.Showcase.cs`): `MakePad(nome, centro, tamX, tamZ, mat)`, `MakePath`/`MakeRoad`, `MakeLamp`, `MakeBeacon`, `MakeGateFrame`, `MakePedestal`, `MakeDoorWall`, `At(origem, x, y, z)`, `GlowMat`, `ColorKey`, `MakeBoard(nome, pos, yaw, larg, alt, título, corpo, cor, tamTítulo, tamCorpo)` e `MakeLabel(nome, pos, yaw, larg, alt, texto, fonte, cor)` (texto em mundo por Canvas `WorldSpace`; `NewUi`/`AddText` são a base). **`yaw` = a direção para onde o LEITOR olha** (0 = norte/+Z, 90 = leste, 180 = sul, 270 = oeste); ajuste `renderMode` do Canvas **antes** de dimensionar. Geometria: terreno com topo em y = −0,3; pads em y = 0; estradas com topo em y = −0,05; o `CharacterController` do jogador tem `stepOffset` 0,35 (degrau maior trava).
5. **SmartEvents por código** (em `.Zones.cs`, namespace `SmartEvents` com alias `using SE = SmartEvents;`): `AddSmartButton(go, id, verbo, objeto, textoBloqueado, desativarQuandoBloqueado, limite)` (põe `SmartEventInteractable` e define um id legível com `SetId`), `AddGameIntCondition`, `AddGameBoolCondition`, `AddEventTriggeredCondition`, `AddChanceCondition`, `AddVariableChange`, e `OnTriggeredSetActive`/`OnRestoredSetActive` (ligam `GameObject.SetActive` ao evento com `UnityEventTools.AddBoolPersistentListener`). Os campos são privados: `SetPrivate(obj, "_campo", valor)` usa reflexão (7.6) e avisa no Console se o campo não existir. **Não misture** um `SerializedObject` aberto com atribuição direta de campo no mesmo componente: o valor de uma das duas formas pode se perder quando o outro lado grava. Por isso os componentes comuns usam só o ajudante `Props(componente).Str/Int/Float/Bool/Enum/Obj/Refs(...).Apply()` (de `HorrorFPSSetup.FullDemo.cs`, via `SerializedObject`) e os `SmartEvent`/condições usam só reflexão (`SetPrivate`). Ouvintes de `UnityEvent` persistentes: `UnityEventTools.AddVoidPersistentListener`/`AddBoolPersistentListener`.
6. **Testar:** abra `Level_Showcase` e dê Play (o `SceneDirector.TryAutoBoot` inicia a sessão e carrega o Core se ele estiver nos Build Settings; sem sessão ativa não há save). Mude a cena **só** pelo menu (recriar apaga edições manuais).

---

## 7. Armadilhas (leia antes de mexer em estado, entrada, tempo, câmera ou nomes)

### 7.1 Save e estado

- **`SaveableEntity` só enxerga `ISaveable` no mesmo GameObject** e guarda a lista em cache na primeira captura/restauração (`GetComponents<ISaveable>()`): um `ISaveable` adicionado depois não entra. [V]
- **Só entram no save os ids que já existiam quando a cena abriu.** Objeto criado com `Instantiate` durante o jogo não é salvo nem recriado ao carregar. Para "coisas que o jogador cria ou larga" use variáveis (`GameSession`) ou um registro próprio (modelo: `DropRegistry`, chave `drops_v1`). [V]
- **"Destruído" = estava na cena ao abrir e sumiu** (ou `MarkDestroyed()` foi chamado). Quem destrói por script um objeto salvável deve chamar `MarkDestroyed()` antes (o `Pickup` faz): assim o save já o conta como destruído. [V]
- **`RestoreState` roda em `sceneLoaded`, antes de qualquer `Start`.** Em objeto inicialmente desativado o `Awake` ainda não rodou: guarde o JSON em um campo `pending` e aplique no `Awake` (modelo: `World/Door.cs`). [V]
- **Id vazio = caminho na hierarquia** (`path:/Raiz[0]/Filho[2]`): renomear ou reordenar objetos muda o id e o estado salvo se perde. O Editor gera um GUID (`Reset`/`OnValidate`) e troca ids duplicados dentro da mesma cena (copiar/colar); nunca copie o campo `id` à mão. [V]
- **`MainMenu` e `Core` não guardam estado de mundo**; qualquer outra cena (níveis e cenas em streaming) entra em `CaptureScene`/`ApplyScene`. [V]
- **`SaveSystem.CanSave` é falso** sem partida ativa, durante troca de nível (`SceneDirector.IsBusy`), em cutscene, durante um save e se **qualquer** jogador estiver morto. `Autosave()` então não faz nada, sem aviso, e `SaveToSlot` mostra `UIText.SaveFailed`: não presuma que salvou. [V]
- **`GameSession` guarda só strings** (bool = `"1"`/`"0"`): use os métodos tipados e não reutilize os prefixos de chave da R11. [V]
- **`JsonUtility`** (usado nos `ISaveable`): sem `Dictionary`, sem polimorfismo; só classes `[Serializable]` com campos públicos ou `[SerializeField]`. [Unity]
- Estado **por jogador** de um módulo global (sanidade, personagem) não vai por `SaveableEntity`: use o padrão `FrameworkBridge.BeforeSave/AfterLoad` da R11. [V]

### 7.2 Entrada e controle

- **A entrada base devolve zero** quando `GameplayEnabled` é falso (pausa, menu, morte) ou `ExternallyLocked` é verdadeiro (qualquer trava ativa). "O jogador não responde" → veja `PlayerContext.IsControlLocked` e quem segura a chave. [V]
- **Travas são um `HashSet<object>` por jogador: chaves iguais são a mesma trava** (duas strings idênticas se anulam no primeiro `Remove`). Chaves existentes: objetos `SceneDirector.LockKey` e `CutsceneDirector.LockKey`; strings `"Dialogue"` (`DialogueBridge`), `"Hiding"` (`PropsBridge`), `"Sanity"` (`SanityEffects`), `"Inventory"` (`FrameworkBridge`), `"Revive"` (`ReviveDownable`). Em sistema novo: `static readonly object LockKey = new object();`. [V]
- **Sempre remova a trava** em `OnDisable`, nos caminhos de saída antecipada e quando o jogador sair (`PlayerContext.PlayerRemoved`): trava esquecida congela o jogador para sempre. [V: enquanto `controlLocks.Count > 0` o bloqueio continua]
- **Jogador que entra depois:** travas pedidas antes do `Awake` dele são aplicadas no `Awake`; o `LocalPlayerManager` dá `SceneDirector.LockKey` a quem entra durante uma troca de nível; a cutscene reaplica a trava (inclusive a quem entrou depois do início). Um sistema novo que trava "todos os jogadores" deve ouvir `PlayerContext.PlayerSpawned` ou reaplicar a cada quadro. [V]
- **`Input.GameplayEnabled` é da pausa e da morte** (`PauseManager`, `PlayerContext.HandleDied/Revive`): não use para travar. [V]
- **`Keyboard.current`/`Mouse.current`/`Gamepad.current` (todos os jogadores misturados)** só aparecem em: menus (`MainMenu`, `PauseMenu`), `LocalPlayerManager` (cria o 1º jogador), cursor do `InventoryScreen`, `ReviveDebugKeys` (debug) e como **fallback** em `PropsInput`/`DialogueInput` quando o jogador não tem `PlayerInput` ou dispositivos. Em gameplay leia as ações do jogador ou `PlayerInput.devices` dele. [V]
- **Mouse em split-screen:** `ItemInput.UsesMouse` é verdadeiro só se o jogador tem um `Mouse` entre os dispositivos dele (sem `PlayerInput`, vale `Mouse.current != null`). [V]
- **Remapeamento:** só as 7 ações de `InputNames.Rebindable` aparecem no painel de controles; `ItemInput`, `PropsInput` e `DialogueInput` ficam fora. [V]

### 7.3 Tempo e pausa

- **Pausa = `Time.timeScale = 0` para todos os jogadores** (`PauseManager.Owner` é só quem abriu o menu). Gameplay usa `deltaTime` (para sozinho); UI e temporizadores que rodam pausados usam `unscaledDeltaTime`/`unscaledTime`. [V]
- **Ação disparada por input de gameplay deve ignorar a pausa:** `if (Time.timeScale <= 0f) return;` (modelos: `EquipmentController`, `HidingOccupant`, `DialogueView`) ou `ItemInput.Paused`. [V]
- `WaitForSeconds`, `Invoke` e `Time.deltaTime` usam o tempo do jogo (param na pausa); `yield return null` **continua rodando todo quadro, mesmo pausado** (por isso um laço que soma `Time.deltaTime` para sozinho); `WaitForSecondsRealtime` e `unscaledDeltaTime` ignoram a pausa. Corrotina de sistema estático que precisa sobreviver à troca de cena: `GameRuntime.Run(...)`. [V só o `GameRuntime.Run`; o resto: Unity]
- Dentro de `CutsceneStep.Run` use só `yield return null` ou `yield return CutsceneContext.Wait(s)`: o `Wait` soma `Time.deltaTime`, então para junto com a pausa (5.9). [V]

### 7.4 Câmera, áudio e split-screen

- **Efeito de câmera só por `ICameraModifier`**: some em `delta` (nunca atribua) e confira `cam == PlayerRef.CameraOf(this)` (o `Contribute` roda uma vez por câmera renderizada). [V]
- Os modificadores são aplicados em `CameraHook.PreRender` e **desfeitos em `PostRender`**: no `Update` a câmera está "limpa" (bom para mira e raycast). Escrever direto em `localPosition`/`localRotation`/`fieldOfView` mistura seu efeito com o gameplay e com os outros modificadores. `CameraHook` assina `Camera.onPreCull/onPostRender` e `RenderPipelineManager.begin/endCameraRendering` (pipeline embutido e URP/HDRP). [V]
- **Só existe um `AudioListener` ativo:** o do primeiro jogador de `PlayerContext.All`, ou o da câmera de cutscene (que suprime os dos jogadores). Não há áudio "só do jogador 2" (`Body/PlayerAudio.cs` avisa). A exposição de posição para a IA (`NoiseSystem`) é separada do áudio e funciona por jogador. [V]
- **Efeito que depende de quem olha** (sanidade, luz, visibilidade): `PerViewerEffect`/`CameraHook`; o mesmo objeto de cena é visto por vários jogadores com estados diferentes. [V]
- Durante cutscene com câmera própria o `CutsceneDirector` esconde as câmeras dos jogadores e liga uma câmera e um `AudioListener` dele; módulo nenhum deve religar a câmera do jogador nesse intervalo. [V]
- **Camada 31** é reservada à inspeção 3D de itens (`InspectStudio.InspectLayer`, configurável). [V]

### 7.5 Código, dados e arquivos

- **Enums serializados guardam o inteiro:** acrescente valores **no fim** (`ItemEffectKind`, `DialogueActionKind`, `DialogueConditionKind`, `StatType`, `NoiseKind`…); inserir no meio muda o significado de assets e cenas existentes. [Unity; os enums existem como na seção 5]
- **`[SerializeReference]`** (nós da árvore do Stalker: `StalkerNodeBase.cs`/`StalkerTreeAsset.cs`; condições dentro de `SmartEvent`) grava o nome completo do tipo (assembly + namespace + classe). **Renomear ou mover** uma classe já usada em assets/cenas perde os dados daquele nó/condição. O projeto não usa `[MovedFrom]`; se precisar renomear, use `[UnityEngine.Scripting.APIUpdating.MovedFrom(...)]`. [V: onde é usado e que `MovedFrom` não aparece; o comportamento é do Unity]
- **Renomear um campo `[SerializeField]`** perde o valor em cenas e prefabs (use `[FormerlySerializedAs("antigo")]`) e quebra os assistentes do Editor que o procuram por texto (`so.FindProperty("campo")` em `Editor/DemoProps.cs`, `Editor/Stalker/StalkerMenu.cs` e outros). Procure o nome entre aspas antes de renomear. [V]
- **`.meta`:** arquivo novo gera `.meta` ao importar; commite os dois. Não mova nem renomeie `.cs` fora do Unity (cenas e prefabs ligam scripts por GUID). [V]
- **`Resources`:** `GameCatalog` (`HorrorFPS/GameCatalog`), `CharacterCatalog` (`HorrorFPS/CharacterCatalog`), `ReviveProfile` (`HorrorFPS/ReviveProfile`), `StalkerDefinition` (`HorrorFPS/Stalkers/`) e `ItemData` (qualquer pasta `Resources`). Asset fora dessas pastas não é encontrado, e o save de itens guarda só o `id`. [V]
- **Estáticos:** todo `static` mutável tem `ResetStatics` (58 arquivos): com domain reload desligado nas opções de Play Mode, estáticos sobrevivem entre execuções. `DialogueActions`/`DialogueConditions` não têm `ResetStatics`: `Register(id, fn)` **substitui** o id (registrar de novo é seguro) e existe `Unregister(id)`, mas o que foi registrado sobrevive entre execuções com domain reload desligado, então não capture objetos de cena nas lambdas (R4). [V + Unity]
- **Buscas:** o projeto mistura `HorrorUtil.FindAll/FindFirst` com `FindObjectsByType`/`FindFirstObjectByType` direto (`DialogueBridge`, `PropsBridge`, `StalkerBridge`, `StalkerManager`, `StalkerDirector`; `SmartEventTrigger` escolhe por `#if`). Em código novo use `HorrorUtil`. [V]
- **UI:** `UnityEngine.UI.Text` legado, sem TextMeshPro; fonte por `UIFactory.GetFont()`. [V]
- **Pipeline de render:** os `.mat` do repositório são `Universal Render Pipeline/Lit` (shader `933532a4…`, propriedades `_BaseMap`/`_BaseColor`/`_WorkflowMode`), o que indica **URP**; sem `ProjectSettings` não confirmo o pipeline ativo. Shader/efeito de tela novo deve funcionar em URP. [? inferido dos `.mat`]

### 7.6 Acoplamentos por nome (reflexão)

16 arquivos usam `System.Reflection` (o 16º, `Editor/HorrorFPSSetup.Showcase.Zones`, vem do pacote A e usa o nome qualificado, sem `using`): `Animation/PlayerAnimBridge`, `Animation/PlayerAnimItemAdapter`, `Body/DamageReaction`, `Body/DynamicHeadBob`, `Core/FrameworkBridge` (importa, mas chama o framework direto), `Dialogue/DialogueBridge`, `Editor/DemoProps`, `Editor/HorrorFPSSetup.Showcase.Zones`, `Editor/Stalker/StalkerMenu`, `Props/PropsBridge`, `Revive/ReviveBridge`, `Revive/ReviveDeath`, `Revive/ReviveHealth`, `Revive/ReviveReflect`, `Stalker/StalkerBridge`, `Stalker/StalkerFinisher`. Eles procuram membros **por nome** (muitas vezes com listas de nomes candidatos). Se você renomear, mover ou mudar a assinatura de um membro abaixo, o compilador **não avisa**: o recurso só deixa de funcionar (alguns registram `LogWarning`). Antes de mexer, procure o nome entre aspas: `grep -rn '"NomeDoMembro"' --include=*.cs .`

| Nome procurado | Quem procura | O que para de funcionar |
|---|---|---|
| Eventos `FirstPersonController.Footstep/Jumped/Landed`, `StatsComponent.Damaged/Died`, `ItemEvents.Equipped/Unequipped/EquippedActivated` | `Animation/PlayerAnimDriver`, `PlayerAnimItemAdapter` (via `PlayerAnimBridge.Bind`) | o corpo/animação deixa de reagir; sem erro (`Bind` devolve `null`) |
| `InventoryOwner.GetEquipped(string)` e o campo `ItemData.equipSlot` | `Animation/PlayerAnimItemAdapter` | o adaptador não reconhece o item na mão |
| `PlayerContext.AddControlLock/RemoveControlLock` (1º parâmetro `string` ou `object`; o diálogo aceita também `ReleaseControlLock`/`ClearControlLock`) | `DialogueBridge`, `PropsBridge`, `ReviveDeath` | diálogo, esconderijo e jogador caído não travam o controle (`LogWarning`) |
| `ReviveApi.IsDowned(Component)`, `ReviveApi.AllowDeath(Component, float)` | `Stalker/StalkerBridge` | o monstro não distingue jogador caído |
| `PlayerAnimDriver.Snap` | `Props/PropsBridge` | esconderijo não alinha o corpo |
| `CameraImpulse.Shake(pos, raio, trauma)`, `NoiseSystem` | `Revive/ReviveBridge` | feedback de câmera/barulho do Revive some |
| Vida e status: `Health`, `CurrentHealth`, `Life`, `Vida`, `HP`, `MaxHealth`, `Current`, `Value`, `Amount`, `IsAlive`/`IsDead`… | `Revive/ReviveHealth`, `ReviveReflect`, `Stalker/StalkerBridge`, `StalkerFinisher`, `Props/PropsBridge` | leitura/dano de vida por reflexão (esses arquivos também usam `StatsComponent`/`IDamageable`/`StatType` direto: 19, 6, 8 e 3 ocorrências em `ReviveHealth`, `StalkerBridge`, `PropsBridge`, `DialogueBridge`) |
| `PlayerIndex`/`playerIndex`/`Slot`, `PlayerInteractor`, `holdDuration`, `RemoveEffect*` | `Revive/ReviveBridge` | índice do jogador, tempo de segurar, remoção de efeito |
| Campo `FirstPersonController.bobAmplitude` (varre campos com "bob" + "amplitude/amount/…") | `Body/DynamicHeadBob` | o head bob antigo não é desligado: os dois balanços se somam (aviso no Console) |
| Campos de `DamageInfo` (`amount`/`damage`/`value`; `source`/`instigator`/`attacker`/`origin`) | `Body/DamageReaction`, `Animation/PlayerAnimBridge` | direção e força da reação ao dano |
| Listas de nomes candidatos (`IsPlaying`/`Running`, `Id`/`LevelId`/`SceneName`, `PlayerIndex`…) | `Stalker/StalkerFinisher`, `StalkerBridge` | detalhes do finalizador e do nível atual |
| Campo privado `NoiseListener.onHeard`; `FindProperty("campo")`; `FindType("HorrorFPS.CutsceneSequence"/"EventStep"/"WaitStep")` | `Editor/DemoProps`, `Editor/Stalker/StalkerMenu`, `WizardUtil` e afins | só os menus de demonstração do Editor |
| Campos privados do SmartEvent por nome: `SmartEvent._limitMode`, `_variablesOnTrigger`; `SmartEventTrigger._activateOn`; nas condições `_key`, `_compare`, `_value`, `_expected`, `_eventId`, `_times`, `_chance`; e (via `Props`/`SerializedObject`) `SmartEventInteractable.verb`, `objectName`, `_blockedText`, `_disableWhenBlocked` | `Editor/HorrorFPSSetup.Showcase.Zones` (`SetPrivate`, `AddVariableChange`, `AddSmartButton`) | a zona Smart Events da cena completa nasce sem a condição, o limite ou a variável: aviso `Campo não encontrado` / `Não achei a lista de variáveis` no Console e o resto da cena continua |

Em **código novo** não acrescente reflexão: chame a API real (seção 5).

### 7.7 Cooperativo (1 a 4 jogadores)

- **`PlayerContext.All` muda durante o jogo** (quem entra/sai): ouça `PlayerSpawned`/`PlayerRemoved` e não guarde `PlayerContext` em estático sem limpar. [V]
- **Tudo "do jogador" é uma instância por jogador** (câmera, HUD, input, inventário, sanidade): objeto de cena trata "qual jogador" pelo `PlayerContext` recebido em `Interact`/gatilho ou por `PlayerRef.ContextOf`. [V]
- **Morte:** `IsDead` é por jogador; `GameOverScreen` espera 2 s e só aparece se **todos** estiverem mortos (reconfere depois do atraso); com um jogador morto não dá para salvar (`CanSave`). [V]
- **Troca de personagem:** `CharacterManager.Switch/SwitchAll`; o inventário vai por `ExportTransfer/ImportTransfer`; travas de controle do jogador antigo **não** passam para o novo (doc de itens, seção 11) [D].
- **Pausa e cursor:** a pausa é global; telas que precisam do cursor usam `PauseManager.RequestFreeCursor/ReleaseFreeCursor`. [V]

---

## 8. Pendências e divergências conhecidas

1. **`ReviveApi.TryIntercept` não está ligado [V].** Nada chama o gancho (só o comentário em `Revive/ReviveApi.cs:103` e uma mensagem em `ReviveDownable.cs:271`). Segundo o comentário do próprio `TryIntercept`, sem o gancho o módulo **desfaz a morte logo depois que ela acontece**: a morte já disparou `Died` (e `PlayerContext.HandleDied` já marcou `IsDead`), e efeitos que ela tenha disparado, como uma tela de "você morreu", não são desfeitos. O `GameOverScreen` atenua esperando 2 s e reconferindo `IsDead`. O gancho exato seria uma linha em `Stats/StatsComponent.cs`, em `void Die(DamageInfo info)` logo depois de `if (!IsAlive) return;`: `if (ReviveApi.TryIntercept(this)) return;`. Isso mexe no **framework base**: faça só se o usuário pedir e teste jogador morto, caído, revivido e game over. A mensagem em `ReviveDownable.cs:271`, que pedia ao usuário que "envie PlayerContext.cs, StatsComponent.cs e GameOverScreen.cs" (texto da época sem o código-fonte), agora aponta para o comentário de `ReviveApi.TryIntercept` (pacote A); o gancho continua **não** ligado.
2. **`PlayerBodyPresenter.AttachHeld(prefab, leftHand, pos, euler)` não tem chamadas [V].** O item na mão visível aos outros jogadores (corpo/terceira pessoa) não está ligado; a visão em primeira pessoa vem do `EquipmentController` (`ItemEquip.heldPrefab`). Não verifiquei como ligar (o `PlayerAnimItemAdapter` já escuta `Equipped/Unequipped`).
3. **Nenhum asset de dados está no repositório [V].** Não há `ItemData`, `DialogueGraph`, `StalkerDefinition/Profile/TreeAsset`, `SanityProfile`, `ReviveProfile`, `CharacterDefinition/Catalog`, `InputGlyphSet` nem `StatusEffectAsset`. Existe `Generated/ItemIcons/chave_ferro.png`, que é o destino do Item Studio (`Editor/ItemStudioWindow.cs`): provavelmente há `ItemData` só na máquina do usuário ou ainda sem commit [?]. A cena `Item Tests` referencia **53 assets e 2 prefabs/modelos sem `.meta` no repositório** (materiais, `ItemData` em campos `data`, `DialogueGraph` em campos `graph`, `TerrainData`, `AnimatorController`…): num clone limpo ficam "Missing". `Level_Hall`, `Level_Cellar`, `Level_Annex`, `MainMenu`, `Core`, `HorrorDemo` e `Player.prefab` só referenciam assets do próprio repositório (e scripts de pacote do Input System). [V]
4. **Cópias de docs divergem [V].** `README.md` é a versão completa do doc de arquitetura (demo de 5 cenas, save, cutscenes); `Explicações de como usar/HorrorFPS-Arquitetura.md` é uma versão mais curta (258 linhas diferem). `Docs/Itens-Sanidade-Corpo.md` e `Explicações de como usar/HorrorFPS-Itens-Sanidade-Corpo.md` também são versões diferentes (280 linhas). `Perseguidor`, `Props` e `Reanimacao` são idênticos nas duas pastas. Os 9 docs do **Projeto** não foram comparados com as cópias do repositório [?].
5. **Docs e pontes foram escritos sem o código-fonte do framework base [D].** A conferência feita aqui foi estática (seção 11): não há Unity no ambiente, então nada foi compilado nem executado. Se o usuário relatar erro de compilação ou comportamento estranho, peça a mensagem do Console. Se um doc e o código divergirem, o **código vence**.
6. **Cenas [V]:** `Level_Annex` **não é nível do catálogo**: é uma cena aditiva carregada por proximidade pelo `SceneStreamer` do Salão (`sceneName: Level_Annex`). `HorrorDemo` e `Item Tests` são cenas de teste fora do catálogo. Se estão no Build Settings é **[?]** (`ProjectSettings` não está no repositório): R9 passo 1.
7. **Unity e pipeline [?]:** versão exata do Unity e pipeline de render ativo não confirmados (ver 7.5).
8. **Menus de demonstração repetidos e o pacote B (opcional) [V].** Com o menu 27, os menus **2** (Demo Scene), **6** (Demo Props), **13** (Dialogue Demo Props), **17** (Props Demo) e **18** (Stalker Demo) passaram a repetir o que a cena completa já monta pelos mesmos construtores. O pacote B os remove (5 arquivos de `Editor/` alterados, sem mexer em nenhum `.meta`) e mantém os construtores públicos (R12); a cena `HorrorDemo` já existente no repositório não é tocada. O **26 fica**: é o exemplo documentado em `HorrorFPS-ArvoreComportamento.md`. Os docs do Projeto **ainda citam** esses menus (`HorrorFPS-Props` → 17, `HorrorFPS-Dialogos` → 13, `HorrorFPS-Perseguidor` → 18, `HorrorFPS-Itens-Sanidade-Corpo` → 6, `HorrorFPS-Arquitetura` → 2): se o B for aplicado, atualize esses trechos [D].
9. **Duas raízes fixas no código antigo [V] e a pasta "fantasma" `Assets/HorrorFPS` [?].** No commit `9ccdc01`, `HorrorFPSSetup` (menus 1, 2 e 3) gravava em `Assets/Huggable Horrors/...` (constante `Root`), enquanto `WizardUtil` (itens, modelos e materiais), `CharacterCatalogUtil`, `AnimationEditorUtil`, `ItemStudioWindow`, `DialogueMenus` e `PropsDemoBuilder` gravavam em `Assets/HorrorFPS/...` (constantes). Se o framework está em `Assets/Huggable Horrors`, é provável que exista também uma pasta `Assets/HorrorFPS` com itens, diálogos, ícones e materiais gerados (não dá para confirmar daqui: o `Assets` do projeto não está no repositório). `ProjectPaths` (R10) passa a descobrir a pasta onde o framework realmente está e todos os assistentes usam essa raiz; quando a pasta de um tipo de asset só existe na raiz antiga, ela continua sendo usada (não duplica). Para consolidar o que já foi gerado, **mova** a pasta antiga pelo Project do Unity (os GUIDs e as referências são mantidos). O comentário de `Items/ItemData.cs:42` ainda cita o caminho antigo como exemplo (só texto).
10. **A cena `Level_Showcase` não está no repositório [V].** Ela nasce do menu 27 (arquivo `<raiz>/Scenes/Level_Showcase.unity`, mais `Materials/ShowcaseSky.mat`, materiais `Showcase*` e o NavMesh ao lado da cena). Nada foi aberto, compilado ou jogado no Unity: o layout foi conferido só por conta (sem sobreposição; folgas ≥ 12 m entre zonas) e as referências, por checagem estática (seção 11). Se algo falhar, peça a mensagem do Console e o nome da zona (`[Zona] ...` na Hierarquia).

---

## 9. Cenas, prefabs e assets do repositório

Só existem estes assets (além dos scripts): `Prefabs/Player.prefab`, `Resources/HorrorFPS/GameCatalog.asset`, 7 cenas, `Input/HorrorControls.inputactions`, 11 materiais em `Materials/` (shader do tipo URP/Lit, ver 7.5: `Battery`, `CellarFloor`, `CellarWall`, `Floor`, `Gold`, `Heavy`, `Metal`, `Paper`, `Shadow`, `Wall`, `Wood`) e `Generated/ItemIcons/chave_ferro.png`. [V]

**Fluxo de níveis do catálogo (`GameCatalog.asset`):** `mainMenuScene = MainMenu`, `coreScene = Core`, `firstLevel = hall`; níveis `hall` → `Level_Hall` ("Salão") e `cellar` → `Level_Cellar` ("Porão"), ambos com `defaultSpawn = start`. No jogo: Salão ──`LevelDoor` (`levelId: cellar`, `spawnId: start`, exige a variável `has_cellar_key`)──▶ Porão ──`LevelDoor` (`levelId: hall`, `spawnId: from_cellar`)──▶ Salão. O Salão tem `SpawnPoint`s `start` e `from_cellar`; o Porão tem `start`. [V]

**Variáveis e ids de exemplo usados nas cenas:** `intro_seen` (`SetVariableStep` da cutscene `intro`, `playOnce`, toca ao começar o nível), `has_cellar_key` (`Pickup.setVariable` → `Door.unlockVariable` e `LevelDoor.requiredVariable`), `cellar_scare_seen` (cutscene `cellar_scare`). Formato: snake_case; os docs também usam exemplos em português (`tem_chave`). [V]

| Asset | O que tem (scripts do projeto, via GUID) |
|---|---|
| `Prefabs/Player.prefab` (29 scripts) | `PlayerContext`, `PlayerInputHandler` (+ `PlayerInput` do Input System), `FirstPersonController`, `PlayerInteractor`, `PlayerDragger`, `PlayerClimber`, `StatsComponent`, `PlayerHUD`, `BodyVisibility`, `PlayerAnimDriver`, `PlayerAnimItemAdapter`, `ItemInput`, `PlayerOverlay`, `InventoryOwner` + `GridContainer` + `ListContainer`, `Hotbar`, `HotbarHUD`, `InventoryScreen`, `PlayerMotionProbe`, `BreathingComponent`, `HeartbeatComponent`, `DynamicHeadBob`, `DamageReaction`, `SanityComponent`, `SanityFX`, `SanityEffects`, `SanityAudio`, `SanitySubtitles`. **Não estão no prefab:** `EquipmentController`/`EquipmentHUD` (instalados em jogo por `PlayerModules.Install`), `CharacterIdentity`, `PlayerBodyRig`/`PlaceholderPuppet`, `SaveableStats` (opcionais; menus 9, 14 e 16). |
| `Scenes/Core` | `LocalPlayerManager` (+ `PlayerInputManager`) |
| `Scenes/MainMenu` | `MainMenu` |
| `Scenes/Level_Hall` (34) | `CutsceneSequence` (`intro`), `CutsceneTrigger`, passos (`CameraMoveStep`, `FlickerLightsStep`, `SetVariableStep`, `SubtitleStep`×2), `Door`×2, `Draggable`×3, `Pickup`, `SavePoint`, `Checkpoint`, `LevelDoor`, `SceneStreamer` (→ `Level_Annex`), `SpawnPoint`×8, `SaveableEntity`×6, `SaveableTransform`×3 |
| `Scenes/Level_Cellar` (22) | `CutsceneSequence` (`cellar_scare`), `CutsceneTrigger`, passos (`ActivateStep`×2, `FlickerLightsStep`×2, `PlayerLookStep`, `SetVariableStep`, `SubtitleStep`), `Draggable`×2, `Pickup`, `LevelDoor`, `SpawnPoint`×4, `SaveableEntity`×3, `SaveableTransform`×2 |
| `Scenes/Level_Annex` (8) | `Draggable`×2, `Pickup`, `SaveableEntity`×3, `SaveableTransform`×2 (cena em streaming; sem `SpawnPoint`) |
| `Scenes/HorrorDemo` (9) | `Door`×2, `Draggable`×4, `Ladder`×2, `LocalPlayerManager` (sala de testes de movimento/escadas/arrastar) |
| `Scenes/Item Tests` (85) | bancada de testes: `WorldItem`×7, `ContainerInteractable`, `InventoryOwner` + `GridContainer`, `Drawer`×3, `OpenablePart`×7, `Breakable`×3, `ExplosiveBarrel`×4, `BearTrap`, `HidingSpot`×2, `CoopButton`×2, `CoopLock`×2, `LightSwitch`, `SwitchableLights`, `MirrorSurface`, `NoiseListener`, `SanityZone`/`SanityLight`/`SanityVisibility`×3/`SanityExaminable`, `DialogueActor`/`DialogueInteractable`/`DialogueTrigger`/`DialogueEventRelay`, `CameraImpulseSource`/`CameraImpulseInteractable`, `PlayerBodyRig`/`Pose*`/`BodyPart`×2, `SaveableEntity`×21, `SaveableTransform`×7, `SpawnPoint` |

Camadas e tags: o código **não exige** nenhuma. Opcionais: camada `Interactable` (consultada por `ReviveBridge`), tag `MainCamera` (checada em `ReviveBridge`/`StalkerBridge`), tag `Player` como padrão de `InstigatorTagCondition`; camada 31 reservada à inspeção 3D de itens (7.4). [V]

### Cena completa `Level_Showcase` (menu 27, pacote A) [V no código do menu; a cena nunca foi aberta no Unity]

Não está no repositório: nasce do menu 27 em `<raiz>/Scenes/Level_Showcase.unity` (mais `Materials/ShowcaseSky.mat`, os materiais `Showcase*` e o NavMesh do perseguidor ao lado da cena). Um mundo único, de noite (névoa, luar fraco sem sombras, céu escuro), com terreno de 600 × 600 m (topo em y = −0,3) e limites invisíveis em ±130 m. A **praça central** (32 × 32 m, em `(0,0,0)`) tem 4 postes, as placas de apresentação, de controles e de jogo/save, e o spawn `start` em `(0,0,−7)` (até 4 jogadores, olhando para o norte). Dela saem sete estradas de 4 m, cada uma da cor da zona, terminando num portal com farol e placa de entrada. Cada zona fica agrupada em `[Zona] <nome>` na Hierarquia. Registro: nível `showcase` ("Cena completa") no `GameCatalog`, só se o catálogo existe e ainda não tem o nível; Build Settings: `Core` e `Level_Showcase`.

| Zona (cor) | Origem ou centro (m) | O que monta | Variáveis, ids e eventos |
|---|---|---|---|
| Movimento (amarelo) | `(-62,0,0)`, pad 30 × 30 | escada fixa e rede de escalada livre (ambas voltadas para +X); 4 arrastáveis (Caixa, Barril, Armário com `dragMode` Slide, Cofre); mesa com a chave; porta livre e porta trancada; tesouro | `tem_chave` (a chave liga; destranca a porta trancada **e** a 3ª gaveta da zona Props); contador `showcase.tesouros` |
| Itens, sanidade e corpo (azul) | `(-55,0,40)` | `SampleItems.EnsureAll` + `DemoProps.Build` | itens de exemplo por id |
| Props (laranja) | `(-50,0,-55)` | `PropsDemoBuilder.BuildAt(origem, true)` (faixa no eixo X; andar para o leste) | 3ª gaveta pede `tem_chave` |
| Diálogo (verde) | `(0,0,45)` | `DialogueMenus.EnsureSamples` + `BuildDemoProps` | item `chave_portao`; evento `abrir_passagem` |
| Smart Events (ciano) | `(62,0,0)`, pad 28 × 24 | 3 fusíveis, caixa de fusíveis, portão elétrico, terminal com tela, faixa-alarme | contador `showcase.fusiveis`; bool `showcase.energia`; `SmartEvent`s `showcase.caixa_fusiveis` (≥ 3 fusíveis → liga a energia), `showcase.portao` (exige o evento da caixa), `showcase.terminal` (exige a energia), `showcase.alarme` (volume, uma vez, jogador, 50%); save em `smartevent.<id>` |
| Perseguidor (vermelho) | `(62,0,62)`, arena 40 × 40 | `StalkerMenu.BuildDemo(origem, true)` (entrada pela abertura da parede sul), 4 luzes, legenda do rádio | NavMesh só da arena; se não gerar, o diálogo final manda dar Bake em "[Stalker Demo]" |
| Fluxo (roxo) | `(62,0,-45)`, pad 28 × 24 | gravador (`SavePoint`), faixa de checkpoint, cutscene de susto, cabine da cutscene de câmera, portas para `hall` e `cellar` | spawn id `fluxo` (grupo de 4); cutscenes `showcase_susto` (`playOnce` desligado; define `showcase.susto_visto`) e `showcase_camera` (`CutsceneTrigger` manual + `UnityEventInteractable`); as `LevelDoor`s só saem se `hall`/`cellar` existem no catálogo (rode o menu 3 antes) |

Ela **não** altera nada que o menu 3 criou. Variáveis `tem_chave`, `showcase.*`: o mesmo padrão de chave do `GameSession` (R11); os ids dos `SmartEvent`s são legíveis de propósito (`smartevent.showcase.…`).

---

## 10. Docs do Projeto e onde estão as cópias

Para uso, menus e campos do Inspector de cada sistema, abra o doc dele (no Projeto: `claude/<nome>.md`); para assinaturas e comportamento exato, o código (seção 5 e Apêndice A). Os docs são narrativos e foram escritos sem o código-fonte do framework base [D].

| Doc do Projeto | Assunto | Cópia no repositório |
|---|---|---|
| `HorrorFPS-Arquitetura.md` | base: input, movimento, interação, status, split-screen, fluxo de cenas, save, cutscenes; receita "adicionar uma action" | `README.md` (versão completa) e `Explicações de como usar/` (mais curta) |
| `HorrorFPS-Itens-Sanidade-Corpo.md` | itens, inventário (lista/grade), sanidade por jogador, corpo (batimento, respiração, head bob, ruído), personagens, Item Studio, abas, lanterna/armas, `NoiseSystem`/`SoundEmitter` | `Explicações de como usar/` e `Docs/Itens-Sanidade-Corpo.md` (versões diferentes) |
| `HorrorFPS-Dialogos.md` | diálogos: grafo, editor, ações e condições | `Explicações de como usar/` |
| `HorrorFPS-Props.md` | props interativos (barril, gaveta, esconderijo, espelho, coop, armadilha) e `PropsBridge` | `Docs/` e `Explicações de como usar/` (idênticas) |
| `HorrorFPS-Perseguidor.md` | IA do perseguidor (Stalker): cérebro, sentidos, diretor, memória | `Docs/` e `Explicações de como usar/` (idênticas) |
| `HorrorFPS-ArvoreComportamento.md` | árvore de comportamento editável do Stalker, nós e módulos | `Docs/` |
| `HorrorFPS-Reanimacao.md` | estado "caído" cooperativo (Revive) | `Docs/` e `Explicações de como usar/` (idênticas) |
| `HorrorFPS-SmartEvents.md` | SmartEvents (eventos com condições) | `Explicações de como usar/` |
| `HorrorFPS-Animacao-Corpo.md` | corpo e animação visíveis aos outros jogadores | `Explicações de como usar/` |

As pastas `Docs/` e `Explicações de como usar/` e o `README.md` têm `.meta`, então viram assets do Unity (aparecem na janela Project); isso é inofensivo. [V]

---

## 11. Como isto foi conferido (e o que não foi)

**Feito**
- Leitura integral dos arquivos do núcleo: `PlayerContext`, `StatTypes`, `StatsComponent`, `Interactable`, `GameSession`, `SaveableEntity`, `FrameworkBridge`, `HorrorUtil`, `PlayerModules`/`PlayerModulesRunner`, `Cutscene*`, `InputNames`, `CameraFx`, `CameraHook`, `LocalPlayerManager`, `SpawnPoint`, `SceneStreamer`, `SaveableTransform`, `GameRuntime`, `Door`, `UnityEventInteractable`, `DamageReaction`, `FadeStep`, `SubtitleStep`, `SmartCondition`, `StalkerTraitSprint`, `StalkerNodeExamples`, `PropsInput`, `DialogueInput`, `README.md`; leitura parcial de `PlayerInputHandler`, `ItemInput`, `GameOverScreen`, `SceneDirector`, `SaveSystem`, `DynamicHeadBob`, `PropsBridge`, `ReviveApi`.
- Assinaturas e usos conferidos por `grep` em cerca de 70 arquivos.
- **Checagem estática de referências** `Tipo.Membro` entre todos os `.cs` (3.610 referências, 0 suspeitas), validada com um teste sintético. Não é compilador: não vê tipos de genéricos, sobrecargas nem erros de sintaxe.
- Mapa de componentes de cenas e prefabs por GUID (seção 9) e lista de referências sem `.meta` (seção 8, item 3).
- Apêndice A gerado por script a partir dos `.cs` (486 tipos de nível superior em 301 arquivos no commit `9ccdc01`; 489 em 304 com o pacote A: o bloco `Editor/` foi regenerado).
- **Modelos de código** (os 13 blocos `csharp` das seções 3 e 6): extraídos e conferidos por (a) equilíbrio de chaves, parênteses e colchetes (0 erros); (b) a mesma checagem estática de `Tipo.Membro` (24 referências nos modelos, 0 suspeitas; um controle negativo com 2 referências inexistentes foi apontado corretamente); (c) leitura das assinaturas e do acesso (`public`/`protected`, `abstract`/`virtual`) das bases que eles sobrescrevem: `Interactable.Interact`, `CutsceneStep.Run/OnSkip`, `StalkerTask`/`StalkerCondition`, `SanityAware`, `PlayerModuleAttribute`, `FrameworkBridge.BeforeSave/AfterLoad`, `CameraDelta`; (d) busca de colisão de nomes (nenhum nome de classe de exemplo existe no projeto). **Nenhum modelo foi compilado.**
- **Pacote A (ajustes e menu 27) [V]:** nos 16 arquivos tocados (e em `HorrorFPSSetup.Levels.cs`, da mesma classe), chaves, parênteses e colchetes equilibrados, só LF e sem tabs; a mesma checagem estática de `Tipo.Membro` refeita no fim (3.634 referências, 0 suspeitas); busca de nomes repetidos e de chamadas sem qualificação nos 5 arquivos da classe `partial HorrorFPSSetup` (um nome repetido ou um erro de compilação em **qualquer** arquivo de `Editor/` derruba todos os menus); nomes de campos privados de `SmartEvent` e de `Interactable` conferidos no código; textos de controles da praça conferidos contra `Input/HorrorControls.inputactions` e `Input/ItemInput.cs`; layout das zonas conferido por conta (sem sobreposição, folgas ≥ 12 m) e rótulos conferidos contra a geometria real das bancadas reaproveitadas. O pacote B foi gerado por um script que **para** se qualquer texto a remover não for o esperado, e depois a mesma checagem foi refeita nele (3.630 referências, 0 suspeitas, nenhuma referência pendente aos menus removidos).

**Não feito**
- Compilar ou rodar no Unity (nem Play Mode). Isso vale também para o pacote A: o menu 27, a cena que ele gera, o NavMesh e todos os ajustes **nunca foram executados**. Um erro de compilação em qualquer arquivo de `Editor/` faz sumir os menus `Tools > Horror FPS`: se acontecer, a mensagem do Console aponta o arquivo e a linha.
- Ler linha a linha os docs do Projeto (marcados **[D]**).
- Ler o corpo da maioria dos arquivos de `Stalker/`, `Revive/`, `Dialogue/`, `Props/`, `UI/` e `Editor/` (só assinaturas, trechos e buscas).
- `ProjectSettings` (versão do Unity, Build Settings, camadas/tags, pipeline): não está no repositório.
- Assets que não estão no repositório (seção 8, item 3).

**Reconferir rápido** (na raiz do repositório):

```
find . -name '*.cs' | wc -l                          # 301 no commit 9ccdc01; 304 com o pacote A
grep -rl "System.Reflection" --include=*.cs .        # 15 arquivos no 9ccdc01; 16 com o pacote A (7.6)
grep -rhn 'MenuItem("Tools/Horror FPS/' --include=*.cs .   # menus ativos (próximo número livre: seção 2)
grep -rl "ResetStatics" --include=*.cs . | wc -l     # 58
grep -rn "NomeDoMembro" --include=*.cs .             # quem usa um membro, antes de renomear
grep -rn "TryIntercept\|AttachHeld" --include=*.cs . # ganchos ainda não ligados (seção 8)
```

---

## Apêndice A. Índice de classes por pasta

Cada arquivo tem o nome da classe principal. A lista "Tipos em arquivo de outro nome" mostra onde moram os tipos de nível superior que **não** seguem a regra (enums, structs, interfaces e classes auxiliares): é o jeito mais rápido de achar, por exemplo, `PlayerRef` (`Core/HorrorUtil.cs`) ou `NoiseKind` (`Body/NoiseSystem.cs`). Antes de criar uma classe nova, confira que o nome não existe aqui.

**Animation/**  (15 arquivos)

Arquivos: BodyPart, BodyViewHook, BodyVisibility, PlaceholderPuppet, PlayerAnimBridge, PlayerAnimDriver, PlayerAnimItemAdapter, PlayerAnimParams, PlayerAnimState, PlayerBodyPresenter, PlayerBodyRig, PoseBreathing, PoseFx, PoseImpulse, PoseWallAvoid

Tipos em arquivo de outro nome:
- `BodyPart.cs` define enum `BodyRegion`, enum `PartView`
- `PlayerAnimState.cs` define enum `HandItem`, enum `PlayerAnimEvent`
- `PoseFx.cs` define struct `PoseDelta`, interface `IPoseModifier`

**Body/**  (13 arquivos)

Arquivos: BreathingComponent, CameraImpulse, CameraImpulseInteractable, CameraImpulseSource, DamageReaction, DynamicHeadBob, HeartbeatComponent, NoiseListener, NoiseSystem, PlayerAudio, PlayerMotionProbe, ProceduralAudio, SoundEmitter

Tipos em arquivo de outro nome:
- `NoiseSystem.cs` define enum `NoiseKind`, struct `NoiseEvent`, interface `INoiseHearer`
- `PlayerAudio.cs` define enum `AudioMode`

**Characters/**  (6 arquivos)

Arquivos: CharacterCatalog, CharacterDefinition, CharacterIdentity, CharacterManager, CharacterRunner, CharacterSwitcher

Tipos em arquivo de outro nome:
- `CharacterDefinition.cs` define enum `InventoryTransfer`

**Core/**  (12 arquivos)

Arquivos: CameraFx, CameraHook, FrameworkBridge, GameRuntime, GameSettings, HorrorUtil, LocalPlayerManager, PauseManager, PlayerModuleAttribute, PlayerModules, PlayerModulesRunner, UIText

Tipos em arquivo de outro nome:
- `CameraFx.cs` define struct `CameraDelta`, interface `ICameraModifier`
- `GameSettings.cs` define enum `PromptMode`, enum `HoldMode`
- `HorrorUtil.cs` define class `PlayerRef`

**Cutscenes/**  (23 arquivos)

Arquivos: ActivateStep, AnimatorStep, AudioStep, CameraMoveStep, CameraShakeStep, CutsceneContext, CutsceneDirector, CutsceneOverlay, CutsceneSequence, CutsceneStep, CutsceneStepRunner, CutsceneTrigger, EventStep, FadeStep, FlickerLightsStep, GoToLevelStep, MoveObjectStep, PlacePlayersStep, PlayerLookStep, SetVariableStep, SubtitleStep, TimelineStep, WaitStep

**Dialogue/**  (17 arquivos)

Arquivos: DialogueAction, DialogueActor, DialogueBridge, DialogueCameraLook, DialogueCondition, DialogueContext, DialogueData, DialogueEventRelay, DialogueEvents, DialogueGraph, DialogueInput, DialogueInteractable, DialogueManager, DialogueStep, DialogueText, DialogueTrigger, DialogueView

Tipos em arquivo de outro nome:
- `DialogueAction.cs` define enum `DialogueActionKind`, class `DialogueActions`
- `DialogueCondition.cs` define enum `DialogueCompare`, enum `DialogueConditionKind`, class `DialogueKeys`, class `DialogueConditions`
- `DialogueContext.cs` define class `DialogueOptions`, class `DialogueSession`
- `DialogueData.cs` define enum `DialogueNodeKind`, enum `DialoguePresentation`, enum `DialogueAudience`, enum `DialogueLockedChoice`, enum `DialogueAnimKind`, class `DialogueNodeIdAttribute`, class `DialogueIds`, class `DialogueAnimation`, class `DialogueTextVariant`, class `DialogueChoice`, class `DialogueRoute`, class `DialogueRandomOption`, class `DialogueNode`
- `DialogueText.cs` define class `DialogueStyle`
- `DialogueView.cs` define struct `DialogueChoiceItem`

**Editor/**  (22 arquivos)

Arquivos: CharacterCatalogEditor, CharacterCatalogUtil, CharacterDefinitionEditor, DemoProps, EquipPresets, HorrorFPSSetup, HorrorFPSSetup.FullDemo, HorrorFPSSetup.Levels, HorrorFPSSetup.Showcase, HorrorFPSSetup.Showcase.Zones, ItemContainerEditor, ItemEquipDrawer, ItemShapeDrawer, ItemStudioWindow, PlayerContextEditor, PlayerInventoryWindow, PlayerPrefabInstaller, ProjectPaths, SampleItemModels, SampleItems, StartingItemsView, WizardUtil

Tipos em arquivo de outro nome:
- `EquipPresets.cs` define enum `EquipPreset`

**Editor/Animation/**  (4 arquivos)

Arquivos: AnimationEditorUtil, ModelBodySetup, PlaceholderBodyBuilder, PlayerAnimatorControllerBuilder

**Editor/Dialogue/**  (4 arquivos)

Arquivos: DialogueDrawers, DialogueGraphInspector, DialogueGraphWindow, DialogueMenus

Tipos em arquivo de outro nome:
- `DialogueDrawers.cs` define class `DialogueEditorUtil`, class `DialogueNodeIdDrawer`, class `DialogueAnimationDrawer`, class `DialogueConditionDrawer`, class `DialogueActionDrawer`

**Editor/Props/**  (3 arquivos)

Arquivos: PropsDemoBuilder, PropsDemoBuilder.Coop, PropsDemoBuilder.Props

**Editor/Revive/**  (1 arquivo)

Arquivos: ReviveMenu

**Editor/Stalker/**  (4 arquivos)

Arquivos: StalkerMenu, StalkerTreeAssetEditor, StalkerTreeMenu, StalkerTreeWindow

**Equipment/**  (4 arquivos)

Arquivos: EquipmentController, EquipmentHUD, HitTarget, ItemHit

Tipos em arquivo de outro nome:
- `ItemHit.cs` define interface `IItemHittable`

**Flow/**  (7 arquivos)

Arquivos: GameCatalog, LevelDoor, LevelTrigger, LoadingScreen, SceneDirector, SceneStreamer, SpawnPoint

**Input/**  (6 arquivos)

Arquivos: InputBindingStore, InputGlyphSet, InputGlyphs, InputNames, ItemInput, PlayerInputHandler

**Inventory/**  (10 arquivos)

Arquivos: ContainerInteractable, DropRegistry, GridContainer, Hotbar, InventoryOwner, ItemContainer, ItemEvents, ItemTransfer, ListContainer, WorldItem

Tipos em arquivo de outro nome:
- `InventoryOwner.cs` define class `OwnerSave`, class `TransferSave`
- `ItemContainer.cs` define enum `ContainerKind`, class `StartingItem`, class `ContainerSave`
- `ItemTransfer.cs` define enum `TransferResult`

**Items/**  (10 arquivos)

Arquivos: InspectHotspot, InventoryTab, ItemCondition, ItemData, ItemDatabase, ItemEffect, ItemEquip, ItemIconShot, ItemInstance, ItemShape

Tipos em arquivo de outro nome:
- `InventoryTab.cs` define enum `ItemTabRule`, class `InventoryTabs`, class `ItemMapInfo`
- `ItemCondition.cs` define enum `ItemConditionKind`, struct `ItemViewContext`, class `ConditionalText`, class `ItemTextSet`
- `ItemData.cs` define enum `ItemCategory`, class `ItemAction`
- `ItemEffect.cs` define enum `ItemEffectKind`, class `ItemUseContext`, interface `IItemUseHandler`
- `ItemEquip.cs` define enum `EquipKind`, class `EquipState`
- `ItemInstance.cs` define class `ItemSave`

**Player/**  (5 arquivos)

Arquivos: FirstPersonController, PlayerClimber, PlayerContext, PlayerDragger, PlayerInteractor

Tipos em arquivo de outro nome:
- `FirstPersonController.cs` define enum `MovementState`

**Props/**  (19 arquivos)

Arquivos: BearTrap, Breakable, CoopButton, CoopLock, Drawer, ExplosiveBarrel, HidingOccupant, HidingSpot, LightSwitch, MirrorPhantom, MirrorSurface, OpenablePart, PropBlast, PropFx, PropHealth, PropSfx, PropsBridge, PropsInput, SwitchableLights

Tipos em arquivo de outro nome:
- `OpenablePart.cs` define enum `OpenMode`
- `PropBlast.cs` define interface `IBlastReceiver`, struct `BlastSettings`

**Revive/**  (12 arquivos)

Arquivos: ReviveApi, ReviveBridge, ReviveDeath, ReviveDebugKeys, ReviveDownable, ReviveHealth, ReviveHud, ReviveInteractable, ReviveManager, ReviveProfile, ReviveReflect, ReviveTypes

Tipos em arquivo de outro nome:
- `ReviveTypes.cs` define enum `ReviveKind`, enum `DownState`, class `ReviveEvents`

**Sanity/**  (18 arquivos)

Arquivos: PerViewerEffect, SanityAudio, SanityAware, SanityComponent, SanityDialogueStep, SanityEffects, SanityEvents, SanityExaminable, SanityFX, SanityLight, SanityLine, SanityProfile, SanityStage, SanityString, SanitySubtitles, SanityText, SanityVisibility, SanityZone

Tipos em arquivo de outro nome:
- `SanityComponent.cs` define class `SanitySave`
- `SanityEvents.cs` define struct `SanityChange`, interface `ISanityListener`
- `SanityString.cs` define class `SanityVariant`

**Save/**  (8 arquivos)

Arquivos: GameSession, SaveData, SaveSystem, SaveableActive, SaveableEntity, SaveableStats, SaveableTransform, StatsSnapshot

Tipos em arquivo de outro nome:
- `SaveData.cs` define class `SaveHeader`, class `VarEntry`, class `ComponentRecord`, class `EntityRecord`, class `SceneRecord`, class `StatRecord`, class `EffectRecord`, class `PlayerRecord`, class `GameSave`, class `SaveHeaderOnly`
- `SaveSystem.cs` define class `SaveSlotInfo`
- `SaveableEntity.cs` define interface `ISaveable`

**SmartEvents/**  (8 arquivos)

Arquivos: BuiltInConditions, HorrorFPSConditions, SmartCondition, SmartEvent, SmartEventInteractable, SmartEventRunner, SmartEventSave, SmartEventTrigger

Tipos em arquivo de outro nome:
- `BuiltInConditions.cs` define class `InstigatorTagCondition`, class `InstigatorLayerCondition`, class `GameBoolCondition`, class `GameIntCondition`, class `EventTriggeredCondition`, class `GameObjectActiveCondition`, class `RandomChanceCondition`
- `HorrorFPSConditions.cs` define class `InstigatorIsPlayerCondition`
- `SmartCondition.cs` define class `ConditionMenuAttribute`, enum `CompareOp`, class `CompareOpExtensions`
- `SmartEvent.cs` define enum `TriggerLimitMode`, enum `ConditionMode`, enum `TriggerResult`, struct `SmartEventContext`, enum `VariableOp`, struct `VariableChange`

**SmartEvents/Editor/**  (1 arquivo)

Arquivos: SmartEventEditor

Tipos em arquivo de outro nome:
- `SmartEventEditor.cs` define class `SmartEventDrawer`

**Stalker/**  (25 arquivos)

Arquivos: PatrolPoint, StalkerActions, StalkerBT, StalkerBrain, StalkerBridge, StalkerDefinition, StalkerDirector, StalkerFinisher, StalkerFinisherEnd, StalkerKnowledge, StalkerManager, StalkerMemory, StalkerModule, StalkerModules, StalkerNoiseTag, StalkerProfile, StalkerRoster, StalkerSchedule, StalkerSenses, StalkerSpawnPoint, StalkerSpawner, StalkerSummonTrigger, StalkerTraitSprint, StalkerTypes, StalkerWorld

Tipos em arquivo de outro nome:
- `StalkerBT.cs` define enum `BTStatus`, class `BTNode`, class `BTSelector`, class `BTSequence`, class `BTGuard`, class `BTCondition`, class `BTAction`
- `StalkerDefinition.cs` define class `StalkerFollowRules`, class `StalkerSummonRules`, class `StalkerRetreatRules`
- `StalkerDirector.cs` define class `StalkerDirectorState`
- `StalkerModule.cs` define enum `AttackDecision`, class `StalkerTreeBuilder`
- `StalkerProfile.cs` define struct `AttentionTuning`
- `StalkerSpawnPoint.cs` define enum `StalkerSpawnKind`
- `StalkerTypes.cs` define enum `StalkerAttention`, enum `StalkerMode`, enum `HintSource`, enum `NoiseTagMode`, class `StalkerTarget`, struct `StalkerNoise`, struct `StalkerHint`

**Stalker/Tree/**  (9 arquivos)

Arquivos: StalkerBlackboard, StalkerNodeBase, StalkerNodeExamples, StalkerNodesCondition, StalkerNodesFlow, StalkerNodesTask, StalkerTreeAsset, StalkerTreeBuild, StalkerTreeTemplates

Tipos em arquivo de outro nome:
- `StalkerNodeBase.cs` define enum `StalkerNodeKind`, class `StalkerNodeMenuAttribute`, class `StalkerNodeInfo`, class `StalkerCondition`, class `StalkerNode`, class `StalkerComposite`, class `StalkerDecorator`, class `StalkerTask`
- `StalkerNodeExamples.cs` define class `StalkerRoarExample`, class `StalkerNearHomeExample`
- `StalkerNodesCondition.cs` define enum `StalkerCompare`, class `StalkerCompareUtil`, class `StalkerVisualCondition`, class `StalkerLeadCondition`, class `StalkerHidingCondition`, class `StalkerConfidenceCondition`, class `StalkerSourceCondition`, class `StalkerAttentionCondition`, class `StalkerIdleCondition`, class `StalkerModeCondition`, class `StalkerDistanceCondition`, class `StalkerSanityCondition`, class `StalkerSkillCondition`, class `StalkerVarCondition`, class `StalkerChanceCondition`
- `StalkerNodesFlow.cs` define class `BTRandom`, class `BTInverter`, class `BTForce`, class `BTCooldown`, class `BTTimeout`, class `BTRepeat`, class `StalkerSelectorNode`, class `StalkerSequenceNode`, class `StalkerRandomNode`, class `StalkerInverterNode`, class `StalkerSucceederNode`, class `StalkerFailerNode`, class `StalkerCooldownNode`, class `StalkerTimeoutNode`, class `StalkerRepeatNode`
- `StalkerNodesTask.cs` define enum `StalkerBranchId`, enum `StalkerMoveTarget`, enum `StalkerMoveSpeed`, enum `StalkerFaceTarget`, class `StalkerBranchNode`, class `StalkerSubtreeNode`, class `StalkerWaitNode`, class `StalkerStopNode`, class `StalkerMoveToNode`, class `StalkerFaceNode`, class `StalkerSetModeNode`, class `StalkerStimulateNode`, class `StalkerSetVarNode`, class `StalkerAnimNode`, class `StalkerMessageNode`, class `StalkerLogNode`
- `StalkerTreeBuild.cs` define class `StalkerTreeTrace`, class `BTTraced`, class `StalkerBuildContext`

**Stats/**  (4 arquivos)

Arquivos: Stat, StatTypes, StatsComponent, StatusEffectAsset

Tipos em arquivo de outro nome:
- `Stat.cs` define class `StatModifier`
- `StatTypes.cs` define enum `StatType`, enum `DamageType`, enum `ModifierOp`, struct `DamageInfo`, interface `IDamageable`, class `StatusEffectData`, class `ActiveEffect`
- `StatsComponent.cs` define enum `StatPreset`

**UI/**  (11 arquivos)

Arquivos: ControlsPanel, GameOverScreen, MainMenu, PauseMenu, PlayerHUD, SaveSlotsPanel, ScrollFollowSelection, SettingsPanel, Toast, UIFactory, UISprites

**UI/Inventory/**  (15 arquivos)

Arquivos: ContainerView, GridView, HotbarHUD, InspectPanel, InspectStudio, InvUI, InventoryScreen, InventoryScreen.Tabs, InventoryScreen.Window, ItemMenu, ItemVisual, ListView, MapViewer, PlayerOverlay, RecordReader

**World/**  (8 arquivos)

Arquivos: Checkpoint, Door, Draggable, Interactable, Ladder, Pickup, SavePoint, UnityEventInteractable

Tipos em arquivo de outro nome:
- `Interactable.cs` define enum `IconKind`, enum `PromptModeOverride`, struct `PromptData`
- `Ladder.cs` define enum `LadderMode`
