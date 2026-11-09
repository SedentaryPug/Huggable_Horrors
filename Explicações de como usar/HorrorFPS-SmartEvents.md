# Smart Events (Unity) — evento customizado com condições, limite de ativações e save integrado

Um "UnityEvent turbinado" para o Horror FPS Framework. Tudo se configura no Inspector: **limite de ativações**, **cooldown**, **delay**, **lista de condições** (E/OU, com NOT por condição),
**variáveis a mexer ao disparar** e três UnityEvents (*Ao disparar*, *Ao ser bloqueado*, *Ao restaurar*). A contagem de ativações vai para o **save do jogo (`GameSession`)**, então o evento não se repete depois de carregar.

## Instalação

Copie a pasta `SmartEvents` para `Assets/HorrorFPS/` (o `Editor/` já vem separado; o código de editor também tem `#if UNITY_EDITOR`). Namespace `SmartEvents`, sem `.asmdef`.
Unity 2019.3+ (usa `[SerializeReference]`). Nenhuma cena ou prefab é necessário.

## Arquivos

| Arquivo | Para quê |
|---|---|
| `SmartEvent.cs` | A classe `[Serializable] SmartEvent` (use como campo em qualquer script, como um `UnityEvent`) + `SmartEventContext`, enums e `VariableChange`. |
| `SmartCondition.cs` | Base `SmartCondition` (herde dela para criar condições), atributo `[ConditionMenu]`, `CompareOp`. |
| `BuiltInConditions.cs` | Condições prontas que não dependem do framework. |
| `HorrorFPSConditions.cs` | Condições que dependem do framework (`PlayerContext`). |
| `SmartEventSave.cs` | Ponte para o `GameSession` (contagem por evento, variáveis do jogo). |
| `SmartEventTrigger.cs` | Componente: tenta o evento no Start / OnEnable / entrar no trigger / colisão, ou por `Trigger()`. Gera o ID sozinho. |
| `SmartEventInteractable.cs` | `Interactable`: o botão de interagir do jogador roda o evento do `SmartEventTrigger` do mesmo objeto. |
| `SmartEventRunner.cs` | Objeto oculto criado sob demanda para o delay (você nunca coloca em cena). |
| `Editor/SmartEventEditor.cs` | Inspector do `SmartEvent`: menu "+ Adicionar" que acha sozinho toda condição, reordenar, remover, NOT. |

Só `SmartEventSave.cs`, `SmartEventInteractable.cs` e `HorrorFPSConditions.cs` conhecem o framework (veja *Premissas*).

## O que o evento tem (Inspector)

| Campo | O que faz |
|---|---|
| **ID no save** | Chave do evento no save (`smartevent.<ID>`). "Novo ID" gera um GUID; também pode ser texto legível (`boss_morto`) — faça isso nos eventos que outras condições consultam. |
| **Limite de ativações** | *Ilimitado*, *Uma única vez* ou *Limitado* (máx. N). |
| **Cooldown (s)** | Tempo mínimo entre duas ativações (tempo de jogo: a pausa congela). |
| **Delay (s)** | Espera antes de *Ao disparar*. A ativação já é gravada na hora. |
| **Salvar no save** | Liga a gravação da contagem no `GameSession`. Sem ID não grava (aviso no Inspector e no Console). |
| **Condições** | Lista com **+ Adicionar**; *Todas (E)* ou *Qualquer (OU)*; botão **NOT** por condição; ↑ ↓ × para ordenar/remover. |
| **Variáveis ao disparar** | Liga/desliga uma flag ou soma num contador do `GameSession` quando o evento dispara. |
| **Ao disparar / Ao ser bloqueado / Ao restaurar** | UnityEvents. *Bloqueado* = limite atingido, em cooldown ou condições falsas. |

Ordem das checagens: **limite → cooldown → condições**. Passando, a ativação é **gravada primeiro**, depois vêm as variáveis, o delay e *Ao disparar* (se algo falhar no meio, o evento não se repete).

## Save

- Chave da contagem: `smartevent.<ID>` (inteiro no `GameSession`, mesmo padrão de `dlgtrigger.<id>` e `stalker_trigger_<id>`).
- Entra no slot quando o jogo salva (SavePoint, Checkpoint, troca de nível, pausa), **volta ao carregar** e **zera em Novo jogo** — igual a todo o resto da partida. O que aconteceu depois do último save se perde junto com o resto.
- *Variáveis ao disparar* e as condições `Jogo/Variável…` usam as **mesmas variáveis** de `Pickup`, `Door.unlockVariable`, `LevelDoor.requiredVariable` e `SetVariableStep`.
- **Ao restaurar**: o `SmartEventTrigger` chama `Restore()` no Start; se a contagem no save é > 0, roda *Ao restaurar*. Serve para reaplicar o estado final (item sumiu, luz apagada). Para estado de objetos o padrão do jogo continua sendo
  `SaveableEntity` + `SaveableActive`/`SaveableTransform`; use *Ao restaurar* para o que não tem isso. Roda a cada vez que a cena abre com o evento já disparado (inclusive voltar a um nível visitado).
- **IDs**: o `SmartEventTrigger` gera um GUID ao ser adicionado e gera outro se o objeto for duplicado (só IDs em formato GUID são renovados; IDs digitados nunca são alterados). Prefabs ficam sem ID e **cada instância na cena gera o seu**.
  Objetos instanciados em tempo de execução dividem o ID do prefab: use `evento.SetId("...")` se precisarem de ID próprio.
- Evento com **Salvar no save desligado** conta só na sessão do objeto (recomeça ao recarregar a cena).

## Componentes

**`SmartEventTrigger`** (*Add Component > Smart Events > Smart Event Trigger*) — `Activate On` (marque vários): `Start`, `OnEnable`, `TriggerEnter` (precisa de collider *Is Trigger*), `CollisionEnter`.
O padrão é **Nothing** (chamada manual): use `Trigger()` / `Trigger(GameObject)` de outro lugar — `UnityEventInteractable`, `EventStep` de cutscene, `DialogueEventRelay`, código. `Restore On Start` liga a restauração.
Quem entrou no volume vira o *instigador* das condições. Vários colliders do mesmo jogador podem tentar o evento mais de uma vez: use limite ou cooldown.

**`SmartEventInteractable`** — herda de `Interactable` (dica com botão, verbo e ícone do próprio `Interactable`) e exige um `SmartEventTrigger` no mesmo objeto, onde ficam limite, condições, save e eventos (deixe `Activate On` em Nothing).
- *Desativar quando bloqueado* **desligado** (padrão): o jogador sempre consegue apertar até o limite acabar, e *Ao ser bloqueado* dá o feedback (porta trancada, áudio, `Toast`). Esgotado, a dica mostra o texto de bloqueio.
- **Ligado**: a dica já mostra o texto de bloqueio e a interação fica desativada enquanto as condições falham.

## Condições prontas (menu **+ Adicionar**)

| Menu | Passa quando |
|---|---|
| `Contexto/Instigador tem Tag` | o instigador (ou um pai dele) tem a tag |
| `Contexto/Instigador está na Layer` | a layer do instigador está na máscara |
| `Contexto/Instigador é um jogador` | o instigador tem `PlayerContext` (ele ou um pai) |
| `Jogo/Variável verdadeira ou falsa` | variável do `GameSession` == valor esperado (ex.: `tem_chave`) |
| `Jogo/Variável numérica` | contador do `GameSession` comparado com um valor (`>=`, `==`...) |
| `Jogo/Outro evento já disparou` | a contagem de outro evento (pelo ID) satisfaz a comparação |
| `Cena/GameObject está ativo` | o objeto está (ou não) ativo na hierarquia |
| `Aleatório/Chance` | sorteio com probabilidade 0–1 |

**Condição nova** — herde de `SmartCondition` (um arquivo, `[Serializable]`, construtor público sem parâmetros). Ela aparece sozinha no menu:

```csharp
using System;
using UnityEngine;
using SmartEvents;

[Serializable, ConditionMenu("Meu Jogo/Sanidade abaixo de")]
public class SanidadeBaixaCondition : SmartCondition
{
    [SerializeField, Range(0f, 1f)] float _limite = 0.3f;

    protected override bool Check(SmartEventContext context)
    {
        // context.Instigator = quem ativou (jogador, objeto que entrou no volume...); pode ser nulo
        return MinhaSanidade.Atual01 < _limite;
    }
}
```
Condição que dá erro conta como falsa e loga o erro no Console. O botão NOT do Inspector inverte qualquer uma.

## Receitas

- **Porta de ferro que pede a chave e abre uma vez**: `SmartEventInteractable` (traz o `SmartEventTrigger`); limite *Uma única vez*; condição `Jogo/Variável verdadeira ou falsa` com `tem_chave`; *Ao disparar* → abrir a `Door`; *Ao ser bloqueado* → áudio/`Toast` "Trancada".
- **Susto único ao entrar na sala**: `SmartEventTrigger` com `TriggerEnter` + collider *Is Trigger*; limite *Uma única vez*; condição `Instigador é um jogador`; *Ao disparar* → `CutsceneSequence.Play()`.
- **Botão que só funciona depois de outro evento**: condição `Jogo/Outro evento já disparou` com o ID do outro (ex.: `gerador_ligado`).
- **Alavanca com 3 usos e 2 s de intervalo**: limite *Limitado* = 3, *Cooldown* = 2.
- **Marcar progresso para outros sistemas**: *Variáveis ao disparar* → `fusivel_ok` ligar; depois use `fusivel_ok` em `LevelDoor.requiredVariable` ou em condições de diálogo.
- **Chamar de uma cutscene/diálogo**: `EventStep` / `DialogueEventRelay` apontando para `SmartEventTrigger.Trigger()`.

## Código

```csharp
[SerializeField] SmartEvent _evento = new SmartEvent();     // aparece com o Inspector completo

void Start()                    { _evento.Restore(); }       // reaplica o estado salvo
void Usar(GameObject quem)      { var r = _evento.Invoke(quem); }   // TriggerResult: Triggered, LimitReached, OnCooldown, ConditionsNotMet

_evento.CanInvoke();            // passaria agora? (não gasta ativação)
_evento.TriggerCount;  _evento.IsExhausted;  _evento.RemainingTriggers;  _evento.ResetProgress();
_evento.Blocked += motivo => ...;                            // por que foi bloqueado
_evento.OnTriggered.AddListener(() => ...);

SmartEventSave.GetTriggerCount("boss_morto");                // do GameSession
SmartEventSave.SetBool("tem_chave", true);  SmartEventSave.AddInt("pilhas", 1);  SmartEventSave.GetInt("pilhas");
```

## Premissas e observações

- **Código escrito sem compilar no Unity**; revisado por script (delimitadores, referências cruzadas, nomes de campos serializados, um componente por arquivo, assinaturas). Se aparecer erro de compilação, envie a mensagem.
- Chamadas ao framework, todas conforme `HorrorFPS-Arquitetura.md`: `GameSession.GetBool/SetBool/GetInt/AddInt` (em `SmartEventSave.cs`), `Interactable` com `DefaultVerb`, `CanInteract`, `GetBlockedReason`, `Interact` e `PlayerContext` como componente (em `SmartEventInteractable.cs` e `HorrorFPSConditions.cs`). Se uma assinatura for diferente, o erro fica só nesses três arquivos.
- O delay usa `WaitForSeconds` (respeita a pausa por `timeScale`) em um objeto oculto `[SmartEventRunner]` que persiste entre cenas; se o objeto dono for destruído antes do fim do delay, o evento ainda dispara e alvos destruídos são ignorados pelo `UnityEvent`.
- Cooldown vale por instância em memória (não vai para o save).
- O `SmartEventInteractable` não declara mensagens do Unity (`Awake`, `Start`...) de propósito, para não esconder as do `Interactable`; a restauração e o ID ficam no `SmartEventTrigger`.
