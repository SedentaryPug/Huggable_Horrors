# Perfil em grafo do perseguidor — o que ele É (características) separado do que ele FAZ (árvore)

*Complementa os docs **Perseguidor** (seção 9) e **Árvore de comportamento**. Não muda nada para quem não usa: sem perfil em grafo o monstro roda o mesmo código de antes. A fase 3 (seção 6) acrescenta o **canvas da árvore de comportamento** e uma **conferência da árvore contra o perfil**.*

Até aqui o "como o monstro é" e o "o que ele faz" estavam misturados: os números ficavam num asset de números (`StalkerProfile`), o golpe vivia no bloco *Ataque* desse asset e um tipo de ataque novo pedia código dentro do ramo `Attack`. Agora são duas peças separadas, ligadas por um **contrato**:

```
 PERFIL EM GRAFO                       CONTRATO                          ÁRVORE
 (o que ele É)                  (o que ele PODE fazer)          (o que ele FAZ, e quando)

 Visão ───────┐                   vision.range  = 30                  Seletor
 Audição ─────┤                   hearing.range = 1.6                  ├─ Atacar (por id: investida)  se Tem capacidade attack.charge
 Locomoção ───┼─ publicam ──►     move.run      = 5.2     ◄─ consulta  ├─ Atacar (por etiqueta: melee)
 Golpe ───────┤                   attack.melee, attack.charge          ├─ Perseguir ...
 Investida ───┤                   ataques: golpe, investida            └─ ...
 Fúria ───────┘                   rage
```

- **Perfil em grafo** (`StalkerProfileGraph`): um asset com a lista de **características** do monstro (Visão, Audição, Locomoção, um ou vários ataques, Fúria...). Cada monstro (ou cada `StalkerDefinition`) aponta para o seu.
- **Característica** (`StalkerTrait`): uma classe pequena, só de dados, que escreve números no monstro e **publica capacidades** no contrato. Quem cria um monstro novo escreve uma classe assim (um script, que aparece sozinha no menu), sem tocar no cérebro nem na árvore.
- **Contrato** (`StalkerCapabilities`): o que o monstro **pode** fazer: `vision.range = 30`, `move.run = 5.2`, `attack.charge`, a lista de ataques. É a única coisa que a árvore consulta.
- **Ataque**: um dado (`StalkerAttackSpec`) guardado numa característica de ataque. O mesmo monstro pode ter vários (golpe, investida, disparo...), cada um com alcance, tempos, dano, recarga, **peso** e **exigências** sobre as capacidades ("só investe se corre a 3 m/s ou mais").
- **Árvore**: decide **o que fazer e quando**, com os nós novos **Atacar**, **Tem capacidade**, **Capacidade comparada** e **Pode atacar**. A mesma árvore serve a monstros diferentes: um ataque que exige uma capacidade que o monstro não tem simplesmente não existe para ele.

> **Código escrito sem compilar no Unity.** Foi conferido por checagem estática, compilado com o compilador C# (Mono) contra um Unity de mentira (só os tipos que o código usa) e exercitado por um teste de comportamento sem Unity que roda o código real do perseguidor: o monstro com perfil em grafo se move e golpeia quadro a quadro como o monstro antigo, nos 4 níveis de atenção e com a árvore padrão, e o modo "Grafo ligado" (seção 5) reproduz o mesmo comportamento depois do "Ligar tudo". A janela e a tela do **canvas da árvore** foram ainda exercitadas por um teste de fumaça no Mono (menus de botão direito, Delete, arrastar uma ligação, Undo e a conferência, contra um GraphView "de mentira" com as mesmas assinaturas). **Nunca rodou no Unity**: o Inspector (IMGUI), os **dois canvas (GraphView)**, os menus e os scripts-modelo só foram compilados (os canvas contra assinaturas copiadas do código-fonte público do Unity 2022.3 a 6000.5), não desenhados nem abertos numa janela. A API do Editor usada está na seção 11. Se algo não compilar ou se comportar de outro jeito, mande a mensagem do Console.
>
> **Fase 2 (canvas do perfil)**: o grafo agora também tem uma **tela de caixas e fios** (seção 5): cada característica é uma caixa, e você liga **sentido → modificador → cérebro**. Ela edita o **mesmo asset** do Inspector, e o monstro só passa a obedecer aos fios quando você marca **Grafo ligado** (assets da fase 1 continuam no modo lista até lá).
>
> **Fase 3 (canvas da árvore e conferência)**: a árvore de comportamento ganhou a sua **tela de caixas** (um nó por caixa, uma ligação do pai a cada filho) e uma **conferência contra o perfil**, que acha sem dar Play o que a árvore pergunta e o monstro não tem (seção 6). As duas convivem com a janela de lista de sempre, e **Tools > Horror FPS > Stalker > Check All Stalker Trees** confere todas as árvores do projeto de uma vez. **Não fazem parte da fase 3**: um teste da árvore sem Play com um `Knowledge` simulado e o nó "Planejar" (seção 14).
>
> **Característica × traço × módulo.** Nos docs antigos, "traço" é o **módulo** (`StalkerModule`, um componente em *Add Component > HorrorFPS > Stalker > Traits*). Aqui, "característica" é a classe `StalkerTrait`: dados dentro do asset, **não** é componente. (`StalkerTraitSprint` é um módulo; `StalkerTraitVision`, `StalkerTraitStrike`... são características.)

## 1. O que vem no pacote

```
Assets/HorrorFPS/Stalker/Traits/
  StalkerTrait.cs           base de toda característica, StalkerLevelValue (um valor por nível de atenção) e os atributos
                            [StalkerTraitMenu], [StalkerPublishes] e [StalkerCapKey]
  StalkerCapabilities.cs    o contrato (StalkerCap = as chaves de série) e a detecção de conflitos
  StalkerAttack.cs          StalkerAttackSpec (a definição de um ataque, dados), StalkerAttack (o ataque em jogo, por monstro)
                            e StalkerRequirement (uma exigência sobre as capacidades)
  StalkerAttackRunner.cs    escolhe e executa os ataques (golpe, investida, disparo)
  StalkerTraitSet.cs        o conjunto de características de UM monstro: monta, ordena, chama os ganchos e isola os erros
  StalkerProfileGraph.cs    o asset (Create > HorrorFPS > Stalker Profile Graph), a conversão de um perfil de números e a validação
  StalkerTraitsBuiltin.cs   Visão, Audição, Locomoção, Astúcia, Golpe, Investida, Disparo e Fúria
  StalkerGraphOps.cs        (fase 2) as REGRAS do canvas, sem Unity: ids, ligações permitidas, quem fica ativo, "ligar tudo", organizar nós
Assets/HorrorFPS/Stalker/Tree/
  StalkerNodesAttack.cs     o nó Atacar e as condições Tem capacidade, Capacidade comparada e Pode atacar
  StalkerTreeOps.cs         (fase 3) as OPERAÇÕES sobre a árvore, sem Unity: mover, envolver, recuar, duplicar, colar, apagar (as regras do canvas)
  StalkerTreeLayout.cs      (fase 3) as posições das caixas, calculadas a partir da hierarquia
  StalkerTreeCheck.cs       (fase 3) a CONFERÊNCIA da árvore contra o contrato do perfil
Assets/HorrorFPS/Editor/Stalker/
  StalkerProfileGraphEditor.cs   o Inspector do asset
  StalkerProfileGraphMenu.cs     menus Stalker > Create Stalker Profile Graph... e Assets > Create > HorrorFPS > Stalker Trait Script...
  StalkerCapKeyDrawer.cs         campo de chave de capacidade, com o menu "▾" das chaves conhecidas
  StalkerLevelValueDrawer.cs     os quatro níveis (C A H F) numa linha só
  StalkerTraitTypes.cs           (fase 2) acha as classes de característica para os menus "Adicionar" (Inspector e canvas)
  StalkerTreeCheckMenu.cs        (fase 3) Stalker > Check All Stalker Trees: confere todas as árvores do projeto (não depende do canvas)
Assets/HorrorFPS/Editor/Stalker/Graph/      (fases 2 e 3) SÓ a camada visual dos canvas; apagar esta pasta tira os canvas sem afetar mais nada
  StalkerGraphWindow.cs          (fase 2) a janela do perfil: barra de ferramentas, painel lateral, Undo e as operações
  StalkerGraphView.cs            (fase 2) a tela (GraphView): arrastar, ligar só o que a regra permite, menu de botão direito
  StalkerGraphNodes.cs           (fase 2) as caixas (característica e cérebro)
  StalkerTreeGraphWindow.cs      (fase 3) a janela da árvore: barra, painel lateral, Undo e as operações
  StalkerTreeGraphView.cs        (fase 3) a tela da árvore: menus de botão direito, arrastar ligação, Delete
  StalkerTreeGraphNodes.cs       (fase 3) as caixas da árvore e a lista de tipos de nó dos menus
  StalkerGraphMenu.cs            os menus que abrem os dois canvas (Tools > ... > Open Stalker Profile Graph Canvas / Open Stalker Tree Canvas, e Assets > ...)
Arquivos que MUDARAM (sobrescreva): Stalker/StalkerBrain.cs, StalkerModules.cs, StalkerDefinition.cs, StalkerActions.cs,
                                    StalkerFinisher.cs, StalkerSpawner.cs, Editor/HorrorFPSMenu.cs (só um comentário),
                                    (fase 3) Stalker/Tree/StalkerNodeBase.cs, StalkerNodesCondition.cs, StalkerNodesTask.cs,
                                    StalkerTreeAsset.cs e Editor/Stalker/StalkerTreeAssetEditor.cs (seção 6.9),
                                    Docs/Menus.md, Docs/HorrorFPS-Perseguidor.md e Docs/HorrorFPS-ArvoreComportamento.md (ponteiros para este doc),
                                    Docs/HorrorFPS-Contexto-Scripts.md (inventário)
```

Sem `asmdef`, igual ao resto do framework. O que mudou nos arquivos existentes (nada foi removido nem renomeado):

- **`StalkerBrain`**: campo `Profile Graph`; `Traits` (o conjunto de características do monstro, `null` sem grafo); `Capabilities` (o contrato); `ProfileGraph`; `AttackSanityLoss`; `RebuildTraits()` e `SetProfileGraph()`. `Profile` agora devolve a **cópia de execução** do perfil quando há grafo (sem grafo é o mesmo de antes).
- **`StalkerModules`**: cada gancho chama primeiro as características do grafo e depois os módulos.
- **`StalkerDefinition`**: campo `Profile Graph` e `EffectiveProfile`. **`StalkerSpawner`**: usa `EffectiveProfile`.
- **`StalkerActions`**: o ramo padrão `Attack` troca para o executor de ataques **só quando** o monstro tem grafo **e** pelo menos um ataque publicado. Em qualquer outro caso a linha original continua valendo.
- **`StalkerFinisher`**: lê a perda de sanidade de `Brain.AttackSanityLoss` (a do último ataque que acertou; sem grafo, a do perfil, como antes).

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` por cima e espere compilar.
2. Selecione o monstro na cena e use **Tools > Horror FPS > Stalker > Create Stalker Profile Graph (From Selected)**. (Também aceita um `Stalker Profile` ou uma `Stalker Definition` selecionados.) Ele cria `Assets/Resources/HorrorFPS/StalkerProfileGraphs/StalkerProfileGraph_<nome>.asset` com **Visão, Audição, Locomoção, Astúcia e Golpe** nos **mesmos números** do perfil do monstro, e **liga o asset ao campo `Profile Graph`** do monstro (ou da definição) se esse campo estiver vazio; se já havia um, avisa no Console e não troca. Sem nada que sirva selecionado, uma janela explica. Para partir dos números padrão sem selecionar nada, use **(Default Monster)**.
3. Dê Play: o monstro deve se comportar **exatamente como antes**. É o ponto de partida: o grafo convertido reproduz o perfil de números.
4. Selecione o asset do grafo. O Inspector mostra a lista de características, os avisos (se houver) e, num bloco que abre e fecha, as **Capacidades publicadas**: o contrato que a árvore vai enxergar, com o valor e quem publicou cada chave.
5. **+ Adicionar característica > Ataques > Investida**. Em Play, clique em **Aplicar nos perseguidores em cena**. Já com a árvore padrão (ou sem asset de árvore) o monstro passa a investir: com o jogador a 3–9 m e a recarga pronta ele para, encara por 0,5 s, corre e acerta ao chegar; de perto continua com o golpe.
6. Acrescente **Modificadores > Fúria**: no nível Frenzy todos os ataques ficam com a recarga 40% menor e 20% mais dano.
7. (Fase 2) No Inspector do asset, clique em **Abrir canvas**: as características aparecem como caixas ligadas ao **Cérebro** (um grafo criado pelo menu já nasce ligado; num asset antigo, marque **Grafo ligado** na barra: o monstro continua igual). Veja a seção 5.
8. (Fase 3) Selecione o monstro na cena (ou a *Stalker Definition*, ou um asset de árvore) e use **Tools > Horror FPS > Stalker > Open Stalker Tree Canvas**: a árvore aparece em caixas e a janela adota o perfil do monstro. Teste a conferência: acrescente a uma caixa a condição *Tem capacidade* com a chave `attack.charge` (num monstro **sem** Investida): a caixa fica esmaecida com o selo `nunca roda` e o painel explica por quê; ponha **Ataques > Investida** no grafo e o aviso some sozinho. Veja a seção 6.

Para a **árvore editável**: troque o nó *Ramo padrão: Attack* por nós **Atacar** (receita 3, seção 9).

## 3. Como funciona

**Montagem (uma vez, no `Awake` do monstro; de novo em `RebuildTraits()`):**

1. O cérebro faz uma **cópia do perfil base** (`Base Profile` do grafo; se vazio, o perfil que o monstro já usava, ou o padrão). Essa cópia é o **perfil de execução**: `Brain.Profile` devolve ela, e **o asset nunca é tocado**. É destruída com o monstro.
2. Cada característica ligada roda `Apply(perfil)` para escrever os seus números na cópia: primeiro as da fase **Base**, depois as **Modificadoras**; dentro de uma fase vale a ordem da lista.
3. Depois de **todos** os `Apply`, cada uma roda `Publish(contrato, perfil)`: publica a partir dos números **finais** (já com os modificadores), então a capacidade nunca contradiz o que o monstro faz.
4. O **primeiro** ataque da lista é espelhado no bloco *Ataque* do perfil de execução (alcance, dano, recarga, perda de sanidade), para quem ainda lê esse bloco.
5. O contrato preenche sozinho as chaves `attack.*` (tabela abaixo).

**Em jogo (todo quadro), na ordem sentido → modificador → cérebro:**

- `ModifyTuning` ajusta os números do nível de atenção atual (características, depois módulos).
- `Tick`: os valores **efetivos** de cada ataque voltam aos da definição e então cada característica roda `ModifyAttack` (é assim que a Fúria encurta a recarga de ataques que não são dela) e `BrainTick`.
- A árvore roda e usa o contrato e os ataques.
- Eventos (`OnSpotted`, `OnLostSight`, `OnHint`, `OnModeChanged`, `OnAttentionChanged`, `OnArrived`) e os filtros (`ModifyVisibility`, `FilterNoise`) vão primeiro para as características e depois para os módulos: **os módulos têm a última palavra**.
- A cada golpe, `DecideAttack`: a primeira resposta diferente de `Normal` vale (características antes dos módulos). `Handled` = alguém assumiu o golpe (sem dano, a recarga corre, a mensagem de golpe não toca); `Cancel` = tenta de novo no quadro seguinte, sem gastar a recarga. Depois do dano, `OnAttackLanded`.

**Capacidades são estáticas.** O contrato guarda o **máximo entre os quatro níveis de atenção** ("ele *pode* enxergar a 30 m", no Frenzy). Serve à pergunta que a árvore faz: *este monstro é do tipo que...?* O valor do nível **atual** continua em `Brain.Tune`; para limitar um ataque por atenção use o campo `Min Attention` dele.

**Chaves de série** (as suas são livres; sugestão `monstro.coisa`, ex.: `faro.alcance`; o prefixo `attack.` é reservado):

| Chave | Quem publica | Significado |
|---|---|---|
| `vision.range` / `vision.fov` | Visão | maior alcance (m) / maior campo de visão (graus) |
| `hearing.range` | Audição | maior multiplicador de audição |
| `move.walk` / `move.run` | Locomoção | maior velocidade de ronda / de perseguição (m/s) |
| `mind.discernment` / `mind.search` | Astúcia | maior desconfiança de iscas (0 a 1) / maior tempo de busca (s) |
| `rage` | Fúria | marca |
| `attack.count` | automático | quantos ataques |
| `attack.maxRange` | automático | o maior alcance entre os ataques (m) |
| `attack.<etiqueta>` | automático | uma marca por etiqueta de ataque (`attack.melee`, `attack.charge`, `attack.ranged`...) |

Uma **marca** vale 1 quando a condição *Capacidade comparada* a lê como número. Capacidade que ninguém publicou conta como **"não tem"**: é por isso que a mesma árvore serve a monstros diferentes. Duas características que publicam o **mesmo número** geram um aviso no Inspector (vale a última).

**Sem perfil em grafo** o contrato também existe: o monstro publica as mesmas chaves a partir dos números do perfil (e o golpe do bloco *Ataque*). Assim uma árvore com os nós de capacidades funciona com e sem grafo.

**Erros ficam isolados.** Se uma característica lança exceção, o Console mostra **uma vez**, ela é desligada **naquele monstro** e as outras continuam. `RebuildTraits()` (o botão *Aplicar*) liga de novo.

**Quem manda em quê.** O campo `Profile Graph` do `StalkerBrain` ganha do da `StalkerDefinition`. Vários monstros podem dividir o mesmo grafo: o asset guarda só os **dados**; a recarga de cada ataque, o estado de cada característica e a cópia do perfil são de cada monstro.

## 4. O Inspector do perfil em grafo

- **Perfil base** (`Base Profile`): os números de partida. Cada característica ligada escreve por cima dos seus. O que nenhuma característica cobre (ganhos de atenção, Diretor, busca, adaptação...) continua vindo dele.
- **Lista de características**: cada uma é uma caixa com a chave de ligar/desligar, o nome, um resumo dos valores (`alcance 14 a 30 m`), os botões **▲ ▼** (ordem), **⧉** (duplicar) e **×** (remover) e, abrindo, a descrição e os parâmetros. *Nome* é opcional (aparece na lista). Tudo vai para o **Ctrl+Z**.
- **Valores por nível de atenção** aparecem em uma linha só: **C**alm, **A**lert, **H**unting, **F**renzy.
- **Chaves** (`[StalkerCapKey]`, nas exigências dos ataques e nas condições da árvore): um campo de texto com o botão **▾** que lista as chaves conhecidas (as de série, as `attack.*` e as de qualquer característica sua que declare `[StalkerPublishes]`).
- **+ Adicionar característica** lista todo tipo que herda de `StalkerTrait` no projeto, agrupado pelo caminho do `[StalkerTraitMenu]` (`Sentidos/Visão`, `Ataques/Investida`...). Classe sem `[Serializable]`, ou sem construtor sem parâmetros, **não aparece** e o Console explica uma vez.
- **Avisos** (caixas amarelas): característica ausente (classe apagada ou renomeada); nenhuma característica ligada; chave publicada por duas; id de ataque repetido; ataque com alcance zero, mínimo maior que o máximo, tempo ou dano negativo, investida sem *Hit Range*, exigência sem chave; **exigência que ninguém publica** (o ataque nunca será usado); e **grafo sem nenhuma característica de ataque** (o ramo *Attack* continua usando o bloco *Ataque* do perfil base e os nós Atacar/Pode atacar nunca acham um ataque).
- **Capacidades publicadas**: a prévia do contrato, sem precisar dar Play.
- **Aplicar nos perseguidores em cena** (só em Play): remonta as características e a árvore dos monstros que usam este grafo.
- **Grafo ligado** (caixa no alto): mostra o modo do asset e tem **Abrir canvas**, **Ativar grafo ligado** e **Voltar ao modo lista** (seção 5). No modo ligado, a linha de uma característica sem caminho até o cérebro ganha `[fora do cérebro: ignorada]`. **Duplicar** cria a cópia **sem ligações** e **remover** leva junto as ligações da característica.
- **Avisos do modo ligado** (além dos acima): característica sem ligação ao cérebro (é ignorada); modificador sem nenhuma entrada ativa (é ignorado); ligações quebradas (apontam para algo que não existe ou quebram a regra sentido → modificador → cérebro); características sem id ou com id repetido.

## 5. O canvas do perfil em grafo (fase 2)

Uma tela de caixas e fios sobre o **mesmo asset** do Inspector: o que você faz aqui aparece no Inspector e vice-versa (a tela confere o asset a cada 150 ms e se refaz sozinha, inclusive depois de um Ctrl+Z).

**Como abrir** (não abre com duplo clique no asset, de propósito: o gancho do Unity para isso muda de uma versão para outra e em versões novas virou erro de compilação):

- Inspector do asset: botão **Abrir canvas** (quadro *Grafo ligado*);
- **Tools > Horror FPS > Stalker > Open Stalker Profile Graph Canvas**: abre o asset selecionado, o da *Stalker Definition* selecionada ou o do perseguidor (*Stalker Brain*) selecionado na cena; sem nada selecionado abre vazio e o campo da barra escolhe o asset;
- botão direito no asset: **Assets > Open Stalker Profile Graph Canvas**.

**O que se vê**

| Elemento | O que é |
|---|---|
| Caixa **azul** | característica base (Visão, Audição, Locomoção, Astúcia, ataques): tem uma **saída** |
| Caixa **laranja** | modificador (Fúria): tem uma **entrada** (recebe as características que ele ajusta) e uma **saída** |
| Caixa **roxa**, "Cérebro" | o perseguidor: tem uma **entrada**; o que chega nele age no monstro |
| Caixa **esmaecida** | não age neste monstro: está desligada ou, no modo ligado, não tem caminho até o cérebro (o balão ao passar o mouse diz o que falta) |
| Painel da direita | os números da caixa selecionada (os mesmos campos do Inspector), as ligações dela, os avisos e as capacidades publicadas |
| Barra de cima | o asset, **+ Característica**, **Ligar tudo**, **Organizar**, **Enquadrar**, o número de avisos, **Grafo ligado** e **Asset** (mostra o arquivo no Project) |

**Como editar**

- **Adicionar** uma característica: botão direito no fundo (ela nasce onde você clicou) ou **+ Característica** (nasce no meio da tela).
- **Ligar**: arraste da bolinha de **saída** de uma caixa até a de **entrada** de outra. Só as ligações permitidas pela regra abaixo são aceitas: as portas que não servem não acendem.
- **Remover um fio**: selecione e aperte Delete, ou botão direito no fio. **Remover uma caixa**: Delete, ou botão direito > Remover (leva os fios dela). **Duplicar**: botão direito (a cópia nasce **sem ligações**). O Cérebro não pode ser apagado.
- **Botão direito numa caixa**: ligar ao cérebro / desligar do cérebro, desligar a característica (ignorada, sem apagar), duplicar, remover e *Disconnect all* (do próprio GraphView).
- **Mover** as caixas: arraste; a posição fica salva no asset (e no controle de versão). **Organizar** põe as bases à esquerda, os modificadores no meio e o cérebro à direita. Arrastar no vazio seleciona em retângulo; roda do mouse = zoom; botão do meio (ou Alt + arrastar) move a tela. **Ctrl+Z / Ctrl+Y** desfazem tudo.
- Ao abrir um asset antigo, o canvas dá **id** às características que não têm, tira ligações quebradas e organiza as caixas se estiverem todas empilhadas (uma vez, com Undo).

**A regra das ligações: sentido → modificador → cérebro**

| De | Para | Vale? |
|---|---|---|
| característica **base** | **cérebro** | sim |
| característica **base** | **modificador** | sim, se o modificador **aceita** aquela característica (`AcceptsInput`) |
| **modificador** | **cérebro** | sim |
| base → base, modificador → modificador, modificador → base, uma caixa nela mesma, sair do cérebro, ligação repetida | | não |

A Fúria só aceita características **de ataque** (ela ajusta ataques). Um modificador seu escolhe quem aceita sobrescrevendo `AcceptsInput`:

```csharp
public override bool AcceptsInput(StalkerTrait source)
{
    return source is StalkerTraitAttackBase;     // o padrão (sem sobrescrever) é aceitar qualquer característica base
}
```

**O modo "Grafo ligado"** (campo `Wired` do asset)

- **Desmarcado = modo lista** (o da fase 1, e o de todo asset antigo): as ligações do canvas são **só guardadas**, e toda característica ligada na lista age em tudo. O monstro se comporta como sempre. O canvas avisa isso numa faixa laranja, que é um botão: um clique ativa o modo ligado.
- **Marcado = os fios valem:**
  1. uma característica só age se existe, está ligada (não desligada) **e tem uma ligação ao Cérebro**;
  2. um **modificador** só age se, além disso, tiver **pelo menos uma entrada** que seja uma característica ativa que ele aceita;
  3. um modificador só mexe nos **ataques** das características que estão ligadas a ele: a Fúria ligada só ao Golpe **não** encurta a recarga da Investida. Os ganchos que não são de ataque (`ModifyTuning`, `ModifyVisibility`, `FilterNoise`...) rodam normalmente se o modificador está ativo;
  4. o resto não muda: a ordem e as fases (Base antes de Modificador), o contrato publicado (só as características ativas entram) e a árvore.
- **Ativar**: **Ativar grafo ligado** (Inspector), o botão **Grafo ligado** da barra ou a faixa laranja. Num asset **sem ligações** isso faz o **Ligar tudo**: cada característica vai ao cérebro e cada ataque vai a cada modificador que o aceita (a Fúria), então o monstro se comporta **igualzinho** ao do modo lista (conferido quadro a quadro, nos 4 níveis de atenção). Num asset que **já tem ligações** (você montou o grafo com o modo lista), elas valem como estão, só limpando as quebradas. **Voltar ao modo lista** guarda as ligações.
- **Assets criados pelo menu** (*Create Stalker Profile Graph...*) já nascem no modo ligado e organizados; os criados por *Assets > Create > HorrorFPS > Stalker Profile Graph* (vazios) e os da fase 1 ficam no modo lista até você ativar.
- **Característica nova num grafo ligado nasce sem ligações** (esmaecida, com aviso), e a cópia de uma caixa também: ligue-as onde quiser.

**O que fica guardado no asset** (campos escondidos, para quem revisa diffs): `id` e `graphPos` de cada característica, `links`, `brainPos` e `wired`. O jogo ignora posições; `id` e `links` só contam no modo ligado.

**Se o canvas não compilar ou der problema.** O canvas é a **única** peça que usa a API `UnityEditor.Experimental.GraphView` (marcada "Experimental" pelo Unity). Ela vive só em `Editor/Stalker/Graph/`. Se uma versão do Unity recusar compilar, **apague essa pasta**: o perfil em grafo, o modo ligado e o monstro continuam funcionando pelo Inspector (as regras estão em `StalkerGraphOps`, sem Unity), as ligações e posições ficam no asset, e o botão **Abrir canvas** passa a avisar que o canvas não está no projeto. Para o canvas voltar, recoloque a pasta.

## 6. O canvas da árvore e a conferência contra o perfil (fase 3)

Duas peças novas sobre os **mesmos assets** de antes (nada novo é salvo neles):

- **O canvas da árvore** (`StalkerTreeGraphWindow`): a árvore de comportamento desenhada como caixas, com uma ligação do pai a cada filho. É o par do canvas do perfil (seção 5). A janela de lista (*Stalker Tree Editor*) continua como era: as duas editam o mesmo asset e cada uma enxerga o que a outra mudou (o canvas confere o asset a cada 150 ms e se refaz sozinho, inclusive depois de um Ctrl+Z).
- **A conferência** (`StalkerTreeCheck`): cruza a árvore com o **contrato** do monstro, sem dar Play, e aponta o que não pode funcionar ("este nó pergunta por `vision.range` e ninguém publica", "este ramo exige uma investida que este monstro não tem"). Ela aparece dentro do canvas e, para o projeto todo, em **Tools > Horror FPS > Stalker > Check All Stalker Trees** (6.6).

### 6.1 Como abrir

- Inspector do asset de árvore: botão **Abrir canvas (grafo)**, ao lado de *Abrir no editor de árvore* (que continua abrindo a janela de lista);
- **Tools > Horror FPS > Stalker > Open Stalker Tree Canvas**: abre o asset de árvore selecionado, a árvore da *Stalker Definition* selecionada ou a do perseguidor (*Stalker Brain*) selecionado na cena, e **adota o perfil dele** (6.4). Sem nada selecionado abre como estava, e o campo da barra escolhe a árvore;
- botão direito no asset: **Assets > Open Stalker Tree Canvas**.

O duplo clique no asset continua abrindo a janela de lista, como antes (de propósito, como no canvas do perfil: o gancho do Unity para o duplo clique muda de versão para versão).

### 6.2 O que se vê

| Elemento | O que é |
|---|---|
| Título **azul** | composto: Seletor, Sequência, Sorteio |
| Título **roxo** | decorador: Inverter, Sempre sucesso, Sempre falha, Recarga, Limite de tempo, Repetir |
| Título **verde** | ação |
| Título **turquesa** | *Ramo padrão* (o comportamento pronto do perseguidor: Attack, Chase...) |
| Título **cinza-azulado** | *Subárvore*: uma caixa só, a árvore dela não é desenhada dentro |
| Número à esquerda do título | a ordem entre os irmãos: **1** é o que o pai tenta primeiro |
| Texto cinza | o resumo dos parâmetros do nó |
| Texto âmbar `se ...` | as condições que guardam o nó (`se NÃO Vê um jogador`); no máximo 5 linhas, depois `+ N condição(ões)` |
| Linha de selos | `! 2 aviso(s)`, `i 1 info`, `! 3 dentro da subárvore`, `nunca roda`, `vazio` (composto ou decorador sem filho: é ignorado), `+6 oculto(s)` (ramo recolhido), `sem subárvore` |
| Caixa **esmaecida** | desligada (`[desligado]` no título) ou, pela conferência, **nunca roda** com o perfil escolhido. O balão (mouse em cima) diz o que o nó é e o que a conferência achou |
| Painel da direita | o nó selecionado (nome, *Desligado*, condições e parâmetros, os mesmos campos da janela de lista), a conferência e, em Play, o estado do monstro |
| Barra de cima | o asset, **Novo**, **◀ Voltar**, **Perfil**, **Árvore padrão**, **Validar**, **Organizar**, **Enquadrar**, o contador de avisos, o seletor de perseguidor (só em Play) e **Travar** |

**As caixas não se arrastam.** A árvore não guarda posições: a de cada caixa é calculada a partir da hierarquia (a raiz à esquerda, os filhos à direita, de cima para baixo na ordem em que o pai os tenta, e o pai alinhado ao meio dos filhos). O que *muda* a árvore são as operações (arrastar uma ligação, os menus). **Organizar** só recalcula as posições e enquadra. As setas desenhadas são a própria hierarquia.

### 6.3 Como editar

- **Selecionar**: clique numa caixa. O painel da direita mostra o nó: nome (opcional), **Desligado**, as **condições** (cada uma com a bolinha ao vivo, **NÃO**, ▲ ▼ e ×) e os parâmetros. Os avisos da conferência sobre aquele nó e sobre cada condição aparecem no próprio painel.
- **Botão direito numa caixa**: *Adicionar filho*, *Adicionar condição*, *Inserir antes/depois* (um irmão), *Envolver com* (põe o nó dentro de um composto ou decorador), *Desembrulhar*, *Subir/Descer*, *Mover para dentro do irmão de cima*, *Mover para fora (ao lado do pai)*, *Duplicar*, *Copiar*, *Colar*, *Desligar*, *Recolher ramo*, *Abrir a subárvore* e *Remover*. O que não vale naquele nó aparece apagado (um decorador que já tem filho não aceita outro, o primeiro irmão não sobe...).
- **Botão direito no fundo**: *Organizar*, *Enquadrar tudo*, *Expandir/Recolher todos os ramos* e *Conferir contra o perfil*. Com a árvore vazia: *Adicionar nó raiz*, *Colar como raiz* e *Preencher com a árvore padrão*.
- **Mudar o pai arrastando**: arraste da bolinha de **saída** do pai até a bolinha de **entrada** de um nó: ele, com tudo que está abaixo, vira o **último filho** do novo pai. As bolinhas que não servem não acendem: a raiz (não tem pai), o próprio nó ou um descendente dele (criaria um ciclo), as ações (não têm filhos) e os decoradores que já têm um filho. Para escolher a **posição** entre os irmãos use *Subir/Descer*. A seta que você soltou não é guardada: a tela se refaz a partir da hierarquia.
- **Delete** remove o nó **e tudo que está abaixo dele** (o item *Remover* do menu diz quantos nós vão junto). Dá para selecionar vários (Ctrl/Shift + clique, ou arrastar no vazio) e apagar de uma vez. Remover a raiz deixa a árvore vazia.
- **Ctrl+Z / Ctrl+Y** desfazem qualquer operação (cada uma é um passo só).
- **Copiar / Colar**: guarda uma cópia do nó com o ramo dele. *Colar* põe a cópia como último filho do nó clicado (ou logo depois dele, se ele não aceita filhos). A cópia vale entre árvores diferentes e dura até o Unity recompilar.
- **Recolher ramo** esconde os descendentes (selo `+N oculto(s)`). Só existe na tela: não vai para o asset e se perde quando o Unity recarrega o asset (recompilar, reabrir o projeto; um Ctrl+Z também reabre os ramos).
- **Subárvore**: *Abrir a subárvore* (menu ou painel) abre a árvore dela nesta janela e **◀ Voltar** retorna. A conferência da árvore inclui a subárvore (os achados de dentro dela aparecem na caixa como `! N dentro da subárvore`).
- **Árvore padrão** (barra) troca TODA a árvore pela padrão do perseguidor, com confirmação. **Novo** cria uma árvore padrão num asset novo.
- Se alguém muda o asset por fora (o Inspector, a janela de lista, um Ctrl+Z), o canvas se refaz sozinho. Uma caixa velha nunca age em dados novos: antes de qualquer operação ele confere se o nó ainda está no lugar e, se não estiver, apenas recarrega.

### 6.4 Qual perfil vale na conferência

A conferência compara a árvore com **o que o monstro publica**. Ela escolhe assim:

- campo **Perfil** da barra: escolha um *Stalker Profile Graph*. Ao lado, um texto diz o que vale: `grafo`, `números: <nome>` (sem grafo, o *Stalker Profile* de números do monstro publica as mesmas chaves, seção 3) ou `sem perfil`;
- selecionar uma **Stalker Definition**, ou um perseguidor na cena, abre a árvore dele **e adota o perfil dele** (o grafo; sem grafo, os números dele). Se a Definição ou o perseguidor não tem árvore própria (roda a do código), a janela não muda;
- selecionar só um asset de árvore mantém o perfil que já estava na barra;
- **Travar** (barra): a janela deixa de seguir a seleção;
- **sem perfil nenhum**, só rodam as conferências que não dependem dele: variáveis, atenção, subárvores e chave de capacidade digitada errado.

O contrato é lido do asset a cada segundo: mudar o grafo (acrescentar uma Investida, apagar a Visão) atualiza os avisos da árvore sem você fazer nada.

### 6.5 O que a conferência acha

| Achado | Nível | Exemplo do texto |
|---|---|---|
| Chave que nenhuma característica do projeto publica (erro de digitação) | aviso | `A chave 'vison.range' não é publicada por nenhuma característica do projeto (erro de digitação?).` Funciona **sem** perfil; com perfil acrescenta "Por isso a condição nunca vale, e este nó (com tudo abaixo dele) não roda." **Não conta** como digitação: uma chave `attack.<etiqueta>` (as etiquetas dos ataques são livres) nem uma que o contrato do perfil escolhido já publica (mesmo que a característica não declare `[StalkerPublishes]`) |
| Condição que **nunca vale** com este perfil | aviso | `Nunca vale com o perfil 'Boneco': o perfil não publica 'attack.charge'. Este nó e tudo abaixo dele não rodam.` (*Tem capacidade*, *Capacidade comparada* e *Pode atacar* quando nenhum ataque do perfil serve) |
| Condição que **sempre vale** | info | `Sempre vale com o perfil 'Boneco': o perfil publica 'move.run'. A condição não filtra nada aqui.` |
| Nó que **sempre falha** com este perfil | aviso | um *Atacar* por id ou etiqueta que o monstro não tem: `Com o perfil 'Boneco' este nó sempre falha: ...` |
| **Contradição de atenção** | aviso | `...este nó só poderia rodar em {Calm, Alert}, e esta condição só vale em {Hunting, Frenzy}. Nunca valem juntas...` (o *Nível de atenção* do nó contra os dos nós de cima) |
| Nó que **nunca é consultado** porque um irmão de cima o trava | info | no Seletor, um irmão que nunca falha; na Sequência, um que sempre falha: `Nunca roda: o irmão acima ('Perseguir') nunca falha, então o Seletor para nele e nunca chega a este.` |
| **Variável lida que ninguém escreve** | aviso | `Lê a variável 'alvo', mas nenhum nó 'Definir variável' desta árvore (nem das subárvores) escreve nela: ela fica sempre em 0.` (se um **módulo** escreve nela, ignore) |
| **Subárvore que contém a si mesma** | aviso | `A subárvore 'X' contém a si mesma (ciclo): este nó é ignorado.` |
| O perfil **tem ataques e a árvore nunca ataca** | aviso (da árvore toda) | `O perfil 'Boneco' tem 2 ataque(s), mas nenhum nó da árvore ataca (...): o monstro nunca atacaria.` |
| Um ataque do perfil que **nenhum *Atacar* escolhe** | info (da árvore toda) | `O ataque 'investida' ... não é escolhido por nenhum 'Atacar' desta árvore (eles escolhem por id ou etiqueta e este não combina).` Só aparece quando nenhum *Atacar* é "melhor disponível" e o ramo padrão *Attack* não está na árvore |

Os nós que **nunca rodam** (a guarda deles nunca passa, ou algo acima os trava) ficam **esmaecidos** na tela, com o selo `nunca roda`, e entram na contagem da barra. Os problemas **estruturais** (composto sem filho, condição ou nó de tipo apagado, subárvore sem asset...) continuam vindo da **validação do asset** (a mesma da janela de lista e do Inspector), numa caixa à parte do painel.

**A conferência só afirma o que tem certeza.** Uma condição ou nó que não sabe responder vale "pode acontecer", e um composto seu, com regra de combinação própria, não leva ninguém a conclusão nenhuma. Por isso um nó novo seu **nunca gera aviso falso**; ele só participa se implementar os ganchos da seção 6.8. Outros limites: o contrato é tratado como **fixo** (um módulo que muda capacidades só em jogo escapa da conferência); *Pode atacar* só diz "nunca" (recarga, alcance e atenção mudam em jogo, então ele nunca "sempre vale"); e a tela mostra o que a árvore **pode** fazer, não o que ela faz num momento.

### 6.6 Conferir o projeto todo

**Tools > Horror FPS > Stalker > Check All Stalker Trees** confere, sem abrir janela e sem Play:

1. cada **Stalker Definition** do projeto (a árvore dela contra o perfil em grafo, ou os números, dela);
2. cada **perseguidor das cenas abertas** (a árvore e o perfil próprios dele);
3. as árvores que ninguém usa (só o que não depende de perfil).

Uma árvore usada por vários monstros é conferida **uma vez por perfil diferente**. No Console sai uma mensagem por árvore que tenha achados (aviso amarelo se há aviso; clicar nela destaca o asset da árvore) e, no fim, um resumo: `[Stalker] Conferidas N árvore(s): X aviso(s), Y informação(ões), Z nó(s) que nunca rodam.` Uma janela avisa se o projeto não tem nenhuma árvore. **Prefabs que não estão numa cena aberta não são abertos** (seria lento num projeto grande): abra o prefab, ou selecione a Definição dele, e use o canvas.

Este menu **não depende do canvas** (fica fora da pasta `Graph`): continua funcionando se você apagar a pasta.

### 6.7 Ao vivo (Play)

Com o jogo rodando, escolha o monstro no seletor da barra (só aparecem os que usam esta árvore). Cada caixa ganha uma linha de estado: **● rodando** (verde), **● sucesso** (azul), **● falhou** (vermelho) ou **○ não consultado** (cinza); as condições ficam azuis quando valeram e vermelhas quando não. É o mesmo rastro da janela de lista. No painel aparecem o modo, a atenção, se vê um jogador, a pista e as variáveis do monstro. Marque **Aplicar ao editar** para que cada edição remonte a árvore dos monstros que a usam (ou use **Aplicar agora**); isso zera o estado interno dos nós, como no botão *Aplicar* do perfil.

### 6.8 Fazer os seus nós participarem da conferência

Os cinco ganchos abaixo são **opcionais** e **não podem usar `Brain`** (a conferência roda no Editor, sem o monstro). Sem eles o seu nó funciona igual e só fica de fora da conferência:

| Gancho | Em | Para quê |
|---|---|---|
| `CapabilityKeyUsed()` | condição | devolve a chave de capacidade que ela consulta; a conferência avisa se nenhuma característica a publica (digitação) |
| `StaticTruth(caps, out why)` | condição | com o contrato, ela vale sempre (`AlwaysTrue`), nunca (`AlwaysFalse`) ou depende do jogo (`Unknown`, o padrão); responda como se **não** houvesse o campo NÃO (a conferência aplica depois) |
| `AttentionMask()` | condição | só se ela depende **exatamente** do nível de atenção: um bit por nível (0 = Calm ... 3 = Frenzy); `-1` = não sei (o padrão) |
| `ImpossibleWith(caps, out why)` | nó | devolva `true` só se tiver **certeza** de que ele sempre falha com este contrato, e diga por quê |
| `CollectVariables(reads, writes)` | nó e condição | os nomes de variáveis do `Blackboard` que ele lê e escreve |

Já participam: *Tem capacidade*, *Capacidade comparada*, *Pode atacar*, *Atacar*, *Nível de atenção*, *Variável* e *Definir variável*. A validação do asset também passou a chamar o `Validate` das **condições** (antes só chamava o do nó) e a tratar uma exceção dele como um aviso em vez de quebrar a janela.

```csharp
[Serializable]
[StalkerNodeMenu("Capacidades/Tem faro", "Vale se o monstro tem a capacidade 'meumonstro.alcance'.")]
public class TemFaro : StalkerCondition
{
    public override string CapabilityKeyUsed() { return "meumonstro.alcance"; }

    public override StalkerTruth StaticTruth(StalkerCapabilities caps, out string why)
    {
        why = caps.Has("meumonstro.alcance") ? "o perfil publica 'meumonstro.alcance'" : "o perfil não publica 'meumonstro.alcance'";
        return caps.Has("meumonstro.alcance") ? StalkerTruth.AlwaysTrue : StalkerTruth.AlwaysFalse;
    }

    protected override bool Check() { return Brain.Capabilities.Has("meumonstro.alcance"); }
}
```

### 6.9 O que mudou nos arquivos existentes

Nada foi removido nem renomeado:

- **`StalkerNodeBase.cs`**: o enum `StalkerTruth`, os ganchos da 6.8 (todos com padrão "não sei") e o campo `editorCollapsed` (só da tela, não serializado);
- **`StalkerNodesCondition.cs`** e **`StalkerNodesTask.cs`**: as condições *Nível de atenção* e *Variável* e a ação *Definir variável* respondem aos ganchos; as duas últimas ganharam `Validate` (nome de variável vazio);
- **`StalkerTreeAsset.cs`**: a validação chama o `Validate` das condições e isola exceções;
- **`StalkerTreeAssetEditor.cs`**: o botão **Abrir canvas (grafo)** (registrado pela pasta `Graph`: sem ela o botão apenas avisa);
- **`HorrorFPSMenu.cs`**: só o comentário (`StalkerOrder`: em uso de +0 a +16).

## 7. As características prontas

Valores entre parênteses são os padrões; os de Visão, Audição, Locomoção e Astúcia são **iguais aos do `StalkerProfile` padrão** (C / A / H / F).

| Característica (menu) | Fase | Parâmetros | Publica |
|---|---|---|---|
| **Sentidos/Visão** | Base | alcance (14/18/24/30 m), campo de visão (100/115/130/150°), segundos para confirmar (1,6/1,0/0,55/0,30) | `vision.range`, `vision.fov` |
| **Sentidos/Audição** | Base | multiplicador (0,9/1,1/1,35/1,6), volume mínimo para reagir (0,25/0,18/0,10/0,06), oclusão por parede (0,6) | `hearing.range` |
| **Corpo/Locomoção** | Base | velocidade de ronda (1,3/1,8/2,3/2,9), de perseguição (3,0/3,6/4,5/5,2) | `move.walk`, `move.run` |
| **Mente/Astúcia** | Base | desconfiança de iscas (0,55/0,80/1/1), segundos de busca (14/22/34/45), antecipação (0/0,4/0,8/1,2 s) | `mind.discernment`, `mind.search` |
| **Ataques/Golpe** | Base | um ataque (modelo abaixo) | um ataque, etiqueta `melee` |
| **Ataques/Investida** | Base | um ataque | um ataque, etiqueta `charge` |
| **Ataques/Disparo** | Base | um ataque | um ataque, etiqueta `ranged` |
| **Modificadores/Fúria** | Modificador | a partir de que nível (Frenzy), fator da recarga (0,6), fator do dano (1,2) | marca `rage` |

**Atenção:** uma característica que **não está** no grafo não zera nada. Sem *Visão* na lista o monstro continua enxergando com os números do perfil base; ele só **deixa de publicar** `vision.range` (e então um ataque que exige essa chave nunca é usado, e o Inspector avisa). Para um monstro cego, deixe a *Visão* com alcance 0 (o Inspector lembra, num aviso, que ali ele não enxerga nada).

### Os ataques

Os três modelos de ataque (todos os campos são editáveis; `Id` precisa ser único no monstro):

| | Golpe | Investida | Disparo |
|---|---|---|---|
| `Id` / etiqueta | `golpe` / `melee` | `investida` / `charge` | `tiro` / `ranged` |
| Estilo | Strike | Charge | Shot |
| Alcance mín. – máx. | 0 – 1,7 m | 3 – 9 m (*Hit Range* 1,6) | 4 – 16 m |
| Preparação / recuperação | 0 / 0 s | 0,5 / 0,6 s | 0,7 / 0,5 s |
| Recarga | 1,4 s | 8 s | 5 s |
| Dano / sanidade | 34 / 6 | 34 / 6 | 18 / 3 |
| Exige | — | `move.run >= 3` | `vision.range >= 12` |

O golpe criado pela conversão de um perfil usa o alcance, o dano, a recarga e a perda de sanidade do bloco *Ataque* **desse perfil**.

**Como cada estilo age.** Todos começam pela **preparação**: o monstro para, encara o alvo e dispara o gatilho de animação (`Anim Trigger`). Terminada a preparação:

- **Strike**: acerta na hora quem ainda estiver ao alcance (com 0,15 m de folga); se o jogador se afastou durante a preparação, **erra** (e a recarga é gasta).
- **Charge**: corre até o alvo (`Charge Speed`; 0 = a velocidade de corrida do nível de atenção) e acerta ao chegar a `Hit Range`. Se o alvo some (esconde-se ou muda de andar) ou passa `Charge Max Seconds` (3 s), desiste e gasta a recarga.
- **Shot**: acerta de longe, mas só se estiver **vendo** o alvo no instante do disparo. O projétil, o som e o efeito ficam por sua conta: `Strike Message` chama um método no monstro (`SendMessage`; 0 parâmetros ou 1 `string` com o id do ataque, para um método servir a vários).

Depois do golpe vem a **recuperação** (parado, encarando). Regras gerais:

- A **recarga** (`Cooldown`) só começa quando o ataque é **gasto**: um ataque interrompido na preparação não gasta nada. A recarga é **de cada monstro** (e de cada ataque).
- O ataque só é usado se: o nível de atenção chega a `Min Attention`; **todas** as exigências (`Requirements`) são cumpridas pelo contrato; está pronto; e o alvo está entre o alcance mínimo e o máximo (no plano; mais de 1,6 m de diferença de altura não vale).
- **Escolha entre vários:** *Melhor disponível* **sorteia pelo peso** (`Weight`) entre os que podem ser usados agora (peso 0 = nunca sorteado); *Por id* só aquele; *Por etiqueta* só os que a têm.
- **Colado no alvo:** se não há nada pronto mas o alvo está ao alcance do ataque corpo a corpo mais curto (os de alcance mínimo 0), o monstro **espera a recarga parado, encarando**, em vez de empurrar o jogador, como o ramo *Attack* sempre fez. Um ataque em andamento (uma investida no meio) continua mesmo que o alvo saia do alcance; o que o interrompe é perder o jogador de vista ou um ramo mais prioritário assumir, e aí a recarga **não** é gasta.
- **Em Play** os números de um ataque (alcance, tempos, dano, peso, exigências) valem **ao vivo**, porque o ataque é compartilhado; o resto (adicionar, remover, ligar ou desligar característica, os números de Visão etc.) pede o botão *Aplicar*.

## 8. Os nós novos da árvore

| Nó (menu) | O que faz |
|---|---|
| **Ações/Atacar** | Escolhe e executa um ataque do contrato. `Pick`: *Best* (melhor disponível), *ById* (por id) ou *ByTag* (por etiqueta); `Id Or Tag` diz qual. Devolve **Falha** se não há o que atacar (o nó seguinte do Seletor assume), **Executando** enquanto prepara, investe, recupera ou espera a recarga colado no alvo. Precisa estar **vendo** um jogador. |
| **Capacidades/Tem capacidade** | Condição: vale se o monstro tem a chave (número ou marca). Não publicada = Falha. |
| **Capacidades/Capacidade comparada** | Condição: compara uma capacidade com um valor (`>=`, `<=`, `=`). Uma marca vale 1. Não publicada = Falha. |
| **Capacidades/Pode atacar** | Condição: existe um ataque que ele poderia usar agora no jogador que vê. `Require Ready` e `Require In Range` vêm ligados; desligue o primeiro para perguntar só "o alvo está ao alcance?" (ficar parado esperando a recarga) ou o segundo para perguntar só "há algum ataque pronto?". Um ataque em andamento conta como "pode". |

Eles aparecem sozinhos nos menus da janela *Stalker Tree Editor* (como qualquer nó). O **ramo padrão** `Attack` (do nó *Ramo padrão*) já usa os ataques do grafo: quem não quer montar nada na árvore já ganha investida e disparo só de adicionar a característica.

## 9. Receitas

**1. Golpe + investida.** Grafo: Visão, Audição, Locomoção, Astúcia, **Golpe**, **Investida**. Não precisa mexer na árvore: de 3 a 9 m e com a recarga de 8 s pronta ele investe; de perto, golpeia; entre uma e outra, persegue. Para investir de mais perto, baixe `Min Range` da investida (de mais longe, suba `Max Range`); para investir só enraivecido, ponha `Min Attention = Frenzy`.

**2. Fúria.** Acrescente **Modificadores/Fúria**. Ela roda depois das características base (fase Modificador), em qualquer posição da lista, e muda os valores **efetivos** de todos os ataques, inclusive os que não são dela. Para ela valer só em Frenzy, deixe `From Level = Frenzy`.

**3. Árvore que escolhe entre ataques.** Num Seletor, na ordem:

```
Seletor
├─ Atacar  (Por id: investida)   se Tem capacidade  attack.charge      ← só monstros com investida passam daqui
├─ Atacar  (Por etiqueta: melee)
└─ ... (perseguir, procurar, ronda)
```

Sem a característica *Investida*, o primeiro nó falha e o monstro vai direto para o golpe. A **mesma árvore** serve aos dois monstros.

**4. Atirador que depende da visão.** Acrescente **Ataques/Disparo** (exige `vision.range >= 12`). Num monstro com visão curta o ataque simplesmente não existe, e o Inspector avisa se ninguém publica `vision.range`.

**5. Comportamento por capacidade.** Ponha a condição *Capacidade comparada* (`vision.range >= 20`) num trecho da árvore, por exemplo uma Sequência "vigiar" com os nós *Encarar* e *Esperar*, antes do ramo de ronda: só os monstros de visão longa executam o trecho; os outros caem direto no ramo seguinte do Seletor.

**6. Característica sua.** *Assets > Create > HorrorFPS > **Stalker Trait Script (Characteristic)*** cria o script (o nome do arquivo vira o nome da classe):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using HorrorFPS;

[Serializable]
[StalkerTraitMenu("Meu Monstro/MeuFaro", "Descreva aqui o que esta característica é.")]
[StalkerPublishes("meumonstro.alcance", "Descreva a capacidade que esta característica publica")]
public class MeuFaro : StalkerTrait
{
    public float range = 20f;

    public override string Summary() { return range.ToString("0.#") + " m"; }

    public override void Publish(StalkerCapabilities caps, StalkerProfile profile)
    {
        caps.SetFloat("meumonstro.alcance", range);
    }
}
```

Ela aparece sozinha em **+ Adicionar característica > Meu Monstro > MeuFaro**; a chave aparece no menu **▾**; uma exigência de ataque `meumonstro.alcance >= 10` ou a condição *Capacidade comparada* passam a enxergá-la. Sem `[StalkerTraitMenu]` ela cai em *Personalizadas/<nome>*. O **(Modifier)** é o mesmo molde na fase Modificador (`ModifyTuning`, `ModifyAttack`...).

Regras de uma característica: **só dados** (campo público = parâmetro; campo privado = estado deste monstro, e listas ou arrays de estado se criam em `OnBuild()`, porque a cópia é rasa); `Apply` e `Publish` rodam **uma vez**, ao montar, e **não podem usar `Brain`** (no Editor ele é `null`); `Summary()` e `Validate()` também não; e **nunca mexa na árvore** daqui: característica diz o que ele É, quem decide é a árvore. Um ataque novo? Herde de `StalkerTraitAttackBase` e preencha o `attack` no construtor (veja `StalkerTraitCharge`).

## 10. Característica, módulo ou árvore?

| Preciso de... | Use |
|---|---|
| um sentido, um atributo ou um ataque que vários monstros combinam e que a árvore possa **consultar**; ajustar números por nível de atenção; mexer nos ataques | **característica** (esta) |
| estado que vai para o **save** (`SaveState`), a **finalização** com cutscene, **ramos novos** para a árvore (`BuildBranches`), algo amarrado à cena | **módulo** (`StalkerModule`, seção 9 do doc *Perseguidor*) |
| decidir **quando** e **em que ordem** o monstro faz as coisas | **árvore** (doc *Árvore de comportamento*) |

Os três convivem. Num mesmo gancho, as características rodam antes dos módulos. O `StalkerFinisher` e o `StalkerTraitSprint` continuam módulos, e o finalizador continua assumindo o golpe pelo `DecideAttack`.

## 11. Premissas de API do Unity (código não compilado no Unity)

| Usado em | Premissa |
|---|---|
| o asset | `[SerializeReference] List<StalkerTrait>` (Unity 2019.3 ou mais novo); cada classe concreta `[Serializable]`, com construtor sem parâmetros; só campos públicos (ou `[SerializeField]`) serializam |
| duplicar uma característica | `JsonUtility.ToJson(this)` / `FromJson(json, tipo)` com o tipo concreto (só campos serializáveis); se falhar, cópia rasa |
| o perfil de execução | `Object.Instantiate` de um `ScriptableObject` com `HideFlags.HideAndDontSave`, destruído em `OnDestroy` do cérebro |
| o Inspector | `SerializedObject`/`SerializedProperty` (`FindPropertyRelative`), `PropertyDrawer` para `[StalkerCapKey]` (um `PropertyAttribute`) e para `StalkerLevelValue`, `TypeCache.GetTypesDerivedFrom<StalkerTrait>`, `GenericMenu`, `Undo.RecordObject` e `Undo.undoRedoPerformed` |
| criar script | `ProjectWindowUtil.CreateScriptAssetFromTemplateFile` (grava um modelo temporário em `Library/`) |
| botão *Aplicar* | `HorrorUtil.FindAll<StalkerBrain>()` (o do projeto: `FindObjectsByType` no Unity 2023.1+, `FindObjectsOfType` antes) |
| o canvas (fase 2) | `UnityEditor.Experimental.GraphView`: `GraphView`, `Node`, `Port`, `Edge`, `GraphViewChange` (`graphViewChanged`), `GetCompatiblePorts`, `ContentDragger`/`SelectionDragger`/`RectangleSelector`/`ContentZoomer`, `GridBackground`; mais `EditorWindow.CreateGUI`, `IMGUIContainer` e `VisualElement.schedule`. As assinaturas foram conferidas no código-fonte público do Unity (2022.3, 6000.0, 6000.3 e 6000.5) e são iguais nelas. Só estilos *inline* (nada de USS/UXML) e nada de `MiniMap`, `Toolbar` ou `[OnOpenAsset]` |
| o canvas da árvore (fase 3) | as mesmas classes do canvas do perfil e mais: `GraphView.SetupZoom`, `viewTransform` e `UpdateViewTransform` (guardar e devolver o zoom ao refazer a tela), `FrameAll`, `graphElements`, `Edge.edgeControl`, as sobrescritas `public virtual` de `GraphView.AddToSelection`, `RemoveFromSelection`, `ClearSelection` e `GetCompatiblePorts` e de `Node.BuildContextualMenu`, `ContextualMenuPopulateEvent`/`DropdownMenu`, `RectangleSelector`, `EditorApplication.delayCall` (a tela nunca é refeita no meio de um evento do GraphView). As caixas só têm `Capabilities.Selectable | Deletable` (sem `Movable`) e as setas são inertes (`pickingMode = Ignore`). O Delete e o arrastar de uma ligação chegam por `graphViewChanged`, que a tela esvazia: quem altera a árvore é a janela, com `Undo.RecordObject`. Conferido no código-fonte público (2022.3, 6000.0, 6000.3 e 6000.5). `StalkerTreeCheck`, `StalkerTreeOps` e `StalkerTreeLayout` são C# puro, sem Unity |
| código | C# "antigo": sem `var`, `$""`, `nameof` ou membros com `=>`; compila no Unity 2022.3 e no Unity 6 |

## 12. Limitações

- **Os fios do canvas do perfil só valem com Grafo ligado**; sem isso o monstro usa a lista, como na fase 1. (O canvas da árvore não tem esse modo: a hierarquia **é** a árvore.)
- **Os fios dizem quem age e o que um modificador alcança**, não passam números: uma característica não "lê" a saída de outra. Quem precisa de um valor de outra característica usa o contrato (`Capabilities`) ou o perfil de execução.
- **Um modificador só distingue ataques pela origem** (qual característica de ataque o publicou); ligar a Fúria só a uma de duas características que publicam o mesmo `Id` de ataque é possível, mas o aviso de id repetido já pede para você mudar um.
- **Capacidades são estáticas** (o máximo entre os níveis). O que vale *agora* está em `Brain.Tune` e `Brain.Level`.
- **O estado das características não vai para o save** (recargas de ataque, o que uma característica sua guardar). Quem precisa salvar estado usa um módulo (`SaveState`).
- **Um grafo por monstro.** Para combinar partes de dois grafos, duplique um asset e monte o terceiro.
- **A conferência da árvore é estática** (seção 6.5): só afirma o que o contrato já decide. Ficam de fora as capacidades que um módulo muda só em jogo, o que depende do momento (recarga, alcance, atenção) e os nós seus que não implementam os ganchos da seção 6.8: um nó seu nunca gera aviso falso, mas também não é conferido. Ela não substitui o Play.
- **O canvas da árvore não guarda posições**: elas vêm da hierarquia, então as caixas não se arrastam e uma árvore com centenas de nós fica larga (use *Recolher ramo*, que não é salvo). Uma *Subárvore* é uma caixa só.
- **Mover um nó arrastando** só o põe como **último filho** do novo pai (para a posição exata use *Subir/Descer*).
- **Check All Stalker Trees** não abre prefabs que não estão numa cena aberta: abra o prefab, ou selecione a Definição, e use o canvas.
- **Editar o asset em Play:** os números dos ataques valem ao vivo; o resto pede o botão *Aplicar*, que zera o estado interno dos nós da árvore (um "Esperar" no meio recomeça) e a recarga dos ataques. As alterações **ficam** no asset: é um asset, não uma cena.
- **O espelho do bloco *Ataque*** vale só para o **primeiro** ataque da lista. Quem lê esse bloco (por exemplo, código seu) enxerga só ele.
- **Renomear a classe de uma característica** a perde do asset (vira "ausente" no aviso). Ao renomear, ponha `[MovedFrom(...)]` (`UnityEngine.Scripting.APIUpdating`) ou refaça o campo.
- **Desempenho:** a montagem é uma vez por monstro; por quadro, cada característica ligada paga os ganchos que sobrescreve (o isolamento de erros usa `try/catch`).

## 13. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Minha característica não aparece no menu | falta `[Serializable]` na classe, ou o construtor exige parâmetros (o Console avisa uma vez); ou o projeto não compilou |
| "Característica N: ausente" | a classe foi renomeada ou apagada; use o **×** para limpar, ou `MovedFrom` ao renomear |
| O ataque nunca é usado | uma **exigência** não cumprida (o Inspector avisa as que ninguém publica); `Min Attention` acima do nível em que o monstro está; alvo fora de `Min Range`–`Max Range`; `Weight` 0 em *Melhor disponível*; recarga ainda correndo |
| Adicionei uma característica de ataque e o monstro bate igual a antes | falta dar Play de novo, ou clicar em **Aplicar nos perseguidores em cena**; ou o monstro usa outro grafo (o campo do `StalkerBrain` ganha do da `StalkerDefinition`) |
| O monstro com grafo não ataca nada | o grafo tem característica de ataque, mas a árvore (asset) não tem um nó **Atacar** nem o *Ramo padrão: Attack*; ou nenhum ataque cumpre as exigências |
| "Nenhuma característica de ataque" | o grafo não tem Golpe/Investida/Disparo; o ramo *Attack* usa o bloco *Ataque* do perfil base. Acrescente um **Golpe** |
| "A capacidade 'x' é publicada por ... e por ..." | duas características publicam a mesma chave; renomeie uma delas |
| "O ataque 'x' aparece mais de uma vez" | dois ataques com o mesmo `Id` (duplicar uma característica de ataque copia o id); mude um |
| Mudei a Visão em Play e nada mudou | os números das características só entram ao montar: clique em **Aplicar nos perseguidores em cena** |
| Erro `[Stalker] Erro na característica ...` | exceção no seu código; leia o stack trace. O monstro continua, sem aquela característica |
| `StalkerTraitSprint` não aparece em *Adicionar característica* | ele é um **módulo** (componente do monstro), não uma característica |
| Liguei no canvas e o monstro não mudou | o asset está no **modo lista**: marque **Grafo ligado** (barra do canvas, Inspector ou a faixa laranja); em Play, clique em **Aplicar nos perseguidores em cena** |
| Uma característica aparece esmaecida no canvas | desligada, ou (no modo ligado) sem ligação ao **Cérebro**, ou é um modificador **sem entrada**; passe o mouse na caixa: o balão diz o que falta |
| Não consigo ligar duas caixas | a regra é sentido → modificador → cérebro: base→base, modificador→modificador e modificador→base não existem, e o modificador pode recusar a entrada (`AcceptsInput`; a Fúria só aceita ataques) |
| "Abrir canvas" avisa que o canvas está indisponível | a pasta `Editor/Stalker/Graph` não está no projeto ou não compilou (veja o Console); o Inspector continua valendo |
| Erro de compilação em `Editor/Stalker/Graph/...` | a API GraphView mudou na sua versão do Unity: apague a pasta `Editor/Stalker/Graph` (o resto não depende dela) e mande a mensagem do Console |
| Duas caixas na mesma posição | uma característica criada por script ou pelo Inspector não tem posição: use **Organizar** |
| "Abrir canvas (grafo)" avisa que o canvas está indisponível (árvore) | a pasta `Editor/Stalker/Graph` não está no projeto ou não compilou (veja o Console); a janela de lista e o menu *Check All Stalker Trees* continuam valendo |
| Não consigo soltar a ligação num nó (a bolinha não acende) | a regra da 6.3: a raiz não tem pai; um nó não vai para dentro dele mesmo nem de um descendente; ações não aceitam filhos; um decorador que já tem filho está cheio |
| Quase todas as caixas estão esmaecidas / com "nunca roda" | o campo **Perfil** da barra aponta para o perfil errado (ou para nenhum: veja o texto ao lado dele), ou o grafo mesmo não publica o que a árvore consulta; passe o mouse na caixa para ver o motivo |
| "A chave 'x' não é publicada por nenhuma característica do projeto" e eu sei que ela existe | a conferência procura a chave nas características do projeto (as de série, as que declaram `[StalkerPublishes]`) e, quando há perfil, no contrato dele; só aparece o aviso se **nenhum dos dois** a tem. Confira o nome (maiúsculas, ponto) e se a característica que a publica está no grafo escolhido na barra, ou ponha `[StalkerPublishes("x", "...")]` nela |
| "Lê a variável 'x', mas nenhum nó escreve nela" e um módulo meu a escreve | pode ignorar: a conferência só vê os nós *Definir variável* da árvore |
| Um nó meu não aparece na conferência | ela só o confere se ele implementa os ganchos da 6.8; sem eles ele vale "pode acontecer" (de propósito, para nunca dar aviso falso) |
| Os ramos recolhidos se abriram sozinhos | o recolher é só da tela e se perde quando o Unity recarrega o asset (recompilar, reabrir o projeto, um Ctrl+Z) |

## 14. Ideias para melhorar (próximos passos)

1. **Teste da árvore sem Play**, com um `Knowledge` simulado (vê um jogador, pista, distância, atenção): uma bancada que mostra no canvas o caminho que a árvore percorreria numa situação, sem montar a cena. A conferência da seção 6 responde "o que o contrato já decide"; esta responderia "o que a árvore faria nesta situação".
2. **Nó "Planejar"** (opcional): uma folha que escolhe, entre os ataques possíveis, o de maior **utilidade** (pontuação por distância, vida do alvo, atenção...) em vez do sorteio por peso, sem virar um planejador GOAP completo.
3. **Estado no save** para características que precisem (recarga de um ataque raro, um "enfureceu" que atravessa a cena).
4. **Mais características de série**: olfato, rastro de calor, armadilhas, invocações, e um modificador "ferido" (mais lento e mais furioso com pouca vida).
5. **Preview de combate**: um quadro no Inspector que mostra, por nível de atenção, o alcance e o dano efetivos de cada ataque com os modificadores aplicados.
6. **Fios que carregam valores** no canvas do perfil (por exemplo, o alcance da Visão alimentando um modificador), se aparecer um caso real em que o contrato não baste.
7. **Mover as caixas à mão e comentários** no canvas da árvore (hoje as posições são sempre calculadas), e agrupar ramos grandes.
8. **Conferir prefabs fora da cena** no *Check All Stalker Trees* (hoje só Definições, perseguidores das cenas abertas e árvores soltas), e rodar a conferência ao entrar no Play ou ao salvar (opcional).
