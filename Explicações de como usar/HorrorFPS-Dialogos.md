# Horror FPS Framework — Sistema de Diálogos

Módulo de diálogos com **árvores ramificadas, condições, eventos, animações, itens, sanidade e multiplayer local (split-screen)**, encaixado nos módulos já documentados (Arquitetura e Itens/Sanidade/Corpo).

> **Aviso importante:** este módulo foi escrito **sem compilar** (como os módulos anteriores) e **sem ter o código real do framework** à mão — só a documentação. Toda chamada ao framework passa por **um único arquivo** (`DialogueBridge.cs`). Se algo não compilar ou não travar o jogador, mande a mensagem do Console: o ajuste é localizado ali (ver seção 14).

---

## 1. O que vem no pacote

```
Assets/HorrorFPS/Dialogue/            (runtime)
  DialogueData.cs            nós, escolhas, enums, ids especiais
  DialogueGraph.cs           ScriptableObject do diálogo + validação
  DialogueCondition.cs       condições + chaves do save
  DialogueAction.cs          ações (itens, variáveis, eventos, animação, sanidade...)
  DialogueText.cs            tokens {var:..}, glitch de sanidade, estilo global (DialogueStyle)
  DialogueContext.cs         DialogueOptions, DialogueContext, DialogueSession
  DialogueBridge.cs          ÚNICO ponto de contato com o framework
  DialogueEvents.cs          eventos por id (código + cena)
  DialogueManager.cs         executa as conversas (singleton criado sozinho)
  DialogueInput.cs           entrada por jogador (teclado/mouse/controle)
  DialogueCameraLook.cs      câmera olha de relance para quem fala
  DialogueView.cs            UI por jogador (uGUI, criada por código)
  DialogueActor.cs           "quem fala" na cena (nome, retrato, Animator, voz)
  DialogueInteractable.cs    NPC que conversa ao apertar Interagir
  DialogueEventRelay.cs      liga eventos do diálogo a objetos da cena
  DialogueTrigger.cs         começa um diálogo ao entrar numa área / ao iniciar o nível
  DialogueStep.cs            passo de cutscene que toca um diálogo
Assets/HorrorFPS/Editor/Dialogue/     (somente editor)
  DialogueGraphWindow.cs     editor visual de nós
  DialogueDrawers.cs         Inspector amigável (condições, ações, animações, id de nó)
  DialogueGraphInspector.cs  botão "Abrir editor" + validar
  DialogueMenus.cs           exemplos prontos + props de demonstração
```

Menus novos: **Tools ▸ Horror FPS ▸ 11. Dialogue Editor**, **12. Create Sample Dialogues**, **13. Create Dialogue Demo Props In Open Scene**.

Requisitos: Unity 2022.3 LTS ou Unity 6, pacote **Input System**, uGUI. Namespace `HorrorFPS`. Sem `.asmdef`.

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` para o projeto e espere compilar.
2. **Tools ▸ Horror FPS ▸ 12. Create Sample Dialogues** — cria `Sample_Zelador` e `Sample_Pensamento` em `Assets/HorrorFPS/Generated/Dialogues/`.
3. Abra a cena de teste e use **13. Create Dialogue Demo Props In Open Scene**. Aparecem: um NPC (cápsula "Zelador"), uma barreira, uma luz de alarme e uma área de gatilho.
4. Dê Play, chegue perto do NPC e use **Interagir**. A opção "Tenho a chave do portão" aparece desabilitada até você ter o item `chave_portao`; entregue-a e a barreira some (evento `abrir_passagem`).
5. Entre na área de gatilho: o pensamento aparece como legenda **sem travar** o personagem.

> Se o jogador não "enxerga" o NPC, a cápsula está na layer errada: coloque-a na **layer de interação do framework** (a mesma das suas portas/alavancas).

## 3. Conceitos

| Conceito | Para que serve |
|---|---|
| **Dialogue Graph** | Asset (`Create ▸ HorrorFPS ▸ Dialogue ▸ Dialogue Graph`) com todos os nós. Dois cliques abrem o editor. |
| **Nó** | Passo da conversa: Fala, Condição, Sorteio, Ação, Salto ou Fim. |
| **Dialogue Actor** | Componente na cena que dá identidade a quem fala (id, nome, retrato, Animator, voz). |
| **Dialogue Interactable** | Faz o objeto "falável" com a tecla de Interagir; escolhe qual grafo tocar. |
| **Evento** | Texto (id) disparado por uma ação; quem escuta (código ou `DialogueEventRelay`) reage. |
| **Variável** | Valor guardado no `GameSession` (entra no save sozinho). Serve para "habilitar outra condição". |

**Apresentação** (campo `presentation` do grafo):
- **Full** — trava os controles do jogador, mostra escolhas, câmera olha o falante.
- **Subtitle** — só legenda, o jogador continua andando; avança sozinho por tempo (bom para pensamentos, rádio, sustos).

**Quem fala** (`speakerId` da fala): vazio = o ator da conversa (quem você passou ao tocar) · id de um `DialogueActor` · `@player` (fala interna do jogador, usa `DialogueStyle.PlayerName`) · `@narrator` (sem nome).

**Audiência** (`DialogueAudience`): `Initiator` (só quem iniciou vê/decide) · `AllPlayers` (todos veem a fala na própria metade de tela; só o iniciador escolhe, os outros veem "Jogador N está escolhendo...").

## 4. Editor visual

- **Criar nó:** botões da barra (+ Fala, + Condição…) ou botão direito no fundo.
- **Ligar:** clique no quadradinho colorido à direita de uma saída, depois clique no nó de destino. Botão direito na saída apaga a ligação.
- **Mover vista:** arraste o fundo ou use o botão do meio. **Mover nó:** arraste o cabeçalho.
- **Menu do nó (botão direito):** definir como início, duplicar, apagar ligações, excluir. Desfazer (Ctrl+Z) funciona.
- **Painel à direita:** campos do nó selecionado (só os que importam para o tipo).
- **Organizar:** arruma em colunas a partir do início. **Validar:** lista ligações quebradas, nós inacessíveis, falas sem texto, sorteios vazios.
- Nós em cinza-escuro = **inacessíveis** a partir do início. O nó inicial tem `*` no título.

## 5. Tipos de nó

| Nó | Saídas | Como funciona |
|---|---|---|
| **Fala (Line)** | uma por escolha + "padrão" | Mostra o texto (máquina de escrever). Sem escolhas: espera Interagir (ou avança sozinha se `autoAdvance`/Legenda). Com escolhas: o jogador escolhe e segue o `next` da escolha; se a escolha não tem `next`, usa o "padrão". |
| **Condição (Branch)** | uma por rota + "senão" | A **primeira rota** cujas condições passam vence. Nenhuma passou: segue o "senão". |
| **Sorteio (RandomPick)** | uma por opção + "nenhuma serve" | Sorteia por **peso** entre as opções cujas condições passam. |
| **Ação (Action)** | próximo | Executa a lista de ações em **sequência** (aceita `Wait`) e segue. |
| **Salto (Jump)** | — / próximo | Vai para outro nó do mesmo grafo (`next`) **ou** para outro grafo (`jumpGraph` + `jumpNode`, vazio = início dele). |
| **Fim (End)** | — | Encerra a conversa. Um nó sem destino também encerra. |

Campos extras da **Fala**:
- `variants`: textos alternativos com prioridade + condições (vence a maior prioridade cujas condições passam; senão o texto principal). Ex.: o NPC fala diferente se a sanidade está baixa.
- `glitchAtLowSanity`: troca letras por símbolos quando a sanidade de **quem lê** está baixa (determinístico: o mesmo texto não "treme" a cada frame).
- `animations`: mexem no Animator de um ator ao começar a fala.
- `onStart`: ações **em paralelo** com a fala. `onEnd`: ações ao terminar (depois da escolha, antes de seguir).
- `voice`: áudio; `duration`: 0 = automático (duração do áudio ou pelo tamanho do texto).
- `choiceTimeLimit` + `timeoutChoice`: escolha **cronometrada**; ao acabar o tempo toma a escolha de índice `timeoutChoice` (−1 = segue o "padrão").

### Escolhas
- `conditions`: todas precisam passar.
- `whenLocked`: **Hide** (some) ou **ShowDisabled** (aparece cinza com `lockedHint`, ex.: "precisa da chave").
- `once`: some depois de escolhida uma vez (vai para o save).
- `actions`: rodam ao escolher, antes de seguir (ex.: tirar a chave do inventário).

### Tokens no texto
`{var:chave}` valor de variável · `{count:itemId}` quantidade do item no inventário de quem lê · `{name}` nome de quem fala. Texto desconhecido fica como está.

## 6. Condições

| Tipo | Campos | Passa quando |
|---|---|---|
| Variable | `key`, `expected` | a variável bool é igual a `expected` |
| Number | `key`, `compare`, `number` | variável numérica compara com o valor |
| Text | `key`, `text` | variável de texto é igual a `text` |
| HasItem | `key` = id do item, `compare`, `number` | quantidade no inventário de quem fala |
| Sanity | `compare`, `number` (0–100) | sanidade de quem fala |
| NodeVisited | `key` = id do nó | aquele nó deste grafo já foi visitado (salvo) |
| GraphCompleted | `key` = id do grafo (vazio = este), `compare`, `number` | diálogo terminou N vezes |
| Chance | `number` (%) | sorteio a cada avaliação |
| PlayerCount | `compare`, `number` | nº de jogadores |
| Custom | `key` = id registrado, `text` = argumento | sua função (ver API) |

Todas têm **NÃO** (inverte o resultado). `compare`: `==, !=, >, >=, <, <=`.

## 7. Ações

| Tipo | Campos | Efeito |
|---|---|---|
| SetVariable | `key`, `flag` | liga/desliga variável bool |
| SetNumber / AddNumber | `key`, `number` | define / soma variável numérica |
| SetText | `key`, `text` | guarda texto |
| GiveItem / RemoveItem | `key` = id do item, `number`, `text` (aviso opcional) | inventário de quem fala; o aviso aparece na tela |
| Event | `key` = id do evento, `text` = argumento | dispara o evento (ver seção 8) |
| Animation | `animation` | mexe no Animator de um ator |
| PlaySound | `clip`, `actorId`, `number` = volume | som 3D no ator (ou 2D sem ator) |
| Sanity / SanityAll | `number` | soma/subtrai sanidade de quem fala / de todos |
| Damage | `number` | dano físico a quem fala |
| CameraShake | `number` (0–1) | treme a câmera de quem fala |
| Scare | `number` (0–1), `actorId`, `flag` | dispara o coração/alerta perto do ator (`flag` = só quem está olhando) |
| Wait | `number` | espera (só tem efeito em listas sequenciais: nó Ação, ações de escolha) |
| GoToLevel | `key` = nível, `text` = spawn | troca de nível **depois** que o diálogo termina |
| StartDialogue | `graph` | começa outro diálogo **depois** que este termina |
| Autosave | — | salva no slot automático **depois** que o diálogo termina |
| Custom | `key` = id registrado, `text` | sua função; se não houver, vira um evento |

Toda ação aceita uma lista **"Só se…"** (condições): a ação só roda se todas passarem.

## 8. Eventos: abrir porta, ligar luz, habilitar condição

**Habilitar outra condição** = ação `SetVariable`/`SetNumber`; qualquer condição `Variable`/`Number` (em qualquer grafo, ou código seu via `GameSession`) enxerga a mudança e ela vai para o save.

**Mexer na cena** = ação `Event` com um id. Quem escuta:

1. **`DialogueEventRelay`** (componente, no NPC, na porta ou num objeto "central de eventos"): lista de entradas `{eventId, onRaised (UnityEvent), onRaisedWithArgument (UnityEvent<string>), interact (Interactable[])}`.
   - **Abrir uma porta:** arraste a `Door` do framework em `interact`. O relé chama `Interact` com o jogador da conversa, **se** `CanInteract` for verdadeiro (porta trancada continua trancada).
   - **Qualquer outra coisa:** use `onRaised` (ativar objeto, tocar áudio, iniciar cutscene, spawnar inimigo...).
2. **Código:** `DialogueEvents.On("alarme", (ctx, arg) => ...)`.
3. **Global:** `DialogueEvents.Raised` recebe todos.

## 9. Animações

O `DialogueActor` controla o `Animator`:
- Parâmetro **`Talking`** (bool; nome em `talkingParameter`) fica ligado enquanto a fala "digita" — crie esse bool no Animator para o NPC mexer a boca/gesticular.
- `DialogueAnimation`: `Trigger`, `Bool`, `Float`, `Int` ou `CrossFade` (estado + tempo de transição + layer), em qualquer ator (`actorId`; vazio = quem fala).
- Rodam no início de uma fala (`animations`) ou como ação (`Animation`).
- Parâmetro inexistente gera **um** aviso no Console (não spamma) e é ignorado. Ator sem Animator é ignorado em silêncio.
- O ator **vira para o jogador** durante a conversa (`faceListener`, `turnSpeed`) e volta à rotação original depois (`restoreRotation`).

## 10. Componentes de cena

**DialogueActor** — `actorId` (único na cena), `displayName`, `nameColor`, `portrait`, `animator`, `talkingParameter`, `voiceSource`, `blipEveryChars`/`blipVolume` (som de "fala" por letra), `faceListener`, `faceRoot`, `lookPoint`/`lookOffset` (para onde a câmera do jogador olha).

**DialogueInteractable** — herda de `Interactable`. `dlgEntries`: lista `{graph, priority, conditions, startNode}`; vale a **maior prioridade** cujas condições passam (o NPC muda de papo conforme a história sem mexer no grafo). Também: `dlgTalkVerb` ("Falar"), `dlgBusyText`, `dlgNothingText`, `dlgAudience`, `dlgFaceSpeaker`.

**DialogueTrigger** — `mode`: `OnEnter` (collider em Trigger), `OnLevelStart`, `Manual` (chame `Play()` de um UnityEvent). `playOnce` (lembrado no save por `id`), `startDelay`, `conditions`, `onFinished`.

**DialogueStep** — passo de cutscene: toca o grafo, espera terminar (`waitForEnd`), pular a cutscene encerra o diálogo.

## 11. Multiplayer local

- A UI é criada **por jogador**, ancorada no retângulo da câmera dele (`cam.rect`), e lê a **sanidade dele** (glitch e condições `Sanity` valem por jogador na hora de ler).
- Com `AllPlayers`, os espectadores veem a fala e a mensagem "Jogador N está escolhendo..." na própria metade.
- Cada jogador usa **o próprio dispositivo** (`PlayerInput.devices`): teclado e controle não se misturam.
- Um jogador pode estar em conversa enquanto o outro anda.
- Se a câmera do jogador está desligada (cutscene), a UI cai para tela cheia.

## 12. Controles

| Ação | Teclado / Mouse | Controle |
|---|---|---|
| Avançar / confirmar | E, Espaço, Enter, clique | A / X |
| Navegar nas escolhas | W/S, setas, scroll | direcional ↑↓ ou analógico esquerdo |
| Escolher direto | 1–9 | — |

Durante escolhas, o mouse é liberado; a UI é um canvas `Overlay` (ordem em `DialogueStyle.SortingOrder`, padrão 250). O "E" que **abriu** a conversa não avança a primeira fala (há uma guarda de 0,3 s).

Estilo global (`DialogueStyle`, estático — altere no seu `Awake`): `CharsPerSecond`, `SortingOrder`, `LookAtSpeaker/LookStrength/LookMaxYaw/LookMaxPitch`, `PlayerName`, `SpectatingText`, cores, `Font`.

## 13. API de código

```csharp
// tocar
DialogueManager.Play(graph, player);
DialogueManager.Play(graph, player, speakerActor);
var s = DialogueManager.Play(graph, player, new DialogueOptions {
    speaker = actor, startNode = "a1b2c3", audience = DialogueAudience.AllPlayers,
    faceSpeaker = true, interrupt = false, onEnded = ses => Debug.Log(ses.Completed) });

// consultar / parar
DialogueManager.IsInDialogue(player);   DialogueManager.SessionOf(player);
DialogueManager.Stop(s);                DialogueManager.StopAll();

// eventos globais
DialogueManager.Started / Ended / NodeEntered / ChoiceMade

// eventos do diálogo
DialogueEvents.On("abrir_passagem", (ctx, arg) => { ... });
DialogueEvents.Raise("alarme");

// condição e ação próprias (tipo Custom)
DialogueConditions.Register("tem_lanterna_ligada", (ctx, arg) => minhaLanterna.Ligada);
DialogueActions.Register("curar", (ctx, arg) => ctx.Player.GetComponent<MeuVida>().Curar(25));
```

`DialogueSession`: `Finished`, `Completed` (chegou ao fim) / `Aborted`, `CurrentNode`, `CurrentSpeaker`, `Viewers`, `Stop()`.

## 14. Ajustes no framework (`DialogueBridge`)

Tudo o que o módulo supõe do framework está em `DialogueBridge.cs`. **Premissas ainda não conferidas contra o código real:**

| Premissa | Se falhar |
|---|---|
| Trava de controle: `AddControlLock(string)` / `RemoveControlLock(string)` no `PlayerContext` (chamados por **reflexão**, chave `"Dialogue"`) | O jogador anda durante o diálogo. O Console mostra um aviso apontando o método; ajuste `SetLock`. |
| `GameSession.GetBool/SetBool/GetInt/AddInt/SetString`; leitura de string por reflexão (`GetString`) | Erros de compilação ou condições `Text` sempre vazias. |
| `InventoryOwner.AddItem/RemoveItem/CountOf` e `ItemDatabase.Find` | Itens não entregues/contados. |
| `SanityComponent.Of/Add/NormalizedOf`, `SanityEvents.AddAll` | Condição/ação de sanidade. |
| `CameraDelta.euler` (câmera de relance): sinal de yaw/pitch | A câmera olha para o lado oposto: inverta o sinal em `DialogueCameraLook`, ou desligue com `DialogueStyle.LookAtSpeaker = false`. |
| `SceneDirector.IsBusy`, `GoToLevel(level, spawn)`; `SaveSystem.CanSave/Autosave()` | Ações `GoToLevel`/`Autosave`. |
| `Interactable` com `DefaultVerb`, `CanInteract`, `GetBlockedReason`, `Interact` (os mesmos do exemplo da Alavanca) | Erro de compilação em `DialogueInteractable`. |

## 15. Limitações conhecidas

- **Compilado só no papel** — espere 1 ou 2 rodadas de ajuste.
- **Sem localização:** os textos ficam no grafo. Para vários idiomas, duplique o grafo ou troque `Text` por chaves da sua tabela de tradução (ponto único: `DialogueText.Expand`).
- **A conversa não é restaurada ao carregar um save** (só o que ela gravou: variáveis, nós visitados, escolhas `once`, contagem de conclusões). Salve no fim da conversa com a ação `Autosave`.
- **Transição de cena aborta** as conversas ativas (`SceneDirector.IsBusy`).
- Máximo de **8 escolhas** na tela por fala.
- Ações de cura/habilitar objeto não têm tipo próprio: use `Event` + `DialogueEventRelay`, ou `Custom`.
- Retratos são `Sprite`; não há animação de retrato.
- Um ator só fala com **uma** conversa por vez (o `Interactable` mostra "Ocupado").

## 16. Receitas

**NPC que muda de papo conforme a história**
`DialogueInteractable.dlgEntries`: (1) `Zelador_Aviso` prioridade 0; (2) `Zelador_Pos_Alarme` prioridade 10 com condição `Variable "alarme.tocou" = true`.

**Porta que abre depois de uma escolha**
Escolha → ação `Event "abrir_cofre"`; na porta, `DialogueEventRelay` com `eventId = abrir_cofre` e `interact = [Door]`.

**Escolha que só aparece com o item**
Condição `HasItem chave_x ≥ 1`, `whenLocked = ShowDisabled`, `lockedHint = "precisa da chave"`, e ação `RemoveItem chave_x`.

**Pensamento ao entrar na sala**
Grafo em `Subtitle` + `DialogueTrigger` (OnEnter, `playOnce`). Use `@player` como falante.

**Susto com escolha cronometrada**
Fala com `choiceTimeLimit = 6`, `timeoutChoice = 1`, `onStart`: `CameraShake` + `Scare` + `Sanity −6`.

**Cena de cinema com diálogo**
`DialogueStep` entre os passos de uma `CutsceneSequence`, `audience = AllPlayers`.

**Diálogo que só acontece uma vez**
`DialogueTrigger.playOnce` (chave `dlgtrigger.<id>`), ou condição `GraphCompleted == 0` num nó Condição no início.

## 17. Dados no save (`GameSession`)

| Chave | Significado |
|---|---|
| `dlg.<grafo>.v.<nó>` | nó visitado |
| `dlg.<grafo>.n` | quantas vezes o diálogo terminou |
| `dlg.<grafo>.c.<nó>.<escolha>` | escolha `once` já tomada |
| `dlgtrigger.<id>` | `DialogueTrigger` já tocou |
| (suas chaves) | variáveis das ações `SetVariable/SetNumber/SetText` |

> Defina um **Id** estável no grafo (campo `id`). Sem ele, vale o nome do asset — renomear o asset "esquece" o progresso.

## 18. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Não consigo falar com o NPC | Layer errada; falta `DialogueActor`/entrada em `dlgEntries`; nenhuma entrada com condições verdadeiras (o texto "Sem nada a dizer" aparece). |
| Jogador anda durante o diálogo | `SetLock` não achou o método de trava (veja o aviso no Console). |
| Texto não aparece | Sem câmera do jogador ativa; canvas de outro sistema com ordem maior que 250 (`DialogueStyle.SortingOrder`). |
| Evento não faz nada | Id diferente (maiúsculas contam); relé em objeto desativado (só escuta quando ativo). |
| Animação não toca | Parâmetro com outro nome/tipo no Animator (veja o aviso). |
| Escolha sumiu | Era `once`, ou condição falsa com `whenLocked = Hide`. |
| Textos com `{...}` aparecem crus | Token digitado errado; use `{var:chave}` ou `{count:item}`. |
