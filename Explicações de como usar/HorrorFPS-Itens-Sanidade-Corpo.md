# Itens, Inventário, Sanidade e Corpo — guia

*Complementa o documento de arquitetura do framework (`HorrorFPS-Arquitetura.md`): ele descreve a base (input, movimento, interação, status, split-screen, cenas, save, cutscenes); este descreve os módulos novos que se apoiam nela.*

Módulos novos do Horror FPS Framework: **itens e dois tipos de inventário (lista e grade de formato livre)**, **sanidade individual por jogador** e **corpo** (batimento, respiração, head bob dinâmico, barulho que expõe a posição).
Tudo é C# puro, a UI é criada por código e **nada precisa ser configurado** para funcionar: ao dar Play, todo jogador ganha os módulos que faltarem.

> **Código escrito sem compilar no Unity** (igual ao resto do framework). Foi conferido por um verificador estático (delimitadores, `using`, um componente por arquivo, membros e número de argumentos das chamadas entre arquivos, `override`/abstratos/interfaces, acesso a membros privados), mas se aparecer algum erro na primeira importação, mande a mensagem do Console.

---

## 1. Instalação em 3 passos

1. Copie a pasta `Assets/HorrorFPS` por cima da existente e espere compilar. Só entram **arquivos novos** (em `Items`, `Inventory`, `Sanity`, `Body`, `Input`, `UI/Inventory`, `Core`, `Editor` e `Docs`): nenhum arquivo do framework base é alterado ou sobrescrito.
2. Menu **Tools > Horror FPS > 5. Create Sample Items** — cria 13 itens de exemplo em `Assets/HorrorFPS/Resources/HorrorFPS/Items` (formas variadas, descrições que mudam pela sanidade, ações com efeitos, modelos de inspeção).
3. Menu **Tools > Horror FPS > 6. Create Demo Props In Open Scene** — monta numa cena aberta uma bancada de testes (baú em grade, itens no chão, zona de sanidade, retrato que descreve diferente, barril que explode, "ouvinte" que vira para o barulho).

Opcional: **Tools > Horror FPS > 4. Add Item, Sanity and Body modules to Player Prefab** grava os módulos no prefab do jogador (para ajustar valores no Inspector). Sem isso, o `PlayerModules` instala tudo sozinho em cada `PlayerContext` que aparecer (inclusive os criados pelo `LocalPlayerManager` em tempo de execução).
Para desligar a instalação automática: `PlayerModules.AutoInstall = false` ou o símbolo de compilação `HORRORFPS_NO_AUTOINSTALL`.

Requisitos: os mesmos do framework (Unity 2022.3 LTS ou Unity 6, Input System, uGUI). Sem `.asmdef`, como o resto.

### Estrutura

```
Assets/HorrorFPS
├─ Core/       FrameworkBridge (ÚNICO ponto que toca APIs do framework base) · HorrorUtil · CameraHook · CameraFx
│              PlayerModules + PlayerModulesRunner (instalação automática)
├─ Items/      ItemData · ItemShape · ItemInstance · ItemCondition/ItemTextSet · ItemEffect · ItemDatabase · InspectHotspot
├─ Inventory/  ItemContainer → ListContainer / GridContainer · ItemTransfer · InventoryOwner · ContainerInteractable
│              WorldItem · DropRegistry · Hotbar · ItemEvents
├─ Input/      ItemInput (ações próprias do inventário, criadas por código)
├─ UI/Inventory/ PlayerOverlay (camada de UI por jogador) · InventoryScreen · GridView · ListView · InspectPanel/InspectStudio · HotbarHUD
├─ Sanity/     SanityComponent · SanityProfile · SanityEvents/SanityAware · SanityString/SanityText · SanityVisibility · SanityLight
│              SanityFX · SanityEffects · SanityAudio · SanityZone · SanitySubtitles/SanityLine · SanityExaminable · SanityDialogueStep
├─ Body/       PlayerMotionProbe · HeartbeatComponent · BreathingComponent · DynamicHeadBob · DamageReaction
│              CameraImpulse/CameraImpulseSource/CameraImpulseInteractable · NoiseSystem/NoiseListener · PlayerAudio/ProceduralAudio
└─ Editor/     ItemShapeDrawer (grade clicável da forma) · menus 4, 5 e 6
```

---

## 2. Controles novos

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Abrir / fechar inventário | Tab ou I | Y |
| Pegar / soltar item | Botão esquerdo | A |
| Menu do item | Botão direito | X |
| Girar o item na mão | R | RB |
| Inspecionar o item sob o cursor | F | LB |
| Jogar fora o item sob o cursor | G | — (use o menu do item) |
| Organizar a grade (junta pilhas e reempacota) | O | D-pad para cima |
| Voltar / devolver à origem | Backspace | B |
| Atalho rápido 1–9 | Teclas 1 a 9 | D-pad ← / → troca, LB usa |
| **Segurar a respiração** | H | R3 (clique do analógico direito) |

O cursor do inventário é virtual (mouse ou analógico esquerdo); o analógico direito gira/aproxima na inspeção. Cada jogador do split-screen tem a **própria tela de inventário**, na própria metade, controlada só pelos dispositivos dele. **O mundo não pausa** com o inventário aberto (em jogo local não dá para pausar os outros) — o jogador fica parado e vulnerável, o que faz parte da tensão.

> As ações novas não aparecem no painel de remapeamento do framework: estão em `ItemInput.cs` (um `Button(...)` por ação). Para torná-las remapeáveis, siga a receita do documento de arquitetura ("adicionar uma action").

---

## 3. Itens

### 3.1 `ItemData` (Create > HorrorFPS > Item)

Dentro de uma pasta **Resources** (o save guarda só o `id` e o `ItemDatabase` carrega tudo de `Resources`).

| Campo | Para quê |
|---|---|
| `id` | Id estável (vai para o save). Vazio = nome do asset. |
| `displayName`, `description` | `ItemTextSet`: texto base + **variantes por situação** (ver 3.3). |
| `category` | Flags: Consumable, Key, Weapon, Ammo, Document, Tool, Quest, Misc, Junk. Contêineres podem aceitar só certas categorias. |
| `icon`, `color` | Sem ícone, a célula é pintada com a cor e mostra o nome. |
| `shape`, `allowRotation` | Forma na grade (3.2). |
| `weight`, `listSlots` | Peso e espaços ocupados nas listas com limite. |
| `stackable`, `maxStack` | Pilhas (munição, pilhas, comprimidos). |
| `worldPrefab` | Modelo ao jogar fora. Vazio = caixinha colorida. **Não ponha `SaveableEntity` nele** (o `DropRegistry` cuida do que é jogado fora). |
| `inspectPrefab` | Modelo da inspeção 3D (vazio = `worldPrefab`; sem nenhum, um bloco com a forma do item). |
| `canDrop`, `canInspect` | Itens de missão costumam ter `canDrop` desligado. |
| `equipSlot` | Ex.: `mao`, `lanterna`. Vazio = não equipável. Equipar só **marca** o item no slot e avisa (`ItemEvents.Equipped`); quem usa a lanterna/arma é o seu código. |
| `actions` | Ações do menu (Usar, Ler, Disparar...). A primeira é a principal. |

### 3.2 Forma na grade (`ItemShape`)

Escreva o desenho: `#` = célula ocupada, `.` = vazia, uma linha por fileira. Qualquer conjunto de células vale — **inclusive peças soltas**, que é o "item quebrado":

```
L          Z          Quebrado       Boneco
#.         .##        #.#            #.#
#.         ##.        .#.            ###
##                                   #.#
```

No Inspector, o campo **Forma** é uma **grade clicável** (`ItemShapeDrawer`): clique ou arraste para pintar, botões de predefinição (1×1, L, T, Z, cruz, diagonal), `Col ±`/`Lin ±`, `Aparar`, e o texto cru ao abrir a seta. Tamanho máximo 12×12. A rotação é em passos de 90° horários e sempre normalizada (o item não "anda" ao girar).

### 3.3 Descrição que muda de acordo com a situação

`ItemTextSet` = texto base + lista de **variantes**; cada variante tem texto, prioridade e **condições** (todas precisam passar). Vence a de maior prioridade (empate: a última da lista). A resolução é feita **por jogador** — dois jogadores olhando o mesmo item podem ler textos diferentes.

| Condição | Passa quando |
|---|---|
| `SanityBelow` / `SanityAtLeast` | a sanidade (0..1) **de quem olha** está abaixo de / a partir de `value` |
| `VariableTrue` / `VariableFalse` / `VariableAtLeast` | variável da partida (`GameSession`) |
| `FlagSet` / `FlagNotSet` | a **instância** do item tem a flag (ex.: `aberto`, `ligada`) |
| `CounterAtLeast` / `CounterBelow` | contador da instância (ex.: `apertos`) |
| `HasItem` | quem olha possui o item `key` |
| `StackAtLeast` | a pilha tem pelo menos `value` |

Tokens no texto: `{stack}`, `{name}`, `{count:chave}`. Com `glitchAtLowSanity`, as letras trocam por símbolos e palavras intrusas aparecem quando a sanidade é muito baixa (determinístico: não treme a cada quadro).

Flags e contadores ficam na instância e vão para o save; mudam via `SetItemFlag` (efeito de ação), `InspectHotspot` ou código (`item.SetFlag("aberto")`, `item.AddCounter("apertos", 1)`).

### 3.4 Ações e efeitos

Uma ação tem `id`, `label`, `consume` (unidades gastas; 0 = não gasta), `requires` (condições; indisponível aparece **desabilitada com o motivo** em `blockedReason`), `effects`, `sound`.

| Efeito | O que faz |
|---|---|
| `RestoreStat` | soma `amount` a um stat ao longo de `seconds` (negativo = dano) |
| `StatModifier` | modificador temporário de stat |
| `SetVariable` / `AddVariable` | escreve na `GameSession` |
| `Sanity` | soma pontos de sanidade (de 100; negativo perde) |
| `HeartRate` | `amount` bpm a mais (negativo acalma) por `seconds` |
| `Noise` | faz barulho de raio `amount` m (quem escuta é a IA) |
| `CameraShake` | tremor de câmera, trauma 0..1 |
| `Recoil` | coice de arma na cabeça (sobe e desvia), força 0..1 |
| `SetItemFlag` | liga/desliga a flag `key` na instância usada |
| `Message` | chama `IItemUseHandler.OnItemMessage(ctx, key)` no jogador — o ponto de extensão para o seu código |

`IItemUseHandler` (implemente em qualquer script do jogador): `OnBeforeUse` pode **cancelar** o uso; `OnItemMessage` recebe os efeitos `Message`. Exemplo: o item "Pilhas" tem a ação "Trocar na lanterna" com `Message("flashlight_recharge")`.

### 3.5 Inspeção 3D e pontos interativos

Inspecionar abre o modelo girável/aproximável (mouse, analógico direito, scroll). Num prefab de inspeção, ponha `InspectHotspot` + `Collider` nas partes interativas: botão da lanterna, tampa, capa do diário, barriga do ursinho. Ao clicar, o hotspot pode ligar uma **flag** do item (muda a descrição), somar um **contador**, ligar uma variável da partida, rodar `effects`, tocar som e disparar um `UnityEvent`. Dois hotspots no mesmo lugar com `requiresFlag`/`hideWhenFlag` opostos formam um **liga/desliga** (a lanterna e o diário de exemplo fazem isso).
A inspeção é desenhada numa câmera própria longe da cena, na **layer 31** (`InspectStudio.InspectLayer`); scripts do prefab são removidos na cópia (só `InspectHotspot` sobrevive).

---

## 4. Inventários

Dois tipos de contêiner, **com a mesma interface** (`ItemContainer`), então qualquer código serve para os dois e **os dois conversam entre si**: arraste um item da grade para a lista, de uma lista para uma grade, da mochila para o baú.

| | `ListContainer` | `GridContainer` |
|---|---|---|
| Estilo | lista com linhas | grade estilo Resident Evil 4 |
| Limite | **infinito** (`unlimited`), **por espaços** (`maxSlots` × `listSlots` do item) e/ou **por peso** (`maxWeight`, 0 = sem limite) | tamanho `width × height`; `blockedPattern` (`#` = célula bloqueada) faz grades de formato irregular (bolso de casaco, mala torta) |
| Itens | ocupam espaços/peso | ocupam as células da **forma** (L, T, Z, peças soltas...), giram em passos de 90° |
| Extras | — | `FindFreeSpot`, `TryRotateInPlace`, `Organize()` (junta pilhas e reempacota), `Resize` |

Comum aos dois: filtro `accepted` (categorias aceitas), `startingItems` (conteúdo inicial), `AddPartial`/`TryAdd`/`Remove`/`RemoveUnits`, eventos `Changed/ItemAdded/ItemRemoved/Overflowed`, e **save automático** (`ISaveable`; a chave é `container_<containerId>`).

### 4.1 Quem pode ter inventário: jogador, NPC e objetos

Basta um **`InventoryOwner`** + um ou mais contêineres **no mesmo objeto (ou filhos)**. Não importa se é jogador, monstro, baú, armário, mesa ou cadáver:

- **Jogador**: `PlayerModules` cria um filho `[Inventory]` com uma grade "Mochila" (8×6) e uma lista "Bolsos" (6 espaços). Ajuste no prefab se quiser.
- **Baú / armário / NPC**: `GridContainer` (ou `ListContainer`) + `ContainerInteractable` (ele traz o `InventoryOwner` junto). Ao interagir, a tela do jogador abre com o conteúdo do objeto **ao lado da mochila** e dá para arrastar nos dois sentidos. `requiredVariable` deixa o baú trancado até uma variável ficar `true` (ex.: `tem_chave`).
- **Para o conteúdo ficar salvo**, ponha `SaveableEntity` **no mesmo objeto** (o inventário implementa `ISaveable`).

API do `InventoryOwner` (para o seu código): `AddItem(data, count, preferContainerId)`, `RemoveItem(id, count)`, `CountOf`, `Has`, `FindFirst`, `GetContainer(id)`, `FirstGrid()/FirstList()`, `UseItem`, `ToggleEquip`, `GetEquipped(slot)`, `Drop(item)`, `RequestInspect`; eventos `Picked/Dropped/Equipped/Unequipped/Used/Changed` (e os equivalentes globais em `ItemEvents`, com o dono como 1º argumento).
`ItemTransfer.Move(item, destino, quantidade)` move entre quaisquer contêineres (devolve um `TransferResult`); `DropOnGrid` coloca numa célula exata; `SplitStack` divide pilhas.

### 4.2 O que o jogador faz no próprio inventário

- **Mover e arrumar**: clique pega, clique solta (ou arraste); **R gira** o item na mão; soltar em cima de UM item junta pilhas iguais ou troca de lugar com ele; "Mover para: <contêiner>" no menu manda direto para a lista/grade (do próprio jogador ou do baú aberto).
- **Usar / equipar**: menu do item (botão direito) lista as ações do item (indisponíveis ficam desabilitadas com o motivo), **Equipar/Desequipar** quando há `equipSlot`.
- **Inspecionar**: mostra o modelo 3D e deixa interagir com os hotspots (3.5).
- **Jogar fora**: arraste para a zona "JOGAR FORA", use **G** sob o cursor ou o menu. O item vira um `WorldItem` no cenário (com física), na frente do jogador; se `canDrop` estiver desligado, não deixa.
- **Atalhos**: menu > "Atalho…" escolhe uma das 9 teclas (ou remove). A barra de atalhos aparece na tela do jogador e as teclas 1–9 usam o item; no controle, D-pad ←/→ troca e LB usa.
- **Organizar**: a tecla **O** (D-pad para cima) junta pilhas iguais e reempacota a grade sob o cursor (ou a mochila), maiores primeiro; se não coubesse tudo, nada é movido. **Dividir pilha**, **Girar** e **Mover para** estão no menu do item.

Itens jogados fora são lembrados pelo `DropRegistry`: somem ao sair do nível, voltam ao entrar e entram no save (variável `drops_v1`). Itens colocados **na cena** (`WorldItem` no editor) usam `SaveableEntity` no mesmo objeto, como qualquer `Pickup`.

### 4.3 Montar na mão (sem o menu)

```csharp
// baú em grade com itens iniciais
var go = new GameObject("Bau");
var grid = go.AddComponent<GridContainer>();     // antes do ContainerInteractable
grid.ContainerId = "bau_porao"; grid.DisplayName = "Baú do porão";
go.AddComponent<ContainerInteractable>();         // traz o InventoryOwner junto
go.AddComponent<SaveableEntity>();                // salva o conteúdo

// dar um item por código a qualquer dono
InventoryOwner dono = go.GetComponent<InventoryOwner>();
ItemData bandagem = ItemDatabase.Find("bandagem");
int couberam = dono.AddItem(bandagem, 2);          // devolve quantas unidades entraram (o resto é com você)
```

---

## 5. Sanidade (individual por jogador)

Cada jogador tem o próprio `SanityComponent`. Valor normalizado **1 = lúcido, 0 = quebrado**; `Insanity = 1 − Normalized`. Faixas (com histerese, para não piscar na borda): **Stable** → **Uneasy** (< 80%) → **Disturbed** (< 60%) → **Fractured** (< 40%) → **Broken** (< 18%). Tudo ajustável num `SanityProfile` (Create > HorrorFPS > Sanity Profile; sem asset, vale o padrão): pontos máximos (100), limites das faixas, regeneração (0,6 pt/s depois de 10 s sem perder, até 85%).
O valor é guardado na partida (`sanity_player_N`) e vai para o save. **É independente do stat `Sanity` do `StatsComponent` do framework base** — se quiser unir os dois, escute `SanityEvents.Changed` e espelhe o valor.

```csharp
SanityComponent s = SanityComponent.Of(jogador);     // aceita o jogador ou qualquer componente dele (câmera, filhos...)
s.Add(-8f, "susto_do_espelho");                      // pontos (de 100); o motivo vai nos eventos
s.SetDrain("escuro", -0.5f);                         // fonte contínua com nome (pontos/s); ClearDrain("escuro") encerra
float n = s.Normalized;  SanityStage faixa = s.Stage;
SanityEvents.Shock(posicao, 12f, 10f, "visao", requireSight: true);   // susto de cena: quem estiver no raio (e olhando) perde 10 pontos
```

### 5.1 Como os outros scripts ficam cientes

- **Herdar `SanityAware`** e sobrescrever `OnSanityChanged(SanityChange)` / `OnSanityStageChanged(quem, de, para)` (o `OnEnable/OnDisable` se inscreve sozinho).
- Ou implementar `ISanityListener` e chamar `SanityEvents.Subscribe(this)`; ou usar os eventos estáticos `SanityEvents.Changed/StageChanged`; ou ler direto `SanityComponent.NormalizedOf(componente)`.
- `SanityChange` traz quem mudou, o valor novo, o delta (negativo = piorou), a faixa e o **motivo** (`"item:calmante"`, `"zone:porao"`, `"shock"`...).
- Mudanças contínuas só avisam a cada 0,5% de variação (não inunda os ouvintes).

### 5.2 Textos que mudam para cada jogador

| Peça | Uso |
|---|---|
| `SanityString` | Campo de texto em qualquer script: `normal` + variantes "abaixo de X" (vale a de **menor** X que serve) + `glitchAtLowSanity`. `texto.Resolve(componenteDoJogador)`. Converte de `string` sozinho. |
| `SanityLine` | `SanityString` + quem fala + tempo + voz. |
| `SanitySubtitles` | Legendas na **metade de tela de cada jogador**. `SanitySubtitles.Of(jogador).Say(linha)` ou `Say("Voz", "texto", segundos)`. Já resolve pela sanidade de quem lê. |
| `SanityExaminable` | Interagível para retrato, bilhete, espelho, cadáver, NPC: mostra as linhas nas legendas de **quem examinou**; pode custar sanidade (`sanityCost` abaixo de `costsBelow`) e ligar uma variável. Uma linha pode existir **só** para quem está muito mal (texto normal vazio + variante). |
| `SanityDialogueStep` | Passo de cutscene em que cada jogador lê a variante da própria sanidade (deixe "Use Cutscene Camera" desligado para cada um manter a sua metade de tela). |
| `ItemTextSet` | Nome/descrição dos itens (3.3), com a mesma ideia. |

### 5.3 O cenário também mente — só para quem está mal

Funciona **individualmente no split-screen**: o objeto é alterado imediatamente antes de a câmera de cada jogador renderizar e restaurado depois (`CameraHook`), então dois jogadores no mesmo lugar veem coisas diferentes. **Só a imagem muda**: colisão, física e IA são iguais para todos. Câmeras que não são de jogo (Scene View, câmera de cutscene) veem o mundo "são".

- **`SanityVisibility`**: `ShowOnlyInRange` (alucinação que só quem está mal vê), `HideInRange` (a mente apaga o que não aguenta ver), `SwapMaterialInRange` (mofo, sangue, ferrugem). Faixa `minSanity..maxSanity` de **quem vê**.
- **`SanityLight`**: a luz fica mais fraca, avermelhada e falha para quem está mal (só luzes em tempo real).
- **Seus próprios efeitos**: herde `PerViewerEffect` e implemente `Apply(Camera, float sanity01)` / `Revert()` (idempotentes — podem ser chamados mais de uma vez por quadro).

### 5.4 Na tela, no corpo e no ouvido do jogador

- **`SanityFX`** (camada de UI do jogador): vinheta, ruído de vídeo, tinta, flash ao perder muito de uma vez (`flashThreshold`), tela preta (`Blackout(fadeIn, hold, fadeOut)`), respiração do FOV e inclinação da câmera (via `CameraFx`). **Dessaturação, aberração e desfoque exigem pós-processamento**, que o pacote não assume: ligue o evento `onInsanity` (`float` 0..1) ao peso de um Volume seu.
- **`SanityEffects`** (efeitos negativos, cada um liga/desliga): lentidão (até ×0,84), tremor constante da câmera, **sobressaltos** aleatórios a partir de Fractured (chute na câmera + coração dispara + arfada), e **desmaio** abaixo de 3% (3,2 s de tela preta, travando os controles com a chave `"Sanity"`, faz barulho ao cair, acorda com 28%, intervalo mínimo de 75 s).
- **`SanityAudio`**: zumbido (tinnitus) e drone que crescem com a insanidade e **alucinações sonoras** (sussurros, batidas, arranhões ao redor da cabeça, com pan estéreo pela direção) a partir de 38% de sanidade. Aceita seus próprios clipes; sem clipes, usa sons gerados por código.
- **`SanityZone`**: volume (Collider em Trigger) que drena/cura (`pointsPerSecond`, mais forte no centro com `falloffFromCenter`), dá um susto único na entrada (`entryShock`, `shockOnlyOnce`) e pode depender de uma variável (`onlyWhileVariable`). Contagem por jogador: sair de uma zona só encerra o efeito dele.

---

## 6. Corpo: batimento, respiração, barulho e head bob dinâmico

### 6.1 Barulho que expõe a posição

`NoiseSystem.Emit(posicao, raio, tipo, origem)` avisa todos os `NoiseListener` no alcance. Passos, pouso, respiração ofegante, arfada, batimento alto, itens, pancadas e explosões emitem sozinhos. Coloque **`NoiseListener`** em monstros/NPCs/objetos: ele guarda `LastPosition/LastKind/LastLoudness/HasRecentNoise` (memória de 8 s), dispara `Heard` (C#) e `onHeard(Vector3)` (UnityEvent), considera parede no caminho (`occlusionFactor`) e ignora o próprio barulho. É o gancho da sua IA: "ouvi algo ali".
Valores padrão: agachado ≈ 1,5 m, andando ≈ 5 m, correndo ≈ 11 m, pouso até 18 m; arfada 9 m; `NoiseSystem.GlobalScale` escala tudo (dificuldade).

### 6.2 `HeartbeatComponent`

BPM de 66 (repouso) até 178: sobe com **correr** (+42), **insanidade** (+38), **ameaça** (+62) e susto; cai segurando a respiração (−12). Som de batimento gerado por código, audível a partir de 20% de intensidade. Efeitos negativos: **mão tremendo** (trauma contínuo na câmera), **corpo pesado** (velocidade ×0,9 acima de 78%) e, a partir de 55%, o coração **faz barulho** (até 6 m) — dá para ser ouvido escondido.
API: `Boost(bpmExtra, segundos)`, `Scare(0..1)`, `SetThreat("monstro", 0..1)` / `ClearThreat`, `HeartbeatComponent.Alert(posicao, raio, nivel, requireSight)` (para monstros assustarem quem os vê), `Bpm`, `Intensity`, `Pulse`, evento `Beat`.

### 6.3 `BreathingComponent`

Taxa de 13 a 44 respirações/min conforme o esforço (correr, sustos, insanidade), com sons de inspirar/expirar gerados por código e barulho a partir de 40% de esforço (até 5 m). **Segurar a respiração** (H / R3): silencia o corpo por até 9 s, acalma o coração, mas depois vem uma **arfada forçada** (9 m de barulho) e 4 s de recarga — o jogo clássico do esconderijo. Scripts podem forçar com `SetHoldingBreath(true)` (ex.: dentro do armário) e provocar `Gasp(volume)`.

### 6.4 `DynamicHeadBob` — a cabeça reage a tudo

Seis molas amortecidas + trauma (tremor Perlin) + balanço de passos, tudo somado numa passada só pelo `CameraFx` (a câmera de gameplay, os raios de interação e a mira **nunca** veem o efeito). Cobre: caminhar/correr/agachar, pulo e pouso (pelo `PlayerMotionProbe`), respiração e pulso do coração, e **forças externas**:

| De onde | Como |
|---|---|
| Explosão | `CameraImpulse.Explosion(pos, poder, raio, raioDoBarulho, sanidadePerdida)` — sacode, empurra a cabeça para longe da explosão, acelera o coração, faz barulho e (opcional) abala a sanidade de todos no raio |
| Pancada / dano | `CameraImpulse.Hit(vitima, deOnde, força)` — chute na câmera; o `DamageReaction` faz isso sozinho ao ouvir o evento `Damaged` do `StatsComponent` (tentativa automática; senão chame `React(dano, deOnde)`) |
| Tiro / arma | `CameraImpulse.Recoil(atirador, força, lateral)` ou o efeito `Recoil` de uma ação de item (coice sobe e desvia para o lado) |
| Tremor de cenário | `CameraImpulse.Shake(pos, raio, trauma)` |
| Do jeito que quiser | `AddTrauma`, `SetConstantTrauma("chave", nivel)`, `Kick(direçãoMundo, força)`, `Impulse(velPosLocal, velAngLocal)` |

No cenário: **`CameraImpulseSource`** (barril, trovão, porta que bate — chame `Fire()` por UnityEvent/animação) e **`CameraImpulseInteractable`** (o jogador aperta e dispara; usado no barril de teste). O head bob antigo do `FirstPersonController` é **desligado por reflexão** (`suppressBaseBob`); se o campo não for achado, aparece um aviso no Console e os dois balanços se somam (zere o bob do controller no Inspector).

---

## 7. Receitas — integrando com os seus scripts

**Reagir ao jogador equipar uma lanterna** (equipar só marca o slot e avisa; quem acende a luz é você):

```csharp
public class Lanterna : MonoBehaviour
{
    [SerializeField] Light luz;
    void OnEnable()  { ItemEvents.Equipped += Equipou; ItemEvents.Unequipped += Desequipou; }
    void OnDisable() { ItemEvents.Equipped -= Equipou; ItemEvents.Unequipped -= Desequipou; }
    void Equipou(InventoryOwner dono, ItemInstance item)    { if (EhMeu(dono, item)) luz.enabled = true; }
    void Desequipou(InventoryOwner dono, ItemInstance item) { if (EhMeu(dono, item)) luz.enabled = false; }
    bool EhMeu(InventoryOwner dono, ItemInstance item) { return item.data.Id == "lanterna" && dono.transform.IsChildOf(transform); }
}
```

**Receber a mensagem de uma ação de item** (efeito `Message`; o script fica no jogador ou num filho dele):

```csharp
public class RecargaDaLanterna : MonoBehaviour, IItemUseHandler
{
    public bool OnBeforeUse(ItemUseContext ctx) { return false; }            // true cancelaria o uso
    public void OnItemMessage(ItemUseContext ctx, string key)
    {
        if (key == "flashlight_recharge") bateria = 100f;
    }
    float bateria;
}
```

**Monstro que ouve o jogador** (`NoiseListener` no monstro):

```csharp
public class Monstro : MonoBehaviour
{
    NoiseListener ouvido; Vector3 destino;
    void Awake() { ouvido = GetComponent<NoiseListener>(); ouvido.Heard += (ev, volume) => { destino = ev.position; }; }
    void Update() { if (ouvido.HasRecentNoise) { /* investigar ouvido.LastPosition */ } }
}
```

**Script que reage à sanidade** (herdar `SanityAware`; se tiver `OnEnable/OnDisable` próprios, chame o `base`):

```csharp
public class PortaQueSabe : SanityAware
{
    public override void OnSanityStageChanged(SanityComponent quem, SanityStage de, SanityStage para)
    {
        if (para >= SanityStage.Fractured) Debug.Log(quem.name + " está surtando");
    }
}
```

**Texto próprio que muda por jogador**:

```csharp
[SerializeField] SanityString descricao = new SanityString("Uma porta velha.");   // variantes no Inspector
void Interagiu(PlayerContext p) { SanitySubtitles.Of(p).Say("", descricao.Resolve(p)); }
```

**Efeito de câmera próprio** (soma-se aos outros sem briga; ignore câmeras que não sejam do seu jogador):

```csharp
public class BalancoDoBarco : MonoBehaviour, ICameraModifier
{
    void OnEnable()  { CameraFx.Register(this); }
    void OnDisable() { CameraFx.Unregister(this); }
    public void Contribute(Camera cam, ref CameraDelta d) { d.euler.z += Mathf.Sin(Time.time * 0.8f) * 1.5f; }
}
```

**Eventos de história**: `SanityEvents.AddAll(5f, "capitulo_concluido")` (todos os jogadores), `CameraImpulse.Explosion(...)` numa cutscene, `HeartbeatComponent.Alert(posicaoDoMonstro, 20f, 0.8f, requireSight: true)` quando o monstro aparece.

---

## 8. Limitações e observações (leia antes de reclamar do código)

- **Nada foi compilado nem rodado no Unity.** Foi escrito contra as APIs descritas no documento de arquitetura. Tudo o que depende do framework base está concentrado em `Core/FrameworkBridge.cs` (`SaveSystem.BeforeSave/AfterLoad`, `SceneDirector.TransitionStarted/LevelLoaded`, `GameSession.Get/Set...`, travas de controle do `PlayerContext` e `FirstPersonController.SetSpeedModifier`) — se algo do tipo falhar ao compilar, é lá que se ajusta. Também há reflexão com aviso no Console em `DamageReaction` (liga ao evento `Damaged`) e no `DynamicHeadBob` (desliga o bob antigo).
- **Áudio no split-screen**: só existe **um** `AudioListener` ativo, então não dá para um jogador ouvir o coração e o outro não. `AudioMode` (`Auto`, `Local2D`, `World3D`) decide: `Auto` = 2D com um jogador e 3D (posicional, na cabeça do dono) com vários. A **exposição de posição para a IA é separada do áudio** (`NoiseSystem`) e funciona certinho por jogador.
- **Visual por jogador** usa o gancho antes/depois da renderização de cada câmera (Built-in e SRP/URP/HDRP). Em pilha de câmeras (Camera Stack) o efeito vale para a câmera do jogador; câmeras de cutscene e do editor veem o mundo "são".
- **Pós-processamento**: o pacote não depende de nenhum. Dessaturar/borrar/aberração: ligue `SanityFX.onInsanity` ao peso de um Volume.
- **Itens** precisam estar em uma pasta `Resources` (o save guarda só o id). `worldPrefab` sem `SaveableEntity`. Baú/NPC só salva o conteúdo com `SaveableEntity` **no mesmo objeto**. Objetos criados em tempo de execução só voltam num load se forem itens jogados fora (`DropRegistry`).
- **Inspeção 3D** desenha numa câmera própria na **layer 31**, em coordenadas longe da cena (4000, 4000, 4000 + 60 por jogador); não use essa layer para outra coisa.
- **O mundo não pausa** com o inventário aberto; o jogador fica travado (chave de trava `"Inventory"`), mas vulnerável.
- As **ações de input novas** (`ItemInput`) não estão no painel de remapeamento do framework.
- A sanidade deste módulo é independente do stat `Sanity` do `StatsComponent` base.
- Os sons de batimento, respiração, zumbido e alucinações são **gerados por código** (`ProceduralAudio`, 22 kHz, cacheados). Para áudio de verdade, preencha os campos `custom...` ou `audioMode` e clipes nos componentes.

## 9. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| O inventário não abre | Confira *Active Input Handling = Input System*; veja se o jogador tem o filho `[Inventory]` (rode o item 4 do menu ou dê Play e espere meio segundo); a tecla é **Tab/I** (Y no controle). |
| Item some depois de carregar o save | O `ItemData` não está numa pasta `Resources` (o `ItemDatabase` só acha o que está lá). |
| A descrição não muda | A condição `SanityBelow` compara a sanidade **de quem olha**; confira prioridades (maior vence) e se o jogador tem `SanityComponent`. |
| Baú perde o conteúdo ao recarregar | Falta `SaveableEntity` no mesmo objeto do baú. |
| Dois balanços de câmera / bob estranho | O aviso de `suppressBaseBob` apareceu: zere o bob do `FirstPersonController` no Inspector. |
| Barulho não atrai o monstro | O monstro precisa de `NoiseListener` e o raio do barulho precisa alcançá-lo (agachado 1,5 m • andando 5 m • correndo 11 m). |
| Não vejo nada de efeito de sanidade | Você precisa estar abaixo de ~80% (Uneasy); use uma `SanityZone`, o item "Caco de espelho" ("Encarar") ou `SanityComponent.Of(p).Add(-40)` para testar. |
| Erro de compilação em `FrameworkBridge` | Um nome/assinatura do framework base difere do documento: mande a mensagem do Console. |
