# Diário e ruído (agachar silencioso)

Dois sistemas: o **diário** (reler notas, páginas de diário e gravações que o grupo achou) e o **ruído do jogador** (agachar e andar devagar fazem menos barulho para quem escuta). Código em `Journal/` e em `Body/` + `UI/` (namespace `HorrorFPS`). O diário é criado sozinho ao iniciar o jogo: não precisa estar em cena.

> Status: escritos e revisados no código, **ainda não compilados nem testados na Unity** (o ambiente onde foram feitos não tem a Unity). Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Diário

### O que o jogador vê

- **Aviso** no canto da tela quando um registro novo entra: `Novo registro no diário: <título>`.
- **Pausa > Diário**: todos os registros achados, agrupados em Diários, Notas, Gravações e Documentos, com `● novo` nos que ainda não foram lidos. Escolha um para ler; o texto vira cartões que se rolam com o teclado, o controle ou a roda do mouse. `Anterior` / `Próximo` passam para o registro do lado; `Voltar à lista` (ou Esc / Pausa) volta à lista. `Marcar tudo como lido` limpa os `● novo`. O botão **Diário** do menu de pausa mostra o contador de não lidos e só aparece quando há algo para mostrar.
- **Nota no mundo** (`JournalNote`): o jogador olha, aperta interagir (`Ler`) e o texto abre no meio da tela **sem pausar o jogo** (ler com um monstro por perto é arriscado). Enquanto lê ele fica parado; fecha com **Voltar** (Backspace / B) ou **E / X**; fecha sozinho ao levar dano, morrer ou começar uma cutscene. Rola com setas / W S / D-pad, roda do mouse ou analógico direito.
- O diário é **do grupo** (como os objetivos): quem achou, achou para todos.

### Como definir os registros

Três formas, que se misturam:

1. **Catálogo** (recomendado): `Tools > Horror FPS > Journal > Create Journal Catalog` cria `Resources/HorrorFPS/JournalCatalog.asset`. Cada registro tem:

| Campo | Para quê |
|---|---|
| `Id` | identificador único, sem espaços nem `;` (`diario_dr_meyer_1`). É por ele que eventos, scripts e o save se referem ao registro |
| `Title` / `Kind` | título e tipo (Nota, Diário, Gravação, Documento: só muda o grupo e o rótulo) |
| `Author` / `Date` | linha embaixo do título (`Dr. Meyer • 12 de março`), opcionais |
| `Text` | texto completo; aceita rich text (`<b>`, `<i>`, `<color=#ff0000>`). **Linha em branco separa parágrafos** (cada parágrafo é um cartão na página do diário) |
| `Item Id` | opcional: id de um item de documento (aba Registros). Ao **pegar o item** o registro entra no diário; título/texto vazios vêm do item |
| `Unlock Variable` | opcional: entra sozinho quando esta variável do jogo ficar verdadeira (a mesma de Pickup, Door, SmartEvent, Set Variable) |
| `Notify` | mostra o aviso ao entrar |

2. **Automático pelo inventário**: todo documento da aba **Registros** (item com categoria Documento / `Tab Rule = Records`) que o jogador pegar, ou já levar consigo ao começar, entra no diário sozinho com o id `item_<id do item>`, usando o *Texto de leitura* do item (ou a descrição). Desligue em `JournalCatalog > Auto Collect Records`. O item continua na aba Registros do inventário: lá é o que ele **carrega**; o diário guarda tudo que ele **achou**, mesmo que largue ou guarde o item.
3. **Código**: `Journal.Register(new JournalEntry { id = "...", title = "...", text = "..." })`. Um registro com o mesmo id de um do catálogo o substitui.

### Como usar em cena (sem programar)

- **`Journal Note`** (`Tools > Horror FPS > Journal > Create Journal Note In Scene`): uma folha de papel com Collider. Escreva o id de um registro do catálogo em `Catalog Id` (o recomendado: o texto fica no catálogo e vale em qualquer cena). Sem `Catalog Id` dá para escrever o registro na própria nota, mas ele só aparece no diário se a cena da nota já foi carregada ao abrir um save: use só para testar. Opções: `Read Now` (abre o texto na hora; desligado, só guarda no diário), `Remove When Taken` (a nota some do mundo; com um `SaveableEntity` no objeto continua sumida ao carregar o save), evento `On Read`. O texto da dica (`Ler`) muda pelos campos `Verb` e `Object Name` do `Interactable`.
- **Componente `Journal Actions`** (`Add Journal Actions To Selected`): `UnlockEntry`, `LockEntry`, `MarkEntryRead` (usam o `Entry Id`) e as versões `...ById(string)`. Ligue num UnityEvent: `SmartEventTrigger` (volume "entrou na sala", com **Once** e **Save**), botão, `Interactable`, fim de diálogo...
- **Passo de cutscene `Journal`** (`HorrorFPS/Cutscene/Steps/Journal`): põe um registro no diário, tira ou marca como lido (ao pular a cutscene o resultado é aplicado igual).
- **Condição de SmartEvent `Diário/Estado do registro`**: roda um evento só se o registro está Não achou / Achou e não leu / Leu.
- **Por variável**: preencha `Unlock Variable` no catálogo; tudo que mexe em variáveis (Pickup, SmartEvent, Set Variable) coloca o registro no diário sozinho.

### Como usar em código

```csharp
Journal.Unlock("diario_dr_meyer_1");          // entra no diário (e aparece o aviso)
Journal.Unlock("id", false);                   // entra em silêncio, sem aviso
bool achou = Journal.IsUnlocked("diario_dr_meyer_1");
JournalEntryState s = Journal.StateOf("id");   // NotFound / Unread / Read
Journal.MarkRead("id");                        // some o "novo"
Journal.Lock("id");                            // tira do diário e esquece que foi lido
JournalReader.OpenFor(player, Journal.Get("id")); // abre o texto na tela deste jogador (e põe no diário)
Journal.Unlocked += e => ...;                  // também Journal.Changed
```

### Como funciona por dentro

- O que o jogador achou e leu fica nas **variáveis do save** (`GameSession`): `journal.order` (ids separados por `;`, na ordem em que achou) e `journal.read.<id>` = 1. Por isso **salvar, carregar e Novo Jogo funcionam sozinhos**, sem código de save. O texto **não** vai para o save: vem do catálogo / do item, então um registro que você edita no catálogo muda nos saves antigos também.
- Um registro achado cujo id não existe mais no catálogo (e que não é `item_...`) simplesmente não aparece na lista (continua contado no save).
- `JournalRunner` (criado sozinho) reavalia os `Unlock Variable` quando uma variável muda e a cada meio segundo, e mostra o aviso na tela.
- `JournalReader` é um módulo do jogador (grupo *Interface*), instalado por `PlayerModules.Install`: abre o leitor (reusa o `RecordReader` da aba Registros), trava o jogador com `PlayerContext.AddControlLock` e liga o inventário ao diário (`InventoryOwner.Picked`). Solta o jogador **dois quadros depois** de fechar, para o mesmo aperto de E que fecha não reabrir a nota que ele está olhando.

### Se algo não funciona

| Sintoma | Causa provável |
|---|---|
| A nota diz `Ilegível` e não deixa interagir | `Catalog Id` não existe no catálogo (ou nota sem `Id`). Veja o aviso amarelo no Console |
| O registro entrou mas não aparece em Pausa > Diário | o id não está no catálogo e a nota era de outra cena (veja acima); ou o id tem `;` |
| O botão Diário não aparece na pausa | ainda não há registro no catálogo nem achado. Crie o catálogo ou pegue um documento |
| Documento da aba Registros não vira registro | `Auto Collect Records` desligado, ou o item não é da aba Registros |
| O leitor fecha na hora | alguém travou/pausou o jogo no mesmo instante (cutscene, loading), ou o jogador levou dano |

---

## 2. Ruído do jogador: agachar silencioso

O `PlayerMotionProbe` já emitia o barulho dos passos para o `NoiseSystem` (o que o Perseguidor e qualquer `NoiseListener` ouvem). Agora o raio do barulho de cada passo é:

> **postura × velocidade × piso × modificadores**

| Fator | Como é |
|---|---|
| Postura | agachado `crouchNoise` (1,5 m) < andando `walkNoise` (5 m) < correndo `runNoise` (11 m) |
| Velocidade | cai junto com a velocidade em relação à normal da postura (`speedInfluence` 0,7; mínimo 25 %): andar bem devagar (analógico de leve, agachado) faz quase nada. Parado não faz barulho nenhum |
| Piso | `NoiseSurface` sob os pés |
| Modificadores | `SetNoiseModifier(chave, multiplicador)`: botas silenciosas (0,5), sapato de salto (1,5), um nível com vento... |

O agachar agora é lido **direto do `FirstPersonController`** (vale no instante do botão, sem esperar a câmera descer); sem controlador, a sonda volta a deduzir pela altura da câmera (`Read Controller State`). Pulo e pouso também respeitam o piso e os modificadores.

### `NoiseSurface` (piso que abafa ou denuncia)

Componente no objeto do piso (ou num pai do collider): `Tapete` 0,45, `Grama` 0,6, `Madeira` 1,1, `Concreto` 1, `Cascalho` 1,6, `Metal` 1,7, `Vidro quebrado` 2,2, `Água` 1,4 (ou `Custom` e digite). Para um tapete sobre um piso comum, crie uma **caixa baixa (até uns 0,3 m)** deitada no chão com um Collider marcado como **Trigger** e o componente: vale o piso mais próximo dos pés. O som do passo (clipe) é do seu sistema de áudio; aqui só muda o que a IA ouve.

### Medidor de ruído na tela (`NoiseMeterHUD`)

Três barrinhas no canto superior direito (abaixo do lugar dos avisos) mostram o quanto o jogador está sendo ouvido: `Silencioso` (cinza, nenhuma barra), `Baixo` (1 barra verde: agachado ou devagar), `Médio` (2 amarelas: andando), `Alto` (3 vermelhas: correndo ou piso barulhento). Cada passo dá uma piscada. Aparece quando há o que mostrar (andando ou agachado) e some sozinho. **Opções > Indicador de ruído** liga e desliga (vale para todos os jogadores deste computador).

Os limites ficam no `PlayerMotionProbe` (`Quiet Below` 2,5 m e `Loud From` 8 m, em raio de barulho).

### Dicas de tutorial relacionadas

- `crouch` (já existia) agora explica que agachar faz menos barulho.
- `noise`: aparece **uma vez** na primeira vez que o jogador faz barulho **alto**, e explica o medidor (`TutorialWhen.LoudNoise`).
- `journal`: aparece uma vez quando o primeiro registro entra no diário (`TutorialWhen.JournalUnlocked`).

### Em código

```csharp
PlayerMotionProbe probe = player.GetComponentInChildren<PlayerMotionProbe>();
probe.Level;               // Silent / Quiet / Normal / Loud
probe.NoiseRadius;         // raio (m) que ele faz agora, suavizado
probe.CurrentStepRadius;   // raio que o próximo passo teria
probe.SetNoiseModifier(this, 0.5f);   // botas silenciosas; 1 remove
probe.SetNoiseModifier(this, 1f);
```

O Perseguidor não precisa de mudança: ele já escuta `NoiseSystem` e decide pelo volume que chega (distância, paredes, audição dele). Agachado, o raio é menor, então ele precisa chegar mais perto para ouvir.
