# Ganchos de cena: assets que disparam, componentes que ligam à cena

*Complementa **Diálogos** (ação `Event`, `DialogueEventRelay`) e **Árvore de comportamento** (nós de ação).*

> **Código conferido sem abrir a Unity**: checagem de sintaxe e uma compilação dos arquivos novos com o `mcs` contra versões simplificadas das classes da Unity e do framework. Abra a Unity, espere compilar e teste antes de confiar. Se algo não compilar, mande a mensagem do Console.

## 1. A ideia

Um `ScriptableObject` (diálogo, árvore do perseguidor, perfil) é um **asset**: vive fora das cenas e **não pode guardar referência a objetos de uma cena**. Nem uma cópia dele resolve: a cópia salva como asset perde as referências, e a cópia feita em jogo (`Instantiate`) não é editável no Inspector.

Por isso o padrão do projeto é separar em duas camadas:

| Camada | Onde fica | O que guarda |
|---|---|---|
| **Asset** (o que acontece) | `DialogueGraph`, `StalkerTreeAsset` | só um **id de evento** ("abrir_porta", "rugido") |
| **Gancho de cena** (com o que acontece) | componente no NPC, no monstro ou num objeto da cena | para cada id, um `UnityEvent` com objetos **desta** cena |

O mesmo asset serve a várias cenas e a vários NPCs ou monstros. Cada um liga o id a coisas diferentes.

## 2. Diálogo: `DialogueEventRelay`

### Escopo

| Escopo | Quem aciona as entradas |
|---|---|
| **Any Conversation** (padrão dos relés que já existiam) | qualquer conversa que dispare o id, e `DialogueEvents.Raise(id)` por código |
| **This Object** | só conversas começadas pelo **dono** (`DialogueInteractable`, `DialogueTrigger` ou `DialogueStep` nele ou nos filhos) ou cujo ator dono (`DialogueActor`) está nele |

Use **This Object** quando o mesmo diálogo é usado por mais de um NPC: o "abrir_porta" do zelador do 1º andar abre a porta dele, e o do 2º andar abre a outra. O campo **Owner** troca o dono (vazio = o próprio objeto).

A conversa seguinte de uma ação **Começar diálogo** continua pertencendo ao mesmo dono, e um **Salto** para outro grafo também. Um `Raise(id)` feito por código sem conversa só chega aos relés **Any Conversation**.

### Preenchimento automático

- **Adicionar o componente** num objeto que já tem diálogo cria uma entrada para cada id de evento que esse diálogo usa, e já escolhe **This Object**.
- O Inspector mostra os ids **usados** pelos diálogos do objeto (e pelos de **Graphs**, a lista extra), avisa quais **ainda não têm ligação** e tem o botão **Criar entradas para os ids que faltam**.
- Entradas com um id que nenhum diálogo listado usa aparecem num aviso (normalmente erro de digitação).
- A busca entra nos grafos ligados por **Salto** e **Começar diálogo**.

Relés antigos continuam como estavam (**Any Conversation**); nada muda nas cenas já montadas.

### Por código

`DialogueOptions.source` (novo) diz quem começou a conversa; `DialogueSession.Source` lê. Quem chama `DialogueManager.Play` por código e quer que os relés **This Object** de um objeto ouçam deve preencher `source` com um componente desse objeto. `DialogueGraph.CollectEventIds(lista)` devolve os ids de um grafo.

## 3. Perseguidor: `StalkerSceneHooks`

### Nó "Ações/Evento de cena"

Na árvore (`StalkerTreeAsset`), o nó **Evento de cena** tem `eventId` e `argument`. Ao rodar, entrega o id aos `StalkerSceneHooks` que atendem aquele monstro e termina bem na hora. Sem ninguém ligado ao id, não acontece nada.

Exemplo: na árvore, antes de "Ramo Chase", um nó *Evento de cena "comecou_perseguicao"*; na cena do porão, o gancho apaga as luzes; na cena do corredor, o mesmo id tranca as portas.

### O componente

`Add Component > HorrorFPS > Stalker > Stalker Scene Hooks`:

- **Brain**: o perseguidor atendido. Vazio = o `StalkerBrain` do objeto (ou do pai).
- **Stalker Id**: para um gancho **fora do monstro**, atende o perseguidor com esse id assim que ele entra em cena. É o jeito certo para monstros que nascem por `StalkerSpawner` ou chegam de outra cena: o prefab não pode apontar para a cena, então o gancho fica na cena e encontra o monstro pelo id.
- **Trees**: árvores extras para a lista de ids (a do Brain entra sozinha). Útil com Stalker Id, quando o monstro ainda não existe no Editor.
- **Entries**: id → `onRaised` e `onRaisedWithArgument`.
- **On Spotted**: notou um jogador.
- **Modes**: entrou/saiu de um modo (Patrol, Investigate, Search, CheckHiding, Chase, Attack).
- **Attention**: a atenção chegou a um nível vindo de baixo (`onReached`) ou caiu para baixo dele (`onDropped`).

O Inspector tem o mesmo quadro de ids do relé de diálogo (usados, faltando, desconhecidos e o botão de criar). Ao adicionar o componente no monstro, as entradas da árvore já são criadas.

Morte e dano não estão aqui de propósito: use `StatsComponent.onDied`/`onDamaged` e `HurtboxOwner` (ver **Hurtbox**).

### Por código

`StalkerSceneHooks.Raise(cerebro, id, argumento)` dispara como o nó; `StalkerSceneHooks.AnyRaised` recebe todos os eventos de todos os monstros; `StalkerSceneHooks.CollectEventIds(arvore, lista)` lista os ids de uma árvore (com subárvores).

## 4. Arquivos

```
Dialogue/DialogueEventRelay.cs           escopo, dono, lista de grafos, preenchimento (alterado)
Dialogue/DialogueContext.cs              DialogueOptions.source, DialogueSession.Source (alterado)
Dialogue/DialogueGraph.cs                CollectEventIds (alterado)
Dialogue/DialogueInteractable.cs,
  DialogueTrigger.cs, DialogueStep.cs,
  DialogueAction.cs                      preenchem source (alterados)
Stalker/StalkerSceneHooks.cs             componente novo
Stalker/Tree/StalkerNodesTask.cs         nó StalkerSceneEventNode (alterado)
Editor/SceneHooksEditorUtil.cs           quadro de ids comum aos dois Inspectors
Editor/Dialogue/DialogueEventRelayEditor.cs
Editor/Stalker/StalkerSceneHooksEditor.cs
```

## 5. Teste rápido

1. **Diálogo:** num NPC com `DialogueInteractable` cujo diálogo tem uma ação `Event` "teste", adicione `Dialogue Event Relay`. A entrada "teste" já aparece; ligue `onRaised` a `GameObject.SetActive` de um cubo. Duplique o NPC, troque o cubo no segundo e confira que cada NPC mexe só no seu.
2. **Perseguidor:** na árvore do monstro, coloque *Evento de cena "teste"* no início do ramo de perseguição. No monstro, adicione `Stalker Scene Hooks`; a entrada "teste" já aparece. Ligue a uma luz e deixe o monstro ver você.
