# Horror FPS Framework — Animação e Corpo do Jogador

Módulo que dá **corpo** ao jogador: o próprio jogador vê **torso, braços e pernas** (sem a cabeça), os outros jogadores no split-screen veem o **corpo inteiro**, e tudo isso funciona com **animação de clipes e animação procedural** (respiração, coice, tranco de pouso, recuo de parede), pronto para receber modelo e clipes de verdade. Encaixado nos módulos já documentados (Arquitetura, Itens/Sanidade/Corpo e Diálogos).

> **Aviso importante:** este módulo foi escrito **sem compilar** e **sem ter o código real do framework** à mão — só a documentação. Tudo o que depende de nomes do framework (`FirstPersonController`, `PlayerClimber`, `PlayerDragger`, eventos de dano, inventário) é lido **por reflexão com nomes alternativos** ou **estimado**, e vira um aviso no Console em vez de erro. Se algo não compilar ou não animar, mande a mensagem do Console (ver seção 12).

---

## 1. O que vem no pacote

```
Assets/HorrorFPS/Animation/                  (runtime)
  PlayerAnimParams.cs        CONTRATO: nomes dos parâmetros/layers do Animator + lista para criar o controller
  PlayerAnimState.cs         o ESTADO (dados puros) + enums HandItem e PlayerAnimEvent
  PlayerAnimBridge.cs        ÚNICO ponto que "adivinha" o framework (reflexão, eventos por assinatura qualquer)
  PlayerAnimDriver.cs        lê o gameplay e escreve o estado todo quadro; dispara os eventos (Raised)
  PlayerAnimItemAdapter.cs   inventário -> o que está na mão, lanterna, equipar/atacar/atirar
  BodyPart.cs                marca um Renderer com a região do corpo (cabeça, torso, braços...)
  BodyVisibility.cs          regras de quem vê o quê (dono x outros)
  BodyViewHook.cs            esconde/mostra as regiões ANTES de cada câmera desenhar (Built-in e URP/HDRP)
  PoseFx.cs                  PoseDelta + IPoseModifier (efeitos procedurais somáveis)
  PlayerBodyPresenter.cs     base dos "apresentadores": ancora o corpo, soma os procedurais, item na mão
  PlayerBodyRig.cs           corpo de verdade: Humanoid + Animator + IK de olhar e de mãos
  PlaceholderPuppet.cs       corpo de TESTE de primitivas (sem modelo, sem clipes)
  PoseBreathing.cs           respiração visível (mais rápida com esforço)
  PoseImpulse.cs             molas: pouso, dano, coice, golpe, equipar, arfada
  PoseWallAvoid.cs           recua as mãos/item perto de parede
Assets/HorrorFPS/Editor/Animation/           (somente editor)
  AnimationEditorUtil.cs                 acha o jogador (seleção ou prefab) e instala o básico
  PlaceholderBodyBuilder.cs              menu 14
  PlayerAnimatorControllerBuilder.cs     menu 15
  ModelBodySetup.cs                      menu 16
```

Menus novos: **Tools ▸ Horror FPS ▸ 14. Add Animation Rig To Player Prefab (placeholder body)**, **15. Create Player Animator Controller**, **16. Setup Selected Model As Player Body**.

Requisitos: Unity 2022.3 LTS ou Unity 6, pacote **Input System** (o `PlayerInput` é usado só no painel de depuração). Namespace `HorrorFPS`. Sem `.asmdef`. Funciona em Built-in, URP e HDRP.

## 2. Teste em 3 minutos (sem modelo e sem clipes)

1. Copie a pasta `Assets` para o projeto e espere compilar.
2. Selecione o **prefab do jogador** no Project (ou uma instância dele na cena) e rode **Tools ▸ Horror FPS ▸ 14. Add Animation Rig To Player Prefab**. Sem seleção, o menu procura um prefab com `PlayerContext` (de preferência chamado "Player").
   - Instala no jogador `PlayerAnimDriver`, `PlayerAnimItemAdapter` e `BodyVisibility`;
   - cria o filho `[Body]` (boneco de primitivas) já ligado ao estado;
   - baixa o **Near Clip** da câmera do jogador para 0,08 se estiver maior (senão o peito é cortado quando se olha para baixo).
   - Selecionou uma **instância na cena**? Só aquela instância é alterada: aplique ao prefab (Overrides ▸ Apply All) para valer para todos os jogadores.
3. Dê Play com **2 jogadores** (split-screen). Marque **Show Debug** no `PlayerAnimDriver` se quiser ver os valores lidos na tela (um painel por jogador).
4. **O que conferir:**
   - olhando para baixo, você vê o **torso, os braços e as pernas** do seu boneco, **sem cabeça** (a sombra no chão ainda mostra a cabeça);
   - ao andar as pernas balançam; ao agachar o tronco desce; equipar um item levanta o braço direito; pistola/arma longa levantam os dois; a lanterna levanta o esquerdo;
   - na tela do **outro** jogador seu boneco aparece **inteiro, com a cabeça**, virando para onde você olha;
   - perto de uma parede com item na mão, os braços recuam; ao levar dano/atirar/pular o corpo reage (molas).

> Se o dono vir a cabeça ou o outro jogador não vir o corpo, veja a seção 13 (a causa mais provável é a câmera não ser filha do objeto com `PlayerContext`).

## 3. Ideia central: um estado, várias apresentações

```
 gameplay (controller, escalada, inventário, vida, coração, sanidade)
            │  lido por reflexão / estimado
            ▼
   PlayerAnimDriver ──escreve──▶  PlayerAnimState   (dados puros: velocidade, olhar, agachar, item, esforço...)
            │                          │
            │ Raised(evento, força, direção)
            ▼                          ▼
   ┌────────────────────────────────────────────┐
   │ Apresentadores (só LEEM o estado)          │
   │   PlayerBodyRig       Animator + IK        │
   │   PlaceholderPuppet   primitivas           │
   │   (o seu: herde de PlayerBodyPresenter)    │
   └────────────────────────────────────────────┘
            ▲
   IPoseModifier (respiração, molas, parede, os seus) somam um PoseDelta antes de apresentar
```

Consequências práticas:
- **Trocar o corpo não mexe no gameplay** e vice-versa. O gameplay nunca fala com o Animator.
- **Todas as câmeras veem o mesmo corpo.** Não existe "modelo em primeira pessoa" separado: o dono vê o próprio corpo de verdade (por isso o torso e as pernas estão certos com a postura, o agachar, a escalada...). A diferença é só **quais regiões** cada câmera enxerga (seção 4).
- **A câmera continua sendo do controller** (não é presa a um osso). O corpo é que a segue: fica de frente para onde a câmera olha (yaw) e **recua alguns centímetros ao olhar para baixo** (`lookDownPullback`, 0,2 m em 80°) para o peito não entrar na lente.
- **Pronto para rede:** se um dia o jogo for online, o que se replica é o `PlayerAnimState` (pequeno) + os eventos; cada máquina apresenta o corpo localmente.
- Outros sistemas podem **ler** o estado (ex.: a IA ver que o jogador está agachado): `player.GetComponent<PlayerAnimDriver>().State.crouch`.

## 4. Quem vê o quê

O `BodyVisibility` (na raiz do jogador) decide por **região** (`BodyRegion`, marcada em cada renderer com o componente `BodyPart`). Antes de cada câmera desenhar, o `BodyViewHook` aplica a visão certa e, depois, restaura.

| Câmera | O que enxerga do corpo |
|---|---|
| **Do próprio jogador** (qualquer câmera filha do `PlayerContext`) | Regiões de `ownerSees`: por padrão **Torso, Braços, Mãos, Pernas, Pés, Item na mão, Acessórios** — **sem a Cabeça**. A cabeça continua **projetando sombra** (`hiddenKeepShadow`) |
| **Do outro jogador** (split-screen) | **Tudo** |
| **Cutscene, câmera do editor, câmera de morte** (fora de qualquer jogador) | **Tudo** |

Regiões: `Head`, `Torso`, `Arms`, `Hands`, `Legs`, `Feet`, `Held` (item na mão), `Accessory` (roupa, mochila, capacete). `ownerSees` é um campo de flags no Inspector: se o seu rosto/capacete for visível de propósito (visor, por exemplo), marque `Head`/`Accessory` ali.

**Como funciona:** cada `BodyPart` liga/desliga o `forceRenderingOff` e o `shadowCastingMode` do renderer **na hora** de cada câmera (`Camera.onPreCull`/`onPostRender` no Built-in; `RenderPipelineManager.beginCameraRendering`/`endCameraRendering` no URP/HDRP). Vantagens: não gasta **layers**, funciona para 4 jogadores, e câmeras que não são de jogador veem tudo. Custo: percorrer os BodyPart duas vezes por câmera (desprezível).

## 5. Contrato do Animator (`PlayerAnimParams`)

Todo Animator Controller do corpo usa estes nomes. **Parâmetro que não existir no seu controller é ignorado** (um aviso no Console lista os ausentes) — dá para começar com poucos.

| Tipo | Parâmetro | Significado |
|---|---|---|
| float | `Speed` | velocidade horizontal (m/s) |
| float | `MoveX`, `MoveY` | velocidade lateral (+ direita) e frontal (+ frente), relativas ao olhar — eixos do blend tree 2D |
| float | `Crouch` | 0 em pé … 1 agachado |
| float | `VerticalSpeed` | velocidade vertical (m/s) |
| float | `Pitch` | olhar vertical em graus (+ para baixo) |
| float | `Exertion` | esforço 0..1 (corrida + coração) |
| float | `Sanity` | sanidade 0..1 (1 = lúcido) |
| float | `HitDir` | ângulo (−180..180°) de onde veio o último golpe, relativo à frente do corpo |
| float | `TurnRate` | giro horizontal em graus/s |
| float | `ClimbX`, `ClimbY` | movimento ao escalar |
| bool | `Grounded`, `Sprint`, `Climbing`, `Dragging`, `HoldingBreath`, `Dead`, `HasFlashlight`, `FlashlightOn` | estados |
| int | `HandItem` | 0 nada · 1 genérico · 2 corpo a corpo · 3 pistola · 4 arma longa |
| int | `SanityStage` | 0 estável … 4 quebrado |
| trigger | `Jump`, `Land`, `Attack`, `Fire`, `Reload`, `Equip`, `Hit`, `Interact`, `Gasp` | acontecimentos pontuais |

Layers (por nome): **`Upper Body`**, **`Flashlight`**, **`Additive`**. O peso das duas primeiras é controlado pelo código (entram quando há item/lanterna); a Additive fica sempre em 1.

## 6. Animator Controller pronto (menu 15)

**Tools ▸ Horror FPS ▸ 15. Create Player Animator Controller** cria `Assets/HorrorFPS/Generated/Animation/PlayerBody.controller` com todos os parâmetros, as layers, as transições e duas máscaras de avatar (`PlayerUpperBody.mask`, `PlayerLeftArm.mask`). **Os estados vêm vazios: arraste os seus clipes.** Se o controller já existir, o menu pergunta se deve só **acrescentar parâmetros** (não perde nada) ou recriar do zero.

| Layer | Estado | Clipe que vai ali | Quando entra |
|---|---|---|---|
| **Base** | `Locomotion` (blend 2D MoveX/MoveY) | idle (0,0), andar (0,2), correr (0,4.5), andar para trás (0,−2), passo lateral (±2,0) | padrão |
| | `Crouch` (blend 2D) | agachado parado (0,0) e andando (0,±1.2 / ±1.2,0) | `Crouch` > 0,5 |
| | `JumpStart` | impulso do pulo | trigger `Jump` |
| | `Air` (blend 1D `VerticalSpeed`) | subindo (4), ápice (0), caindo (−6) | `Grounded` falso |
| | `Land` | pouso | trigger `Land` + `Grounded` |
| | `Climb` (blend 2D ClimbX/ClimbY) | escalada | `Climbing` |
| | `Drag` (blend 2D) | andar arrastando | `Dragging` |
| | `Dead` | morte | `Dead` (de qualquer estado) |
| **Upper Body** (máscara tronco+braços, **IK Pass**) | `Empty` | — (sem item: o braço fica com a Base) | padrão |
| | `Hold_Generic`, `Hold_Melee`, `Hold_Pistol`, `Hold_Long` | pose segurando cada tipo | `HandItem` = 1..4 |
| | `Attack`, `Fire`, `Reload`, `Equip`, `Interact` | ação pontual (volta sozinha à pose de segurar) | triggers |
| **Flashlight** (máscara braço esquerdo, **IK Pass**) | `Flashlight_Lowered` / `Flashlight_Raised` | lanterna apagada / acesa | `FlashlightOn` |
| **Additive** (aditiva, sem máscara) | `Empty`, `HitReact` (blend 1D `HitDir`: −180, −90, 0, 90, 180), `Gasp` | reação ao dano por direção; arfada | triggers `Hit`, `Gasp` |

Dicas:
- Sem clipes o corpo fica na pose de referência (T-pose). É esperado: o controller só dá a estrutura.
- Clipes de ação (`Attack`, `Fire`...) devem **terminar** na pose de segurar; a saída da ação é por tempo (90%).
- Se uma layer não conseguir ser criada sozinha, o menu avisa no Console qual foi; crie-a à mão seguindo a tabela.

## 7. Modelo de verdade (menu 16)

**Antes: o modelo precisa de**
- Rig **Humanoid** (Import Settings ▸ Rig ▸ Animation Type = Humanoid ▸ Apply). Sem Humanoid os parâmetros funcionam, mas **IK de olhar e de mãos não**.
- **Um renderer por região** (cabeça / corpo / mãos / pernas / sapatos, e opcionalmente braços/acessórios). Num mesh único não dá para esconder só a cabeça do dono: o menu avisa. No mínimo separe **a cabeça** do resto.
- Escala em metros e pés na origem do modelo.
- **Near Clip ≤ 0,1** na câmera do jogador, para o peito não ser cortado ao olhar para baixo (o menu 14 já ajusta).

**Passos**
1. Selecione o **FBX/prefab do modelo** no Project (ele é instanciado dentro do prefab do jogador, que o menu acha sozinho: o primeiro prefab com `PlayerContext`, de preferência chamado "Player") — ou um modelo que você já arrastou para dentro do jogador na cena.
2. **Tools ▸ Horror FPS ▸ 16. Setup Selected Model As Player Body.**
3. O menu: instala o básico (como o 14), posiciona o modelo nos **pés do CharacterController**, põe `Animator` (controller do menu 15, se existir; `applyRootMotion` desligado; `Always Animate`), `PlayerBodyRig`, `PoseBreathing`, `PoseImpulse`, `PoseWallAvoid`, e um `BodyPart` em cada renderer com a região **adivinhada pelo nome** (head/hair/face… → Cabeça; hand/glove → Mãos; arm/sleeve → Braços; shoe/boot/foot → Pés; leg/pant/jeans → Pernas; o resto → Torso). O boneco `[Body]` de teste é **desativado** (não apagado).
4. **Confira os `BodyPart` (Region)** — o chute por nome erra com nomes esquisitos. O resumo no Console diz quantos renderers caíram em cada região; se nenhum caiu em Cabeça, ele avisa.
5. Se a altura do modelo difere do `CharacterController` em mais de 25 cm, o menu avisa (ajuste o *Scale Factor* na importação).

### `PlayerBodyRig` (no objeto do Animator)

| Campo | Uso |
|---|---|
| `locomotionDamp` (0,07), `layerBlendSpeed` (6) | suavização dos parâmetros / entrada das layers |
| `lookAtIK`, `lookWeight`, `bodyWeight`, `headWeight`, `eyesWeight`, `clampWeight`, `lookDistance` | a cabeça acompanha a câmera (o feixe da lanterna sai para onde o corpo olha) |
| `handIK`, `rightHandTarget`/`leftHandTarget` | alvos das mãos. Vazios = o rig cria `[HandTarget R/L]` como filhos da câmera, nas posições `rightHandLocalPosition`/`leftHandLocalPosition` (+ Euler). **Ajuste a posição/rotação até a mão ficar certa no seu modelo** |
| (herdados do `PlayerBodyPresenter`) | `followCameraYaw`, `lookDownPullback` (0,2 m), `lookUpPullback` (0,05), `pullbackSmoothing`, `maxPitch` (80°), `fitEyeHeightOnStart` + `eyeAnchor` + `eyeDrop` (alinha a altura do osso da cabeça à câmera) |

Pesos do IK das mãos: a **direita** vai para o alvo quando há item na mão; a **esquerda** quando é pistola/arma longa (duas mãos) ou há lanterna. Por isso **ligue `IK Pass`** nas layers `Upper Body` e `Flashlight` (o menu 15 já liga).

## 8. Efeitos procedurais (`IPoseModifier`)

Mesma ideia do `CameraFx`/`ICameraModifier` do framework, mas para o corpo: cada efeito **soma** um `PoseDelta` no quadro e o apresentador aplica o total. Funciona com o Animator **e** com o boneco de teste, e os outros jogadores veem o efeito junto com o corpo.

`PoseDelta`: `bodyOffset` (m, espaço do corpo) · `lookOffset` (graus: x = pitch + para baixo, y = yaw) · `lookWeightAdd` · `rightHandPos`/`leftHandPos` (m, espaço da **câmera**) · `rightHandEuler`/`leftHandEuler` (graus) · `rightHandWeightAdd`/`leftHandWeightAdd`.

Incluídos (todos desligáveis desmarcando o componente):

| Componente | O que faz | Ajustes principais |
|---|---|---|
| `PoseBreathing` | peito sobe e desce; cabeça balança de leve; mais rápido/fundo com `Exertion`; quase parado com `HoldingBreath` | `restRate` 13, `maxRate` 40 resp/min, `chestAmplitude`, `headAmplitude`, `holdBreathFactor` |
| `PoseImpulse` | molas amortecidas: **pouso** (corpo afunda), **dano** (empurrão e torção para longe de quem bateu), **tiro** (coice da cabeça e das mãos; maior com arma longa), **golpe** (avanço), **equipar** (mãos baixam), **arfada** | `stiffness` 85, `damping` 11, `landDip`, `hitPush`, `hitTwist`, `recoilPistol`, `recoilLong`, `handRecoil`, `lungeMelee` |
| `PoseWallAvoid` | com item/lanterna, um SphereCast à frente da câmera recua e baixa as mãos e levanta a ponta do item quando há parede perto (sem câmera extra de "viewmodel" — vale em qualquer pipeline) | `reach` 0,85, `probeRadius`, `mask`, `pullBack`, `lower`, `raiseAngle`, `smoothing` |

**Escrevendo o seu** (ex.: mãos tremem com a sanidade baixa):

```csharp
using UnityEngine;
using HorrorFPS;

public class PoseTremor : MonoBehaviour, IPoseModifier
{
    public void Contribute(PlayerBodyPresenter body, PlayerAnimState s, ref PoseDelta d, float dt)
    {
        float t = (1f - s.sanity01) * 0.004f;
        d.rightHandPos.x += Mathf.Sin(Time.time * 41f) * t;
        d.leftHandPos.x  += Mathf.Cos(Time.time * 37f) * t;
    }
}
```

Coloque o script no jogador (ou no objeto do corpo): o apresentador o acha no `Start`. Se você adicionar o componente **em tempo de execução**, chame `body.AddModifier(meuModificador)`. Para reagir a acontecimentos, assine `driver.Raised`.

## 9. Itens na mão

O `PlayerAnimItemAdapter` (raiz do jogador) escuta `ItemEvents.Equipped/Unequipped/EquippedActivated` do módulo de itens:
- **Lanterna:** item do slot `lanterna` (campo `flashlightSlot`) ou com "lanterna"/"flashlight" no id → `hasFlashlight`/`flashlightOn`; não conta como item de mão.
- **Item de mão:** classificado pela lista **`rules`** (Inspector), lida de cima para baixo, vale a primeira que casa com um trecho do id: `faca`, `cabra`, `taco`, `machado`, `martelo`, `bastao` → Corpo a corpo; `pistola`, `revolver` → Pistola; `espingarda`, `rifle`, `fuzil`, `automatica`, `shotgun` → Arma longa; qualquer outro → Genérico. **Ajuste as regras aos ids do seu projeto.**
- Ao usar o item equipado dispara `Attack` (corpo a corpo), `Fire` (pistola/longa) ou `Interact` (genérico). Equipar/guardar dispara `Equip`/`Unequip`.
- Ao iniciar, sincroniza com o que já estiver equipado (`GetEquipped` por reflexão).
- **Reload não tem evento no framework:** chame `driver.Raise(PlayerAnimEvent.Reload)` de onde a recarga começa.

**Modelo do item visto pelos outros jogadores (ainda não ligado):** hoje o `EquipmentController` prende o modelo do item na **câmera** — o dono vê certo, mas os outros jogadores o veem "flutuando" na frente da câmera dele. A base para resolver já existe: `body.AttachHeld(prefab, leftHand, posLocal, eulerLocal)` cria uma cópia **só visual** (sem colisor/script/rigidbody) presa à mão do corpo, marcada como região `Held`. **Falta chamar isso do `EquipmentController`** — me mande o `EquipmentController.cs` que eu ligo (e escondo o modelo preso à câmera para os outros). Para testar já: `GetComponentInChildren<PlayerBodyPresenter>().AttachHeld(prefab, false, Vector3.zero, Vector3.zero)`; `ClearHeld(false)` remove. Num corpo de teste o socket é o filho `Hand` do braço; num corpo Humanoid é o osso da mão.

## 10. API de código

```csharp
var driver = jogador.GetComponent<PlayerAnimDriver>();

// estado (somente leitura fora do módulo)
PlayerAnimState s = driver.State;   // s.speed, s.crouch, s.sprinting, s.grounded, s.handItem, s.exertion, s.sanity01, s.dead ...

// acontecimentos
driver.Raised += (tipo, forca, direcaoNoMundo) => { if (tipo == PlayerAnimEvent.Footstep) /* som de passo por jogador */; };
driver.Raise(PlayerAnimEvent.Reload);                       // quando o gameplay não avisa sozinho
driver.Raise(PlayerAnimEvent.Hit, 0.8f, direcaoDoGolpe);    // dano "manual"
driver.Snap();                                              // depois de teleporte/spawn/load (já é chamado em SceneDirector.LevelLoaded)

// forçar estados que o framework não expõe
driver.manualClimbing = true;  driver.manualDragging = true;  driver.manualHoldingBreath = true;

// visibilidade
var vis = jogador.GetComponent<BodyVisibility>();
vis.ownerSees |= BodyRegion.Head;     // o dono passa a ver a cabeça (visor etc.)
vis.Refresh();                        // depois de trocar o modelo / anexar peças

// item visual na mão do corpo
var corpo = jogador.GetComponentInChildren<PlayerBodyPresenter>();
corpo.AttachHeld(prefabDoItem, false, Vector3.zero, Vector3.zero);
corpo.ClearHeld(false);
```

`PlayerAnimEvent`: `Footstep, Jump, Land, Attack, Fire, Reload, Equip, Unequip, Hit, Gasp, Death, Revive, Interact`.

## 11. Como o driver lê o jogo (e o que estima)

| Dado | Fonte | Se o framework não expuser |
|---|---|---|
| Velocidade, direção, giro | **deslocamento da raiz do jogador** entre quadros (não depende de nome nenhum), com suavização e detecção de teleporte (`teleportDistance`) | — |
| Olhar (yaw/pitch) | rotação da câmera do jogador | usa a rotação da raiz |
| Chão | `FirstPersonController` (`IsGrounded`/`Grounded`...) | `CharacterController.isGrounded` + raio curto para baixo |
| Agachar | `CrouchAmount`/`IsCrouching`... | queda da altura da câmera (`crouchDepthEstimate`) |
| Correr | `IsSprinting`/`IsRunning`... | velocidade > `sprintSpeedThreshold` (4,6 m/s) |
| Passo, pulo, pouso | eventos `Footstep`/`Jumped`/`Landed` do controller | passo a cada `fallbackStepLength` (1,7 m); pulo/pouso pela velocidade vertical |
| Escalar / arrastar / segurar o ar | `PlayerClimber` / `PlayerDragger` / `BreathingComponent` (nomes alternativos) | `manualClimbing` etc. (ou me mande os arquivos) |
| Dano / morte | eventos `Damaged`/`Died` do `StatsComponent` (qualquer assinatura; o driver extrai quantidade e origem) | `driver.Raise(...)` à mão |
| Coração / sanidade | `HeartbeatComponent` (`Bpm`/`Intensity`/`Pulse`), `SanityComponent.Of(player)` | esforço pela velocidade |

O Console mostra **uma** mensagem por jogador dizendo o que foi estimado. Estimado funciona, só é menos exato.

## 12. Ajustes no framework (premissas ainda não conferidas)

| Premissa | Onde ajustar se falhar |
|---|---|
| Existem as classes `FirstPersonController`, `PlayerClimber`, `PlayerDragger`, `StatsComponent`, `HeartbeatComponent`, `BreathingComponent`, `EquipmentController`, `SanityComponent`, `InventoryOwner`, `ItemEvents`, `ItemInstance` (`data.Id`), `SceneDirector.LevelLoaded(string)` — todas são **Components** (MonoBehaviour) | Erro de compilação em `PlayerAnimDriver`/`PlayerAnimItemAdapter`: me mande a mensagem |
| A **câmera do jogador é filha** do objeto com `PlayerContext` | `BodyVisibility.IsOwnerCamera` (uma linha) |
| Os eventos do framework se chamam `Footstep`, `Jumped`, `Landed`, `Damaged`, `Died`, `Equipped`, `Unequipped`, `EquippedActivated` | `PlayerAnimBridge.Bind` — nome errado vira aviso, o resto cai nas estimativas |
| O slot de equipamento é uma `string` (`equipSlot`) | Se for enum, só vale o id (`lanterna`/`flashlight`) para achar a lanterna |
| Os nomes dos membros de estado (`IsGrounded`, `IsCrouching`...) | listas no topo do `PlayerAnimDriver` (arrays `GroundNames`, `CrouchNames`...) |

## 13. Limitações conhecidas

- **Compilado só no papel** — espere 1 ou 2 rodadas de ajuste.
- **Modelo do item nas mãos dos outros jogadores:** ainda não ligado ao `EquipmentController` (seção 9).
- **Sem IK de pés, sem ragdoll, sem torção do tronco** (o corpo gira inteiro com a câmera; a cabeça/tronco acompanham o pitch por IK). Dá para acrescentar depois como mais um `IPoseModifier`/layer.
- **O corpo fica sempre de frente para o olhar** (como em jogos de primeira pessoa com corpo): andar de lado ou para trás é feito pelo blend tree, o tronco não gira independente.
- **Mesh único** não permite esconder só a cabeça do dono (seção 7).
- **`HandItem` é classificado pelo id** (regras editáveis); itens sem regra viram "genérico".
- **Sombras:** a cabeça escondida do dono projeta sombra para ele (de propósito). Em URP, confira se a luz principal projeta sombras na qualidade que você usa.
- **Câmera de morte/espectador** fora do jogador vê tudo, inclusive a cabeça do corpo do dono.
- **Câmeras de jogador que não são filhas do `PlayerContext`** são tratadas como câmeras de fora (veem tudo).
- Há **um único corpo ativo** por jogador: ao rodar o menu 16, o boneco de teste é desativado.

## 14. Receitas

**Ver o próprio corpo sem a cabeça, e o outro jogador com tudo** — menu 14, Play com 2 jogadores (seção 2).

**Trocar o boneco por um modelo** — importar Humanoid, dividir por região, menu 15 (uma vez), menu 16 com o modelo selecionado, arrastar clipes no controller.

**Mostrar o capacete/máscara para o dono** — marque o renderer como `Accessory` e deixe `Accessory` em `ownerSees` (já está); para esconder, tire a flag.

**Tremor nas mãos com sanidade baixa** — o `PoseTremor` da seção 8.

**Som de passo por jogador** — assine `driver.Raised` e toque o som quando `tipo == Footstep` (a força é 0..1 pela velocidade).

**Reação extra ao susto do coração** — `driver.Raise(PlayerAnimEvent.Gasp, 1f)`; o `PoseImpulse` faz a arfada e, no Animator, o trigger `Gasp` toca o clip aditivo.

**Cutscene que mostra o jogador de fora** — nenhuma configuração: a câmera da cutscene não é de nenhum jogador e vê o corpo inteiro. Para esconder o corpo numa cutscene em primeira pessoa, desative o **objeto do corpo** (`[Body]` ou o modelo); desligar só o `BodyVisibility` faz o dono ver tudo, inclusive a cabeça.

## 15. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| O dono vê o interior da própria cabeça | Nenhum renderer da cabeça com `BodyPart.Region = Head`; modelo de mesh único; ou a câmera não é filha do `PlayerContext` (então é tratada como "de fora") |
| O outro jogador não vê o corpo | `BodyVisibility` ausente/desligado; o corpo está desativado; ou a câmera dele é filha do jogador errado |
| Não vejo as pernas/torso ao olhar para baixo | `Near Clip` alto (use ≤ 0,1); `ownerSees` sem Torso/Legs; `lookDownPullback` pequeno para o seu modelo (aumente); câmera acima demais do modelo (use `fitEyeHeightOnStart` + `eyeAnchor`) |
| O peito atravessa a câmera | Aumente `lookDownPullback` e/ou diminua o Near Clip |
| Corpo em T-pose | Controller sem clipes (seção 6), sem controller no Animator, ou Humanoid sem Avatar |
| Mãos não pegam a lanterna/arma | Modelo não Humanoid; `IK Pass` desligado nas layers; alvo da mão mal posicionado (`rightHandLocalPosition`) |
| Corpo desaparece de longe/fora de tela | `Culling Mode` do Animator diferente de *Always Animate* (o menu 16 define) |
| Mensagem "estimando pelo deslocamento" | O controller não expõe aquele membro com nomes conhecidos (seção 11); funciona assim, mas me mande o arquivo se quiser exato |
| Aviso "não tem N parâmetro(s) do contrato" | Seu controller não tem esses parâmetros; eles são ignorados (menu 15 acrescenta os faltantes) |
| O boneco de teste ainda aparece depois do menu 16 | Rode o menu com o jogador/modelo selecionado; ou desative `[Body]` à mão |
| Reação de dano sempre do mesmo lado | O evento `Damaged` não trouxe origem/direção: o módulo usa um lado aleatório. Passe a direção com `driver.Raise(PlayerAnimEvent.Hit, força, direção)` |
