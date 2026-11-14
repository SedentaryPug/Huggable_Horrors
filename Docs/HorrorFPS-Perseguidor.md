# Perseguidor — IA com Diretor, atenção e memória adaptativa, vários monstros, troca de cena e finalização

*Complementa Arquitetura, Itens/Sanidade/Corpo e Props. Usa o `NoiseSystem`, o `HidingSpot`, o coração, a sanidade, o `SceneDirector`, o `GameSession` e as cutscenes que já existem.*

Um perseguidor para o Horror FPS Framework: **Árvore de Comportamento** (C# puro, sem pacote) + um **Diretor** que sabe tudo mas só entrega pistas quando uma regra libera + **atenção** (4 níveis) + **memória adaptativa** que vai para o save. Funciona com 1 a 4 jogadores (split-screen). Desde a segunda versão:

- **Vários perseguidores ao mesmo tempo**, com memória, ronda e pressão que não se atropelam (seção 7).
- **Troca de cena**: ele segue os jogadores para o outro nível, ou, se não está no nível atual, pode ser **chamado por tempo ou por um barulho muito alto** (seção 8).
- **Traços exclusivos por monstro** (módulos) sem mexer no cérebro (seção 9).
- **Finalização**: no último golpe, uma cutscene, e só depois a vítima morre (seção 10).
- **Árvore editável**: a árvore também pode ser um asset, com janela de edição, visão ao vivo e nós feitos por você em uma classe (doc *Árvore de comportamento*).

> **Código escrito sem compilar no Unity**, só contra a documentação dos outros módulos. Tudo que toca o framework está em **`StalkerBridge.cs`** (seção 13), mais o campo `CutsceneSequence` do `StalkerFinisher`. Se algo não compilar, mande a mensagem do Console.

## 1. O que vem no pacote

```
Assets/HorrorFPS/Stalker/
  StalkerBridge.cs       ÚNICO ponto de contato com framework/outros módulos (jogadores, dano, vida, ruído, eventos de cena e save)
  StalkerTypes.cs        enums e dados (StalkerTarget, StalkerNoise, StalkerHint)
  StalkerProfile.cs      todos os números (ScriptableObject; sem asset vale o padrão)
  StalkerKnowledge.cs    quadro de conhecimento: o ÚNICO que a árvore lê
  StalkerMemory.cs       iscas, esconderijos, mapa de calor, experiência (uma por perseguidor ou grupo; vai para o save)
  StalkerDirector.cs     o porteiro da informação (nasce sozinho na cena)
  StalkerSenses.cs       visão (medidor de suspeita) e audição (INoiseHearer)
  StalkerBrain.cs        atenção, conhecimento, movimento; liga tudo
  StalkerBT.cs           mini árvore de comportamento (Selector, Sequence, Guard...)
  StalkerActions.cs      os ramos padrão: atacar, perseguir, revistar, investigar, procurar, rondar
  PatrolPoint.cs         pontos de ronda            StalkerNoiseTag.cs  marca fontes de som (opcional)
  -- traços (seção 9) --
  StalkerModule.cs       base dos traços + StalkerTreeBuilder (árvore por nome e prioridade)
  StalkerModules.cs      chama os ganchos de cada traço, em ordem
  StalkerTraitSprint.cs  exemplo de traço (arrancada ao avistar)
  StalkerFinisher.cs     traço: finalização no último golpe     StalkerFinisherEnd.cs  fim da cutscene
  -- entre cenas (seção 8) --
  StalkerDefinition.cs   quem é o perseguidor + regras de seguir / ser chamado / recuar
  StalkerRoster.cs       o ELENCO: onde cada um está agora, mesmo sem objeto na cena (vai para o save)
  StalkerManager.cs      gerente único (sobrevive às cenas) + API de roteiro
  StalkerWorld.cs        concilia a cena com o elenco       StalkerSchedule.cs  chegada, chamado e recuo
  StalkerSpawner.cs      escolhe onde aparece              StalkerSpawnPoint.cs  pontos de aparição
  StalkerSummonTrigger.cs  gatilho de roteiro (chamar, dispensar, desligar)
Assets/HorrorFPS/Editor/Stalker/StalkerMenu.cs   menus Stalker > Add Stalker To Selected Object e Create Stalker Definition From Selected e os menus Create Stalker Test Scene / Create Stalker Test Arena In Open Scene (a bancada `BuildDemo`)
  -- árvore editável (doc "Árvore de comportamento") --
Assets/HorrorFPS/Stalker/Tree/   9 arquivos: base dos nós, asset, nós prontos (fluxo, ações, condições), modelos, variáveis, rastro ao vivo
Assets/HorrorFPS/Editor/Stalker/StalkerTreeWindow.cs, StalkerTreeMenu.cs, StalkerTreeAssetEditor.cs   janela e menus Stalker > Stalker Tree Editor e Create Stalker Tree...
```
Requisitos: os do framework + **NavMesh** na cena (o módulo `AI` do Unity, já incluso por padrão).

> **Atualização (reanimação):** `StalkerBridge.cs` e `StalkerFinisher.cs` mudaram. `IsAlive` devolve falso para jogador caído, e há `StalkerBridge.Kill(alvo, atacante, final)` mais o campo `Final Blow` do finalizador (10.4). Ambos funcionam sem a pasta `Revive`.

> **Atualização (árvore editável):** `StalkerBrain.cs`, `StalkerDefinition.cs`, `StalkerModule.cs` e `StalkerActions.cs` ganharam membros novos (campo `Tree`, `Blackboard`, `TreeTrace`, `Home`, `SetTree()`, `RebuildTree()`; `StalkerTreeBuilder.Get()`/`Names()`; `StalkerActions.Home`) e nada foi removido. **Sem asset de árvore o monstro roda a mesma árvore de antes.**

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` por cima e espere compilar.
2. Use **Tools > Horror FPS > Stalker > Create Stalker Test Scene**: cria a cena jogável `Test_Stalker` (já nos Build Settings, junto com o Core) com `[Stalker Demo]` — arena 40×40 com 4 salas, 5 pontos de ronda, 4 pontos de aparição, o perseguidor vermelho **com `Stalker Finisher` e uma cutscene de teste**, um **rádio isca** verde —, a entrada dos jogadores na sala nordeste e uma **mesa de armas** (espingarda, pistola, faca, taco, pé de cabra, munição, lanterna) para testar o monstro junto com as armas. Para colocar a mesma arena numa cena sua, use **Stalker > Create Stalker Test Arena In Open Scene** (sem jogadores). Se avisar que não gerou o NavMesh: *Window > AI > Navigation > Bake*.
3. Play. O painel no canto mostra modo, atenção, pista e memória. Experimente: ficar parado no escuro à vista dele (demora a notar), correr (nota rápido), fazer barulho longe, entrar num esconderijo **vendo-o vendo você**, ficar perto do rádio sem ser visto várias vezes (ele aprende que é isca) e **deixá-lo bater até o último golpe** (ele para, a cutscene de teste toca por 2,5 s e você morre).

Para o seu monstro: selecione-o e use **Stalker > Add Stalker To Selected Object** (NavMeshAgent, collider, `Stalker Senses`, `Stalker Brain`; arraste a cabeça para `Eyes`). A finalização é um componente à parte (`Add Component > HorrorFPS > Stalker > Traits > Finisher`, seção 10). Para ele **seguir entre cenas** e ser **chamado**: **Stalker > Create Stalker Definition From Selected** (salva o prefab em `Assets/Prefabs/Stalkers`, se faltar, e cria a `StalkerDefinition` em `Assets/Resources/HorrorFPS/Stalkers`). O monstro da bancada `BuildDemo` **não** tem definição: rode o menu `Stalker > Create Stalker Definition From Selected` nele para testar a troca de cena.

Teste rápido da troca de cena: na definição, `Follow > Chance = 1` e `Min Attention = Calm`; passe para outro nível (pela porta ou gatilho de troca do seu jogo) com ele por perto: ele chega depois de 3–8 s. Para o chamado: `Summon > First Delay = 10`, `Base Chance = 1` **e** `Max Chance = 1`, e fique 10 s num nível onde ele não está.

## 3. Como funciona

```
 Diretor (sabe tudo)  ──pista (só quando uma regra libera)──┐
                                                            ▼
 Sentidos (visão/audição) ──────────────►  QUADRO DE CONHECIMENTO ──► Árvore de comportamento ──► NavMeshAgent
                                                    ▲                          │           ▲
 Atenção (0..1 → Calm/Alert/Hunting/Frenzy) ────────┴── ajusta números ◄───────┘           │
 Memória (iscas, esconderijos, rotas) ── persiste no save                  Traços (módulos): ramos, números, sentidos, golpe
 Elenco + Gerente: quem existe em qual cena, seguir, chamar, recuar ── persiste no save
```
**A árvore nunca lê os jogadores.** Ela só vê o `StalkerKnowledge`: se enxerga alguém, e a melhor pista (posição, **incerteza em metros** que cresce com o tempo, **confiança** que cai, origem, esconderijo suspeito).

Prioridade da árvore (reavaliada a cada quadro; um ramo de maior prioridade interrompe o que estava rodando). A árvore é montada por **nome e prioridade** (`StalkerTreeBuilder`), então um traço pode acrescentar, trocar ou remover ramos:

| # | Ramo (nome / prioridade) | Quando | O que faz |
|---|---|---|---|
| 1 | Attack / 100 | vê o alvo a menos de `attackRange` | para, encara, pergunta aos traços (`DecideAttack`), dá dano (`IDamageable`) e tira sanidade, com recarga |
| 2 | Chase / 200 | vê alguém | corre até a **última posição realmente vista** |
| 3 | CheckHiding / 300 | pista com esconderijo | vai até lá, pausa de tensão, `HidingSpot.Inspect` com bônus |
| 4 | Investigate / 400 | pista nova | anda (rápido se alerta), olha em volta ao chegar |
| 5 | Search / 500 | depois de investigar/perder o jogador | N paradas aleatórias no raio da pista; revista esconderijos ao alcance |
| 6 | Patrol / 900 | nada acontecendo | próximo `PatrolPoint` por peso, distância, tempo desde a última visita e **mapa de calor** |

**Editar a árvore sem código:** a tabela acima é a árvore padrão. Ela também pode ser um **asset** (campo `Tree` do `StalkerBrain` ou da `StalkerDefinition`), editado na janela do **menu `Stalker > Stalker Tree Editor` com sequências, condições, recargas e nós seus. O nó "Ramo padrão" reaproveita estes seis ramos (e os que os traços criam). Veja o doc *Árvore de comportamento*.

## 4. Atenção

Valor contínuo `Attention01` (0..1) → nível com histerese. Sobe com estímulos (ouvir, entrever, notar, ver continuamente, pista do Diretor); decai sozinha depois de `decayHoldSeconds` sem estímulo; fica no mínimo `Hunting` durante uma perseguição e num piso (`afterChaseFloor`) por 40 s depois. Cada nível troca um pacote de números (`AttentionTuning`):

| | Calm | Alert | Hunting | Frenzy |
|---|---|---|---|---|
| Velocidade andando / correndo (m/s) | 1,3 / 3,0 | 1,8 / 3,6 | 2,3 / 4,5 | 2,9 / 5,2 |
| Tempo para notar (s) | 1,6 | 1,0 | 0,55 | 0,30 |
| Alcance / cone de visão | 14 m / 100° | 18 / 115° | 24 / 130° | 30 / 150° |
| Audição (x) / volume mínimo | 0,9 / 0,25 | 1,1 / 0,18 | 1,35 / 0,10 | 1,6 / 0,06 |
| Discernimento de iscas | 0,55 | 0,80 | 1,0 | 1,0 |
| Antecipa o jogador ao perdê-lo | 0 s | 0,4 | 0,8 | 1,2 |
| Procura por | 14 s | 22 | 34 | 45 |

A **visão** é um medidor por jogador: enche mais rápido com atenção alta, perto, em movimento e com o corpo todo à vista (3 raios: olho, peito, pés); parado enche devagar; escondido = invisível (só `Inspect` descobre). Passou de `glimpseAt` vira "vi algo" (pista fraca); chegou a 1 é "notei". A suspeita tem histerese (não pisca) e ele só segue o **último ponto realmente visto**, não o jogador atrás da parede.

## 5. Diretor: a informação que ele tem e só libera às vezes

O Diretor lê, a 5 Hz, posição, velocidade, sanidade, batimento, escondido/vivo e **isolamento** de cada jogador. Ele só entrega ao monstro:

| Pista | Quando | Detalhe |
|---|---|---|
| **Pressão** | monstro em ronda, atenção Calm/Alert, fora do sossego | **área aproximada** (raio 14 m, confiança 0,35) perto do jogador mais "exposto": isolado do grupo, sanidade baixa, coração alto, correndo. 1 a cada 30–55 s, no máximo 3 por minuto. Nunca atropela uma pista melhor |
| **Esconderijo** | o jogador entra num e o monstro o **notou nos últimos 3 s** | posição exata do móvel, confiança 0,9 (não descobre esconderijo que não viu) |
| **Ping** | você chama | `StalkerDirector.Instance.Ping(pos, raio, confiança)` (alarme, porta proibida) |
| **Sossego** | depois de cada perseguição (25 s) ou `GiveRelief(s)` | zero pistas de pressão: o ritmo respira |
| **Compartilhada** | um colega do mesmo **grupo** notou alguém (seção 7) | pista de confiança média (`shareConfidence`) |

Quem está **escondido nunca** recebe pista de pressão. Durante uma cinemática (finalização) o Diretor não entrega pistas de pressão. Escolha de alvo em co-op: maior "exposição", com um pouco de teimosia para não trocar a cada tique.

## 6. Adaptação (`StalkerMemory`, vai para o save)

| O que aprende | Como | Efeito |
|---|---|---|
| **Iscas** (`SoundEmitter`, objetos jogados) | foi investigar um som que **não era de um jogador** e não achou ninguém: +0,34 de desconfiança daquela fonte (3 viagens em vão = isca). Achou alguém por perto: −0,5 | chance de **ignorar** = desconfiança × discernimento do nível. Calmo ainda cai às vezes; caçando quase nunca. Esquece devagar (0,02/min) |
| **Esconderijos** | viu alguém entrar | `Inspect` ganha bônus (+0,08 por uso, até 0,5) e a busca prefere esses móveis |
| **Rotas** (mapa de calor) | onde viu/ouviu jogadores | a ronda é puxada para essas áreas |
| **Experiência** | +1 por perseguição encerrada (máx. em 12) | até +15% de audição, +25% de ganho de atenção, +30% de busca, notar mais rápido, pistas mais frequentes |

**Cada perseguidor tem a sua memória**, chaveada pelo `Memory Group` do brain (ou da `StalkerDefinition`); sem grupo, vale o id. Dois monstros com o **mesmo** `Memory Group` aprendem juntos (a isca que enganou um já não engana o outro); com chaves diferentes cada um aprende sozinho. A memória é uma string JSON na variável de partida `stalker_memory_v2_<chave>` (`GameSession`): atravessa troca de nível, entra no save sozinha e zera em **Novo jogo** (saves da primeira versão, que usavam `stalker_memory_v1`, começam sem memória). Para o **corpo** do monstro (posição) ir para o save, o elenco já cuida disso quando ele tem `StalkerDefinition` (seção 8); sem definição, use `SaveableEntity` + `SaveableTransform` no objeto.

## 7. Vários perseguidores ao mesmo tempo

Cada monstro é um `StalkerBrain` próprio (árvore, atenção, sentidos e conhecimento só dele). O que é **dividido** ou **coordenado**:

| Assunto | Como funciona |
|---|---|
| **Limite** | `StalkerManager.MaxActive` (padrão **2**; campo `Max Active` do gerente). Com o limite cheio ninguém novo é **chamado** por tempo ou barulho; quem já estava a caminho (seguir/chamado) tenta de novo a cada 3 s, até 10 vezes, e desiste. `Summon` de roteiro **ignora** o limite |
| **Memória** | uma por chave: `Memory Group` do brain → `Memory Group` da definição → o `Id`. Mesma chave = aprendem juntos; chaves diferentes = cada um aprende só |
| **Pistas entre colegas** | `Group` igual (brain ou definição) **e** `Share Radius > 0` no perfil: quem **nota** alguém avisa os colegas do grupo que estão sem visual, a até `Share Radius` m, com confiança `Share Confidence` |
| **Pressão do Diretor** | cada um tem o seu ritmo (começam dessincronizados). Entre pistas de pressão de **qualquer** monstro há no mínimo `Global Pressure Gap` (6 s), e um jogador que outro já pressiona vale `Target Share Penalty` (0,6×): em co-op a pressão se espalha |
| **Ronda** | `PatrolPoint` que outro monstro está indo visitar (`claimedBy`) vale `Claimed Point Factor` (0,15×); `Only For` (ids separados por vírgula) torna o ponto exclusivo de certos monstros |
| **Esconderijos** | o Diretor entrega "ele entrou aqui" só a quem **viu** entrar |
| **Batimento** | cada monstro usa a **sua** chave em `HeartbeatComponent.SetThreat`: um não apaga a ameaça do outro |
| **Custo** | `Sense Interval` (0,05 s) espaça as varreduras de visão; o gerente roda a cada `Tick Interval` (0,5 s) |
| **Depuração** | uma janelinha por monstro, empilhadas |

Dois monstros **diferentes** na mesma cena: dois prefabs, `Id` diferentes (e perfis diferentes, se quiser). **Irmãos** que caçam juntos: mesmo `Group` + `Share Radius` ≈ 25 no perfil e, se quiser que aprendam juntos, o mesmo `Memory Group`.

## 8. Troca de cena: seguir, ser chamado, recuar

### 8.1 As peças

- **`StalkerDefinition`** (ScriptableObject em `Assets/Resources/HorrorFPS/Stalkers`): id, prefab, perfil, grupo, níveis permitidos/proibidos, `Home Level` e três caixas de regras (`Follow`, `Summon`, `Retreat`). **Sem definição o monstro funciona, mas só vive na cena em que foi colocado.**
- **`StalkerRoster`** (o elenco): para cada id, o estado (`Dormant`, `Active`, `Arriving`, `Disabled`), o nível, a posição, a atenção, a pista que ele traz e o estado dos módulos. Fica na variável de partida `stalker_roster_v1` (`GameSession`): entra no save sozinho e zera em Novo jogo.
- **`StalkerManager`**: um só, criado sozinho quando existe alguma definição (`DontDestroyOnLoad`). Concilia a cena com o elenco, rola os dados e expõe a API de roteiro. Para mudar `Max Active` ou `Tick Interval`, ou ligar `Show Roster` (o elenco no canto da tela, para teste), ponha um `StalkerManager` (`HorrorFPS > Stalker > Stalker Manager`) na primeira cena.
- **`StalkerSpawnPoint`**: onde ele pode aparecer (8.4).

Como objetos criados em tempo de execução **não voltam** num load, quem recria o monstro é o gerente, a partir do elenco: ele instancia o prefab de cada perseguidor que o elenco diz estar naquele nível, na posição guardada, e devolve a atenção e o estado dos módulos. A posição, a atenção e os módulos de quem está em cena são guardados a cada 2 s, em cada troca de nível e antes de cada save.

### 8.2 Seguir os jogadores (`Follow`)

Quando a troca de nível **começa**, cada monstro ativo decide se atravessa. Ele só segue se **todas** valerem:

| Regra | Padrão |
|---|---|
| `Enabled` ligado e ele não está parado em cutscene | ligado |
| estava a menos de `Max Distance` do jogador vivo mais perto | 25 m |
| estava **perseguindo** (viu alguém) **ou** a atenção era ≥ `Min Attention` (com `Only If Chasing`, só o primeiro vale) | Alert |
| passou na rolagem `Chance` | 85% |
| a definição permite o nível novo (`Allowed` / `Forbidden Levels`) | — |

Quem **não** segue fica *morando* no nível que os jogadores deixaram, onde estava; se eles voltarem, ele reaparece ali.

Quem segue entra em `Arriving` e, quando o nível novo carrega, aparece depois de `Delay` (3–8 s) num `StalkerSpawnPoint` **Arrival** (os mais perto de onde os jogadores entraram) ou, sem pontos, no NavMesh a pelo menos `Min Player Distance` (6 m) deles; `Require Unseen` o impede de surgir à vista. Chega com uma **pista** aproximada de onde os jogadores estão (`Lead Radius`), com atenção `Arrive Attention`, e faz o barulho da chegada (`Arrival Noise`, a porta batendo).

### 8.3 Ser chamado para o nível atual (`Summon`) e recuar (`Retreat`)

Se ele **não está** no nível, a definição o permite aqui, já passou `Cooldown After Retreat` e há vaga em `Max Active`, duas coisas podem chamá-lo:

| Gatilho | Regra |
|---|---|
| **Por tempo** | depois de `First Delay` (90 s) no nível, a cada `Roll Interval` (30 s) rola `Base Chance` (8%) **+ `Pity Per Roll`** (+4% a cada rolagem em vão, até `Max Chance` 60%): quanto mais o jogador demora, mais perto o susto. A contagem zera ao trocar de nível |
| **Por barulho** | um ruído do `NoiseSystem` com raio ≥ `Noise Radius` (30 m: tiro, explosão, vidro quebrando) rola `Noise Chance` (70%), respeitando `Noise Cooldown` (60 s). Ele chega já sabendo **onde foi o barulho** |

Decidido o chamado, ele aparece depois de `Delay` (4–10 s) num ponto **Ambush** (ou, sem pontos, no NavMesh), a pelo menos `Min Player Distance` (18 m) de qualquer jogador e **fora da vista** deles (se nenhum ponto servir, a regra da vista é relaxada, nunca a da distância). Barulhos feitos por monstros, durante troca de nível ou durante uma cinemática são ignorados, então a chegada dele não chama outro.

**Recuo** (só vale para quem chegou por seguir ou chamado): sem notar ninguém por `Calm Seconds` (60 s), longe de todos os jogadores (`Min Player Distance`, 25 m), **fora da vista** deles e depois de `Min Lifetime` (90 s) em cena, ele sai. O estado fica no elenco, `StalkerManager.Retreated` dispara e ele só pode ser chamado de novo depois do cooldown. Um monstro "morador" (`Home Level`, ou posto na cena) nunca recua.

### 8.4 Pontos de aparição (`StalkerSpawnPoint`)

| `Kinds` | Quem usa |
|---|---|
| **Arrival** | chegada de quem seguiu (portas, escadas, corredores de entrada) |
| **Ambush** | aparição por tempo ou barulho (prefere pontos a uns 28 m do jogador mais perto) |
| **Home** | onde o monstro mora ao carregar o nível |

`Stalker Ids` (vírgula) torna o ponto exclusivo desses monstros; `Weight` pesa o sorteio. A rotação do ponto (a seta do gizmo) é a de quem nasce ali; pontos só `Ambush` nascem virados para o jogador mais perto. **Sem nenhum ponto útil** o sistema sorteia um lugar do NavMesh entre uns 14 e 34 m de um jogador vivo. Se não achar lugar (ou o limite estiver cheio) ele tenta de novo a cada 3 s e, depois de 10 tentativas, desiste e volta a ficar fora de cena.

### 8.5 Roteiro e gatilhos

`StalkerManager` (estático; qualquer script ou UnityEvent):

| Chamada | Efeito |
|---|---|
| `Summon(id)` / `Summon(id, atraso)` | aparece neste nível; ignora tempo, barulho, `Max Active` e a regra de níveis. Devolve `false` se já está aqui, chegando ou desligado |
| `Dismiss(id)` | tira de cena guardando o estado; pode ser chamado de novo depois |
| `SetDisabled(id, true/false)` | desliga de vez (nunca aparece nem é chamado) / religa |
| `SetLevel(levelId)` | só se o framework não informar o id do nível **e** ele não for o nome da cena |
| `Get(id)` / `ActiveCount` | consulta (`Get` devolve `null` se não está em cena) |
| `BeginCinematic()` / `EndCinematic()` / `Cinematic` | congela **todos**, bloqueia chegadas, pistas e barulhos (é um contador: pode aninhar) |
| eventos `Arrived(brain, motivo)` / `Retreated(id)` | motivos: `follow`, `time`, `noise`, `script` |

`StalkerSummonTrigger` (`Summon`, `Dismiss`, `Disable`, `Enable`): num collider **Is Trigger**, dispara quando um jogador entra (`On Enter`); `Fire()` é público para ligar a qualquer UnityEvent (`EventStep` de cutscene, porta, diálogo). `Once` fica gravado na partida (`stalker_trigger_<Trigger Id>`; vazio = cena/nome do objeto), então não repete depois de carregar o save.

### 8.6 Casos especiais

- Monstro **sem** `StalkerDefinition`: vive e morre na cena onde foi posto; não atravessa nem é chamado.
- Monstro **com** definição já posto na cena: na primeira vez o elenco o **adota** (se a definição não tem `Home Level`, ou ele é esta cena). Depois disso o elenco manda: se ele diz que o monstro está em outro nível (ou desligado), o objeto da cena é desativado e guardado; duplicatas também.
- **Destruir o objeto à mão não o tira do jogo**: o elenco ainda o dá como ativo aqui e o gerente o recria em até 2 s na última posição. Para tirar de verdade use `Dismiss(id)` (volta depois) ou `SetDisabled(id, true)` (nunca mais).
- Salvou e carregou: o gerente destrói os monstros que ele mesmo criou e refaz tudo a partir do elenco gravado.

## 9. Traços exclusivos por monstro (módulos)

O cérebro é genérico; **o que é só de um monstro mora num módulo**: um componente que herda de `StalkerModule` e vai no mesmo objeto do `StalkerBrain` (no prefab). Ele viaja com o monstro entre cenas e o cérebro nunca precisa saber que ele existe. Exemplo completo (um monstro cego que ouve melhor e ruge antes de correr):

```csharp
using UnityEngine;

namespace HorrorFPS
{
    [AddComponentMenu("HorrorFPS/Stalker/Traits/Blind")]
    public class StalkerTraitBlind : StalkerModule
    {
        [Range(0f, 1f)] [SerializeField] float sight = 0.2f;     // enxerga 20% do normal
        [SerializeField] float hearingBoost = 1.6f;              // ouve 60% a mais
        [SerializeField] float roarSeconds = 1.5f;

        float roarLeft;
        StalkerTarget roarTarget;

        // sentidos: quanto ele enxerga o alvo (0 = nada)
        public override float ModifyVisibility(StalkerTarget target, float visibility) { return visibility * sight; }

        // números do nível de atenção atual (todo quadro)
        public override void ModifyTuning(ref AttentionTuning t) { t.hearing *= hearingBoost; }

        // evento: ao notar alguém, arma o rugido
        public override void OnSpotted(StalkerTarget target) { roarTarget = target; roarLeft = roarSeconds; }

        // ramo novo da árvore: logo antes de perseguir, para e ruge
        public override void BuildBranches(StalkerTreeBuilder tree)
        {
            BTNode roar = new BTAction("rugir", RoarTick, RoarAbort);
            tree.Add("Roar", StalkerTreeBuilder.PriChase - 1, new BTGuard("rugido pendente", () => roarLeft > 0f, roar));
        }

        BTStatus RoarTick()
        {
            Brain.Stop();
            if (roarTarget != null && roarTarget.Valid) Brain.FaceToward(roarTarget.Position, 240f);
            roarLeft -= Time.deltaTime;
            return roarLeft > 0f ? BTStatus.Running : BTStatus.Success;
        }

        void RoarAbort() { roarLeft = 0f; }
    }
}
```
**Ganchos** (sobrescreva só os que precisar):

| Gancho | Para quê |
|---|---|
| `BuildBranches(tree)` | acrescentar (`Add`), trocar (`Replace`), remover (`Remove`) ou reprioritizar (`SetPriority`) ramos. Prioridades de referência: `PriFinisher` 50, `PriAttack` 100, `PriChase` 200, `PriCheckHiding` 300, `PriInvestigate` 400, `PriSearch` 500, `PriPatrol` 900 (**menor roda primeiro**). Mesmo nome = substitui |
| `ModifyTuning(ref t)` | velocidades, visão, audição, discernimento... (`walkSpeed`, `runSpeed`, `visionRange`, `visionFov`, `confirmSeconds`, `hearing`, `noiseThreshold`, `baitDiscernment`, `searchSeconds`, `predictSeconds`), todo quadro, sobre o nível de atenção atual |
| `ModifyVisibility(alvo, v)` | quanto ele enxerga o alvo (0 = nada): luz, escuridão, camuflagem |
| `FilterNoise(ref ruído)` | ouvir só certos sons: devolva `false` para ignorar, ou altere o volume/tipo |
| `OnSpotted` / `OnLostSight` / `OnHint` | reagir a notar / perder de vista / receber uma pista |
| `OnModeChanged` / `OnAttentionChanged` | trocar animação, som, emissão da pele... |
| `DecideAttack(alvo, ref dano)` | mudar o dano, **assumir** o golpe (`Handled`: sem dano, a recarga corre) ou **adiar** (`Cancel`: tenta de novo no quadro seguinte). É por aqui que entra o finalizador |
| `OnArrived(motivo)` | efeito de entrada ao aparecer vindo de outra cena (porta rangendo, fumaça): `follow`, `time`, `noise`, `script` |
| `BrainTick(dt)` | atualização por quadro (não roda com o cérebro congelado em cutscene) |
| `SaveState()` / `LoadState(texto)` | estado que acompanha o monstro entre cenas e vai no save (texto livre, em geral `JsonUtility`). Um módulo por tipo por monstro |
| `Order` | ordem dos ganchos encadeados (`ModifyTuning`, `ModifyVisibility`, `FilterNoise`); menor primeiro |

**O que um módulo pode usar do cérebro** (`Brain`): `Knowledge` (ex.: `hasVisual`), `Mode`, `Level`, `Attention01`, `Profile`, `Tune`, `Senses`, `Memory`, `Agent`, `Frozen`, `Stimulate(x)`, `ForceAttention(v)`, `ReceiveHint(h)`, `MoveTo(destino, velocidade)`, `Stop()`, `FaceToward(ponto, graus/s)`, `Arrived(dist)`, `Freeze(parado, desligaAgente)`, `Warp(pos, rot)` e os eventos `Spotted`, `HintReceived`, `ModeChanged`, `AttentionChanged`.

Vários módulos no mesmo monstro funcionam juntos. O `StalkerTraitSprint` do pacote (arrancada ao avistar o jogador) é o exemplo mínimo. Outras ideias: *O Sombra* (some na luz: `ModifyVisibility` + `ModifyTuning`), *O Rastreador* (`OnArrived` + uma pista permanente via `ReceiveHint`), *O Duplo* (um ramo que imita a voz de um jogador). A árvore **sem módulos** continua exatamente a padrão: os ramos de série são registrados no mesmo `StalkerTreeBuilder`.

**Módulo ou árvore?** Módulo para números, sentidos, o golpe, a finalização e estado salvo; **árvore editável** para decidir *quando* e *em que ordem* o monstro faz as coisas, e para ações novas reutilizáveis (uma classe `StalkerTask`). Com uma árvore em asset, um ramo criado em `BuildBranches` só roda se a árvore tiver um nó **Ramo padrão** do tipo **Custom** com o nome dele (o Console avisa se faltar). Detalhes: doc *Árvore de comportamento*, seções 3 e 8.

## 10. Finalização: o último golpe vira cutscene

O `StalkerFinisher` é um módulo (`HorrorFPS > Stalker > Traits > Finisher`). Quando o monstro vai dar um golpe e **este seria o último**, ele não dá o dano à toa: para tudo, toca uma cutscene e, no fim, a vítima morre.

### 10.1 O que é "o último golpe" (nessa ordem)

1. **`Lethal Damage` > 0** e o golpe tem pelo menos esse dano (monstro de um golpe só);
2. **Vida legível** (`Use Health`): vida atual ≤ dano do golpe (a vida é lida do `StatsComponent`, ver seção 13);
3. **Contagem de golpes**, se a vida não puder ser lida: o golpe de número `Hits To Kill` (3) dentro de `Hit Window Seconds` (30 s).

`Only When Last Alive`: em co-op, só finaliza quando a vítima é o último jogador vivo (senão o golpe é normal). Se **outra** cinemática estiver em curso, o golpe é adiado.

> **Com o módulo de reanimação** (doc *Reanimação*): um jogador **caído** não conta como vivo para o perseguidor. Ele não é mais alvo (o monstro "não bate em corpo"), não conta em `Only When Last Alive` e não recebe a finalização. A morte do fim da cena, porém, ainda pode virar "caído" se houver outro jogador de pé: para ela ser definitiva, ligue `Final Blow` (10.4).

### 10.2 O que acontece

```
 golpe → é o último? ──não──► golpe normal
            │sim
            ▼
  congela o cérebro · BeginCinematic (todos param, ninguém chega, sem pistas)
  (Hit Before Scene) aplica o golpe final deixando 1 de vida
  vira o monstro para a vítima · toca a cutscene · onStart
            ▼
  fim: Signal() da cutscene, ou ela deixa de tocar, ou Max Seconds
            ▼
  mata a vítima (Kill At End) · descongela · EndCinematic · onEnd · apaga o prefab (≥ 1 s depois)
```
Se o golpe final já matar a vítima, ela morre sem cena. Se algo der errado (o monstro é desativado, o nível troca, carregou um save), o `OnDisable` do finalizador encerra a cinemática e o gerente zera o contador, então ninguém fica congelado para sempre.

### 10.3 Montando a cutscene (receita)

**Referencial:** a cutscene toca com a **origem no monstro** e o **+Z apontando para a vítima**. Posicione câmera e atores em cima disso e funciona em qualquer ponto do mapa.

**Opção A, a mais simples: cutscene filha do monstro.** Crie um filho `FinisherCutscene` com um `CutsceneSequence` (o finalizador acha sozinho, ou arraste em `Sequence`). Ele pode referenciar o próprio monstro (animação, som). **`Play Once` DESLIGADO** (senão só toca na primeira vez da partida). Se quiser marcadores de câmera, ponha-os num objeto e arraste em `Anchor`: ele é movido para o monstro, com +Z para a vítima, antes de tocar.

**Opção B: prefabs.** Arraste prefabs (com um `CutsceneSequence`) em `Cutscene Prefabs`: um é sorteado a cada finalização (sem repetir o último) e nasce na posição do monstro com +Z para a vítima. Eles não enxergam o monstro: use `onStart` para disparar a animação dele.

**Avisar o fim.** O último passo da cutscene deve ser um `EventStep` chamando **`StalkerFinisherEnd.Signal()`** (componente no mesmo objeto). Sem ele o fim é detectado pela própria sequência, se ela expõe `IsPlaying` (lido por reflexão), e por último pelo `Max Seconds` (30 s). **`Allow Skip`**: desligue, ou garanta que o `EventStep` final dispare também ao pular. `StalkerFinisherEnd.KillNow()` mata a vítima na hora (para a morte acontecer no meio da cena e o resto ser só fade).

O método `StalkerMenu.BuildDemo` (menus Stalker > Create Stalker Test Scene / Create Stalker Test Arena In Open Scene) já montava tudo isso numa cena de teste (cutscene de 2,5 s com `WaitStep` + `EventStep` ligado ao `Signal`).

### 10.4 Opções

| Campo | Efeito |
|---|---|
| `Sequence` / `Cutscene Prefabs` / `Anchor` | de onde vem a cena (filha, prefabs) e onde ficam os marcadores |
| `Kill At End` | ligado: a vítima morre quando termina. Desligado: a cutscene decide (use `KillNow`) |
| `Final Blow` | **só com o módulo de reanimação**. Ligado: a morte do fim da cena é definitiva, o jogador não vira "caído" mesmo havendo outro de pé. Desligado (padrão): em co-op a vítima fica caída e pode ser reanimada. Sem o módulo não faz nada |
| `Hit Before Scene` | o golpe final acontece de verdade antes da cena (dano real deixando 1 de vida, mais a perda de sanidade do ataque). Precisa que a vida possa ser lida |
| `Only When Last Alive` | co-op: só finaliza o último vivo |
| `Max Seconds` | trava de segurança se o fim nunca for detectado |
| `onStart` / `onEnd` | UnityEvents (tocar a animação do monstro, apagar as luzes, liberar a tela de game over) |

Para um finalizador **diferente por monstro**, herde de `StalkerFinisher` e sobrescreva `IsLethal` (outra regra: só de costas, só com a lanterna apagada...), `PickPrefab` (qual cutscene, conforme o jogador ou o lugar), `OnBegan` e `OnFinished`. `StalkerFinisher.Current` diz se há uma finalização em curso.

## 11. Receitas e ganchos

- **Fonte de som que ele nunca aprende** (história): `StalkerNoiseTag` com `AlwaysInvestigate`. **Ruído de ambiente**: `NeverInvestigate`. Sem a etiqueta, a chave é `NomeDoObjeto:TipoDoBarulho` (todos com o mesmo nome compartilham o aprendizado).
- **Sustos/música**: `brain.Spotted`, `brain.ModeChanged`, `brain.AttentionChanged` e `brain.HintReceived` (bom para um *tell*: lâmpada que pisca, som distante quando o Diretor "sopra"); `StalkerManager.Arrived` / `Retreated` para a trilha de quando ele entra e sai de cena.
- **Cutscene em que ele acorda**: `brain.ForceAttention(0.7f)`; **momento de roteiro sem pressão**: `StalkerDirector.Instance.GiveRelief(60f)`; **cutscene sua com o monstro por perto**: `StalkerManager.BeginCinematic()` ... `EndCinematic()` (todos param, ninguém chega, ninguém recebe pista).
- **Puzzle `CoopLock` em modo Hold com ele por perto**: o barulho do botão já chama a IA.
- **Esconderijo**: nada a configurar; `Find Chance` do `HidingSpot` continua valendo junto com o bônus.
- **Dificuldade**: `NoiseSystem.GlobalScale`, `Tune` por nível e `maxHintsPerMinute` no `StalkerProfile`; `Max Active` do gerente; `Follow.Chance` e `Summon.Base Chance` / `Pity Per Roll` na definição.
- O monstro precisa de um **Collider** (o `NoiseSystem` só avisa quem tem collider no raio); sem nenhum, o módulo cria um trigger de "orelhas". Se usar `NoiseSystem.HearingMask`, ponha o monstro na camada incluída.

**Receitas de entre cenas** (tudo na `StalkerDefinition`, menos onde indicado):

| Quero... | Faça |
|---|---|
| Um monstro que **mora** num capítulo e nunca sai dele | `Home Level` = nome da cena, `Allowed Levels` = só ela, `Follow > Enabled` desligado. Um `StalkerSpawnPoint` **Home** diz onde nasce |
| Um monstro que **só aparece por roteiro** | `Summon > By Time` e `By Noise` desligados, `Follow > Enabled` desligado, e um `StalkerSummonTrigger` (ou `StalkerManager.Summon("id")` num `EventStep`) no momento certo |
| Um monstro que **persegue sem descanso** pelos níveis | `Follow > Chance` 1, `Min Attention` Calm, `Max Distance` grande, `Retreat > Enabled` desligado |
| Um monstro que **só segue quem corre** | `Follow > Only If Chasing` ligado |
| Um nível **seguro** (hub, safe room) | nele em `Forbidden Levels` (ninguém chega, nem por tempo nem por barulho; só `Summon` de roteiro passa por cima) |
| **Susto de porta**: ele chega pela porta por onde você entrou | `StalkerSpawnPoint` **Arrival** na porta, `Follow > Require Unseen` desligado, `Arrival Noise` alto |
| **Tiro/explosão atrai o monstro de outro lugar** | `Summon > By Noise` ligado, `Noise Radius` menor ou igual ao raio do ruído (ex.: 30 m para tiros e explosões); `Noise Chance` 1 para garantir |
| Monstro que **aparece mais quanto mais você demora** | `Summon > Base Chance` baixa e `Pity Per Roll` alta (a chance sobe a cada rolagem em vão até `Max Chance`) |
| Dois monstros, **um de cada vez** | `Max Active` = 1 no gerente: o segundo só entra quando houver vaga (por exemplo, quando o primeiro recua) |
| Dois monstros que **caçam juntos** | mesmo `Group`, `Share Radius` ≈ 25 no perfil; (opcional) mesmo `Memory Group` |
| Um monstro que **ignora** certo jogador (ex.: quem carrega o amuleto) | módulo seu: `ModifyVisibility` devolvendo `0` para esse alvo |

## 12. O que vai para o save

| Dado | Onde | Zera em Novo jogo |
|---|---|---|
| Memória adaptativa (iscas, esconderijos, rotas, experiência) | `GameSession`: `stalker_memory_v2_<chave>` (uma por `Memory Group`/id) | sim |
| Elenco: estado, nível, posição, atenção, pista que ele traz, estado dos módulos | `GameSession`: `stalker_roster_v1` | sim |
| Gatilhos `Once` já disparados | `GameSession`: `stalker_trigger_<Trigger Id>` | sim |
| Estado exclusivo de cada módulo (`SaveState`) | dentro do elenco | sim |

O gerente grava elenco e memórias **antes de cada save**, a cada troca de nível e, no meio do jogo, de poucos em poucos segundos quando algo mudou. **Não** vão para o save (recomeçam do zero ao carregar): relógios das rolagens de tempo e cooldowns, pistas pendentes do Diretor, o ritmo de pressão, a janela de golpes do finalizador. Uma finalização em curso **não** é salva: ao carregar, a cinemática é zerada e o monstro volta descongelado.

## 13. Premissas do framework (todas em `StalkerBridge.cs`, mais o `CutsceneSequence` do `StalkerFinisher`)

| Premissa | Se falhar |
|---|---|
| `FindObjectsByType<PlayerContext>` acha os jogadores; `IsAlive`/`IsDead` lidos por reflexão | mortos contam como vivos (aviso no Console) |
| A câmera do jogador é filha do `PlayerContext` e enxerga a layer Default | olho do jogador cai para 1,6 m acima da raiz |
| `HidingSpot.IsHidden(PlayerContext)`, `HidingSpot.SpotOf(PlayerContext)`, `spot.Inspect(Transform, float)` → bool | erro de compilação em `IsHidden/SpotOf/Inspect` |
| `SanityComponent.Of(p)`, `.Normalized`, `.Add(float, string)` | erro em `Sanity01/LoseSanity` |
| `HeartbeatComponent.Intensity`, `.SetThreat(string, float)` e o estático `Alert(pos, raio, nível, bool)` | erro em `Tension01/SetThreat/FrightNear` |
| `NoiseEvent.position/radius/kind/source`, `ev.IsFrom(Transform)`, `NoiseSystem.LoudnessAt(ev, Vector3, float, float)`, `INoiseHearer.OnNoiseHeard(NoiseEvent)` | erro em `SourceOf/Loudness` e no `StalkerSenses` |
| `NoiseSystem.Emit(Vector3, float, NoiseKind, Transform)`, `NoiseKind.Custom` e o evento estático `NoiseSystem.Emitted` (`Action<NoiseEvent>` ou `(Vector3, float, NoiseKind, objeto)`) | erro em `EmitNoise/HookFramework`; sem chegada por barulho |
| `SceneDirector.IsBusy` e os eventos estáticos `SceneDirector.TransitionStarted` e `SceneDirector.LevelLoaded` (sem argumento, `int`, `string` ou um objeto com `Id`/`Name`/`SceneName`) | erro em `HookFramework/LevelBusy`; sem isso o sistema não percebe a troca de cena |
| `SaveSystem.BeforeSave` e `SaveSystem.AfterLoad` (sem argumento, `int` ou objeto) | erro em `HookFramework`; o elenco não é regravado/refeito a tempo |
| Id do nível = nome da cena (quando não há catálogo) | `Allowed Levels` / `Home Level` não batem: use o id certo ou `StalkerManager.SetLevel` |
| `IDamageable.ApplyDamage(new DamageInfo(float, DamageType.Physical, GameObject))`; um dano de 100000 mata o jogador | erro em `Damage`; se o seu jogador morre de outro jeito, ajuste `StalkerBridge.Kill` |
| `StatsComponent` + enum `StatType`: a vida é lida por reflexão (propriedade `Health`/`CurrentHealth`/`Life`/`VitalValue`... ou um método `Get...(StatType.Health)`) | sem a vida lida, a finalização **conta golpes** (`Hits To Kill`) e `Hit Before Scene` não age (aviso no Console) |
| `CutsceneSequence.Play()` e, opcional, um `bool` `IsPlaying`/`Playing`/`IsRunning`/`Running` (lido por reflexão) | erro de compilação no `StalkerFinisher`; sem o `bool` o fim vem do `Signal()` ou de `Max Seconds` |
| `GameSession.GetString(string)` / `SetString(string, string)` (guardadas no save, zeradas em Novo jogo) | erro em `GetString/SetString` (nada persiste) |
| **Opcional:** a classe `HorrorFPS.ReviveApi` (módulo de reanimação) com `static bool IsDowned(Component)` e `static void AllowDeath(Component, float)`, ligadas por **reflexão** (`StalkerBridge.IsDowned/AllowDeath`) | sem o módulo, ninguém está "caído" e `Kill(..., final)` é igual a `Kill`; nada quebra e nada precisa ser editado |
| `FindFirstObjectByType` / `FindObjectsByType` (Unity 2022.3 ou 6) | trocar por `FindObjectOfType/FindObjectsOfType` |
| `SoundEmitter` com campos `noiseRadius`, `repeatSeconds`, `repeatJitter`, `playOnEnable`, `noiseKind` e a `CutsceneSequence` / `EventStep` / `WaitStep` (só a bancada `StalkerMenu.BuildDemo`, por reflexão) | a bancada fica com os padrões ou sem a cutscene de teste; ajuste no Inspector |

## 14. Limitações

- Exige **NavMesh** bakeado; sem ele o perseguidor avisa uma vez e fica parado. Cenas aditivas com NavMesh próprio funcionam (ele tenta de novo).
- Não abre/quebra portas, não usa escadas nem vãos (`NavMeshLink`); sem animação (o `Animator` fica por sua conta, leia `Agent.velocity` e `brain.Mode`).
- A luz não afeta a visão de série (um módulo com `ModifyVisibility` resolve); "jogador sem tela de morte" depende de `IsAlive` (seção 13).
- Iscas: se o jogador **estava** por perto mas fora de vista, a viagem conta como "em vão". É uma simplificação.
- **Sem simulação fora de cena**: enquanto o monstro está em outro nível (ou fora de cena) nada acontece com ele; quando volta, está onde ficou (ou aparece num ponto). O tempo de ausência não o faz andar.
- **Um nível atual por vez**: o sistema acompanha o último nível carregado; o monstro "existe" ou nele ou só no elenco.
- `Max Active` é **um número global**, não por nível nem por monstro.
- As definições precisam estar em `Resources/HorrorFPS/Stalkers`; uma definição em outra pasta só é conhecida depois que um monstro dela passa por uma cena carregada.
- Uma finalização por vez no jogo; se dois monstros baterem juntos, o segundo espera. Em co-op a cena mostra **uma** vítima por vez.
- Com o módulo de reanimação, o monstro **ignora jogadores caídos** (não os executa). Quem os mata é o sangramento ou a derrota total; um monstro que "executa" caídos é uma das ideias do doc *Reanimação*.
- A finalização depende da cutscene ser feita por você, com o referencial da seção 10 e um fim detectável; e da vida ser legível (senão conta golpes).
- Monstro **sem** `StalkerDefinition` e instanciado em tempo de execução não é recriado num load (regra do framework): deixe-o na cena ou crie uma definição.
- A árvore editável é uma **lista indentada**, não um canvas de grafo; o estado interno dos nós e as variáveis do `Blackboard` não vão para o save nem atravessam a troca de cena (limitações completas no doc dela).

## 15. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Fica parado / aviso de NavMesh | falta bake, ou o monstro está fora do NavMesh (a bancada faz o bake antes de criar o monstro) |
| Não ouve nada | sem Collider no corpo (o módulo cria um trigger, confira o aviso), `HearingMask` sem a camada dele, ou `noiseThreshold` alto demais |
| Nunca me nota | `visionRange`/`visionFov` do nível Calm, ou `sightMask` incluindo triggers; jogador parado no escuro vale 0,6 |
| Me nota através da parede | camada da parede fora de `sightMask` (padrão: tudo) |
| Não ignora o rádio | a chave da fonte mudou (objetos com nomes diferentes); use `StalkerNoiseTag.key`; precisam ser 3 viagens em vão |
| Não recebe pistas | atenção acima de Alert (`pressureOnlyWhenCalm`), sossego ativo, cinemática em curso, ou o alvo está escondido |
| Esconderijo nunca é revistado | ele não **notou** o jogador entrando (3 s) |
| Ele **não me segue** para o outro nível | sem `StalkerDefinition` (ou fora de `Resources/HorrorFPS/Stalkers`); `Follow` desligado; longe demais (`Max Distance`) ou calmo (`Min Attention`); `Chance`; o nível está em `Forbidden Levels`; ou o framework não avisou a troca (ligue `Show Roster` no `StalkerManager` para ver o nível e o estado de cada um; use `StalkerManager.SetLevel`) |
| **Nunca é chamado** por tempo | ele já está no nível, está `Disabled`, `Max Active` cheio, em `Cooldown After Retreat`, nível proibido, `First Delay` ainda não passou, ou o nível não tem NavMesh |
| **Nunca é chamado** por barulho | o raio do barulho é menor que `Noise Radius`; a fonte é um monstro (ignorada); há cinemática; `Noise Cooldown`; ou `NoiseSystem.Emitted` não está chegando (aviso de compilação da seção 13) |
| Aparece à vista / colado em mim | sem `StalkerSpawnPoint` (cai no NavMesh a 14–34 m); `Min Player Distance` baixa; `Follow > Require Unseen` desligado (é de propósito no seguir) |
| O monstro aparece duas vezes / some | dois objetos com o mesmo `Id`: o elenco guarda a sobra; um `Id` mudou (renomear "esquece" onde ele estava) |
| **Destruí o monstro e ele voltou** | o elenco é a fonte da verdade e o recria; use `StalkerManager.Dismiss(id)` ou `SetDisabled(id, true)` |
| A finalização nunca começa | o módulo `Stalker Finisher` não está no monstro (o menu `Stalker > Add Stalker To Selected Object` não o adiciona); `Hits To Kill` alto, `Only When Last Alive` com outro vivo, ou vida não legível (aviso "Não consegui ler a vida" e conta golpes) |
| A finalização acaba na hora, sem cena | `Play Once` ligado na `CutsceneSequence`, ou a sequência não tem passos |
| A finalização trava até `Max Seconds` | falta o `EventStep` final com `StalkerFinisherEnd.Signal()` e a sequência não expõe `IsPlaying`; ou `Allow Skip` pulou o `EventStep` |
| A câmera da cutscene fica no lugar errado | o referencial é a origem no monstro e +Z para a vítima, não o mundo; ponha os marcadores num objeto em `Anchor` |
| Monstros ficam congelados depois de um erro | `StalkerManager.EndCinematic()` não foi chamado por um roteiro seu (cada `Begin` precisa do seu `End`); carregar um nível ou um save zera o contador |
| Erro em `StalkerBridge` / `StalkerFinisher` | premissa da seção 13: mande a mensagem do Console |

## 16. Ideias para melhorar (próximos passos)

1. **Luz e escuridão**: agora cabe num módulo (`ModifyVisibility` + `ModifyTuning`): visão maior com a lanterna ligada, menor no escuro; sombra e `SanityLight` como fator. Dá também o monstro que **foge da luz** ou **só se move no escuro**.
2. **Portas**: abrir (com barulho e tempo), arrombar, e o jogador usar porta fechada/trancada como ferramenta (`Door` + `Breakable`).
3. **Hábitos por jogador**: aprender que o jogador 1 se esconde e o 2 corre, e escolher o método de busca para cada um.
4. **Isca de propósito**: item arremessável (`Impact`) que o jogador usa de verdade; premiar a variedade, já que o monstro aprende a repetição.
5. **Faro por sanidade**: jogador com sanidade baixa "cheira" mais forte para o Diretor, ligado ao `SanityAudio`.
6. **Papéis em dupla**: um monstro **caça** e o outro **corta a saída** (módulo que pede ao `Group` a rota do colega e escolhe a oposta); acuar o jogador isolado e flanquear quando o grupo está junto.
7. **Atalhos**: `NavMeshLink` para ventilação e alçapões; ele usa atalhos quando está caçando.
8. **Áudio do monstro**: estados e níveis de atenção tocando sons e música; tells do Diretor audíveis para o jogador sentir justiça; a chegada (`Arrived`) com trilha própria.
9. **Curva de dificuldade**: o `Skill` dirigir a velocidade de ganho de atenção por capítulo, e um modo "calmo" para tutorial; `Max Active` e as chances de seguir/chamar por capítulo.
10. **Tempo fora de cena**: ao voltar a um nível, reposicionar o monstro em função do tempo que passou (ele "andou"), em vez de reaparecer onde parou.
11. **Finalizações por contexto**: `PickPrefab` escolhendo a cena pelo lugar (no armário, de costas, no escuro, em dupla com outro monstro) e `Hit Before Scene` com o ferimento visível na cena.
12. **Depuração**: gizmo do raio de audição, histórico de decisões, botão de zerar a memória e o elenco na janela do Editor, e uma janela que lista o elenco e permite `Summon`/`Dismiss` em Play. (A árvore ao vivo já existe na janela do menu `Stalker > Stalker Tree Editor`.)
13. **Árvore em canvas de grafo** (`GraphView`) sobre os mesmos assets, e teste da árvore sem Play com um `Knowledge` simulado: seção 12 do doc *Árvore de comportamento*.
