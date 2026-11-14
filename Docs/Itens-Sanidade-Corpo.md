# Itens, Inventário, Sanidade e Corpo — guia

*Complementa o documento de arquitetura do framework (`HorrorFPS-Arquitetura.md`): ele descreve a base (input, movimento, interação, status, split-screen, cenas, save, cutscenes); este descreve os módulos novos que se apoiam nela.*

Módulos novos do Horror FPS Framework: **itens e dois tipos de inventário (lista e grade de formato livre)**, **sanidade individual por jogador** e **corpo** (batimento, respiração, head bob dinâmico, barulho que expõe a posição).
Tudo é C# puro, a UI é criada por código e **nada precisa ser configurado** para funcionar: ao dar Play, todo jogador ganha os módulos que faltarem.

> **Código escrito sem compilar no Unity** (igual ao resto do framework). Foi conferido por um verificador estático (delimitadores, `using`, um componente por arquivo, membros e número de argumentos das chamadas entre arquivos, `override`/abstratos/interfaces, acesso a membros privados), mas se aparecer algum erro na primeira importação, mande a mensagem do Console.

---

## 1. Instalação em 3 passos

1. Copie a pasta `Assets/HorrorFPS` por cima da existente e espere compilar. Só entram **arquivos novos** (em `Items`, `Inventory`, `Sanity`, `Body`, `Input`, `UI/Inventory`, `Core`, `Editor` e `Docs`): nenhum arquivo do framework base é alterado ou sobrescrito.
2. Menu **Tools > Horror FPS > 5. Create Sample Items** — cria 18 itens de exemplo (inclusive lanterna, facas e armas de fogo que funcionam, seção 15) em `Assets/HorrorFPS/Resources/HorrorFPS/Items` (formas variadas, descrições que mudam pela sanidade, ações com efeitos, modelos de inspeção).
3. Menu **Tools > Horror FPS > 6. Create Demo Props In Open Scene** — monta numa cena aberta uma bancada de testes (baú em grade, itens no chão, zona de sanidade, retrato que descreve diferente, barril que explode, "ouvinte" que vira para o barulho).

Opcional: **Tools > Horror FPS > 4. Add Item, Sanity and Body modules to Player Prefab** grava os módulos no prefab do jogador (para ajustar valores no Inspector). Sem isso, o `PlayerModules` instala tudo sozinho em cada `PlayerContext` que aparecer (inclusive os criados pelo `LocalPlayerManager` em tempo de execução).
Itens iniciais no inventário do jogador: **Tools > Horror FPS > 7. Player Starting Inventory** (seção 10). Vários personagens / troca de prefab durante a história: **8** e **9** (seção 11).
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
├─ Equipment/  EquipmentController (lanterna, arma branca, arma de fogo) · EquipmentHUD · HitTarget/ItemHit (alvo de treino)
├─ Input/      ItemInput (ações próprias do inventário, criadas por código)
├─ UI/Inventory/ PlayerOverlay (camada de UI por jogador) · InventoryScreen · GridView · ListView · InspectPanel/InspectStudio · HotbarHUD
├─ Sanity/     SanityComponent · SanityProfile · SanityEvents/SanityAware · SanityString/SanityText · SanityVisibility · SanityLight
│              SanityFX · SanityEffects · SanityAudio · SanityZone · SanitySubtitles/SanityLine · SanityExaminable · SanityDialogueStep
├─ Body/       PlayerMotionProbe · HeartbeatComponent · BreathingComponent · DynamicHeadBob · DamageReaction
│              CameraImpulse/CameraImpulseSource/CameraImpulseInteractable · NoiseSystem/NoiseListener/SoundEmitter · PlayerAudio/ProceduralAudio
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
| Organizar a grade (junta pilhas e reempacota) | O | L3 (clique do analógico esquerdo) |
| **Mover o cursor célula a célula** (sem mouse) | Setas ou WASD | D-pad |
| Pegar / soltar (teclado) | Enter ou Espaço | A |
| Menu do item (teclado) | E | X |
| **Menu aberto: escolher a opção em foco** | E, Enter ou Espaço (ou clique na linha) | A ou X |
| **Menu aberto: mover o foco** | Setas ou WASD | D-pad |
| **Menu aberto: fechar** | Botão direito, Backspace ou clique fora | B |
| Voltar / devolver à origem | Backspace | B |
| Atalho rápido (equipável: **equipa/guarda**; consumível: usa) | Teclas 1–9, 0, - e = (até 12, ver 17.3) | D-pad ← / → troca, LB usa |
| **Usar o item equipado** (fora do inventário) | Botão direito do mouse | LT |
| **Segurar a respiração** | H | R3 (clique do analógico direito) |

Com **mouse**, o cursor do inventário é o **ponteiro real do sistema** (mesma velocidade e aceleração de qualquer menu; o cursor do jogo é liberado enquanto a mochila está aberta e devolvido ao fechar). Com analógico esquerdo ou teclado, é um cursor virtual desenhado pelo jogo; o analógico direito gira/aproxima na inspeção. Cada jogador do split-screen tem a **própria tela de inventário**, na própria metade, controlada só pelos dispositivos dele. **O mundo não pausa** com o inventário aberto (em jogo local não dá para pausar os outros) — o jogador fica parado e vulnerável, o que faz parte da tensão.

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
| `icon`, `color` | Sem ícone, a célula é pintada com a cor e mostra o nome. O ícone ideal é gerado pelo **Item Studio** (seção 13) a partir do modelo 3D. |
| `shape`, `allowRotation` | Forma na grade (3.2). |
| `listSlots` | Espaços ocupados nas listas com limite. |
| `stackable`, `maxStack` | Pilhas (munição, pilhas, comprimidos). |
| `worldPrefab` | Modelo ao jogar fora. Vazio = caixinha colorida. **Não ponha `SaveableEntity` nele** (o `DropRegistry` cuida do que é jogado fora). |
| `inspectPrefab` | Modelo da inspeção 3D (vazio = `worldPrefab`; sem nenhum, um bloco com a forma do item). |
| `canDrop`, `canInspect` | Itens de missão costumam ter `canDrop` desligado. |
| `equipSlot` | Ex.: `mao`, `lanterna`. Vazio = não equipável. Equipar **marca** o item no slot e avisa (`ItemEvents.Equipped`). A tecla de atalho de um equipável só equipa/guarda; a ação dele (acender, atirar, bater) vem depois, no botão de **usar equipado** (`InventoryOwner.UseEquipped()` → executa a ação principal do item, se houver, e sempre avisa `ItemEvents.EquippedActivated`). |
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
| Limite | **infinito** (`unlimited`), **por espaços** (`maxSlots` × `listSlots` do item) | tamanho `width × height`; `blockedPattern` (`#` = célula bloqueada) faz grades de formato irregular (casaco com bolsos, mala torta) |
| Itens | ocupam espaços | ocupam as células da **forma** (L, T, Z, peças soltas...), giram em passos de 90° |
| Extras | — | `FindFreeSpot`, `TryRotateInPlace`, `Organize()` (junta pilhas e reempacota), `Resize` |

Comum aos dois: filtro `accepted` (categorias aceitas), `startingItems` (conteúdo inicial), `AddPartial`/`TryAdd`/`Remove`/`RemoveUnits`, eventos `Changed/ItemAdded/ItemRemoved/Overflowed`, e **save automático** (`ISaveable`; a chave é `container_<containerId>`).

### 4.1 Quem pode ter inventário: jogador, NPC e objetos

Basta um **`InventoryOwner`** + um ou mais contêineres **no mesmo objeto (ou filhos)**. Não importa se é jogador, monstro, baú, armário, mesa ou cadáver:

- **Jogador**: `PlayerModules` cria um filho `[Inventory]` com uma grade "Mochila" (8×6). Não há mais "Bolsos"; se o prefab ainda tiver essa lista (vazia), ela é removida ao jogar. Ajuste no prefab se quiser.
- **Baú / armário / NPC**: `GridContainer` (ou `ListContainer`) + `ContainerInteractable` (ele traz o `InventoryOwner` junto). Ao interagir, a tela do jogador abre com o conteúdo do objeto **ao lado da mochila** e dá para arrastar nos dois sentidos. `requiredVariable` deixa o baú trancado até uma variável ficar `true` (ex.: `tem_chave`).
- **Para o conteúdo ficar salvo**, ponha `SaveableEntity` **no mesmo objeto** (o inventário implementa `ISaveable`).

API do `InventoryOwner` (para o seu código): `AddItem(data, count, preferContainerId)`, `RemoveItem(id, count)`, `CountOf`, `Has`, `FindFirst`, `GetContainer(id)`, `FirstGrid()/FirstList()`, `UseItem`, `UseEquipped(slot)`, `ToggleEquip`, `GetEquipped(slot)`, `Drop(item)`, `RequestInspect`; eventos `Picked/Dropped/Equipped/Unequipped/EquippedActivated/Used/Changed` (e os equivalentes globais em `ItemEvents`, com o dono como 1º argumento).
`ItemTransfer.Move(item, destino, quantidade)` move entre quaisquer contêineres (devolve um `TransferResult`); `DropOnGrid` coloca numa célula exata; `SplitStack` divide pilhas.

### 4.2 O que o jogador faz no próprio inventário

- **Mover e arrumar**: clique pega, clique solta (ou arraste); **R gira** o item na mão; soltar em cima de UM item junta pilhas iguais ou troca de lugar com ele; "Mover para: <contêiner>" no menu manda direto para a lista/grade (do próprio jogador ou do baú aberto).
- **Usar / equipar**: menu do item (botão direito) lista as ações do item (indisponíveis ficam desabilitadas com o motivo), **Equipar/Desequipar** quando há `equipSlot`.
- **Inspecionar**: mostra o modelo 3D e deixa interagir com os hotspots (3.5).
- **Jogar fora**: arraste para a zona "JOGAR FORA", use **G** sob o cursor ou o menu. O item vira um `WorldItem` no cenário (com física), na frente do jogador; se `canDrop` estiver desligado, não deixa.
- **Atalhos**: menu > "Atalho…" escolhe uma das teclas da barra (ou remove); a quantidade de teclas é configurável (17.3). A barra de atalhos aparece na tela do jogador e as teclas (1–9, 0, - e =) equipam o item (consumíveis são usados); no controle, D-pad ←/→ troca e LB usa. Depois de equipar, o botão de usar equipado aciona o item.
- **Organizar**: a tecla **O** (L3 no controle) junta pilhas iguais e reempacota a grade sob o cursor (ou a mochila), maiores primeiro; se não coubesse tudo, nada é movido. **Dividir pilha**, **Girar** e **Mover para** estão no menu do item.

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

`NoiseSystem.Emit(posicao, raio, tipo, origem)` lança um **`OverlapSphere`** no ponto e avisa quem alcançar (`INoiseHearer` e `NoiseListener`, ver seção 16; para tocar um som **e** fazer barulho de uma vez, use `SoundEmitter`). Passos, pouso, respiração ofegante, arfada, batimento alto, itens, pancadas e explosões emitem sozinhos. Coloque **`NoiseListener`** em monstros/NPCs/objetos: ele guarda `LastPosition/LastKind/LastLoudness/HasRecentNoise` (memória de 8 s), dispara `Heard` (C#) e `onHeard(Vector3)` (UnityEvent), considera parede no caminho (`occlusionFactor`) e ignora o próprio barulho. É o gancho da sua IA: "ouvi algo ali".
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

**Reagir ao jogador equipar uma lanterna** (equipar só marca o slot e avisa; quem acende a luz é você — ligue/desligue no `EquippedActivated` se quiser que ela espere o botão de usar equipado):

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
| Os itens iniciais não aparecem no inventário do jogador | O jogador só tem inventário no **prefab** depois do menu 4 (ou do botão "Instalar inventário" da janela 7). Configure os itens no prefab; os que não couberem aparecem em amarelo na prévia e no Console. |
| Troca de personagem não acontece | Falta o `CharacterCatalog` em `Resources`, o personagem não está na lista, ou o prefab dele não tem `PlayerContext` + `PlayerInput` (veja os avisos no inspector do catálogo). |
| Jogador criado pela troca nasce no lugar errado | O `LocalPlayerManager` reposiciona depois da troca: mande o código dele para ligarmos a escolha direto no ponto de nascimento. |

## 9. Tela do inventário e arrastar

- **Só a mochila aberta**: janela de uma coluna, com células maiores (`maxCellSolo` no `InventoryScreen`, padrão 72 px), a grade centralizada e o painel de informações (nome, descrição, ajuda) embaixo dela. Com baú ou NPC aberto, volta o layout de duas colunas.
- **Mouse: clique x arrasto**: um **clique rápido** num item abre o menu dele. **Segurar** o botão (0,4 s, `dragHoldSeconds` no `InventoryScreen`) ou arrastar uns pixels (`dragMovePixels`) pega o item: depois de uma espera de 0,18 s (`dragChargeDelay`) uma barrinha começa a encher junto do ponteiro — um clique rápido nunca a mostra — e quando pega a peça "levanta" e passa a seguir o ponteiro (17.4). Soltar o botão larga onde estiver; se não couber ou soltar fora de tudo, o item volta ao lugar de onde saiu. Arrastar para a zona **JOGAR FORA** joga no mundo e para um slot da barra de atalhos atalha. O botão direito e a tecla E abrem o menu na hora; com o menu aberto, o E (ou Enter/Espaço, ou A/X no controle) **escolhe a opção em foco** em vez de clicar no item de novo (17.1). No teclado (Enter) e no controle (A) continua pegar/soltar.
- **Onde o item cai ao ser jogado fora**: na frente do jogador, na direção em que ele olha (olhar para baixo não enterra o item no chão). O jogo varre uma esfera do olho até o ponto, confere o volume inteiro do item e recua se bater em parede, móvel ou chão; se nem junto do jogador couber, solta aos pés dele. O item usa colisão contínua, não colide com quem o soltou por 0,8 s e uma rede de segurança o traz de volta se mesmo assim cair para fora do mundo. Com o caminho bloqueado ele só cai (sem o empurrãozinho para frente).
- **Pegar pela célula**: o arrasto começa exatamente na célula clicada (um T pego pela base continua sendo segurado pela base). Girar com R gira a peça em torno dessa célula. Itens pegos de uma lista são centrados no cursor.
- O nome do item não aparece mais dentro da grade; ele fica no painel de informações. Não existe peso.
- **Só teclado ou só controle**: o cursor também anda por passos. Setas/WASD (teclado) e D-pad (controle) movem uma célula; com a mão vazia, um passo atravessa o item inteiro; na borda o cursor pula para o contêiner vizinho (mochila → baú). Enter/Espaço pega e solta, E abre o menu do item (o foco já cai na primeira opção habilitada; setas movem o foco e E/Enter/Espaço escolhem). O menu "Atalho…" substitui o arrastar para a barra de atalhos, e "Jogar fora" (ou G) substitui a zona de descarte. Na inspeção, as setas giram o modelo; clicar nos pontos interativos exige mouse ou o analógico esquerdo.

## 10. Itens iniciais no Inspector

Todo `GridContainer`/`ListContainer` ganhou um editor para os **itens iniciais** (o que ele tem quando o jogo começa):

- **Lista reordenável**: arraste itens (`ItemData`) do Project para dentro dela, ou use **＋ Escolher item…** (menu por categoria). Cada linha tem item + quantidade; itens empilháveis viram várias pilhas de `Max Stack`.
- **A ordem importa**: o jogo encaixa de cima para baixo, no primeiro espaço onde couber (girando se o item permitir). Coloque os itens grandes primeiro.
- **Prévia da grade**: mostra onde cada item vai cair (mesma regra do jogo), com a quantidade de cada pilha e a linha selecionada contornada. Clique numa peça para selecionar a linha dela.
- **Avisos**: item que não cabe (com a linha marcada), categoria que o inventário não aceita, linha vazia, e item fora de uma pasta `Resources` (não volta num load).

**Para o jogador**: menu **Tools > Horror FPS > 7. Player Starting Inventory**. A janela acha o prefab do jogador (ou use o campo / o menu do catálogo de personagens), tem uma aba por inventário do prefab (Mochila, Chaves...) e grava direto no prefab, sem abrir a cena. Se o prefab ainda não tem inventário, o botão **Instalar inventário** roda a instalação dos módulos nele. A janela serve também para qualquer prefab com inventário (baús, armários).

Observações:
- Os itens iniciais entram **uma vez, quando o jogador nasce**. Ao carregar um save, vale o que foi salvo.
- Cada personagem (seção 11) tem os próprios itens iniciais: abra o prefab dele na janela.

## 11. Personagens — escolher e trocar o prefab do jogador

Para a história poder trocar de personagem (ex.: capítulo 2 joga com outra pessoa), o jogador passa a ser escolhido por um **catálogo**.

**Montar (uma vez)**
1. Selecione no Project o(s) prefab(s) do jogador (um por personagem; copie o prefab base e troque modelo, câmera, valores) e rode **Tools > Horror FPS > 9. Add Selected Player Prefab(s) as Characters**. Isso cria o `CharacterCatalog` em `Resources/HorrorFPS` (também pelo menu **8. Character Catalog**) e um `CharacterDefinition` por prefab.
2. No catálogo, escolha o **Starting Character**. **O prefab que o `LocalPlayerManager` usa hoje tem que ser um dos personagens** (o jogo o reconhece pelo nome do prefab).
3. Em cada `CharacterDefinition`: `id` (curto e estável, vai para o save), nome, retrato, prefab e a regra do inventário.

**Regra do inventário** (`Inventory` no personagem — vale quando o jogo troca PARA ele)
- `Separate` (padrão): cada personagem tem a **própria mochila**. Na primeira vez começa com os itens iniciais do prefab; depois lembra o que tinha (e isso vai para o save).
- `Carry`: a mochila **acompanha o jogador**: o personagem recebe os itens (e o equipado) de quem estava antes, no que couber (grandes primeiro; o que sobrar é avisado no Console).

**Trocar durante a história**
- Por código: `CharacterManager.SwitchAll("filho")`, `CharacterManager.Switch(player, "mae")`, `CharacterManager.Select(1, "mae", applyNow: false)` (vale no próximo nascimento/load). Evento `CharacterManager.CharacterChanged(player, definição)`.
- Sem código: componente **Character Switcher** (`Apply()` ligado a um `UnityEvent` de gatilho, cutscene ou interagível; `Player = -1` troca todos). Faça a troca durante um fade/tela preta: a câmera "pula" para a do novo prefab.
- A escolha de cada jogador fica nas variáveis da partida (`character_p0`, `character_p1`...) e vai para o save. Ao carregar, quem estiver com outro prefab é trocado para o personagem salvo.

**O que a troca faz**: desativa o jogador atual, cria o novo com o **mesmo índice, esquema de controle e dispositivos** (teclado/mouse ou o mesmo controle), o põe no **mesmo lugar e olhando para o mesmo lado** e instala os módulos (inventário, sanidade, corpo) se faltarem. Os `PlayerSpawned/PlayerRemoved` do framework disparam normalmente, então a IA e o HUD acompanham.

**Limites conhecidos**
- Travas de controle (cutscene/loading) do jogador antigo **não** passam para o novo: troque durante o fade ou depois da cutscene.
- Vida, estamina e sanidade começam do zero no novo personagem (cada prefab tem os seus valores).
- Se ninguém criar o `CharacterCatalog`, o sistema fica desligado e nada muda.
- Escrito sem o código do `LocalPlayerManager`: ele deve reagir à entrada de um `PlayerInput` (evento do `PlayerInputManager`) como reage a qualquer jogador novo. Se na troca o jogador nascer fora do lugar, ou a divisão de tela não se ajustar, mande o `LocalPlayerManager.cs` que a ligação é feita direto nele.

## 12. Inspector do jogador em grupos (`[PlayerModule]`)

O jogador acumula muitos componentes (movimento, câmera, status, inventário, sanidade, corpo...). Em vez de juntar scripts em um só (o que misturaria responsabilidades e quebraria o uso de `StatsComponent` em NPCs e objetos), o Inspector do **`PlayerContext`** agora mostra os outros componentes do jogador **em abas por grupo**.

- **Etiqueta**: `[PlayerModule("Movimento")]` (opcional: `[PlayerModule("Movimento", 2)]` para ordenar dentro do grupo) em cima da classe de qualquer `MonoBehaviour` do jogador, inclusive os seus. Todos os módulos novos deste pacote já vêm etiquetados.
- **Framework base sem etiqueta** (não alteramos os arquivos dele): `PlayerInputHandler`/`PlayerInput` → Entrada • `FirstPersonController`/`PlayerClimber` → Movimento • `PlayerInteractor`/`PlayerDragger` → Interação • `StatsComponent` → Status • `PlayerHUD` → Interface. Outros componentes `HorrorFPS` na raiz sem etiqueta → "Outros". Para mudar um deles de grupo, coloque a etiqueta na classe.
- **Agrupar / Desagrupar**: o botão no `PlayerContext` esconde os componentes soltos do Inspector (só visual, `HideFlags.HideInInspector`; continuam funcionando e salvos no prefab) e passa a mostrá-los só nas abas. "Desagrupar" volta tudo ao normal. Cada módulo tem a barra de título normal (ligar/desligar, menu de engrenagem com remover/copiar/reset).
- Limites: o botão afeta os módulos que existem no prefab/objeto (módulos que o jogo instala sozinho em tempo de jogo aparecem normais); handles de cena (`OnSceneGUI`) de um componente escondido não aparecem enquanto ele estiver agrupado — use "Desagrupar" para editar com handles.

## 13. Item Studio — fotografar o modelo 3D e gerar o ícone

**Tools > Horror FPS > 10. Item Studio** (ou `ItemStudioWindow.OpenFor(item)` por código). Uma janela só para os itens, no mesmo estilo da janela de inventário inicial.

**Layout**
- **Esquerda**: lista de todos os `ItemData` do projeto (busca por nome/id, miniatura do ícone, aviso "sem ícone — pronto p/ foto"). **＋ Novo item** cria um asset em `Resources/HorrorFPS/Items`.
- **Centro**: o modelo 3D numa moldura com a **proporção da caixa da forma** do item (2×1 células → imagem 2:1). A moldura mostra a grade de células e escurece as células vazias de formas como L/T. O que aparece dentro da moldura é exatamente o que sai na foto.
- **Direita**, duas abas: **📷 Foto** (câmera, luz, resolução, prévia "no inventário") e **Item** (o Inspector normal do `ItemData`, com o editor de forma).

**Controles no modelo**: botão esquerdo = girar • botão direito/meio (ou Alt + esquerdo) = mover • roda = zoom • duplo clique = enquadrar. Atalhos embaixo: Frente, Trás, Esquerda, Direita, Topo, 3/4, Enquadrar. Sliders de giro, inclinação, rolagem, zoom, campo de visão (ou ortográfica), margem, e a direção da luz (a luz acompanha a câmera, então o modelo tem a mesma iluminação em qualquer ângulo).

**Tirar foto**: o botão verde renderiza o modelo duas vezes (fundo preto e branco) e calcula a transparência pela diferença, o que dá **PNG com fundo transparente e bordas suaves**, sem halo. O arquivo vai para `<pacote>/Generated/ItemIcons/<id>.png` (a pasta pode ser trocada na aba Foto), é configurado como Sprite (sem mipmap, alpha como transparência) e ligado ao campo **Icon** do item. Tirar de novo sobrescreve o mesmo PNG e a referência continua válida.

**Fica guardado no item**: ângulo, zoom, posição, luz, resolução e qual modelo usar (campo `iconShot`, escondido no Inspector). Dá para reabrir o item meses depois e refazer a foto do mesmo ponto, ou mudar só o zoom.

**Modelo usado**: o `Inspect Prefab` do item; se estiver vazio, o `World Prefab` (e dá para inverter a preferência na aba Foto). Arraste um prefab para o palco 3D para definir o modelo de um item que ainda não tem. Scripts do prefab não rodam no estúdio (só o visual).

**Gerar ícones que faltam** (barra do topo): fotografa de uma vez todos os itens que têm modelo e não têm ícone, com o enquadramento padrão (3/4) ou o já guardado. Depois refine os que quiser, um a um. Com os itens de exemplo: rode **5. Create Sample Items** e depois esse botão.

**No inventário**: o ícone é desenhado sobre a peça (o fundo das células escurece quando há ícone, para a imagem aparecer bem) e **gira junto** quando o jogador rotaciona a peça (passos de 90°). A aba Foto tem uma prévia da peça com botão "↻ Girar peça" para conferir. No hotbar e nos slots da lista o ícone aparece como antes.

**Observações**
- Uma imagem sua (sem 3D) funciona igual: arraste o Sprite para o campo Icon na aba Item. O estúdio só mexe no Icon quando você tira a foto.
- A foto usa a câmera/luzes de pré-visualização do Editor; materiais com efeitos de tela (pós-processamento) não entram. Em URP/HDRP o brilho pode diferir um pouco do jogo — ajuste a luz.
- Meshes sem *Read/Write* ligado usam a caixa do mesh para enquadrar (um pouco mais folgada); com Read/Write o enquadramento é pelo contorno real.
- O tamanho máximo da imagem é 2048 px no lado maior.

## 14. Abas do inventário: Mochila, Chaves, Registros e Mapa

A tela do inventário do jogador agora tem **abas** (barra no canto superior direito da janela). Trocar de aba: **clique na aba**, **Z / X** (ou PageUp/PageDown) no teclado, **LT / RT** no controle. Com um baú aberto ao lado, a tela fica só com a mochila (sem abas), como antes.

| Aba | O que guarda | Como aparece |
|---|---|---|
| **Mochila** | itens normais | a grade de sempre |
| **Chaves** | chaves e itens de missão (categoria `Key`/`Quest`) | lista **ilimitada**: não ocupam espaço da mochila |
| **Registros** | diários, notas, gravações (categoria `Document`) | lista à esquerda + **leitor** do texto completo à direita; "● novo" até ler |
| **Mapa** | mapas (categoria `Map`) | lista à esquerda + **visualizador** com zoom e a seta de cada jogador |

**Como o item escolhe a aba**: pelo campo **Aba do inventário** do item (`Tab Rule`). Em `Auto` (padrão): categoria Mapa → Mapa; Documento → Registros; Chave ou Missão → Chaves; o resto → Mochila. Para forçar, escolha a aba no campo (ex.: um bilhete que deve ocupar espaço na mochila: `Tab Rule = Bag`). Ao pegar qualquer item do chão, de um baú ou por código (`AddItem`), ele já entra na aba certa; se o jogador não tiver a lista daquela aba (prefab antigo), cai na mochila. Soltar um item de aba especial na grade da mochila também o manda para a aba dele.

**Instalação**: os módulos do jogador criam sozinhos as listas `[Chaves]`, `[Registros]` e `[Mapas]` (filhas do objeto `[Inventory]`) ao instalar (menu **4. Add Item, Sanity and Body modules to Player Prefab**, ou automaticamente ao jogar). Elas são `ListContainer` normais com o campo **Tab** do contêiner na aba certa, então aparecem na janela **7. Player Starting Inventory** e dá para dar itens iniciais a elas (ex.: um mapa de início). Para outro objeto (baú, NPC) o campo **Tab** do contêiner também existe, mas as abas só aparecem na tela do jogador.

**Registros (diários e notas)**
- O texto completo vem do campo **Read Text** do item; se estiver vazio, usa a descrição. Ele aceita **variantes por sanidade/variáveis** e o glitch de sanidade baixa, como a descrição: o mesmo diário pode contar outra coisa para quem está mal (cada jogador lê o seu).
- Clique (ou Enter/A) numa linha para ler; roda do mouse/analógico direito rola o texto. Botão direito/X abre o menu do item (Inspecionar, Atalho...). Registros não são arrastáveis. Ao abrir um registro ele é marcado como lido (flag `read` da instância, vai no save); a aba mostra quantos não lidos há.
- Itens de exemplo: `diario` e `bilhete` já vêm com texto de leitura (crie com **5. Create Sample Items**).

**Mapa**
- No item (categoria `Map`), seção **Map**: `Image` (sprite do mapa; o **topo da imagem é o +Z do mundo**), `World Min` (canto inferior esquerdo, X e Z em metros) e `World Max` (canto superior direito) e `Show Players`. Com isso o jogo converte a posição de cada jogador para a imagem e desenha uma **seta** (amarela = você, azul = outros jogadores do split-screen) apontando para onde ele olha.
- Controles: roda do mouse (ou analógico direito) = zoom no ponto do cursor; segurar o botão de pegar (clique do mouse/A) e mover o cursor = arrastar o mapa. Se você estiver fora da área do mapa, ele avisa. Desligue `Show Players` para um mapa "de papel" que não mostra onde você está.
- Para acertar `World Min/Max`, use dois pontos conhecidos: posicione o jogador nos cantos do mapa e leia a posição (ou coloque dois objetos vazios nos cantos).
- Um mapa por nível: cada um é um item; o jogador pode ter vários, e a lista mostra todos. Para mapas que só valem num nível, nomeie-os assim ("Mapa — Porão") e use `HasItem` nas ações/variantes se precisar de lógica.

**Limites**
- Abas só aparecem com a mochila aberta sozinha; com baú aberto as chaves/registros ficam fora da tela (use o menu do item > "Mover para: Chaves" para mandar um item de um baú direto para a aba).
- Saves antigos: itens-chave que já estavam na grade da mochila continuam lá; só o que for pego depois vai para as abas.
- A janela cresce para caber o leitor e o mapa (altura mínima dedicada); em telas muito pequenas ela se reduz proporcionalmente.

## 15. Itens com função: lanterna, armas brancas e armas de fogo

Para **testar o inventário de verdade**, os itens equipados agora fazem coisas. Tudo mora no campo **Equipamento (ao equipar)** do item (`ItemData.equip`, tipo `ItemEquip`). O estado fica **na instância** do item (contadores/flags: balas no carregador, bateria, durabilidade, lanterna ligada), então vai no save, acompanha o item ao ser jogado no chão ou guardado no baú e é independente por cópia (duas pistolas têm carregadores separados).

### 15.1 Controles (só com o inventário fechado)

| Ação | Teclado/mouse | Controle |
|---|---|---|
| Equipar / guardar | atalho 1-9 do hotbar, ou menu do item > Equipar | igual |
| **Golpear / atirar** (usar equipado) | botão **direito** | **LT** |
| Recarregar | **R** | D-pad baixo |
| Ligar/desligar lanterna | **F** | D-pad cima |

Se só a lanterna estiver na mão, o botão de usar equipado também liga/desliga. **F** sem lanterna equipada equipa a primeira que houver na mochila e acende. O botão esquerdo continua sendo o de pegar/arrastar objetos do framework (por isso o ataque ficou no direito). Os botões são ações do `ItemInput` (`UseEquipped`, `Reload`, `ToggleLight`) e podem ser remapeados lá.

Arma na **mão** e lanterna ficam **ao mesmo tempo** (slots `mao` e `lanterna`). Trocar de arma tira a anterior da mão; guardar a arma durante uma recarga cancela a recarga.

### 15.2 Tipos de equipamento

| Tipo | O que faz | Campos principais |
|---|---|---|
| **Lanterna** | luz (Spot) presa à câmera, só você a controla; bateria que acaba, **pisca quando fraca** (abaixo de 20%) e falha com a sanidade baixa (`SanityLight`) | `lightRange`, `lightAngle`, `lightIntensity`, `lightColor`, `batterySeconds` (0 = infinita) |
| **Arma branca** | golpe na frente da câmera (SphereCast): arma o braço, bate e volta; o dano acontece no meio do movimento | `damage`, `range`, `swingSeconds`, `hitRadius`, `durability` (0 = não quebra), `noise` |
| **Arma de fogo** | disparo por raio, carregador, dispersão, vários projéteis (espingarda), automática (segurar), coice na cabeça, clarão e barulho que expõe a posição | `damage` (por projétil), `range`, `shotsPerSecond`, `automatic`, `pellets`, `spread`, `magazineSize`, `ammoItem`, `reloadSeconds`, `recoil`, `noise` |

Comuns: **modelo na mão** (`heldPrefab`; vazio = modelo de inspeção do item; sem nenhum, um bloco colorido), `heldPosition` (em relação à câmera), `heldEuler`, `heldScale` e `useSound` (vazio = som gerado por código; `emptySound` e `reloadSound` também).
Os modelos de exemplo ficam **deitados no eixo X** (lanterna, armas de fogo) ou **em pé no eixo Y** (armas brancas, com a origem no punho): por isso as predefinições usam `heldEuler = (0,-90,0)` e `(-25,0,-10)`; ajuste ao trocar por modelos seus. O modelo na mão é só visual (colisores, scripts e sombras são removidos na cópia).

**Munição**: a arma de fogo consome o item definido em `ammoItem` da **mochila** (qualquer aba/contêiner do jogador) ao recarregar; sem `ammoItem` a munição é infinita. O carregador começa cheio. Sem balas, o clique seco avisa "Sem balas no carregador — R para recarregar".
**Pilhas**: o item `pilhas` de exemplo tem a ação *Trocar na lanterna* (efeito `Message("flashlight_recharge")`); o `EquipmentController` recebe a mensagem (`IItemUseHandler`) e enche a bateria da lanterna. Ele cancela o uso (e não gasta a pilha) se a lanterna já estiver carregada ou não existir.
**Quebra**: com `durability > 0` cada golpe que acerta algo gasta 1; no fim o item **quebra** e some do inventário. Aviso quando falta 20%.

### 15.3 No Inspector: botões de modelo

No campo **Equipamento**, o seletor **Tipo** mostra só os campos daquele tipo, e a linha **Modelos** tem cinco botões que preenchem tudo (e o **slot de equipar**, `lanterna` ou `mao`): *Lanterna, Arma branca, Pistola, Espingarda, Automática*. Mantêm o modelo na mão, a munição e os sons já escolhidos. Para criar uma arma nova: duplique um item, clique no modelo mais próximo e ajuste os números. O painel de informações do inventário mostra o estado (**Carregador 7/9**, **Bateria 82% • ligada**, **Durabilidade 32/40**), e o canto inferior direito da tela mostra o mesmo em jogo (balas / reserva, durabilidade, bateria, barra de recarga).

### 15.4 Dano e inimigos

Cada acerto faz **três coisas**, nesta ordem, em tudo que o raio/golpe atingir:
1. `IItemHittable.OnItemHit(ItemHit)` em quem tiver (inclusive nos pais do colisor atingido). `ItemHit` traz quem atacou (`attacker`), a arma (`weapon`), o dano, o ponto, a direção e se foi à distância.
2. `IDamageable.ApplyDamage(new DamageInfo(dano, DamageType.Physical, jogador))` — quem tem `StatsComponent` (preset **Monstro** ou **Objeto**, como no framework) perde vida normalmente.
3. Impulso físico se o objeto tiver `Rigidbody` não-cinemático (dano × 0,12 por padrão, ajustável em `hitImpulsePerDamage`) e uma faísca no ponto de impacto.

`HitTarget` (menu de componentes **HorrorFPS > Equipment > Hit Target**) é um **boneco de treino**: perde vida, pisca, cai ao morrer e levanta depois de 4 s, e mostra o dano na tela de quem bateu (também no Console). A bancada de testes (menu 6) cria quatro. Para inimigos de verdade, use `StatsComponent` ou implemente `IItemHittable` no seu script.
Barulho: `noise` vira um evento de `NoiseSystem` (`Gunshot` para tiros, `Impact` para golpes); um tiro de pistola (55 m) é ouvido de longe, uma faca (6 m) quase não. O coração acelera e a cabeça balança com cada tiro (`HeartRate`, `DynamicHeadBob`, `CameraImpulse.Recoil`).

### 15.5 Itens de exemplo e como atualizar

**Tools > Horror FPS > 5. Create Sample Items** agora também cria **faca, taco com pregos, espingarda, cartuchos calibre 12 e kit médico** (e modelos 3D simples para eles e para o pé de cabra), e **atualiza os itens de exemplo que você já tinha** (lanterna, pé de cabra, pistola) para os novos equipamentos, **só se o Tipo ainda for Nenhum**; remove as ações de teste antigas (*Balançar*, *Disparar (teste)*), que o equipamento substitui. Quem já escolheu um Tipo no Inspector não é tocado.

| Item | Tipo | Notas |
|---|---|---|
| Lanterna | Lanterna | 6 min de bateria; as pilhas recarregam |
| Pé de cabra | Arma branca | 30 de dano, não quebra |
| Faca | Arma branca | 18 de dano, rápida e silenciosa, quebra em 40 golpes |
| Taco com pregos | Arma branca | 40 de dano, lento e barulhento, quebra em 25 golpes |
| Pistola | Arma de fogo | 9 balas, 35 de dano, munição `municao` |
| Espingarda | Arma de fogo | 6 cartuchos, 8 projéteis de 12, recarga lenta, munição `cartuchos` |
| Kit médico | Consumível | +75 de vida em 8 s |

A bancada de testes (menu 6) tem todos no chão, o baú tem kit e cartuchos, e quatro bonecos de treino a 7–20 m.

### 15.6 Limitações

- O modelo na mão é desenhado **na mesma câmera do jogo**: perto de paredes ele pode atravessá-las (não há câmera de "viewmodel" separada). Se incomodar, afaste `heldPosition` ou diminua `heldScale`.
- Não há animação de mira (zoom) nem de troca de arma; a recarga é uma animação simples de abaixar e subir.
- O golpe e o tiro acertam qualquer colisor (menos o do próprio jogador); ajuste a `hitMask` do `EquipmentController` para ignorar camadas.
- O ataque usa o botão direito/LT porque o esquerdo é o de pegar objetos do framework; remapeie `UseEquipped` no `ItemInput` se preferir.
- `DamageInfo`/`IDamageable` seguem a assinatura do documento de arquitetura (código do framework base não foi alterado nem lido diretamente): se o seu projeto tiver outra assinatura, ajuste a linha em `EquipmentController.ApplyHit`.

## 16. Som que a IA escuta: `SoundEmitter`, `INoiseHearer` e o OverlapSphere

Como o barulho chega até a IA: o `NoiseSystem.Emit(posição, raio, tipo, origem)` faz um **`Physics.OverlapSphere`** no ponto com o raio do barulho (sem alocar memória) e avisa, **uma vez só por barulho**:
1. todo script que implementa **`INoiseHearer`** e esteja num objeto (ou num pai) que tenha um **Collider** dentro do raio — o collider do corpo do monstro já serve; pode ser também um trigger só para "orelhas";
2. todo **`NoiseListener`** ativo, mesmo sem collider (o NoiseListener é um `INoiseHearer` pronto);
3. o evento estático `NoiseSystem.Emitted` (para sistemas globais).

Avisos de dentro de um ouvinte que também faz barulho (o monstro que ruge ao ouvir) entram numa fila e são entregues depois, sem reentrada. Uma exceção num ouvinte é registrada no Console e não impede os outros.

### 16.1 Qualquer script de IA ouvindo

```csharp
public class Monstro : MonoBehaviour, INoiseHearer
{
    public Transform ouvido;                    // cabeça
    public UnityEngine.AI.NavMeshAgent agente;

    public void OnNoiseHeard(NoiseEvent ruido)
    {
        if (ruido.IsFrom(transform)) return;    // ignora o próprio barulho
        float volume = NoiseSystem.LoudnessAt(ruido, ouvido.position, 1f, 0.6f);   // 0 = não chegou (longe ou muita parede)
        if (volume <= 0.05f) return;
        agente.SetDestination(ruido.position);  // vai investigar
    }
}
```
`NoiseEvent` traz `position`, `radius`, `kind` (`Footstep`, `Gunshot`, `Impact`, `Explosion`, `Custom`...), `source` (quem fez) e `time`. `NoiseSystem.LoudnessAt(evento, posiçãoDoOuvido, hearingMultiplier, occlusionFactor, máscaraDeParedes)` devolve 0..1: considera o alcance, quanto o ouvinte ouve bem (2 = ouve o dobro) e **paredes no caminho** (reduzem o alcance para a fração `occlusionFactor`; 1 = paredes não abafam). O `OverlapSphere` já filtra por distância bruta, então só quem está dentro do raio × `SearchMultiplier` (2) é avisado; `LoudnessAt` é o veredito real.

### 16.2 `SoundEmitter` — toca o som e faz o barulho

Componente (**HorrorFPS > Body > Sound Emitter**) para portas, rádios, alarmes, goteiras, vidro quebrando, objetos que caem:

| Campo | O que faz |
|---|---|
| `clips` | um é sorteado a cada vez (vazio = só barulho, sem áudio) |
| `volume`, `pitchMin/Max`, `hearDistance` | o som em si: o jogador o ouve em 3D até `hearDistance` metros |
| `noiseRadius`, `noiseKind` | o **OverlapSphere** da IA: raio em metros (0 = silencioso para a IA) e o tipo do barulho |
| `origin` | de onde sai o som (vazio = o próprio objeto); quem está na mesma hierarquia não ouve o próprio som |
| `playOnEnable`, `repeatSeconds`, `repeatJitter` | tocar ao ativar e/ou repetir (alarme a cada 8 s ± 2) |
| `onPlayed` | UnityEvent depois de tocar |

`Play()` toca (ligue a um UnityEvent, Animation Event, botão), `PlayClip(clip)` toca outro clip, `MakeNoise()` só faz o barulho. Selecionado, o objeto mostra o raio como esfera amarela na Scene.
Por código, sem componente: `SoundEmitter.PlayAt(clip, posição, raio, NoiseKind.Impact, quemFez, volume, distânciaQueOJogadorOuve)` (clip pode ser nulo: só barulho).

### 16.3 Ajustes e depuração

- `NoiseSystem.HearingMask` — camadas do OverlapSphere (padrão: todas). Ponha seus inimigos numa camada "Enemy" e use `NoiseSystem.HearingMask = LayerMask.GetMask("Enemy")`: o OverlapSphere fica bem mais barato com muitos colliders.
- `NoiseSystem.SearchMultiplier` (2) — o OverlapSphere procura até raio × isto, para alcançar ouvintes com `hearingMultiplier` alto.
- `NoiseSystem.GlobalScale` — escala todos os raios (dificuldade, vento).
- `NoiseSystem.DebugDraw = true` — desenha a esfera de cada barulho (amarela) e linhas até quem foi avisado (laranja) por 1,5 s, com **Gizmos** ligados na Game view.
- Um monstro sem collider no corpo só é avisado se tiver `NoiseListener` (ou um filho com collider/trigger e o `INoiseHearer` no pai). Pés/passos do jogador, tiros (`Gunshot`), golpes (`Impact`) e itens já emitem sozinhos.

## 17. Inventário: menu por foco, animações, sons, tamanhos e arrasto

Tudo isto vive na tela do inventário de cada jogador (`InventoryScreen`, no filho `[Inventory]`/overlay do jogador) e nos módulos `Hotbar` e `InventoryOwner`.

### 17.1 Menu do item: o E escolhe a opção

O menu do item (Usar, Equipar, Inspecionar, Atalho…, Jogar fora…) tem **sempre uma opção em foco**: a primeira habilitada ao abrir, com uma barrinha e um destaque que desliza de uma linha para a outra.

| Entrada | O que faz com o menu aberto |
|---|---|
| **E**, Enter, Espaço (teclado) • **A** ou **X** (controle) | ativa a opção **em foco** (não clica no item de baixo) |
| Setas/WASD • D-pad | move o foco; pula opções bloqueadas e dá a volta nas pontas |
| Mouse | o foco acompanha o ponteiro **só quando ele anda** (ponteiro parado não rouba o foco do teclado); clique na linha ativa essa linha; clique fora fecha |
| Botão direito • Backspace • B | fecha sem escolher |

Opção bloqueada (aparece com o motivo): o E só **balança a linha** e toca o som `MenuDenied`. O menu consome toda a entrada enquanto está aberto, então nenhuma tecla vaza para o item que está por baixo. A dica no rodapé do menu muda com o dispositivo ("E escolhe • Setas movem • Botão dir. fecha" no teclado; "A/X escolhe • D-pad move • B volta" no controle). A lógica fica em `ItemMenu.cs` (`MoveFocus`, `PickFocused`, `Click`, `UpdatePointer`) e na ação `MenuPick` do `ItemInput`.

**Por que não o EventSystem/navegação da Unity?** O sistema de navegação da Unity (`Selectable`, `EventSystem`) tem **uma única seleção global**; o inventário daqui é **um por jogador** (split-screen, cada um com os próprios dispositivos), é desenhado por código com um cursor virtual e tem grade de formato livre com arrastar e soltar. Para usá-lo seria preciso um `MultiplayerEventSystem` + módulo de entrada por jogador, e a grade ainda teria de ser feita à mão. O problema real era o menu não ter o conceito de "opção em foco", e foi isso que se resolveu (o mesmo modelo do `Selectable`, dentro do `ItemMenu`). O EventSystem continua nos menus **globais** (menu principal, pausa, game over, slots de save, controles, opções), onde há um só jogador navegando.

### 17.2 Animações

Ligadas por padrão; ajustáveis no `InventoryScreen`, seção **Animações**:

| Campo | Para quê |
|---|---|
| `animations` | liga/desliga tudo (desligado = tudo instantâneo) |
| `animationSpeed` | 0,5 a 3 (1 = normal; 2 = o dobro de rápido) |
| `shakePixels` | quanto a peça/atalho balança de um lado ao outro quando **não cabe** (0 = não balança) |

| Momento | Animação |
|---|---|
| **Encaixou** | cada célula ocupada pisca em **verde**, uma moldura se expande em volta da peça e um "eco" dela assenta (cresce e some) |
| **Juntou pilha** | pisca em **azul** e sobe um "+N" |
| **Trocou de lugar** com outra peça | pisca em **amarelo** |
| **Não coube** (ocupado, fora da grade, contêiner não aceita) | pisca em **vermelho**, a peça na mão balança e depois **voa de volta** ao lugar de origem |
| **Cancelou** (Backspace/B, ou o botão de abrir/fechar o inventário) | a peça voa de volta à origem |
| **Soltou na barra de atalhos** | o slot pulsa e ganha moldura (balança em vermelho se recusar); a peça voa até ele |
| **Jogar fora / bandeja** | a peça voa até a zona ou a bandeja; a zona pisca |
| **Organizar (O)** | as peças da grade pulsam |
| **Menu do item** | brota do ponteiro, o destaque desliza, some com um piscar ao escolher |
| **Abrir/fechar a janela** | fade da janela e do fundo escurecido |

Os efeitos são desenhados numa camada própria (`fxLayer`) acima da janela e abaixo do menu e do cursor, então sobrevivem quando as grades se redesenham. O código está em `InvFx.cs` (motor de efeitos: `Flash`, `Frame`, `Settle`, `Fly`, `ShakeOut`, `Punch`, `Shake`, `Float`) e `InventoryScreen.Fx.cs` (quando cada um dispara). Para um efeito novo, escreva um método `Fx...` ali e chame-o onde o momento acontece.

### 17.3 Sons (deixado pronto)

Cada momento chama `Play(InvSound)`:

- O evento **`InventoryScreen.SoundPlayed`** (`Action<InvSound>`) é disparado **sempre**, mesmo sem nenhum clipe — dá para ligar FMOD, Wwise ou o seu sistema de áudio sem mexer no inventário.
- Se quiser só arrastar clipes: campo **Sounds** do `InventoryScreen` (`InventorySounds`): um clipe por momento (todos opcionais) e `volume` geral. Toca em 2D num `AudioSource` criado sozinho.

Momentos (`InvSound`): `Open`, `Close`, `TabSwitch`, `PickUp`, `Rotate`, `DropFit` (encaixou), `DropMerge` (juntou pilha), `DropSwap` (trocou), `DropReject` (não coube), `DropHotbar`, `HotbarClear`, `DropWorld` (jogou fora), `DropTray`, `ReturnToOrigin`, `Organize`, `MenuOpen`, `MenuMove`, `MenuPick`, `MenuDenied`, `MenuClose`.

> No split-screen o som 2D é **compartilhado** (só existe um `AudioListener`): se os dois jogadores mexerem no inventário ao mesmo tempo, os sons se somam. Cada jogador tem a própria tela (e o próprio evento `SoundPlayed`), então quem precisar separar os sons por jogador consegue pelo evento (`InventoryScreen.Player` diz de quem é a tela).

### 17.4 Arrasto: espera antes da barrinha

Com o mouse, a barrinha que enche ao segurar um item só aparece depois de `dragChargeDelay` (padrão 0,18 s); um clique mais rápido que isso (que abre o menu) nunca a mostra. Ela enche entre `dragChargeDelay` e `dragHoldSeconds` (padrão 0,4 s), e o item é pego ao terminar. Se `dragHoldSeconds` for menor que `dragChargeDelay + 0,15 s`, vale `dragChargeDelay + 0,15 s` (o enchimento nunca dura menos de 0,15 s). Atenção: um prefab que já salvou `dragHoldSeconds = 0,22` continua com esse valor no Inspector — o efetivo vira 0,33 s; ajuste no Inspector para o 0,4 s novo.

### 17.5 Quantidade de atalhos (hotbar) e tamanho da mochila

**Atalhos.** As teclas são 1–9, 0, - e = (**até 12**, `Hotbar.MaxSlots`). A quantidade vem de:
- `GameCatalog` > **Hotbar Slots** (1–12): o padrão de todo jogador;
- `Hotbar` do jogador > **Slots Override** (0 = usa o padrão; 1–12 = só este jogador);
- por código: `jogador.GetComponent<Hotbar>().SetSlots(6)`.

A barra na tela e o texto das dicas se adaptam à quantidade (os slots encolhem para caber). Para mais de 12, aumente `Hotbar.MaxSlots` e acrescente teclas em `Hotbar.KeyPath()`/`KeyLabel()`; o controle (D-pad) já escolhe qualquer slot, com ou sem tecla.

**Mochila.** No `InventoryOwner` do jogador, seção **Tamanho da mochila**: `Bag Columns` e `Bag Rows` (0 = usa o tamanho escrito no `GridContainer` da mochila). Por código: `owner.SetBagSize(10, 8)` (ou `GridContainer.SetSize(colunas, fileiras)` direto). Ao mudar, a grade é redimensionada: o que não couber mais é realocado e, se ainda sobrar, jogado no chão (a mesma conferência que roda ao abrir/fechar o inventário).
