# Reanimação em co-op — cair em vez de morrer, levantar com ou sem kit médico

*Complementa Arquitetura, Itens/Sanidade/Corpo, Props e Perseguidor. Usa o `StatsComponent`, o `Interactable`, o inventário, a sanidade, o `CameraFx`, o `GameSession` e o `SaveSystem` que já existem.*

Em partida com **2 ou mais jogadores**, quem morreria **cai**: fica no chão com 1 de vida, sem poder se mexer, sangrando. Um colega de pé **segura Interagir** nele para levantá-lo. Há duas formas de levantar:

| | **Sem kit médico** | **Com kit médico** |
|---|---|---|
| Quem escolhe | o reanimador **não tem** kit no inventário | o reanimador **tem** um kit: 1 unidade é gasta |
| Tempo segurando Interagir | 4 s | 2 s |
| Vida ao levantar | **baixa**: 20% do máximo | **pelo menos metade**: 50% do máximo (nunca menos do que já tinha) |
| Sanidade | **piora na hora** (−20 de 100) | nada |
| Efeito passivo negativo | **ferido por 120 s**: anda a 85% da velocidade, câmera treme, sanidade escorre | **nenhum** (e o kit ainda cura um ferimento anterior) |

Todos os números estão em um asset (`ReviveProfile`, seção 5). Sozinho (1 jogador) ou com todos os outros caídos/mortos, a morte é a **normal do framework**.

> **Sobre "sanidade aumentada":** entendi como *insanidade aumentada*, isto é, a sanidade **piora** ao levantar sem kit, porque a frase lista efeitos negativos. É o campo `Bare Sanity Delta` (padrão **−20**). Se a sua ideia era o contrário (levantar mais lúcido), ponha um valor **positivo**.

> **Código escrito sem compilar no Unity**, só contra a documentação dos outros módulos. O que toca o framework está em **três arquivos** (`ReviveBridge.cs`, `ReviveHealth.cs`, `ReviveDeath.cs`, mais o `Interactable` em `ReviveInteractable.cs`; seção 11). **A morte do jogador não é documentada no framework**, então o módulo a descobre por nome, confere o resultado e, se não conseguir, avisa no Console e deixa a morte normal valer. Espere 1 ou 2 rodadas de ajuste (seção 11 diz o que me enviar).

## 1. O que vem no pacote

```
Assets/HorrorFPS/Revive/
  ReviveBridge.cs        ponte 1/3 com o framework: jogadores, câmera, ferimento, sanidade, kit, save, eventos, diagnóstico
  ReviveHealth.cs        ponte 2/3: ler e escrever a vida, dano
  ReviveDeath.cs         ponte 3/3: saber que morreu e DESFAZER a morte
  ReviveReflect.cs       utilidades de reflexão (sem tipos do framework)
  ReviveTypes.cs         ReviveKind (Bare/WithKit), DownState (Up/Downed/Dead), eventos estáticos
  ReviveProfile.cs       todos os números e textos (ScriptableObject; sem asset vale o padrão)
  ReviveDownable.cs      o estado de caído de UM jogador (nasce sozinho em cada PlayerContext)
  ReviveInteractable.cs  o "Reanimar Jn" (herda de Interactable; só existe enquanto o jogador está caído)
  ReviveManager.cs       gerente (nasce sozinho): decide quem pode cair, derrota total, save, troca de nível
  ReviveHud.cs           tela do caído, marcadores e dica "com/sem kit" (OnGUI, por metade de tela)
  ReviveApi.cs           fachada estática para o seu código e para o perseguidor
  ReviveDebugKeys.cs     teclas F6 a F11
Assets/HorrorFPS/Editor/Revive/ReviveMenu.cs   menus Revive > Create Revive Profile, Toggle Revive Debug Keys e Revive Diagnostics (Play Mode)
Alterados no pacote do perseguidor: Stalker/StalkerBridge.cs e Stalker/StalkerFinisher.cs (seção 7)
```
**Não precisa pôr nada na cena.** O gerente nasce sozinho ao iniciar o jogo e coloca o componente em cada jogador. Requisitos: os do framework (inventário, sanidade, `Interactable`, `StatsComponent`).

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` por cima e espere compilar.
2. **Tools > Horror FPS > Revive > Create Revive Profile**: cria `Assets/Resources/HorrorFPS/ReviveProfile` e **procura o id do seu kit médico** entre os `ItemData` (veja o Console; seção 6).
3. **Tools > Horror FPS > Revive > Toggle Revive Debug Keys** (liga as teclas de teste).
4. Play com **2 jogadores** (split-screen). Teclas:

| Tecla | Faz |
|---|---|
| **F6** / **F7** | derruba o J1 / o J2 (não precisa morrer de verdade) |
| **F8** / **F9** | levanta os caídos **sem kit** / **com kit** (direto, sem reanimador e sem gastar kit) |
| **F10** | diagnóstico no Console (o que foi achado no framework; seção 11) |
| **F11** | dá um kit médico ao J1 |

5. O caminho de verdade: **F6** (J1 cai) → com o J2, **mire no J1** (a dica "Reanimar J1" é a do framework; a nossa mostra "Sem kit médico: levanta fraco…") e **segure Interagir** 4 s: ele levanta com 20% de vida, −20 de sanidade e ferido. Depois **F11** (kit para o J1), **F7** (J2 cai) e com o J1: dica verde "Com kit médico…", 2 s, levanta com 50% e sem efeito.
6. Deixe o caído sangrar 90 s (ou baixe `Bleed Out Seconds`) para ver a morte. Mate o J2 de verdade com o J1 caído: derrota total e o game over do framework.

Com **1 jogador** o F6 funciona para ver a tela do caído, mas ele não morre sozinho no instante (não há derrota total solo): use F8/F9 ou espere o sangramento.

## 3. Como funciona

```
 jogador morre (vida 0 / IsDead)
        │  ReviveDownable percebe (todo quadro, ANTES dos scripts do framework)
        ▼
 sistema ligado · jogadores ≥ Min Players · há OUTRO jogador de pé? ──não──► morte normal do framework
        │sim
        ▼
 desfaz a morte (ReviveDeath.Undo) · vida = Downed Health (1) · trava os controles
 câmera tomba · coração acelera · sanidade escorre · anima "Death" · barulho · "Reanimar Jn" aparece
        │
        ├─ colega segura Interagir ──► tem kit? ──sim──► gasta 1 · levanta COM kit
        │                                         └─não──► levanta SEM kit (vida baixa, −sanidade, ferido)
        ├─ o sangramento acaba (Bleed Out Seconds, 90) ──► morre de vez
        ├─ leva mais dano (qualquer um) ──► morre de vez
        └─ não há mais ninguém de pé (derrota total) ──► Wipe Delay Seconds (1,5) depois, morrem; o GameOver do framework aparece
```

- **O módulo desfaz a morte depois que ela acontece** (leitura por reflexão do framework, seção 11). Quando você me enviar o `StatsComponent`, dá para trocar isso por um **gancho exato** de uma linha, que cancela a morte antes de ela acontecer (seção 11.3).
- **Foto do jogador vivo:** a cada 1,5 s, com o jogador vivo, o módulo guarda o que está ligado (scripts, animadores, colisores, filhos diretos). Ao desfazer uma morte ele **religa** o que a morte desligou. Nunca desliga nada.
- **A vida do caído é mantida** em `Downed Health` (a regeneração do framework não o "cura"). Se algo curar o caído de fora (script, save carregado) para 50% ou mais, ele sai do estado de caído sem efeitos.
- **Quem pode ajudar** (`IsAble`): de pé, vivo e visto vivo. Um jogador caído ou morto não ajuda.
- **Sem alvo para o monstro:** o perseguidor trata o caído como fora de combate (seção 7).

## 4. As duas formas de levantar, em detalhe

**Quem reanima** segura Interagir olhando para o caído (o framework mostra "Reanimar J1" e a barra de segurar). O tempo é o do **reanimador**: `Hold Seconds Kit` (2 s) se ele tem kit, senão `Hold Seconds Bare` (4 s). Soltar cancela (comportamento do framework).

**Kit:** se `Use Kit When Available` está ligado e o reanimador tem **qualquer** id da lista `Medkit Item Ids` no inventário, **1 unidade é removida** (`RemoveItem`, conferida) e o levantar é **com kit**. Se o item não sair, vale o levantar sem kit. Desligado, o kit nunca é usado.

**COM kit:** vida = o maior entre a vida atual e `Kit Health Fraction` (0,5) × vida máxima. Sem sanidade perdida, sem ferimento; `Kit Clears Injury` também **cura o ferimento** de uma reanimação anterior.

**SEM kit:** vida = `Bare Health Fraction` (0,2) × vida máxima. `Bare Sanity Delta` (−20) pontos de sanidade na hora. E o **ferimento**, por `Injury Seconds` (120 s):

| Efeito do ferimento | Campo | Padrão |
|---|---|---|
| Velocidade de movimento × | `Injury Speed Multiplier` | 0,85 (efeito de status `revive_injury`, via `StatsComponent.ApplyEffect`) |
| Tremor constante da câmera | `Injury Tremor` | 0,12 (0 = sem) |
| Sanidade escorrendo por segundo | `Injury Sanity Drain` | −0,05 (0 = nada) |
| Sangramento: vida perdida por segundo | `Injury Bleed Per Second` | 0 (desligado; **pode matar**) |

O ferimento **pausa** enquanto o jogador está caído de novo, vai para o save e some sozinho ao fim do tempo. Um kit usado depois o cura (`Kit Clears Injury`), e `ReviveApi.CureInjury` cura por código (item de cenário, pílula).

Mensagens na tela (SanitySubtitles): "J2 caiu!" para os outros, "Você caiu. Espere ajuda." para o caído, "Você levantou." / "Você levantou, mas está ferido." e "J2 levantou com o kit médico." / "J2 levantou fraco (sem kit)." Todas editáveis.

## 5. O perfil (`ReviveProfile`)

Menu `Revive > Create Revive Profile` cria o asset em `Assets/Resources/HorrorFPS/ReviveProfile`. Sem asset, valem os padrões abaixo.

| Grupo | Campo | Padrão | Efeito |
|---|---|---|---|
| Geral | `Enabled` | ligado | desligado: a morte é a do framework |
| | `Min Players` | 2 | jogadores na partida para alguém poder cair (sozinho a morte é normal) |
| | `Fallback Max Health` | 100 | vida máxima usada nas contas se o `StatsComponent` não deixar ler o máximo |
| Caído | `Downed Health` | 1 | vida enquanto caído |
| | `Bleed Out Seconds` | 90 | segundos até morrer se ninguém levantar (0 = não morre sozinho) |
| | `Wipe Delay Seconds` | 1,5 | derrota total: tempo até os caídos morrerem |
| | `Downed Sanity Drain` | −0,15 | sanidade por segundo enquanto caído |
| | `Downed Heart Threat` | 0,6 | quanto o coração acelera (0..1) |
| | `Down Noise Radius` | 8 | barulho da queda que a IA ouve (m; 0 = silencioso) |
| | `Camera Roll` / `Camera Pitch` | 32° / 12° | inclinação e olhar baixo da câmera de quem caiu |
| | `Reach Box` | desligado | caixa trigger em volta do corpo para facilitar a mira (seção 12) |
| Reanimar | `Medkit Item Ids` | `kit_medico`, `kit_medico_pequeno`, `kitmedico`, `kit-medico`, `medkit` | ids de `ItemData` tratados como kit (seção 6) |
| | `Use Kit When Available` | ligado | usa o kit do reanimador se ele tiver |
| | `Hold Seconds Bare` / `Kit` | 4 / 2 | segundos segurando Interagir |
| | `Revive Noise Radius` | 6 | barulho ao levantar |
| Com kit | `Kit Health Fraction` | 0,5 | fração da vida máxima (no mínimo) |
| | `Kit Clears Injury` | ligado | o kit cura o ferimento anterior |
| Sem kit | `Bare Health Fraction` | 0,2 | fração da vida máxima |
| | `Bare Sanity Delta` | **−20** | pontos de sanidade (negativo = piora; positivo = melhora) |
| Ferido | `Injury Seconds` | 120 | duração |
| | `Injury Speed Multiplier` / `Injury Tremor` / `Injury Sanity Drain` / `Injury Bleed Per Second` | 0,85 / 0,12 / −0,05 / 0 | ver seção 4 |
| Textos | `text…`, `msg…` | em português | tudo editável; `{0}` é o nome do jogador (J1, J2…) ou o número |
| Tela | `Show Hud` / `Show Messages` | ligados | vinheta, marcadores e dica / legendas |
| | `Debug Keys` | desligado | teclas F6 a F11 (menu `Revive > Toggle Revive Debug Keys`) |

## 6. O id do kit médico

O kit é um `ItemData` do seu inventário; o módulo precisa do **id** dele. O menu `Revive > Create Revive Profile` varre os `ItemData` do projeto procurando por "medkit", "first aid", "kit" + "med/saúde/socorro", "curativo", "bandage", "heal", "cura" (no id ou no nome do asset), escreve os achados em `Medkit Item Ids` (melhor palpite primeiro) e **lista os candidatos no Console**. Se não achar nenhum, abra o perfil e escreva o id do seu kit no campo (é o campo `id` do `ItemData`, o mesmo de `inventory.CountOf("…")`).

Para conferir: **F10** diz se algum id da lista está no inventário do jogador e **quais ids o `ItemDatabase` conhece**. Para itens de cura mais fracos (bandagem) com efeito próprio, deixe-os fora da lista: só os ids listados contam como kit.

## 7. Integração com o perseguidor

- **Caído não é alvo.** `StalkerBridge.IsAlive` devolve falso para o caído. O monstro não bate em "corpo" (o finalizador já ignorava), não o conta como vivo em `Only When Last Alive` e segue atrás dos que estão de pé.
- **Finalização que mata de vez.** No `StalkerFinisher` há o campo novo **`Final Blow`**. Desligado (padrão), a morte do fim da cena também vira "caído" se houver outro jogador de pé (a vítima fica no chão, o colega pode reanimar). **Ligado**, a morte é definitiva: o finalizador avisa o módulo (`ReviveApi.AllowDeath`) e dá o golpe. Para o seu código: `StalkerBridge.Kill(alvo, atacante, final: true)`.
- **Sem acoplamento.** O perseguidor liga `ReviveApi.IsDowned/AllowDeath` por **reflexão**: o pacote do perseguidor compila e funciona sem a pasta `Revive`, e vice-versa.
- A morte do fim da cena **sem outro jogador de pé** é sempre definitiva (não há quem reanime).

## 8. Sangramento, derrota total e o game over

- **Sangramento:** `Bleed Out Seconds` (90) corre só com o jogo rodando (pausa pausa). A barra vermelha aparece na tela do caído e o tempo restante, ao lado do nome, na dos colegas.
- **Dano em cima do caído** (monstro que não respeita, armadilha, fogo) o mata de vez: ele fica com 1 de vida.
- **Derrota total:** se há caído e **ninguém de pé** (os outros caíram ou morreram), passados `Wipe Delay Seconds` os caídos morrem e o **`GameOverScreen` do framework aparece sozinho**, porque agora todos estão mortos. Só vale com `Min Players` ou mais jogadores na cena.
- A morte do caído é repetida a cada 0,5 s (depois a cada 2 s) até o framework confirmá-la; se algo o torna imortal, aparece aviso no Console.
- Quando o último jogador de pé morre com um colega caído, ele morre **normalmente** (ninguém para reanimá-lo).

## 9. Save e troca de nível

- **Save:** antes de salvar, o gerente grava no `GameSession` a chave `revive.<n>` por jogador (`n` = 0 para o J1): `D;segundos que faltam;total;ferimento` se está caído, `U;0;0;ferimento` se só está ferido, vazio se está bem. Ao carregar (e quando o jogador aparece), o estado volta: o caído continua caído e sangrando de onde parou, o ferido volta ferido pelo tempo que faltava. O save **não é bloqueado** por haver caído (a morte desfeita não conta como morto).
- **Troca de nível:** os jogadores moram no Core e atravessam a troca. Durante a transição (`SceneDirector.IsBusy`) o módulo fica parado (o sangramento também). Quando o nível termina de carregar, as travas, a câmera e as fontes de sanidade são **reafirmadas**.
- Carregar um save **zera** o estado ao vivo e refaz tudo a partir do que está gravado.

## 10. Código: API e eventos

Todo método aceita o `PlayerContext`, qualquer componente do jogador ou um objeto dentro dele.

```csharp
ReviveApi.IsDowned(p)                    // está caído?
ReviveApi.StateOf(p)                     // DownState.Up / Downed / Dead
ReviveApi.BleedLeft(p)                   // segundos até morrer
ReviveApi.IsInjured(p)                   // com o ferimento de quem levantou sem kit?
ReviveApi.Down(p)                        // derruba sem ele ter morrido (cena, teste)
ReviveApi.Revive(p, ReviveKind.WithKit)  // levanta sem reanimador e sem gastar kit
ReviveApi.ReviveBy(vitima, reanimador)   // igual a segurar Interagir: usa o kit dele se tiver
ReviveApi.AllowDeath(p, 3f)              // nos próximos 3 s a morte NÃO vira queda (golpe final)
ReviveApi.KillFinal(p)                   // mata de vez, ignorando a reanimação
ReviveApi.CureInjury(p)                  // cura o ferimento (item de cenário, pílula)
ReviveApi.TryIntercept(componente)       // GANCHO exato (seção 11.3)

ReviveEvents.Downed   += p => ...;               // Action<PlayerContext>
ReviveEvents.Revived  += (p, por, tipo) => ...;  // por = null se foi por código; tipo = Bare / WithKit
ReviveEvents.BledOut  += p => ...;               // o caído morreu de vez (sangrou, levou dano, derrota total)
```
Pelo gerente: `ReviveManager.All` (todos os `ReviveDownable`), `ReviveManager.Find(p)`, `ReviveManager.CanDown(d)`. Num `ReviveDownable`: `State`, `IsDowned`, `IsAble`, `BleedLeft`, `BleedTotal`, `InjuryLeft`, `ForceDown()`, `StandUpNow(tipo)`, `ReviveBy(p)`, `Execute()`, `CureInjury()`.

Exemplo: item "Desfibrilador" que levanta o colega caído **sem** ferimento e sem gastar o kit, num script do item:
```csharp
ReviveApi.Revive(alvo, ReviveKind.WithKit);   // metade da vida, sem efeito negativo
```

## 11. Premissas do framework, diagnóstico e o que me enviar

### 11.1 Premissas (tudo em `ReviveBridge.cs`, `ReviveHealth.cs`, `ReviveDeath.cs` e `ReviveInteractable.cs`)

**Documentadas no framework (uso direto; erro de compilação se o nome estiver diferente):**

| Premissa | Se falhar |
|---|---|
| `StatusEffectData { id, displayName, duration, modifiers, overTime }`, `StatusEffectData.StatChange { stat, op, value }`, `StatusEffectData.StatTick { stat, perSecond }`, `StatType.MoveSpeed`, `ModifierOp.Multiply`, `StatsComponent.ApplyEffect(data, GameObject)` | erro em `ReviveBridge.ApplyInjury/ClearInjury` e `ReviveHealth` |
| `IDamageable.ApplyDamage(new DamageInfo(float, DamageType.Physical, GameObject))`; dano enorme mata | erro em `ReviveHealth.Damage` |
| `SanityComponent.Of(p)`, `.Add(float, string)`, `.SetDrain(string, float)`, `.ClearDrain(string)`; `SanitySubtitles.Of(p).Say("", texto, segundos)` | erro em `ReviveBridge.AddSanity/SetSanityDrain/Say` |
| `CameraFx.Register/Unregister` e `ICameraModifier.Contribute(Camera, ref CameraDelta)` com `d.euler` (x = pitch, + para baixo; z = giro) | erro em `CamAdapter`; câmera tomba para o lado oposto: inverta o sinal de `Camera Roll` |
| `InventoryOwner.CountOf(string)`, `RemoveItem(string, int)`, `AddItem(ItemData, int)`; `ItemDatabase.Find(string)` | erro em `KitIdOf/TakeKit/GiveKit` |
| `Interactable` com `DefaultVerb` (protected), `CanInteract`, `GetBlockedReason`, `Interact` (os da Alavanca) | erro em `ReviveInteractable` |
| `GameSession.SetString(string, string)`; `SaveSystem.BeforeSave/AfterLoad`; `SceneDirector.IsBusy`, `SceneDirector.LevelLoaded` (sem argumento, `int`, `string` ou objeto) | erro em `Hook/LevelBusy/SetString`; o estado não vai para o save |

**Descobertas por nome em tempo de execução (reflexão; falha vira aviso no Console, nunca erro de compilação):**

| Premissa | Se falhar |
|---|---|
| Morte: um `bool` `IsDead`/`IsAlive` (campo ou propriedade, em `PlayerContext` ou `StatsComponent`) e/ou vida chegando a 0. Para desfazer: os mesmos indicadores com `set`, ou um método sem parâmetros `Revive/Resurrect/Reanimate/…` | "Não consegui desfazer a morte": o jogador morre normalmente (sem queda) |
| Vida: propriedade `Health/CurrentHealth/Life/VitalValue…` (número ou objeto `Stat` com `Current/Value`) ou método `Get…(StatType.Health)`; escrita por propriedade, objeto `Stat`, `Set…(StatType, float)`, `Add…(StatType, delta)`, `Heal(float)` ou um efeito de cura rápido | sem ler a vida não há queda por vida zero nem vida ao levantar (aviso); sem escrever, ele levanta com a vida que tinha |
| `PlayerContext.AddControlLock(string)` / `RemoveControlLock(string)` (chave `Revive`) | o caído continua podendo se mexer (aviso) |
| Campo de tempo de segurar no `Interactable` (`holdDuration`…) | reanimar é **instantâneo** (aviso) |
| `PlayerInteractor` com um `LayerMask` | a caixa de alcance usa a layer do corpo; se o interactor não a enxerga, ela vai para a layer certa sozinha |
| `GameSession.GetString(string)` | o estado não volta ao carregar (aviso) |
| Opcionais, sem aviso: `HeartbeatComponent.SetThreat`, `BreathingComponent.Gasp`, `DynamicHeadBob.SetConstantTrauma`, `CameraImpulse.Shake`, `NoiseSystem.Emit(pos, raio, NoiseKind.Impact/Custom, origem)`, `PlayerAnimDriver.Raise("Death"/"Revive")` | falta só o tempero (coração, respiração, tremor, barulho, pose) |

### 11.2 Diagnóstico

**F10** (com as teclas de teste) ou **menu `Revive > Revive Diagnostics (Play Mode)` (Play): uma linha por item para cada jogador:

```
[Revive] Diagnóstico de J1 (Player 1)
  vida: lida por propriedade 'Health' = 100 / 100 | escrita: propriedade com set; métodos (StatType, número): …
  morte: indicadores: StatsComponent.IsDead (propriedade) | método para reviver: nenhum | travas: AddControlLock ok, RemoveControlLock ok | agora: vivo
  kit médico: 'kit_medico' está no inventário
  ids que o ItemDatabase conhece: kit_medico
  corpo animado: PlayerAnimDriver ok
  câmera do jogador: ok
  Interactable: tempo de segurar ok (holdDuration)
  eventos do framework: ligados
  estado do módulo: Up
```
Tudo que disser **NÃO ACHEI / NENHUM / NÃO CONSEGUI** é o que precisa de ajuste. A **primeira queda** de cada sessão também escreve uma linha no Console com o que foi feito para desfazer a morte ("indicadores zerados: 1; método do framework: nenhum; religados: 3; vida escrita: sim; depois: vivo").

### 11.3 O que me enviar para ligar tudo com exatidão

Se o diagnóstico mostrar problemas, ou se aparecer um efeito de morte que não some, mande: **`PlayerContext.cs`**, **`StatsComponent.cs`** (e `Stat.cs`) e **`GameOverScreen.cs`**. Com eles eu troco as descobertas por nome por chamadas diretas e ponho o **gancho exato**, que cancela a morte antes de ela acontecer:

```csharp
// no começo do método que trata a morte do jogador no StatsComponent (antes de qualquer efeito de morte):
if (ReviveApi.TryIntercept(this)) return;
```
Isso também resolve o caso de o `GameOverScreen` **contar eventos `Died`** em vez de olhar o estado de cada jogador (nesse caso o game over pode aparecer cedo demais depois de uma reanimação: veja a seção 13).

## 12. Limitações

- **Compilado só no papel** — espere 1 ou 2 rodadas de ajuste.
- **A morte é desfeita depois de acontecer** (até chegar o gancho exato). O que o framework fizer *no instante* da morte e não for um indicador, um componente ligado/desligado ou uma trava (tela de "você morreu", drop de itens, evento `Died` já ouvido por outros scripts, ragdoll) não é desfeito.
- **A câmera do caído só tomba e olha para baixo** (o `CameraDelta` documentado só tem rotação, não posição). O corpo continua sendo a cápsula de pé; a pose deitada vem da animação "Death" do `PlayerAnimDriver`, se o módulo de corpo existir.
- O caído **não olha em volta** (a trava de controle do framework bloqueia tudo) e **não pode se auto-reanimar** com o próprio kit.
- O reanimador só usa o **próprio** kit. Não há carregar o caído, nem rastejar.
- O ferimento é um efeito de status do `StatsComponent`, mais tremor e sanidade nossos: se o `StatsComponent` não tiver como **remover** um efeito, o módulo o sobrescreve com um efeito vazio (funciona, mas o rótulo do efeito pode ficar até o fim do tempo).
- `Reach Box` é um **trigger novo** na raiz do jogador: dentro de um volume de gatilho (diálogo, nível) ele pode "entrar" de novo. Por isso vem desligado e só é criado sozinho quando o `PlayerInteractor` não enxerga a layer do corpo.
- Um jogador que o framework **desativa** (`SetActive(false)`) ao morrer não é visto pelo módulo: use o gancho exato.
- `Min Players` conta os jogadores presentes na cena, e a derrota total só roda com eles.

## 13. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Morre normalmente, sem cair | só 1 jogador na cena; `Enabled` desligado; não há outro jogador de pé; ou o Console diz "Não consegui desfazer a morte…" (seção 11) |
| O game over aparece quando o PRIMEIRO cai | o `GameOverScreen` conta eventos `Died`: mande o arquivo (11.3) |
| Cai, mas continua andando | aviso "PlayerContext não tem AddControlLock": ajuste `ReviveDeath.SetLock` |
| Não aparece "Reanimar" ao mirar no caído | o `PlayerInteractor` não enxerga o corpo: ligue `Reach Box`; confira `holdDuration`/layer no diagnóstico (F10) |
| Reanimar é instantâneo | o `Interactable` não tem o campo `holdDuration` (aviso no Console) |
| Levanta com a vida errada | o diagnóstico mostra como a vida foi lida/escrita; sem escrita ele levanta com 1 de vida |
| O kit nunca é gasto | o id do seu kit não está em `Medkit Item Ids` (menu `Revive > Create Revive Profile`; F10 lista os ids conhecidos) |
| A tela de "você morreu" pisca ao cair | efeito de morte já disparado: instale o gancho exato (11.3) |
| Câmera tomba para o lado errado ou sobe | inverta o sinal de `Camera Roll`/`Camera Pitch` |
| O monstro continua batendo no caído | o perseguidor não achou `ReviveApi` (pasta `Revive` não importada ou o código não compilou) |
| Ferimento não passa / não dá efeito | `StatsComponent.ApplyEffect` aceita o efeito `revive_injury`? Veja se o Console tem erro de compilação |
| O estado não volta ao carregar o save | `GameSession.GetString` ausente (aviso) ou os eventos do framework não ligaram (F10 diz "NÃO ligados") |

## 14. Ideias para melhorar (próximos passos)

- **Auto-reanimação** com o próprio kit (segurar por 8 s, com chance de falhar) para o último caído ainda com o colega longe.
- **Rastejar** em baixa velocidade enquanto caído, e **arrastar/carregar** o colega até um lugar seguro (reaproveitando o `Draggable`).
- **O monstro executa caídos**: um módulo do perseguidor que, ao ver um caído sem ninguém por perto, o finaliza (`ReviveApi.KillFinal`), forçando o grupo a protegê-lo.
- **Limite de quedas por nível** (a terceira vez vira morte definitiva) ou kit que restaura o limite.
- **Teste de habilidade** ao reanimar (acertar o tempo), mais rápido com sanidade alta, mais lento no escuro.
- **Adrenalina/desfibrilador** como itens que levantam o caído com regras próprias (`ReviveApi.Revive`).
- **Sinal de socorro:** o caído emite um apito (barulho maior, mais risco) que mostra a direção para os colegas.
- Integrar com o **diálogo**: falas do caído e do reanimador por `ReviveEvents`.
