# Horror FPS Framework (Unity) — input, movimento, escadas, interação, arrastar, status, pausa e split-screen

Base para um jogo de terror em primeira pessoa com **multiplayer local / split-screen** (1 a 4 jogadores).
Tudo é C# puro + o asset de input; a UI é criada por código (nenhum prefab de interface necessário).

## Requisitos

- Unity **2022.3 LTS** ou **Unity 6** (usa `#if` onde a API mudou).
- Pacote **Input System** (`com.unity.inputsystem`) e **Project Settings > Player > Active Input Handling = Input System Package (New)** (ou *Both*).
- uGUI (vem por padrão). Texto usa `UnityEngine.UI.Text` com a fonte embutida; para usar TextMeshPro/sua fonte, troque em `UIFactory`.

## Instalação (3 passos)

1. Copie a pasta `Assets/HorrorFPS` para o `Assets` do seu projeto e espere compilar.
2. Menu **Tools > Horror FPS > 2. Create Demo Scene** (cria o prefab do jogador, materiais e a cena `HorrorDemo`).
3. **Play.** Jogador 1 = teclado e mouse. Para o jogador 2, aperte um botão em um **controle**: a tela divide sozinha.

Para usar no seu jogo: arraste `Prefabs/Player.prefab` para um `LocalPlayerManager` (objeto com `PlayerInputManager` + `LocalPlayerManager`), como na cena demo.

## Controles padrão

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Mover / Olhar | WASD / mouse | Analógico esq. / dir. |
| Pular | Espaço | A |
| Correr (gasta fôlego) | Shift | L3 |
| Agachar | Ctrl | B |
| Interagir / soltar escada | E | X |
| Agarrar e arrastar objeto | Botão esquerdo do mouse | RT |
| Aproximar / afastar objeto | Scroll | D-pad ↑ / ↓ |
| Pausar | Esc | Start |

Tudo é remapeável no menu de pausa (**Controles**).

## Arquitetura

```
Assets/HorrorFPS
├─ Input/    HorrorControls.inputactions   (mapa "Gameplay", esquemas Keyboard&Mouse e Gamepad)
│            InputNames        nomes das actions/esquemas (único lugar com strings)
│            PlayerInputHandler  ÚNICA ponte PlayerInput -> gameplay (um por jogador)
│            InputBindingStore   salva/carrega remapeamentos (PlayerPrefs)
│            InputGlyphs / InputGlyphSet   texto/sprite do botão ligado a uma action
├─ Player/   PlayerContext (hub por jogador), FirstPersonController, PlayerClimber,
│            PlayerInteractor, PlayerDragger
├─ World/    Interactable (base), Ladder, Draggable, Door, UnityEventInteractable
├─ Stats/    StatsComponent + Stat + StatusEffectData/Asset   (jogador, NPCs e objetos)
├─ UI/       PlayerHUD (mira, dica, barras), PauseMenu, ControlsPanel, UIFactory, UISprites
├─ Core/     LocalPlayerManager (split-screen), PauseManager, GameSettings, UIText
└─ Editor/   Tools > Horror FPS (prefab do jogador + cena demo)
```

Regra de ouro: **gameplay nunca lê `InputAction` direto** — só `PlayerInputHandler`. Por isso pausar, travar controles
(`GameplayEnabled = false`) e remapear funcionam para todo o jogo sem mexer nos sistemas.

## Sistemas

### Input e remapeamento
- Cada jogador tem sua própria cópia do asset (o `PlayerInput` clona), então mouse/teclado e controles não se misturam.
- Remapeamento por captura interativa, **só aceita dispositivos do esquema editado**, troca automática quando o botão já está em uso
  e salva por ID de binding (vale para todos os jogadores). Botão "Restaurar padrão" incluso.
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
Sem código: `UnityEventInteractable` (eventos no Inspector). Exemplo com estado: `Door` (abre/fecha/tranca).

### Arrastar objetos (`Draggable` + `PlayerDragger`)
Objeto com `Rigidbody` + `Draggable`. O ponto da mira é puxado até um alvo na frente da câmera **por força** (mola + amortecimento): objetos pesados aceleram
menos, o jogador anda mais devagar, o objeto balança ao ser puxado e é solto sozinho se ficar preso. Scroll/D-pad aproximam e afastam.
- `Carry`: segue a mira em 3D. `Slide`: só desliza no chão (móveis pesados).
- Acima de `maxMass` (padrão 60 kg) aparece "Muito pesado". Modo **segurar** ou **alternar** em Opções > Arrastar objetos.

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
Criado automaticamente (não precisa estar na cena). Qualquer jogador pausa; **quem pausou é o dono** e navega/remapeia com o dispositivo dele
(mouse também funciona). `Time.timeScale = 0`, áudio pausado, gameplay de todos desligado. Se um controle desconecta, o jogo pausa. Páginas: principal, **Controles**, **Opções**
(sensibilidade de mouse e controle, inverter Y, estilo da dica, agarrar/agachar segurar×alternar).

### Split-screen (`LocalPlayerManager`)
Usa o `PlayerInputManager` do Unity: cada jogador tem câmera, `AudioListener` (só o 1º fica ativo), HUD no próprio viewport e input próprio.
Quem entra depois usa um dispositivo livre (aperte um botão). Pontos de spawn por índice.
- `Max Players` (padrão 4): quando lota, a entrada fecha sozinha e reabre se alguém sair.
- `Join By Button Press`: ligado = qualquer botão de um dispositivo livre cria um novo jogador; desligado = você chama `PlayerInputManager.JoinPlayer(...)` quando quiser (ex.: pelo menu do jogo).
- `First Player Prefers Gamepad`: o jogador 1 já nasce no controle se houver um conectado.
- Os esquemas de controle ficam travados por jogador (`neverAutoSwitchControlSchemes`) enquanto a entrada por botão estiver ligada ou houver 2+ jogadores,
  senão o jogador 1 "roubaria" o controle que o jogador 2 acabou de usar. Para um jogo só solo com troca teclado ↔ controle ao vivo, desligue `Join By Button Press`.

## Adaptando ao seu projeto
- **Textos**: `Core/UIText.cs` (PT-BR; troque ou ligue à sua localização).
- **Visual da UI**: `UIFactory` (cores, fonte) e `UISprites`.
- **Camadas/máscaras**: `PlayerInteractor.mask`, `FirstPersonController.obstructionMask`.
- Sem `.asmdef` de propósito (para não quebrar referências). Se usar assemblies, crie um para `HorrorFPS` referenciando `Unity.InputSystem` e `UnityEngine.UI`, e outro só-Editor.

## Observações importantes
- **Código escrito sem compilar no Unity.** Foi revisado (delimitadores e referências cruzadas checados por script), mas se aparecer algum erro de compilação na primeira importação, envie a mensagem que eu corrijo.
- Os `.meta` não vão no pacote: o Unity gera na importação.
- Cenas com **URP + Camera Stack**: o HUD usa Canvas *Screen Space - Camera* na câmera do jogador; funciona com câmera base única.
