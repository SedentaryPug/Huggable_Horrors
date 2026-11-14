# Horror FPS Framework — Props de cenário (esconderijos, gavetas, barris, quebráveis, luzes, armadilhas, espelho, botão duplo)

Módulo de objetos de cenário com função, encaixado nos módulos já documentados (Arquitetura, Itens/Sanidade/Corpo, Diálogos e Animação/Corpo). O **lote 2** acrescentou o **espelho por jogador** (seção 4.7) e o **botão duplo / portas que exigem o grupo junto** (seção 4.8).

> **Aviso importante:** este módulo foi escrito **sem compilar** e **sem ter o código real do framework** à mão, só a documentação. Tudo o que toca o framework ou os módulos anteriores está em **um único arquivo** (`PropsBridge.cs`); se algo não compilar ou não funcionar, mande a mensagem do Console (ver seção 9).

## 1. O que vem no pacote

```
Assets/HorrorFPS/Props/               (runtime)
  PropsBridge.cs        ÚNICO ponto de contato com o framework e com os outros módulos
  PropsInput.cs         botões próprios (sair do esconderijo, olhar de dentro), lidos dos dispositivos DO jogador
  PropSfx.cs            sons gerados por código (clique, pancada, rangido, gaveta, mola, vidro, explosão)
  PropFx.cs             efeitos gerados por código (explosão, fogo)
  PropHealth.cs         base de props que levam dano (recebe golpes/tiros do módulo de itens e explosões)
  PropBlast.cs          explosão em área reutilizável (dano, empurrão, barulho, câmera, sanidade, reação em cadeia)
  OpenablePart.cs       peça que abre/fecha com animação (porta = rotação, gaveta = deslize)
  HidingSpot.cs + HidingOccupant.cs   esconderijo (armário, guarda-roupa, locker...) para 1 ou mais jogadores
  Drawer.cs             gaveta / porta de armário / tampa de baú, com tranca e conteúdo
  ExplosiveBarrel.cs    barril explosivo
  Breakable.cs          caixa, vaso, janela, garrafa
  SwitchableLights.cs + LightSwitch.cs   luzes que ligam/desligam + interruptor
  BearTrap.cs           armadilha de urso
  MirrorSurface.cs + MirrorPhantom.cs   espelho de verdade (um reflexo por jogador) que reage à sanidade de quem olha   [lote 2]
  CoopButton.cs + CoopLock.cs           botão duplo e portas que exigem o grupo junto (apertar juntos, segurar juntos, todos perto)   [lote 2]
(a bancada de teste `PropsDemoBuilder*`, antigo menu 17, foi removida junto com a cena Showcase; está no histórico do Git, no commit anterior a "Remove a cena Showcase")
```

Requisitos: os mesmos do framework (Unity 2022.3 LTS ou Unity 6, Input System, uGUI). Namespace `HorrorFPS`. Sem `.asmdef`. Depende dos módulos de **itens/sanidade/corpo** (inventário, `NoiseSystem`, `CameraImpulse`, `HeartbeatComponent`...). O módulo de **animação** é opcional (só é chamado por reflexão).

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` para o projeto e espere compilar.
2. Monte uma cena de teste com os props a partir de `Add Component > HorrorFPS > Props` (a bancada pronta que o antigo menu 17 criava foi removida). Ela criava `[Props Demo]` na frente da vista do editor: dois guarda-roupas, uma cômoda de 3 gavetas (a 3ª trancada), 4 barris, 2 caixas, uma mesa com vaso, um interruptor com lâmpada e uma armadilha de urso. Mais adiante no eixo X (lote 2): um **espelho com dois vultos**, uma **porta de botão duplo** (dois pedestais a 12 m um do outro) e um **portão de proximidade**. Tudo de primitivas.
3. Dê Play:
   - **Guarda-roupa**: Interagir → você entra, a porta fecha, o olhar é limitado (mouse/analógico direito), a respiração fica presa. **E / X / Backspace / B** sai. O de 2 vagas aceita dois jogadores.
   - **Gavetas**: a 1ª e a 2ª abrem e passam o item direto para a mochila (ids `pilhas` e `bandagem`, dos itens de exemplo). A 3ª só destranca com a variável `tem_chave` ligada.
   - **Barris**: atire ou bata (módulo de itens). Abaixo de 50% de vida o barril pega fogo e explode em ~3 s; os vizinhos acendem com atraso proporcional à distância.
   - **Caixas e vaso**: quebram com golpes, tiros, explosão ou batida forte (arrastar rápido, jogar).
   - **Interruptor**: liga/desliga a lâmpada (com piscada). **Armadilha**: pise (dano, lentidão, barulho) ou mire e **Desarmar**.
   - **Espelho**: fique de frente, a 2 m: você vê o reflexo do cômodo e do seu corpo (com a cabeça). Com a sanidade alta é um espelho comum. Baixe a sanidade (zona de sanidade, item, o debug do módulo de sanidade) e o reflexo muda de cor, se atrasa em relação a você e, abaixo de 90% e depois de 50%, aparecem **dois vultos atrás de você, só no reflexo** (a sanidade escorre enquanto você os encara). No split-screen cada jogador vê o *próprio* reflexo, com a *própria* sanidade.
   - **Porta de botão duplo**: dois pedestais longe um do outro. Um jogador aperta um (**Apertar**, lâmpada amarela) e o outro, **outro jogador**, aperta o segundo em até 2 s: lâmpadas verdes e a porta abre (fica aberta e vai para o save). Sozinho, no teste: "Precisa ser outro jogador" (a regra relaxa só se houver menos jogadores vivos do que botões).
   - **Portão de proximidade**: o círculo verde no chão tem 5 m de raio. A porta só destrava quando **todos** os jogadores vivos estão dentro; quem chegou antes lê "Esperando os outros jogadores (1/2)". Fica aberta 8 s depois da última vez em que o grupo estava junto.

> Se o jogador não "enxerga" um prop, ele está numa layer fora da máscara de interação do framework (`PlayerInteractor.mask`).

## 3. Como o módulo se encaixa

- Props com interação herdam de **`Interactable`** (dica na tela, bloqueio com motivo). Props que levam dano herdam de **`PropHealth`**, que implementa `IItemHittable`: o `EquipmentController` já acerta qualquer prop sem configurar nada.
- **Save**: ponha `SaveableEntity` no mesmo objeto. `Drawer`, `ExplosiveBarrel`, `BearTrap`, `SwitchableLights` e `CoopLock` implementam `ISaveable` (aberta/destrancada/o que sobrou, explodido/vida, estado da armadilha, ligado/desligado, puzzle resolvido e chaves ligadas). O espelho não guarda nada. Caixas e barris empurráveis: some `SaveableTransform`. O que for **destruído** (caixa quebrada, barril sem casca) continua destruído sozinho.
- **IA**: tudo que faz barulho usa o `NoiseSystem` do módulo de itens (abrir gaveta, quebrar vidro, explosão, mola). Esconderijos têm API própria (seção 4.1).
- **Sem assets**: sons e efeitos são gerados por código. Cada componente tem campos para clipes/prefabs seus; vazio = gerado.

## 4. Props

### 4.1 `HidingSpot` — esconderijo
Guarda-roupa, armário, locker, barril, caixote. Ponha no objeto raiz (precisa de colisor em algum filho para a mira) e preencha:

| Campo | Uso |
|---|---|
| `Hide Points` | um `Transform` por vaga (2 pontos = 2 jogadores cabem). É onde fica a **raiz** do jogador (os pés) e para onde ele olha (a rotação do ponto). Vazio = o próprio objeto |
| `Exit Point` / `Exit Distance` | onde o jogador reaparece (vazio = `Exit Distance` m à frente do objeto, olhando para fora) |
| `Doors` | `OpenablePart`s que abrem ao entrar/sair e fecham em seguida (`Keep Doors Open` deixa abertas) |
| `Look Yaw/Pitch Limit` | quanto dá para olhar de dentro (graus) |
| `Hold Breath` / `Calm Bpm` | segura a respiração (silencia o corpo) e acalma o coração ao entrar |
| `Ignore Collisions While Inside` | o jogador atravessa os colisores do móvel enquanto está dentro (evita ser expulso por paredes finas) |
| `Find Chance`, `Scare On Found`, `Sanity Loss On Found` | ser descoberto por um monstro |

**Para a IA** (monstro procurando):
```csharp
HidingSpot.IsHidden(jogador);                 // o jogador está escondido?
HidingSpot.SpotOf(jogador);                   // em qual esconderijo
bool achou = spot.Inspect(transform);         // olha dentro: chance base + coração acelerado; se achar, joga o jogador para fora com susto
spot.Inspect(transform, 0.3f);                // com bônus de chance (monstro farejador)
spot.EjectAll();                              // arrombou: tira todos
HidingSpot.Entered += (spot, jogador) => ...; // também Exited e Discovered
```
Um jogador escondido continua sendo alvo do `NoiseSystem` (o coração só faz barulho acima de 55% de intensidade). Se o esconderijo é desativado/destruído com alguém dentro, o jogador é destravado.

### 4.2 `Drawer` + `OpenablePart` — gavetas, portas de armário, tampas
`OpenablePart` vai no objeto que se move: **Slide** (gaveta, desloca `Open Offset`) ou **Rotate** (porta, gira `Open Euler`). Em portas, use um **objeto vazio na dobradiça** como pivô e a porta como filho. `Drawer` aponta para a peça (vazio = acha sozinho) e acrescenta:
- **Tranca**: `Unlock Variable` (variável da partida) e/ou `Required Item Id` (+ `Consume Key`). Destrancou, fica destrancada (vai para o save).
- **Conteúdo** (`Loot`: `itemId` + `count`, ids de `ItemData` em `Resources`): ao abrir vai direto para a mochila de quem abriu. O que não coube fica lá dentro (avisa "Mochila cheia") e é tentado de novo na próxima abertura.
- **Barulho** ao abrir (`Open Noise`, m).

### 4.3 `ExplosiveBarrel` + `PropBlast` — barril explosivo
Cilindro com `Rigidbody` + `ExplosiveBarrel`.
- **Vida** (`Max Health`, na base `PropHealth`). Abaixo de `Ignite Below Health` (50%) pega fogo (chama + luz tremida) e explode em `Burn Seconds` (3 s ± jitter); a 0 explode na hora.
- **`Blast`** (`BlastSettings`): `radius`, `damage` (no centro, cai com a distância, parede no caminho reduz por `occlusion`), `velocity` (empurrão em m/s, independe da massa), `playerDamageMultiplier`, `noiseRadius`, `cameraPower/cameraRadius`, `sanityLoss`, `chainDelayPerMeter`.
- Depois: `Leave Shell` deixa a casca escura (e salva), ou destrói o objeto. `Explosion Prefab` e `Explosion Sound` trocam o efeito gerado por código.
- Por código: `barril.Ignite(2f)`, `barril.Explode()`. Para outros explosivos (botijão, granada): `PropBlast.Detonate(posicao, BlastSettings.Default, gameObject)`; implemente `IBlastReceiver` para reagir a explosões alheias.

### 4.4 `Breakable` — caixa, vaso, janela
`Rigidbody` opcional + `Breakable`. Quebra por vida zero (golpes, tiros, explosões) **ou por impacto**: `Impact Break Speed` (m/s relativos; 0 desliga). Vira `Shard Count` estilhaços com física (somem em `Shard Lifetime`), faz barulho, toca som (`Glass` liga o som/forma de vidro) e instancia `Spawn On Break` (ponha aqui o prefab de um item no chão). `onBroken` é um `UnityEvent`.

### 4.5 `SwitchableLights` + `LightSwitch` — luzes
`SwitchableLights` (no lustre/abajur) controla `Lights` e `Emissive Renderers`; `Power Variable` (ex.: `energia_ligada`) faz as luzes só acenderem com energia (e reagirem sozinhas quando a variável muda: bom para quadro de fusíveis e cutscenes). `LightSwitch` é o interruptor (aceita vários conjuntos). Por código: `lights.SetOn(false)`, `lights.Toggle()`, `lights.IsLit`. Luzes de tempo real convivem com o `SanityLight`.

### 4.6 `BearTrap` — armadilha
Objeto com colisor **Trigger** (o próprio objeto) + `BearTrap`. Quem pisa leva `Damage`, fica `Trap Seconds` com `Speed While Trapped` (status "Preso" no `StatsComponent`), gera barulho (`Noise Radius`), sacode a câmera e dá susto/perda de sanidade. `Jaw A/B` (opcional) fecham com animação. Mirar nela dá **Desarmar** (e **Rearmar** se `Rearmable`). `Trigger By Others` inclui monstros (qualquer `IDamageable`).

### 4.7 `MirrorSurface` + `MirrorPhantom` — espelho que reage à sanidade
Objeto vazio na parede com `MirrorSurface`. **A frente é o +Z do objeto** (o gizmo azul mostra), a escala fica em (1,1,1) e o tamanho do vidro é o campo `Size` (m): o componente cria o quad sozinho, sem malha nem material.

É um espelho de verdade (reflexo planar) **desenhado separadamente para cada jogador**: no split-screen cada um vê o próprio reflexo, o outro jogador aparece nele, e o corpo de quem olha aparece **inteiro, com a cabeça** (a câmera do reflexo não é filha de nenhum jogador, então o módulo de corpo a trata como câmera "de fora"). O reflexo só é renderizado para quem está olhando de frente, a menos de `Max Distance` e com o espelho no campo de visão.

| Campo | Uso |
|---|---|
| `Size`, `Surface Material` | tamanho do vidro; material próprio (precisa de textura em `_MainTex`/`_BaseMap`). Vazio = material sem luz gerado por código |
| `Resolution`, `Render Every N Frames`, `Reflect Mask`, `Max Distance` | custo e o que aparece no reflexo |
| `Tint By Insanity` | gradiente de cor do reflexo: 0 = lúcido … 1 = quebrado (padrão: branco → esverdeado → avermelhado → quase preto) |
| `Lag Start Insanity`, `Max Lag Seconds` | a partir desta insanidade o reflexo **se atrasa**: o ponto de vista refletido persegue o seu olho com atraso (até `Max Lag Seconds`) e o reflexo parece "outra pessoa" |
| `Blackout Below`, `Blackout Chance` | abaixo desta sanidade o espelho apaga por instantes |
| `Phantoms` | lista de `MirrorPhantom`: `Root` (objeto no cômodo, **sem colisores**), `Visible Below` (sanidade de quem olha) e `Flicker` (chance de falhar por um instante). O objeto é invisível para todas as câmeras normais; só aparece **dentro do reflexo**, e só para quem está abaixo do limite |
| `Phantom Drain Per Second`, `Drain Distance` | enquanto há um vulto à vista no reflexo e você está a menos de `Drain Distance`, a sanidade escorre (fonte contínua nomeada; cessa ao desviar o olhar ou se afastar) |

Sanidade usada: `SanityComponent.Normalized` de quem olha (1 = lúcido). Quem está lúcido só vê um espelho.

### 4.8 `CoopButton` + `CoopLock` — botão duplo e portas que exigem o grupo junto
Dois componentes. **`CoopButton`** é o botão físico (interagível; lâmpada vermelha = parado, amarela = apertado/esperando, verde = grupo concluído; êmbolo opcional que afunda). Sozinho não faz nada. **`CoopLock`** é a regra: todos os botões do puzzle apontam para o mesmo `CoopLock` (ou ficam como filhos dele; o gizmo amarelo liga o lock a cada botão).

Modos (`Mode`):
- **Timed** (padrão): todos os botões apertados dentro de `Window Seconds` (2 s), por **jogadores diferentes**. Quem aperta e não recebe resposta a tempo lê "Tarde demais! Apertem juntos".
- **Hold**: todos **segurados** (E / X mantido) ao mesmo tempo por `Hold Seconds`; soltar, morrer ou se afastar mais de `Hold Range` do botão cancela. Um botão em uso por outro jogador mostra "Em uso".
- **Toggle**: cada botão liga/desliga (alavanca); ativa quando todos estão ligados, por jogadores diferentes.

| Campo do `CoopLock` | Uso |
|---|---|
| `Buttons` | os botões (vazio = os `CoopButton` filhos deste objeto) |
| `Require Distinct Players` | cada botão precisa de um jogador diferente |
| `Short Handed Fallback`, `Short Handed Grace Seconds` | se há **menos jogadores vivos do que botões** (alguém morreu, ou teste solo), a regra relaxa: o mesmo jogador vale para vários botões e cada aperto dura 12 s, dá para correr de um botão ao outro |
| `Proximity Radius`, `Proximity Center`, `Max Height Difference` | 0 = desligado. Senão, **todos os jogadores vivos** precisam estar a até esse raio (distância horizontal; andar de cima não conta como perto). Funciona **sem nenhum botão**: vira um portão de proximidade que avisa "Esperando os outros jogadores (1/2)" |
| `Latch Seconds` | `-1` = fica ativo para sempre (e vai para o save); `0` = só enquanto a condição vale; `N` = continua ativo por N s depois (use 5–10 s em portas, para o grupo passar) |
| `Output Variable` | variável da partida mantida ligada enquanto ativo. Use como `Door.unlockVariable` ou `LevelDoor.requiredVariable` |
| `Doors`, `Close Doors When Inactive` | `OpenablePart`s abertas direto pelo lock (sem depender da `Door` do framework) |
| `On Changed (bool)`, `On Activated` | `UnityEvent`s (luz, som, spawn de monstro...) |
| `Status Lamps`, cores, `Success Sound` | luzes do próprio lock; vazio = som gerado |
| textos (`Hint …`) | todas as mensagens ao jogador, em português, editáveis |

Por código: `lock.IsActive`, `lock.IsSolved`, `lock.Activate()` (força a solução), `lock.ResetLock()` (puzzle que reinicia). Cada aperto faz barulho para a IA (`Press Noise` do botão): um puzzle em modo Hold com monstro por perto fica tenso de graça.

> **Troca de nível com todos:** o `LevelTrigger` do framework já tem `Require All Players`. O `CoopLock` serve para portas **dentro** do nível e para exigir *proximidade* de verdade (e não só presença no volume).

## 5. Receitas

**Esconderijo que o monstro revista** — no seu script de IA, ao chegar perto de um `HidingSpot` onde viu o jogador entrar: `spot.Inspect(transform, 0.2f)`.

**Armário que abre só com a chave** — `Drawer` com `Required Item Id = chave_armario` (ou `Unlock Variable = tem_chave`).

**Quadro de fusíveis** — `SwitchableLights` com `Power Variable = energia_ligada` em todas as lâmpadas; uma ação de diálogo/`Pickup`/cutscene liga a variável e a casa acende (com piscada).

**Barril como armadilha** — barris perto de um corredor + monstro: o jogador atira num deles e a explosão arrasta os outros.

**Item escondido num vaso** — `Breakable` com um prefab de item em `Spawn On Break` (o prefab do `WorldItem`, sem `SaveableEntity`).

**Porta que exige dois jogadores** — dois `CoopButton` longe um do outro, um `CoopLock` (Timed) ligando os dois e `Doors` apontando para a porta (`OpenablePart`). Para usar a `Door` do framework no lugar: `Output Variable = porta_x` e `Door.unlockVariable = porta_x`.

**Grupo junto antes de passar** — `CoopLock` **sem botões**, `Proximity Radius` de 4–6 m com `Proximity Center` diante da porta, `Output Variable = grupo_reunido`, `Latch Seconds = 8` e `Door.unlockVariable` (ou `LevelDoor.requiredVariable`) = `grupo_reunido`. Para exigir também os dois botões, preencha os botões **no mesmo lock**: só vale se todos estiverem perto.

**Puzzle sob pressão** — modo Hold, três botões e o monstro rondando: cada jogador segura um, ninguém pode soltar, e o barulho do aperto chama a IA.

**Espelho que assusta** — `MirrorSurface` + um vulto (`MirrorPhantom`) parado na porta, atrás de onde o jogador fica, com `Visible Below = 0.5`. Os dois jogadores veem o mesmo cômodo, mas só quem está mal vê o vulto.

**"Olhar-se no espelho"** — no mesmo objeto do `MirrorSurface`, um `BoxCollider` fino (tamanho do vidro) e um `SanityExaminable` do módulo de sanidade, com falas diferentes por sanidade.

## 6. Controles novos

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Sair do esconderijo | E ou Backspace | X ou B |
| Olhar de dentro | Mouse | Analógico direito |
| Segurar um `CoopButton` em modo Hold | E mantido | X mantido |

São lidos direto dos dispositivos do jogador (não aparecem no remapeamento do framework, como as ações de itens e diálogos).

## 7. Detalhes que valem saber

- **Esconder = travar**: usa a trava de controles do framework com a chave `"Hiding"` (a mesma de cutscenes/diálogos), então o jogador não anda, não pula e não interage enquanto está dentro. O olhar de dentro é um deslocamento somado pelo `CameraFx` (a câmera do controller não é movida).
- **Pausa**: a leitura de entrada e as animações respeitam `Time.timeScale`.
- **Áudio no split-screen**: o som dos props usa `AudioSource.PlayClipAtPoint`; só existe um `AudioListener` ativo (do jogador 1), como nos outros módulos. O barulho para a IA é separado e funciona por jogador.
- **Em build**: efeitos de partícula usam `Shader.Find` (URP Particles/Unlit, depois alternativas). Se a explosão sair invisível numa build, inclua o shader em *Always Included Shaders* ou atribua `Explosion Prefab`.
- **Objetos com `Interactable`** não usam `Awake/Start/Update` próprios (para não esconder os da base). A lógica que precisa de ciclo de vida fica em componentes à parte (`SwitchableLights`, `HidingOccupant`, `OpenablePart`, `CoopLock`).
- **Espelho e desempenho**: cada espelho visível custa **um render extra por jogador que o olha**. Reduza com `Resolution` (256–512), `Render Every N Frames` (2–3) e `Max Distance`. O reflexo é feito em `LateUpdate` por uma câmera oculta e a textura de cada jogador é trocada por câmera (`Camera.onPreCull` no Built-in, `beginCameraRendering` no URP/HDRP), sem shader especial.
- **Hold lê E / X direto do dispositivo** (como a saída do esconderijo): se você remapear "Interagir" nas opções do framework, o Hold continua em E / X.
- **Lista de jogadores**: `PropsBridge.Players` reescaneia a cena a cada 0,5 s; quem entra depois (split-screen) é visto em até meio segundo. O `CoopLock` conta jogadores vivos/perto a cada 0,15 s.
- **Pausa**: o tempo do `CoopLock` usa `Time.time`, então a janela de 2 s e os 8 s de porta aberta congelam junto com o jogo.

## 8. Limitações conhecidas

- **Compilado só no papel** — espere 1 ou 2 rodadas de ajuste.
- **Sem esconderijo "deitado"** (embaixo da cama): a câmera e a cápsula do jogador continuam em pé. Funciona para móveis onde um adulto cabe em pé.
- O ponto de esconderijo é onde fica a **raiz** do jogador: se o seu prefab tem a raiz no centro da cápsula, suba os `Hide Points` (ou use `Height Offset`).
- O jogador escondido continua visível para os outros jogadores se o móvel não tem porta que fecha.
- Dano de explosão é `DamageType.Physical` (usa só o que o framework documenta).
- Gaveta com conteúdo: não instancia item no chão; entrega direto à mochila. Para itens soltos use `Spawn On Break` em outros props ou um `WorldItem` na cena.
- Explosão não destrói a geometria do cenário; barril sem `SaveableEntity` volta intacto ao carregar um save.
- **Espelho**: só plano. O reflexo é do mundo "são": as alucinações do `SanityVisibility`/`SanityLight` **não** aparecem nele (é para isso que existem os vultos). Um espelho dentro do reflexo de outro, e câmeras que não são de jogador (Scene View, cutscene), veem o vidro **preto**. Sem pós-processamento no reflexo. **HDRP não testado.** Os vultos ficam parados (mova o `Root` por script/Animator se quiser que andem).
- **`CoopLock`**: "perto" é a distância horizontal entre as raízes dos jogadores (com tolerância de altura). Se o `PlayerContext` não expõe `IsDead`/`IsAlive`, jogadores mortos contam como vivos (aviso no Console). `On Changed`/`On Activated` **não** disparam ao carregar um save; o que é restaurado é `Output Variable` e `Doors`. No modo Toggle, "jogador diferente" considera quem apertou o botão por último.

## 9. Ajustes no framework (premissas ainda não conferidas contra o código real)

Tudo em `PropsBridge.cs`, exceto onde indicado.

| Premissa | Se falhar |
|---|---|
| `PlayerContext.AddControlLock(string)` / `RemoveControlLock(string)` (chamados por **reflexão**, chave `"Hiding"`) | O jogador anda dentro do esconderijo. O Console avisa; ajuste `SetLock` |
| Raiz do jogador com `CharacterController`; yaw guardado em um campo float do `FirstPersonController` com nome comum (`yaw`, `_yaw`...) | Ao sair do esconderijo o olhar "estala": ajuste `Teleport` |
| `NoiseSystem.Emit(Vector3, float, NoiseKind, Transform)` e `NoiseKind.Impact/Explosion` | Erro de compilação em `PropsBridge.Noise` (se o 4º parâmetro não for `Transform`/`Component`) |
| `CameraImpulse.Explosion(pos, poder, raio, raioDoBarulho, sanidadePerdida)` e `CameraImpulse.Shake(pos, raio, trauma)` | Erro em `ExplosionCamera`/`Shake` |
| `IItemHittable.OnItemHit(ItemHit)` retornando `void`; campos do `ItemHit` lidos por reflexão (`damage`, `point`, `direction`, `attacker`) | Erro em `PropHealth`; ou dano sempre 1 (ajuste os nomes em `ReadHit`) |
| `IDamageable.ApplyDamage(DamageInfo)`, `new DamageInfo(float, DamageType.Physical, GameObject)` | Erro em `Damage` e em `PropBlast` |
| `StatusEffectData` (`id, displayName, duration, modifiers, overTime`) + `StatsComponent.ApplyEffect(data, GameObject)` | Armadilha sem efeito de lentidão (erro em `Immobilize`) |
| `InventoryOwner.AddItem(ItemData, int)` (devolve quantas entraram), `RemoveItem(string, int)`, `CountOf(string)`; `ItemDatabase.Find(string)` | Gavetas/tranca por item |
| `SanityComponent.Of(p).Add(float, string)`, `SanitySubtitles.Of(p).Say("", texto, segundos)` | Perda de sanidade/dicas na tela |
| `HeartbeatComponent.Boost/Scare/Intensity`, `BreathingComponent.SetHoldingBreath(bool)` | Respiração/coração dentro do esconderijo |
| `GameSession.GetBool(string)` e o evento `GameSession.VariableChanged` (`Action<string>`) — em `SwitchableLights` e `PropsBridge.GetBool` | Energia por variável |
| `CameraFx.Register/Unregister` e `ICameraModifier.Contribute(Camera, ref CameraDelta)` com `d.euler` (x = pitch, + para baixo; y = yaw) — em `HidingOccupant` | O olhar de dentro vai para o lado oposto: inverta o sinal em `PropsInput.Look` |
| `Interactable` com os mesmos membros do exemplo da Alavanca (`DefaultVerb`, `CanInteract`, `GetBlockedReason`, `Interact`) e `ISaveable` (`SaveKey`, `CaptureState`, `RestoreState`) | Erro de compilação em `HidingSpot`, `Drawer`, `LightSwitch`, `BearTrap` |
| A câmera do jogador é **filha** do objeto com `PlayerContext` (`PropsBridge.CameraOf/PlayerOf`) | O olhar de dentro não aparece; ajuste `HidingOccupant.Contribute`. No espelho, o jogador vê o vidro **preto** |

**Lote 2 (espelho e botão duplo):**

| Premissa | Se falhar |
|---|---|
| `SanityComponent.Normalized` (0..1, 1 = lúcido), `SetDrain(string, float)` (pontos/s; **negativo drena**) e `ClearDrain(string)` — `PropsBridge.Sanity01/SanityDrain` | Erro de compilação nessas duas funções; se o vulto *curar* em vez de drenar, o sinal está invertido: ajuste em `MirrorSurface.SetDrain` |
| `GameSession.SetBool(string, bool)` — `PropsBridge.SetBool` | `Output Variable` não liga (aviso no Console); use `Doors` ou `On Changed` |
| `Door.unlockVariable` / `LevelDoor.requiredVariable` consultam a variável **no momento da interação** | Porta do framework continua trancada: confira o nome da variável e use `Latch Seconds` > 0 (ou -1) para a variável não voltar a falso antes da hora |
| `PlayerContext` (ou o `StatsComponent`) expõe `IsDead` ou `IsAlive` (lido por **reflexão**) — `PropsBridge.IsAlive` | Mortos contam como vivos nas regras de proximidade e de "poucos jogadores" (aviso no Console). Ajuste `IsAlive` |
| `FindObjectsByType<PlayerContext>` acha todos os jogadores ativos — `PropsBridge.Players` | Espelho e botões não enxergam ninguém |
| URP/Built-in: `Camera.Render()` em `LateUpdate` com projeção personalizada, e `RenderPipelineManager.beginCameraRendering` para trocar a textura por câmera | Reflexo preto, parado ou igual para todos os jogadores. **HDRP não testado** |
| `Shader.Find("Universal Render Pipeline/Unlit")` (ou `Sprites/Default`) disponível na build | Espelho rosa/invisível na build: atribua `Surface Material` ou inclua o shader em *Always Included Shaders* |
| O `BodyVisibility` trata câmeras fora do `PlayerContext` como câmeras "de fora" (mostram o corpo inteiro) | O dono não vê a própria cabeça no reflexo: nenhum ajuste no espelho, é regra do módulo de corpo |

## 10. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Não consigo interagir com o prop | Layer fora da máscara do `PlayerInteractor`; prop sem colisor; `CanInteract` falso (gaveta trancada/em movimento, esconderijo cheio) |
| Entro no armário mas ainda ando | `SetLock` não achou o método de trava (aviso no Console) |
| Fico no chão/afundado ao entrar | `Hide Point` na altura errada: use `Height Offset` |
| O olhar estala ao sair do armário | O campo de yaw do controller tem outro nome (aviso no Console) |
| Barril não explode com tiro | Confira se o tiro chega (máscara `hitMask` do `EquipmentController`), se `Max Health` não é enorme e se o barril tem colisor |
| Explosão sem fumaça/fogo na build | Shader de partícula fora da build (seção 7) |
| Gaveta não entrega o item | Id fora de uma pasta `Resources` (aviso no Console) |
| Lâmpada não acende | `Power Variable` preenchida e desligada; ou a luz não está em `Lights` |
| Armadilha não dispara | O colisor do objeto precisa ser **Trigger**; quem pisa precisa de `CharacterController`/`Rigidbody` |
| Estado não volta ao carregar | Falta `SaveableEntity` no mesmo objeto (gaveta/barril/armadilha/lâmpada/`CoopLock`) |
| Espelho todo preto | Câmera que não é de jogador (Scene View, cutscene); câmera do jogador que não é filha do `PlayerContext`; você está fora de `Max Distance` ou atrás do espelho; `Surface Material` próprio sem textura em `_MainTex`/`_BaseMap` |
| Espelho rosa na build | Shader do material gerado fora da build (seção 9, lote 2) |
| Reflexo invertido ou deslocado | A frente do objeto não é o +Z (veja o gizmo azul); escala negativa; escala diferente de (1,1,1) com `Size` já ajustado |
| O vulto não aparece | A sanidade de quem olha ainda está acima de `Visible Below`; o vulto está *atrás* do espelho (precisa estar na frente dele); a layer do vulto está fora de `Reflect Mask`; ele tem colisor (vira obstáculo invisível) |
| `CoopButton` aperta e nada acontece | Botão sem `CoopLock` (gizmo amarelo ausente); `Require Distinct Players` com um jogador só e `Short Handed Fallback` desligado; `Window Seconds` curto demais; botão em outro `CoopLock` |
| O Hold solta sozinho | O jogador não está segurando E / X no dispositivo dele, ou está a mais de `Hold Range` do botão |
| Porta do framework não abre com o `CoopLock` | `Door.unlockVariable` diferente de `Output Variable`; com `Latch Seconds = 0` a variável volta a falso logo depois |
| "Esperando os outros" nunca passa | Um jogador fora do raio ou em outro andar (`Max Height Difference`); veja o círculo verde do gizmo; jogador morto mas contado como vivo (seção 9) |

## 11. Próximo lote (sugestões)

Porta trancada/arrombável com tranca e barulho, cofre com combinação, quadro de fusíveis/gerador, rádio/TV que liga sozinha (atrai a IA), ventilação por onde se engatinha, cadeira/prateleira que cai ao puxar, alçapão/escotilha com `LevelDoor`, caixa de música, sino/corda de alarme, vidro que racha antes de quebrar.

Ideias de co-op na mesma linha do botão duplo: alavancas que precisam ser puxadas **em ordem** (por jogadores diferentes), placa de pressão que exige o peso de dois, elevador que só sobe com todos dentro, um segurando a ponte/grade enquanto o outro passa.
