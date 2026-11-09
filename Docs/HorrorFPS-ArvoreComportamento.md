# Árvore de comportamento editável do perseguidor — asset, janela de edição e nós feitos por você

*Complementa o doc **Perseguidor** (seções 3 e 9). Não muda nada para quem não usa: sem asset de árvore o monstro roda a mesma árvore de antes.*

Até aqui a árvore do perseguidor era montada em código (`StalkerActions` + traços). Agora ela também pode ser um **asset** que você edita numa **janela do Editor**, e **qualquer classe sua** que herde de `StalkerTask` ou `StalkerCondition` vira um nó disponível nos menus, sem mexer em nenhum arquivo do pacote. Isso serve para o caso "um comportamento só deste inimigo": você escreve uma classe de 20 linhas e a coloca na árvore dele.

- **Árvore como asset** (`StalkerTreeAsset`): cada monstro (ou cada `StalkerDefinition`) aponta para a sua. Vários monstros podem dividir o mesmo asset.
- **Janela de edição**: lista indentada, com parâmetros, condições, desfazer/refazer, copiar/colar e **visão ao vivo** (qual nó está rodando agora, quais condições valem, as variáveis do monstro).
- **Nós prontos**: 4 de fluxo, 6 decoradores, 11 ações, 13 condições.
- **Nós seus**: um script de uma classe, criado por um menu já com o esqueleto pronto.

> **Código escrito sem compilar no Unity**, só conferido por checagem estática. A janela usa a API do Editor (seção 9). Se algo não compilar ou se comportar de outro jeito, mande a mensagem do Console.
>
> **O que não é**: não é um canvas de grafo com caixas e setas. É uma árvore em lista indentada (como a Hierarquia). Foi uma escolha de robustez; os dados não dependem da interface, então um canvas pode ser feito depois (seção 12).

## 1. O que vem no pacote

```
Assets/HorrorFPS/Stalker/Tree/
  StalkerNodeBase.cs        base de todo nó: StalkerNode, StalkerComposite, StalkerDecorator, StalkerTask, StalkerCondition,
                            [StalkerNodeMenu], StalkerNodeInfo
  StalkerTreeAsset.cs       o asset (Create > HorrorFPS > Stalker Behavior Tree) + montagem e validação
  StalkerTreeBuild.cs       contexto de montagem de UM monstro + rastro do "ao vivo" (StalkerTreeTrace)
  StalkerBlackboard.cs      variáveis do monstro para os nós conversarem
  StalkerNodesFlow.cs       Seletor, Sequência, Sorteio e os decoradores
  StalkerNodesTask.cs       ações (Ramo padrão, Esperar, Ir até, Encarar...) e Subárvore
  StalkerNodesCondition.cs  as 13 condições
  StalkerTreeTemplates.cs   árvore padrão e o exemplo do rugido
  StalkerNodeExamples.cs    dois nós-modelo: uma ação (Rugir) e uma condição (Perto de casa)
Assets/HorrorFPS/Editor/Stalker/
  StalkerTreeWindow.cs      a janela de edição
  StalkerTreeMenu.cs        menus 24, 25, 26 e Assets > Create > HorrorFPS (script de nó), duplo clique no asset
  StalkerTreeAssetEditor.cs inspetor do asset (resumo, problemas, botão "Abrir")
Arquivos que MUDARAM (sobrescreva): Stalker/StalkerBrain.cs, StalkerDefinition.cs, StalkerModule.cs, StalkerActions.cs
```
Sem `asmdef`, igual ao resto do framework. O que mudou nos quatro arquivos: o `StalkerBrain` e a `StalkerDefinition` ganharam o campo **`Tree`**; o cérebro ganhou `Blackboard`, `TreeTrace`, `Home`, `SetTree()` e `RebuildTree()`; o `StalkerTreeBuilder` ganhou `Get()` e `Names()`; o `StalkerActions` ganhou `Home`. Nada do que existia foi removido ou renomeado.

## 2. Teste em 2 minutos

1. Copie a pasta `Assets` por cima e espere compilar.
2. Selecione o monstro (o da bancada do menu 18 serve) e use **Tools > Horror FPS > 25. Create Stalker Tree (Default)**. Ele cria o asset em `Assets/Resources/HorrorFPS/StalkerTrees`, **liga ao campo `Tree` do monstro selecionado se estiver vazio** e abre a janela. Se o campo já tinha uma árvore, ele avisa no Console e não troca; arraste o asset para lá.
3. A árvore padrão é **exatamente** o que o monstro já fazia: um Seletor com os seis ramos (Attack, Chase, CheckHiding, Investigate, Search, Patrol). Dê Play: o resultado deve ser igual ao de antes.
4. Com o Play ligado, a janela mostra **ao vivo** o que o monstro está fazendo (bolinhas verdes, azuis, vermelhas, cinzas). Fique à vista dele e olhe a janela.
5. Experimente o **26. Create Stalker Tree (Example: Roar On Spot)**: ele para, encara e manda a mensagem `OnRoar` ao avistar você, uma vez por perseguição. Ponha no monstro um componente com um método `void OnRoar()` para tocar o som (sem ele a mensagem é ignorada, e o resto funciona).

Duplo clique em qualquer `StalkerTreeAsset` abre a janela. Para a árvore vazia: **Create > HorrorFPS > Stalker Behavior Tree** (aí use o botão "Árvore padrão" da janela para ter um ponto de partida).

## 3. Como funciona

**O asset guarda DADOS; cada monstro monta a SUA cópia.** Ao acordar (`Awake`), o `StalkerBrain` percorre o asset e cria, para cada nó, uma cópia de execução (`MemberwiseClone`). Por isso:

- o **estado** de um nó (tempo restante, "já rugiu"...) é só daquele monstro, mesmo com vários dividindo o asset;
- os **parâmetros** (campos públicos) são do asset: editar muda todos os monstros que o usam (duplique o asset para diferenciá-los);
- campos de **referência** de estado (uma `List`, um array) têm de ser criados em `OnBuild()`, porque a cópia é rasa e eles seriam divididos entre os monstros.

**Qual árvore o monstro usa:** o campo `Tree` do próprio `StalkerBrain`; se vazio, o `Tree` da `StalkerDefinition`; se vazio, **a árvore do código** (igual a antes). Asset vazio, ou com tudo desligado, também cai na árvore do código, com um aviso no Console.

**Condições.** Qualquer nó pode ter uma lista de condições. O nó só roda enquanto **todas** valerem (na ordem; a primeira que falhar derruba o nó). Se uma condição cair com o nó rodando, ele é **interrompido** (`Abort`) e devolve Falha. Cada condição tem o botão **NÃO** que inverte o resultado.

> **Condições são reativas: valem a cada quadro, não só na hora de começar.** Para uma condição que só importa para *começar* algo (ex.: "só investe se estiver a mais de 12 m"), ponha-a no **primeiro filho de uma Sequência**: a Sequência continua do filho em que estava e não reavalia os que já passaram. Condições no próprio nó da Sequência serviriam para interrompê-la assim que deixassem de valer, o que às vezes também é o que você quer (receita 2).

**Fluxo.** O **Seletor** é reativo: a cada quadro tenta os filhos de cima para baixo e roda o primeiro que não falhar; um filho de cima que passa a valer interrompe o que rodava embaixo. É assim que "perseguir interrompe investigar". A **Sequência** tem memória (continua do filho em que estava). Os nós `Success`/`Failure`/`Running` são os mesmos de `StalkerBT.cs`.

**O nó "Ramo padrão"** é a ponte com o resto do sistema: ele pega do estoque (`StalkerTreeBuilder`) o ramo de série **ou o que um traço (módulo) acrescentou ou trocou** em `BuildBranches`. Cada ramo de série já traz a própria condição (Attack só quando vê e está perto, etc.). Por isso a árvore padrão é só um Seletor com seis "Ramo padrão".

> **Atenção com módulos que criam ramos.** Com uma árvore em asset, um ramo que um módulo criou (ex.: `Roar` no exemplo da seção 9 do doc *Perseguidor*) só roda se a árvore tiver um nó **Ramo padrão** do tipo **Custom** com o mesmo nome. Se faltar, o Console avisa ("O ramo 'X' (criado por um módulo) não está na árvore"). Os traços do pacote (Finisher e Sprint) não criam ramos, então não são afetados, e os ganchos de módulo (`ModifyTuning`, `DecideAttack`, `FilterNoise`...) não dependem da árvore e continuam funcionando.

**Variáveis (`Blackboard`).** Cada monstro tem variáveis numéricas por nome (`1` = ligado, `0` = desligado) para os nós conversarem: o nó **Definir variável** liga `rugiu`, a condição **Variável** pergunta por ela. Também dá para mexer por código: `brain.Blackboard.SetBool("furioso", true)` (um traço ou um gatilho seu, por exemplo, e a árvore reage). Zeram quando o monstro é reativado; **não vão para o save** e não atravessam a troca de cena (para isso, `StalkerModule.SaveState`).

**Em jogo:** `brain.SetTree(outroAsset)` troca a árvore (ex.: fase 2 de um chefe); `brain.RebuildTree()` remonta a atual (zera o estado interno dos nós; a memória adaptativa e o conhecimento ficam).

## 4. A janela (Tools > Horror FPS > 24. Stalker Tree Editor)

```
[asset] [Novo] [Árvore padrão] [Validar] [Expandir] [Recolher]        (Play: [monstro] [Aplicar ao editar] [Aplicar agora]) [Travar]
 ┌ a árvore ───────────────────────────────────────┐ ┌ o nó selecionado ────────────────┐
 │ ▾ ▌● Prioridades do perseguidor      +  ×  ···  │ │ Nome / Desligado                 │
 │     ▌● Ramo padrão  Attack        ▲ ▼  ×  ···   │ │ Condições  [+ Condição]          │
 │     ▌● Sequência  se Vê um jogador   + ▲ ▼ × ···│ │   ● Vê um jogador   NÃO ▲ ▼ ×    │
 │        ▌● Parar                                 │ │ Parâmetros                       │
 └─────────────────────────────────────────────────┘ └──────────────────────────────────┘
 (problemas encontrados na árvore)
```
- **Barra:** escolher o asset; **Novo** (cria uma árvore padrão); **Árvore padrão** (troca TODA a árvore deste asset pela padrão, com confirmação); **Validar**; **Expandir/Recolher** tudo; **Travar** (a janela deixa de seguir a seleção do Editor).
- **Seguir a seleção:** selecionar um asset de árvore, ou um monstro da cena que tenha uma, troca a árvore mostrada (se a janela não estiver travada).
- **Cada linha:** a barra colorida diz o tipo (azul = composto, roxo = decorador, verde = ação), a bolinha é o estado ao vivo, o texto traz o nome em negrito, os valores em cinza e `se ...` em laranja com as condições. `(vazio)` em vermelho = composto/decorador sem filho (é ignorado). Botões: **+** adicionar filho, **▲ ▼** mudar a ordem, **×** remover, **···** menu completo (também no botão direito). A tecla **Delete** remove o nó selecionado.
- **Menu do nó:** Adicionar filho · Adicionar condição · Inserir antes/depois · **Envolver com** (põe o nó dentro de um Seletor, Sequência ou decorador) · Desembrulhar (tira o decorador e mantém o filho; as condições do decorador se perdem) · Subir/Descer · Mover para dentro do irmão de cima / para fora · Duplicar · Copiar/Colar (funciona entre assets) · Desligar/Ligar · Remover. Tudo vai para o **Ctrl+Z**.
- **Painel direito:** nome (opcional, aparece na árvore), **Desligado** (o nó e tudo abaixo dele somem da árvore de execução, bom para testar), condições e os parâmetros do tipo. O texto cinza no topo é a descrição do nó.
- **Menus de tipos:** listam **todo tipo** que herda de `StalkerNode` / `StalkerCondition` no projeto, agrupados pelo caminho do `[StalkerNodeMenu]`. Classe sem `[Serializable]`, ou sem construtor sem parâmetros, **não aparece** e o Console explica (uma vez).
- **Problemas** (barra amarela embaixo, e botão Validar): composto sem filhos, condição ou nó de tipo apagado, ramo Custom sem nome, subárvore sem asset, e o mais traiçoeiro: *um filho que nunca falha e não tem condição trava os de baixo num Seletor*.

**Ao vivo (Play).** Escolha o monstro no canto da barra (só aparecem os que usam este asset). Bolinha **verde** = rodando, **azul** = terminou bem agora, **vermelha** = falhou agora, **cinza** = não foi consultado. As condições têm a bolinha delas (verde = valeu, vermelha = não). Abaixo do painel direito aparecem modo, atenção, se vê alguém, a pista e as **variáveis** do monstro. Editar em Play: com **Aplicar ao editar** ligado, cada alteração remonta a árvore dos monstros que usam o asset (zera o estado interno dos nós, o que pode reiniciar um "Esperar" no meio); desligado, use **Aplicar agora**. As alterações em Play **ficam** no asset (é um asset, não uma cena): desfaça se não quiser.

**Inspetor do asset:** mostra as anotações (`Notes`), o número de nós, um resumo de uma linha da árvore, os problemas, e o botão que abre a janela.

## 5. Os nós prontos

Os nomes são os do menu "Adicionar". Parâmetros entre parênteses (valor padrão).

**Fluxo e decoradores**

| Nó | O que faz |
|---|---|
| Seletor (prioridade) | tenta os filhos de cima para baixo todo quadro; roda o primeiro que não falha; reativo (interrompe o de baixo) |
| Sequência | um filho depois do outro; falha se um falhar; tem memória |
| Sorteio | tenta os filhos em ordem aleatória e fica com o primeiro que não falha, até ele acabar |
| Subárvore | roda aqui dentro outro `StalkerTreeAsset` (reaproveitar uma ronda, uma fuga). Uma subárvore que contém a si mesma é ignorada com aviso |
| Inverter | sucesso vira falha e falha vira sucesso |
| Sempre sucesso | quando o filho termina, devolve sucesso. Passo opcional dentro de uma Sequência |
| Sempre falha | quando o filho termina, devolve falha. **Efeito colateral dentro de um Seletor sem parar o Seletor** (ele segue para o próximo irmão) |
| Recarga (10 s; também ao ser interrompido: sim) | depois que o filho termina bem, falha depois de começar, ou é interrompido, falha por N s antes de poder rodar de novo. Falhar de cara (nem chegou a começar) não gasta a recarga |
| Limite de tempo (10 s) | passou de N s rodando: interrompe o filho e falha. Ponha uma Recarga em volta, senão o filho recomeça no quadro seguinte |
| Repetir (0 = sempre; parar na falha: sim) | roda o filho de novo e de novo |

**Ações**

| Nó | O que faz |
|---|---|
| Ramo padrão (Chase) | usa um ramo do estoque: Attack, Chase, CheckHiding, Investigate, Search, Patrol, ou **Custom** (nome de um ramo de módulo) |
| Esperar (1 s; parar de andar: sim) | espera e termina bem |
| Parar | para o monstro e termina bem |
| Ir até (pista; andar; chegada 1,2 m; desistir em 25 s) | anda até a pista, o último ponto visto ou a casa dele; falha se não houver destino ou estourar o tempo. Velocidade Walk/Run/Custom |
| Encarar (alvo visto; 240 °/s; 1 s) | gira para onde vê alguém ou para a pista; 0 s = termina quando virou. Falha se não houver para onde olhar |
| Definir modo (Chase) | muda o modo mostrado (música, animação e traços que escutam o modo reagem). Os ramos padrão já definem o próprio |
| Aumentar atenção (0,3; soma) | soma à atenção, ou com **Force** coloca exatamente nesse valor |
| Definir variável | troca ou soma (**Add**) uma variável do monstro |
| Animator: gatilho | dispara um `Trigger` do `Animator` do monstro (procura nos filhos); avisa uma vez se não houver |
| Enviar mensagem (OnRoar) | `SendMessage` no monstro, sem exigir receptor; argumento de texto opcional |
| Log (teste) | escreve no Console; para descobrir se a árvore chegou ali |

**Condições** (todas aceitam **NÃO**; "comparar" = `>=`, `<=` ou `=`)

| Condição | Vale quando |
|---|---|
| Vê um jogador | o monstro enxerga alguém agora |
| Tem pista (só nova: sim) | existe pista (e, se marcado, ainda não checada) |
| Suspeita de esconderijo | a pista aponta para um esconderijo |
| Confiança da pista (>= 0,5) | compara a confiança (0 a 1, cai com o tempo); falsa sem pista |
| Origem da pista (Heard) | a pista veio de `Seen`, `Glimpse`, `Heard`, `DirectorPressure`, `DirectorHiding`, `Ping` ou `Shared` |
| Nível de atenção (>= Alert) | compara Calm < Alert < Hunting < Frenzy |
| Tempo sem estímulo (20 s) | passaram N s sem ouvir, ver ou receber nada (tédio) |
| Modo atual | o modo mostrado é este |
| Distância do alvo visto (<= 8 m) | compara a distância até quem ele vê; falsa se não vê ninguém |
| Sanidade do alvo visto (<= 0,4) | compara a sanidade (0 louco, 1 lúcido) de quem ele vê |
| Experiência (>= 0,5) | compara a experiência acumulada (adaptação, vai para o save): libera truques ao monstro veterano |
| Variável | compara uma variável do monstro (nunca definida vale 0) |
| Chance (30 %, mantida por 5 s) | sorteia e **mantém** o resultado por N s (senão piscaria a cada quadro) |

## 6. Criar um nó seu

Este é o ponto principal para "funções específicas de cada inimigo".

**Passo a passo**

1. No Project: **Assets > Create > HorrorFPS > Stalker Node Script (Action)** (ou **(Condition)**). Digite o nome: ele vira o nome da classe e do arquivo. O script já nasce com o esqueleto e comentários.
2. Escreva a lógica (exemplos abaixo). Espere compilar.
3. Na janela da árvore, o nó aparece em **Adicionar** (ou **Adicionar condição**) no caminho que você deu em `[StalkerNodeMenu("Meu Monstro/Nome", "descrição")]`. Sem o atributo ele fica em "Personalizados/".

**Uma ação com estado e parâmetros** (é o `StalkerRoarExample` do pacote, que serve de modelo):

```csharp
using System;
using UnityEngine;
using HorrorFPS;

[Serializable]                                    // OBRIGATÓRIO: sem isto o nó não aparece no menu
[StalkerNodeMenu("Meu Monstro/Rugir", "Para, manda a mensagem, encara e espera.")]
public class MeuRugido : StalkerTask
{
    public string message = "OnRoar";             // campo PÚBLICO = parâmetro editável na janela
    public float seconds = 1.6f;

    float left;                                   // campo PRIVADO = estado DESTE monstro

    public override string Summary() { return seconds.ToString("0.##") + " s"; }   // texto curto na árvore (não use Brain aqui)
    public override bool CanFail { get { return false; } }                          // diz à validação que ele nunca falha

    protected override void OnEnter()             // começou a rodar
    {
        left = seconds;
        Brain.Stop();                             // Brain = o monstro dono desta cópia
        Brain.gameObject.SendMessage(message, SendMessageOptions.DontRequireReceiver);
    }

    protected override BTStatus OnTick()          // todo quadro. Running = continua; Success/Failure = terminou
    {
        if (Brain.Knowledge.hasVisual) Brain.FaceToward(Brain.Knowledge.visualPos, 240f);
        left -= Time.deltaTime;
        return left > 0f ? BTStatus.Running : BTStatus.Success;
    }

    protected override void OnAbort() { left = 0f; }   // um nó de prioridade maior interrompeu; OnExit(resultado) também existe
}
```

**Uma condição:**

```csharp
[Serializable]
[StalkerNodeMenu("Meu Monstro/Perto de casa", "Vale a menos de N metros de onde nasceu.")]
public class PertoDeCasa : StalkerCondition
{
    public float meters = 12f;
    public override string Summary() { return "< " + meters.ToString("0.#") + " m"; }
    protected override bool Check() { return (Brain.transform.position - Brain.Home).sqrMagnitude < meters * meters; }
}
```

**Um decorador seu** (um nó que altera o jeito de o filho rodar): herde de `StalkerDecorator`, implemente `Wrap()` e devolva um `BTNode` de execução. Duas classes no mesmo arquivo:

```csharp
[Serializable]
[StalkerNodeMenu("Meu Monstro/Só uma vez", "Roda o filho até ele terminar uma vez; depois falha para sempre.")]
public class SoUmaVez : StalkerDecorator
{
    protected override BTNode Wrap(BTNode inner) { return new SoUmaVezBT(DisplayName(), inner); }
}

public sealed class SoUmaVezBT : BTNode            // o nó de execução (não é serializado; cada monstro tem o seu)
{
    readonly BTNode child;
    bool running, done;
    public SoUmaVezBT(string n, BTNode c) { name = n; child = c; }
    public override BTStatus Tick()
    {
        if (done) return BTStatus.Failure;
        BTStatus s = child.Tick();
        running = s == BTStatus.Running;
        if (!running) done = true;
        return s;
    }
    public override void Abort() { if (running) child.Abort(); running = false; }
}
```
Para um **composto** novo, herde de `StalkerComposite` e implemente `Create()` usando `BuildChildren(ctx, path)`. Para qualquer coisa fora do comum, herde direto de `StalkerNode` e implemente `Create()`.

**Regras de ouro**

1. `[Serializable]` em **cada** classe de nó (a herança não passa o atributo) e um construtor sem parâmetros (ou nenhum).
2. **Público = parâmetro, privado = estado.** Listas e arrays de estado: crie em `OnBuild()` (a cópia é rasa).
3. **A árvore não lê os jogadores.** Responda com o que o monstro sabe: `Brain.Knowledge` (`hasVisual`, `visualPos`, `focus`, `hasLead`, `leadPos`, `leadConfidence`...), `Brain.Mode`, `Level`, `Attention01`, `Skill`, `Home`, `Blackboard`. Mover e olhar: `MoveTo(destino, velocidade)`, `Stop()`, `FaceToward(ponto, graus/s)`, `Arrived(dist)`. O resto do cérebro está na seção 9 do doc *Perseguidor*.
4. **Falha de verdade:** devolva `Failure` quando o nó não pôde fazer o que devia (sem destino, sem alvo). Um nó que **nunca** falha e não tem condição trava os irmãos de baixo num Seletor; marque `CanFail => false` para a validação avisar, e ponha uma condição nele.
5. **Efeito colateral sem parar o Seletor:** embrulhe o nó em **Sempre falha**.
6. Uma exceção no seu `OnTick`/`Check` não derruba o monstro: o nó devolve Falha (a condição, falso) e o erro aparece **uma vez** no Console.
7. `Summary()` e `Validate()` rodam também no asset (sem monstro): não use `Brain` neles.
8. **Renomear ou apagar a classe de um nó faz ele sumir** das árvores que o usam (aparece "nó ausente" em vermelho). Ao renomear, use `[UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceClassName: "NomeAntigo")]` na classe nova.
9. Build IL2CPP com *Managed Stripping Level* alto pode remover classes que só aparecem em assets: se um nó sumir só na build, ponha `[UnityEngine.Scripting.Preserve]` na classe.

## 7. Receitas

Todas as receitas abaixo foram conferidas numa simulação em Python da lógica dos nós (Seletor reativo, Sequência com memória, Recarga, Sempre falha), não no Unity.

**1. Investida a distância com recarga** (um poder a cada 30 s, só longe e com atenção alta). As condições vão no **primeiro filho** da Sequência: só valem para começar; a distância cai abaixo de 12 m durante a investida e isso não a interrompe:
```
Seletor
├─ Ramo padrão: Attack
├─ Recarga 30 s
│    └─ Sequência "Investida"
│         ├─ Parar              se Vê um jogador, Distância >= 12 m, Atenção >= Hunting
│         ├─ Encarar (alvo visto, 0,8 s)
│         ├─ Enviar mensagem "OnCharge"
│         └─ Ir até (alvo visto, Run, desistir em 3 s)
├─ Ramo padrão: Chase
└─ (demais ramos)
```
Se `Attack` assumir no meio, a investida é interrompida e a recarga começa (opção "também ao ser interrompido").

**2. Desistir da caça** (sem notícia dos jogadores há 40 s, volta para perto de casa). Aqui as condições ficam no **próprio nó**, de propósito: qualquer ruído ou visão interrompe a volta:
```
...
├─ Sequência "Desistir"    se NÃO Vê um jogador, Tempo sem estímulo >= 40 s, NÃO Perto de casa (12 m, nó-modelo)
│    ├─ Definir modo: Patrol
│    └─ Ir até (casa, andar)
├─ Ramo padrão: Search
```
Ao chegar a menos de 12 m de casa a condição "NÃO Perto de casa" cai e a árvore segue para os ramos de baixo.

**3. Rugidos variados** (o monstro ruge ao avistar, de um jeito sorteado; é o exemplo do menu 26 com um Sorteio). O `Definir variável` é sempre o **último** passo, porque a condição `rugiu <= 0` do Sorteio deixa de valer assim que ele roda:
```
├─ Sorteio "Reação ao avistar"    se Vê um jogador, Variável rugiu <= 0
│    ├─ Sequência: Parar, Animator "Roar1", Esperar 2 s, Definir variável rugiu = 1
│    └─ Sequência: Parar, Animator "Roar2", Esperar 1 s, Definir variável rugiu = 1
├─ Ramo padrão: Chase
├─ Sempre falha "Rearmar"         se NÃO Vê um jogador, Variável rugiu >= 1
│    └─ Definir variável rugiu = 0
```
**4. Fase de chefe:** um traço ou gatilho seu faz `brain.Blackboard.SetFloat("fase", 2)`; a árvore tem um filho `se Variável fase >= 2` com uma **Subárvore** da fase 2 antes do resto.

## 8. Módulos ou árvore?

| Preciso de... | Use |
|---|---|
| mudar números (velocidade, visão, audição), o que ele ouve, o golpe, a finalização, salvar estado | **módulo** (`StalkerModule`, seção 9 do doc *Perseguidor*) |
| decidir **quando** e **em que ordem** o monstro faz as coisas, sequências, recargas, condições que o designer ajusta sem código | **árvore** (esta) |
| um comportamento complexo todo em código | um **ramo** num módulo (`BuildBranches`) + um nó **Ramo padrão (Custom)** na árvore |
| uma ação nova usada em vários monstros | um **nó** seu (seção 6) |

Os dois convivem: a árvore decide, o módulo mexe nos números e nos sentidos.

## 9. Premissas de API do Unity (código não compilado)

| Usado em | Premissa |
|---|---|
| os assets e nós | `[SerializeReference]` com listas e campos de tipo abstrato (Unity 2019.3 ou mais novo); cada tipo concreto `[Serializable]` com construtor sem parâmetros |
| a janela | `SerializedObject`/`SerializedProperty` com caminhos como `root.children.Array.data[2].child`; `TypeCache.GetTypesDerivedFrom`; `GenericMenu`; `Undo.RecordObject` sobre `SerializeReference`; `FindObjectsByType` (2021.3.18+/2022.2.5+/Unity 6) |
| duplo clique | `[OnOpenAsset]` com `EditorUtility.InstanceIDToObject` (em versões novas dá só aviso de obsoleto) |
| criar script de nó | `ProjectWindowUtil.CreateScriptAssetFromTemplateFile` (grava um modelo temporário em `Library/`) |
| copiar/colar/duplicar | `JsonUtility` só para os campos simples; filhos e condições são recriados à mão |
| ao vivo | só em Editor e Development Build (`Application.isEditor || Debug.isDebugBuild`); numa build final a árvore roda sem rastro |

## 10. Limitações

- **Não é um canvas de grafo.** Lista indentada; sem arrastar nós para reordenar (use ▲ ▼ e "Mover para dentro/fora").
- **Estado interno não sobrevive** a `RebuildTree`, a desativar/reativar o monstro nem à troca de cena; a memória adaptativa e o conhecimento sim. Variáveis do `Blackboard` não vão para o save.
- **Editar o asset muda todos os monstros que o usam.** Para um monstro diferente, duplique o asset.
- Um ramo do estoque usado **duas vezes** na mesma árvore divide o estado (aviso no Console); use cada "Ramo padrão" uma vez por árvore.
- **Montagem e desempenho:** a árvore é montada uma vez por monstro. Cada nó com condições custa uma avaliação por quadro enquanto é consultado; a visão ao vivo só existe no Editor/Development Build.
- **Renomear a classe de um nó** o perde (regra 8 da seção 6).
- A janela usa `Undo.RecordObject` sobre o asset; um desfazer troca os objetos do asset por novos, então a **seleção** e os nós recolhidos podem se perder (a janela se recompõe sozinha).

## 11. Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Meu nó não aparece no menu | falta `[Serializable]` na classe, ou o construtor exige parâmetros (o Console avisa uma vez); ou o projeto não compilou |
| "nó ausente" em vermelho | a classe foi renomeada ou apagada; use o × para limpar, ou `MovedFrom` ao renomear |
| Mudei o asset em Play e nada mudou | "Aplicar ao editar" desligado (clique em "Aplicar agora"); ou o monstro usa outra árvore (o `Tree` do `StalkerBrain` ganha do da `StalkerDefinition`) |
| A janela fica toda cinza em Play | nenhum monstro usa este asset; ou é uma build sem Development |
| Aviso "O ramo 'X' (criado por um módulo) não está na árvore" | falta um nó **Ramo padrão (Custom)** com esse nome |
| Aviso "A árvore não gerou comportamento nenhum" | árvore vazia ou toda desligada: o monstro usa a do código |
| O Seletor nunca passa para os de baixo | um filho de cima nunca falha e não tem condição (a validação avisa) |
| O monstro "pisca" entre dois comportamentos | uma condição oscila a cada quadro; use **Chance** (que mantém o sorteio), uma **Recarga** ou limites diferentes para entrar e sair |
| O estado de um monstro aparece em outro | uma `List`/array de estado foi criada no campo em vez de em `OnBuild()` |
| "contém a si mesma" | uma Subárvore aponta (direta ou indiretamente) para o próprio asset |
| Erro `[Stalker] Erro no nó ...` | exceção no seu `OnEnter/OnTick/OnExit`; leia o stack trace. O monstro continua |

## 12. Ideias para melhorar (próximos passos)

1. **Canvas de grafo** (`GraphView`) sobre os mesmos assets: caixas e setas, arrastar e soltar. Os dados já são independentes da interface.
2. **Teste sem Play**: uma bancada que simula `Knowledge` (vê, pista, distância) e mostra o caminho que a árvore percorreria.
3. **Tempo por nó** na visão ao vivo (quanto cada ramo rodou), para ajustar o ritmo do monstro.
4. **Comentários** (notas soltas) e agrupamento de nós para árvores grandes.
5. **Variáveis no save** (opcional por variável), para fases de chefe que atravessam a sessão.
6. **Nós de som e música** (cruzando com o áudio do monstro), e um nó "Esperar até" por condição.
