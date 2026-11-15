# Iluminação e níveis: luz que falha, atalhos que se abrem, alavancas e níveis novos

Quatro peças que se encaixam por **variáveis da partida** (`GameSession`), do jeito do resto do jogo: uma **luz que falha** de verdade (pisca e apaga conforme a sanidade, o medo ou o roteiro), um **atalho** que abre de um lado só e fica aberto para sempre, uma **alavanca** que liga uma variável (e com ela acende luz, abre atalho, destranca porta) e dois **níveis novos** na demonstração (Túneis e Casa de força) mais um menu para criar a cena de um nível pronta para montar. Código em `Lighting/` (`LightFailure`), `World/` (`Shortcut`, `Lever`) e `Editor/` (menus e geradores); namespace `HorrorFPS`. Nenhum arquivo de som ou modelo é preciso.

> Status: escrito e revisado no código, **ainda não compilado nem testado na Unity** (o ambiente onde foi feito não tem a Unity). Os níveis da demo saem de código (caixas e cilindros): servem para testar o fluxo e a sensação, não para o visual final. Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Resumo

| O quê | Componente | Como entra no jogo |
|---|---|---|
| Luz que pisca e apaga | `LightFailure` (em cima de um `SwitchableLights`) | sanidade baixa, jogador assustado por perto, uma variável ligada, ou código/UnityEvent |
| Luz mais fraca e vermelha **só para quem está com a sanidade baixa** | `SanityLight` (já existia) | o `LightFailure` põe um em cada luz do conjunto |
| A zona de visibilidade escurece quando a luz apaga | `VisibilityZone` com `Light Source` | liga a zona ao `SwitchableLights` |
| Atalho que abre de um lado só e fica aberto | `Shortcut` | o jogador abre do lado certo; ou uma variável abre sozinho |
| Alavanca que liga uma variável | `Lever` | o jogador puxa |
| Nível novo | menu `Levels > Create New Level Scene...` | cena pronta + Build Settings + `GameCatalog` |

A cadeia típica: **alavanca → variável → luzes acendem / atalho abre / porta destranca**.

---

## 2. Luz que falha

### 2.1 Como montar

1. A luz já está num `SwitchableLights` (lustre, abajur, luz do corredor)? Selecione o objeto e use `Tools > Horror FPS > Levels > Add Light Failure To Selected`. Se o objeto só tem `Light` (a lâmpada solta), o mesmo menu cria o `SwitchableLights` juntando as luzes dele e dos filhos.
2. Ou crie uma nova: `Tools > Horror FPS > Levels > Create Failing Light` (uma luz de ponto com os dois componentes).
3. Pronto: com a sanidade dos jogadores abaixo de 50 % a luz começa a piscar, de vez em quando apaga por alguns segundos e volta.

Para a luz **só acender com energia**, escreva a variável em `Power Variable` do `SwitchableLights`. Para o interruptor, o `LightSwitch` de sempre. Ponha `SaveableEntity` no mesmo objeto para salvar a posição do interruptor (a falha em si é passageira e **não** entra no save).

### 2.2 O que decide a gravidade (0 a 1)

O `LightFailure` mede uma **gravidade**; quanto maior, mais seguido e mais longo é o defeito. Valem **todas as fontes ligadas, vence a maior**:

| Fonte | Campo | Como conta |
|---|---|---|
| Sanidade | `Follow Sanity`, `Sanity Mode` (média ou a mais baixa dos jogadores vivos), `Sanity Below` | 0 com a sanidade em `Sanity Below` (padrão 0,5) ou acima; 1 com sanidade zero |
| Medo | `Follow Fear`, `Fear Range` (12 m), `Fear Above` (0,5) | um jogador vivo **assustado** (batimento do coração acima de `Fear Above`) dentro do alcance: a luz "sente" o monstro pelo medo, sem conhecer o monstro |
| Variável | `Fail Variable` | enquanto estiver ligada a gravidade é 1 (o roteiro decide: "o gerador está falhando") |
| Código | `Flicker(s)`, `Blackout(s)`, `BlackoutUntilRestored()`, `Restore()` | manual (cutscene, SmartEvent, UnityEvent) |

A gravidade é reavaliada a cada 0,4 s. A média ou o mínimo contam só **jogadores vivos**; sem jogadores (menu) não há falha.

### 2.3 Como é a falha

Com gravidade acima de 0,02, depois de um intervalo sorteado (`Interval`, padrão 5 a 16 s; com gravidade alta o intervalo encurta até 0,4× e com baixa alonga até 1,5×) vem um defeito:

- **Pisca-pisca** (`Stutter Seconds`, 0,4 a 1,4 s): a luz oscila em saltos entre quase apagada e quase firme (`Stutter Speed`);
- **Apagão** (`Out Seconds`, 2 a 7 s, mais longo com gravidade alta): a chance é `Blackout Chance × gravidade` (padrão 40 % com gravidade máxima). Apaga com um **estalo** (`Pop Sound`, ou um estalo gerado por código) e volta piscando por menos de um segundo;
- Eventos `On Failed` e `On Restored` (UnityEvent) a cada defeito.

**Barulho do estalo para a IA** (`Pop Noise`, em metros): padrão **0** (a IA não ouve). Com valor, o estalo atrai o monstro: uma luz que estoura longe do jogador vira isca.

### 2.4 As três camadas da luz

| Camada | Quem vê | O que faz |
|---|---|---|
| `SwitchableLights` | todos | o interruptor e a energia (`Power Variable`); o `SetFailure` por cima deles |
| `LightFailure` | **todos** (é a luz de verdade) | piscar e apagar; a IA e o `VisibilityZone` percebem |
| `SanityLight` | **só quem está com a sanidade baixa** (é só a imagem dele, no split-screen cada metade vê a sua) | luz mais fraca, avermelhada e tremendo, mesmo sem falha acontecendo |

`Viewer Sanity Effect` (ligado) faz o `LightFailure` pôr um `SanityLight` em cada luz do conjunto sozinho. Desligue se quiser só a falha real. Todas só valem para luz em **tempo real** (luz "baked" já está gravada na textura).

### 2.5 Zona de visibilidade que segue a luz

Em `VisibilityZone` (a que deixa o jogador mais fácil ou difícil de ser visto pelo perseguidor: ver `HorrorFPS-Visibilidade.md`), arraste o conjunto de luzes para **`Light Source`**:

- luz acesa e firme: a zona vale o `Multiplier` normal (ex.: sala iluminada, 1,3);
- luz apagada ou em falha: a zona vale `Multiplier When Off` (padrão 0,4, escuridão), e **nunca fica mais visível sem luz que com luz**;
- no meio, acompanha o brilho. O `PlayerVisibility` já suaviza (sobe rápido, desce devagar), então um pisca de meio segundo mal mexe.

Efeito de jogo: apagar o interruptor do corredor (ou a luz estourar) deixa quem está ali mais difícil de achar; acender a luz faz de um corredor seguro um holofote.

---

## 3. Atalho que se abre (`Shortcut`)

Um portão, uma grade, uma porta, uma estante que desliza, ligando duas partes do mapa. A ideia é o jogador descobrir o caminho de volta: **de um lado não abre** ("Não abre deste lado"), do outro abre, e depois de aberto **fica aberto** (no save inclusive).

### 3.1 Como montar

`Tools > Horror FPS > Levels > Create Shortcut` cria um objeto raiz com o componente e uma folha (um cubo 3 × 2,6 m) como filho, e já põe `SaveableEntity`. Troque a folha pelo seu modelo (ela precisa de um Collider). A **seta azul** na Scene View mostra de que lado o jogador abre; a **frente é o +Z** do objeto raiz: gire o raiz para trocar.

### 3.2 Campos

| Campo | Para quê |
|---|---|
| `Leaf` | a folha que se move (vazio = o próprio objeto) |
| `Motion` | `Slide` (sobe ou desliza) ou `Hinge` (gira em Y) |
| `Slide Offset` | quanto a folha anda até abrir, no espaço do pai dela. `(0, 2,6, 0)` sobe 2,6 m; `(3, 0, 0)` desliza 3 m para o lado (para dentro da parede, a folha some atrás dela) |
| `Open Angle` / `Open Seconds` / `Start Open` | dobradiça, duração (1,6 s) e se começa aberto |
| `Open From` | `Front` (padrão), `Back` ou `Both` |
| `Required Variable` | variável que precisa estar ligada para abrir (uma chave, uma alavanca). Sem ela: "Trancado" (`Locked Text`) |
| `Auto Open Variable` | quando esta variável **liga**, o atalho abre sozinho (a alavanca de um portão elétrico) |
| `Set Variable` | variável ligada quando o atalho abre (para objetivos, portas e eventos) |
| `Complete Objective` | id de um objetivo do `ObjectiveCatalog` a completar ao abrir |
| `Opened Hint` | dica mostrada a quem abriu |
| `Open Sound` / `Open Noise` (9 m) / `Rattle Noise` (3 m) | som (vazio = gerado por código), barulho de abrir e de forçar o lado errado para a IA (0 = silencioso) |
| `Blocker` | `NavMeshObstacle` da passagem (ver 3.5) |
| `On Opened` / `On Wrong Side` | UnityEvents |

### 3.3 O que o jogador vê

- Lado certo e destrancado: o verbo `Abrir` e o nome do objeto (`Object Name`);
- lado errado: **"Não abre deste lado"** (`Wrong Side Text`); apertar o botão faz uma pancada surda e o barulho de `Rattle Noise` (a IA pode ouvir);
- trancado: **"Trancado"** (`Locked Text`);
- aberto: a dica some (o atalho não pode mais ser fechado pelo jogador).

### 3.4 Abrir por variável (alavanca, gerador, objetivo)

`Auto Open Variable` abre o atalho no momento em que a variável liga, de qualquer fonte: uma `Lever`, uma ação de diálogo, um `SetVariableStep` de cutscene, um `Pickup` com `Set Variable`. Ao carregar um save com a variável já ligada, ele já começa aberto. Combine com `Required Variable` igual para o portão **não deixar o jogador abrir à mão antes da hora** (ele mostra "Sem energia" até a alavanca; depois abre sozinho).

### 3.5 Perseguidor

Um atalho fechado precisa barrar o caminho do monstro e um aberto precisa liberar. O jeito comum: gere o **NavMesh com o atalho aberto**, ponha um `NavMeshObstacle` (marque **Carve**) na passagem e arraste para `Blocker`. O componente liga o obstáculo enquanto o atalho está fechado e desliga ao abrir. Com `Blocker` vazio o NavMesh não é tocado.

### 3.6 Save

Com `SaveableEntity` no mesmo objeto, "aberto" vai para o save (`SaveKey` = `shortcut`). O `Set Variable` já vai no save das variáveis. Ao carregar, o atalho aparece aberto sem animação, som, barulho ou evento.

---

## 4. Alavanca (`Lever`)

`Tools > Horror FPS > Levels > Create Lever`. Puxar liga a variável `Variable` (padrão `energia_ligada`); com **Can Toggle** o jogador pode soltar de volta (desliga). O cabo (`Handle`, um objeto que gira em X) acompanha a variável, então volta sozinho para a posição certa ao carregar um save. Outros campos: `Required Variable` (exige outra antes, mostra `Locked Text`, "Sem energia"), `Pulled Hint`, `Sound`, `Noise` (6 m para a IA), `Pulled Angle`, `On Pulled` / `On Released`.

A face da base que encosta na parede é a de trás (-Z): gire o objeto para o +Z apontar para a sala.

Para ligar um roteiro sem código, **a mesma variável** nos lugares certos:

| Onde | Campo | Efeito |
|---|---|---|
| `SwitchableLights` | `Power Variable` | as luzes acendem quando a variável liga (e desligam se ela cair) |
| `Shortcut` | `Auto Open Variable` | o portão abre |
| `LevelDoor` | `Required Variable` (+ `Locked Text`) | a porta de nível destranca |
| `LightFailure` | `Fail Variable` | a luz passa a falhar |
| `Door` | `Unlock Variable` | a porta comum destranca |
| `RadioSet` | `Requires Variable` de uma fala | a fala só toca depois |

---

## 5. Receitas

**Gerador que liga e fica instável.** Luzes com `Power Variable = gerador_ligado` e `Fail Variable = gerador_ligado`: apagadas até a alavanca, depois acendem **e falham sempre** (gravidade 1). É o que a Casa de força da demo faz.

**Apagão até alguém resolver.** Um evento chama `BlackoutUntilRestored()` (uma cutscene, um trigger); o jogador acha o disjuntor e um evento chama `Restore()`. Entre os dois a luz fica morta e a `VisibilityZone` ligada a ela vira escuridão.

**Volta rápida.** Um `Shortcut` com `Open From = Front` no fim de um laço: de um lado "Não abre deste lado", do outro abre e liga o caminho de volta ao começo. É o que os Túneis e o Salão fazem.

**Esconder-se apagando a luz.** `LightSwitch` na parede + `SwitchableLights` na lâmpada + `VisibilityZone` com `Light Source` na sala: apagar a luz vira esconderijo. O Salão tem isso no corredor.

---

## 6. Níveis

### 6.1 Criar a cena de um nível novo

`Tools > Horror FPS > Levels > Create New Level Scene...` pede onde salvar (o nome da cena vira o nível: `Level_Biblioteca` vira o id `biblioteca`) e cria: uma sala 16 × 16 (troque pela sua), atmosfera de terror, uma luz que falha, uma zona de sombra, o som padrão do nível e o grupo de spawn `start` para quatro jogadores. Põe a cena nos Build Settings e, se já existe um `GameCatalog` (`Resources/HorrorFPS`), acrescenta o nível a ele. Sem catálogo, avisa no Console o que falta.

Para chegar nele: num outro nível, um `LevelDoor` com **Level Id** = o id do nível e **Spawn Id** = o grupo de spawn do destino (`start` é o padrão). Para o jogador voltar, um `LevelDoor` no nível novo apontando para o anterior, com o `Spawn Id` de um grupo que exista lá (um `SpawnPoint` por jogador, todos com o mesmo id).

### 6.2 Os níveis da demonstração

`Tools > Horror FPS > Demo > Create Full Game Demo` agora monta cinco cenas de jogo (as três antigas mais duas):

| Nível | O que tem |
|---|---|
| **Salão** (`hall`) | como antes, mais: a luz do corredor falha (sanidade e medo), tem interruptor e uma zona de visibilidade que a segue; o **portão de serviço** na divisória só abre pelo lado do corredor (volta rápida depois do porão); um grupo de spawn `from_lift` |
| **Porão** (`cellar`) | como antes, mais uma lâmpada solta que falha (com a zona dela) e o **alçapão** para os túneis; spawn `from_tunnels` |
| **Túneis** (`tunnels`) | corredores em laço (feitos de um mapa de texto), escuros: luzes A falham com a sanidade, luzes B só acendem com a energia; a **alavanca** liga `tunnels_power` (acende as luzes B e **abre sozinho o portão elétrico**); o **atalho** no fim do laço só abre de dentro dele, de volta ao começo; porta de serviço para a Casa de força; uma pilha |
| **Casa de força** (`power`) | sala escura com o **gerador** (alavanca `generator_on`): as luzes da sala acendem, as **instáveis** passam a falhar sempre (estalo de 6 m para a IA); o rádio dá a segunda fala só depois do gerador; o **elevador de serviço** (precisa do gerador) leva direto ao Salão |
| **Anexo** (`Level_Annex`) | como antes, por streaming ao lado do Salão |

Fluxo para testar: Novo jogo → chave na mesa → porta de ferro → porta do porão → no Porão o alçapão → Túneis (puxe a alavanca; o portão elétrico abre; o atalho serve de volta) → porta de serviço → Casa de força (ligue o gerador, ouça o rádio, suba pelo elevador). Como todos se ligam por variáveis, **Carregar** restaura luz, alavanca e atalhos.

`Create Full Game Demo` **recria as cenas do zero** (o que você editou nelas se perde; ele avisa antes).

### 6.3 Mapas de texto (só no gerador da demo)

Os Túneis saem de um mapa de texto em `Editor/HorrorFPSSetup.MoreLevels.cs` (`GridMap`): `#` é parede, qualquer outra letra é piso, e o código põe piso, teto e paredes sozinho. É um atalho para montar corredores de teste; para o seu nível final use a sua geometria.

---

## 7. Menus do Editor

| Menu | O que faz |
|---|---|
| `Tools > Horror FPS > Levels > Create Failing Light` | luz de ponto com `SwitchableLights` e `LightFailure` |
| `Tools > Horror FPS > Levels > Add Light Failure To Selected` | põe `SwitchableLights` (se faltar) e `LightFailure` nos objetos selecionados |
| `Tools > Horror FPS > Levels > Create Shortcut` | portão que sobe, com `Shortcut` e `SaveableEntity` |
| `Tools > Horror FPS > Levels > Create Lever` | alavanca com cabo e `Lever` |
| `Tools > Horror FPS > Levels > Create New Level Scene...` | cena de nível pronta (6.1) |

Os objetos nascem no ponto que a Scene View está olhando. A área `Levels` tem `LevelsOrder = 100` e `Demo` passou de 100 para 110.

---

## 8. Em código

```csharp
// luz que falha
LightFailure f = lamp.GetComponent<LightFailure>();
f.Flicker(2f);                       // pisca-pisca por 2 s
f.Blackout(5f);                      // apagão de 5 s
f.BlackoutUntilRestored();           // apagão até Restore()
f.Restore();                         // acaba na hora
bool apagada = f.IsOut, falhando = f.IsFailing; float grav = f.Severity;
f.onFailed.AddListener(() => Debug.Log("estalou"));

SwitchableLights luzes = ...;
luzes.SetOn(false);                  // o interruptor (como antes)
luzes.SetFailure(0.3f);              // falha manual: 0 apagada ... 1 normal (o LightFailure usa isto)
float brilho = luzes.Level;          // 0..1 com interruptor, energia e falha

// atalho e alavanca
Shortcut s = ...;
s.Open();                            // som, barulho, variável, objetivo e evento
s.Close();                           // só para armadilhas e cutscenes
bool aberto = s.IsOpen;
Lever l = ...;
l.Pull(); l.Release();
bool puxada = l.IsPulled;

// a ponte é a variável
GameSession.SetBool("energia_ligada", true);   // acende as luzes, abre o atalho, destranca a porta de nível
```

Em um `UnityEvent` do Inspector use `Flicker`, `Blackout`, `BlackoutUntilRestored`, `Restore` (luz), `Open`, `Close` (atalho), `Pull`, `Release` (alavanca).

---

## 9. Se algo não funciona

| Sintoma | Causa provável |
|---|---|
| A luz nunca falha | a sanidade dos jogadores está acima de `Sanity Below`; `Follow Sanity` desligado e nenhuma outra fonte; o conjunto não tem `Light`; ou não há jogador vivo (sem jogadores a gravidade é 0) |
| A luz falha, mas a zona de visibilidade não escurece | `Light Source` da `VisibilityZone` vazio, ou apontando para outro `SwitchableLights` |
| Só uma metade da tela vê a luz vermelha | é o `SanityLight` por jogador: só quem está com a sanidade baixa vê |
| Depois de uma cutscene a luz ficou fraca | a cutscene `FlickerLightsStep` mexe na `Light` direto e pode ter guardado a intensidade no meio de uma falha: não ponha `LightFailure` em luzes que uma cutscene pisca |
| O atalho não abre | `Required Variable` desligada ("Trancado"); o jogador está do lado errado da seta azul; ou o Collider da folha não está no objeto raiz nem em um filho |
| O atalho abre nos dois lados | `Open From = Both` |
| O monstro atravessa o atalho fechado | faltou o `NavMeshObstacle` (Carve) em `Blocker` e o NavMesh gerado com ele aberto |
| O monstro não passa por um atalho aberto | o NavMesh foi gerado com ele **fechado**: gere de novo com ele aberto |
| A alavanca não acende as luzes | a variável da alavanca (`Variable`) é diferente da `Power Variable` do conjunto (confira a grafia) |
| O portão elétrico abre sem a alavanca | `Required Variable` vazio e `Open From = Both`: o jogador abre à mão; ponha a mesma variável da alavanca em `Required Variable` |
| O nível novo não aparece no menu Carregar | não está no `GameCatalog`: rode `Create New Level Scene` com o catálogo existente ou acrescente o nível à mão |
| Ao descer o alçapão os jogadores caem fora do mapa | falta o grupo de `SpawnPoint` com o `Spawn Id` do `LevelDoor` no nível de destino |

---

## 10. Arquivos

| Arquivo | O que faz |
|---|---|
| `Lighting/LightFailure.cs` | falha de luz por sanidade, medo, variável ou código |
| `World/Shortcut.cs` | atalho que abre de um lado, fica aberto, abre por variável, salvo |
| `World/Lever.cs` | alavanca que liga uma variável |
| `Editor/Levels/LevelsMenu.cs` | menus `Tools > Horror FPS > Levels` (luz, atalho, alavanca) |
| `Editor/HorrorFPSSetup.NewLevel.cs` | `Create New Level Scene...` |
| `Editor/HorrorFPSSetup.MoreLevels.cs` | os níveis Túneis e Casa de força, o mapa de texto e as peças reaproveitáveis do gerador |

Alterados: `Props/SwitchableLights.cs` (`Lights`, `Level`, `Failure`, `SetFailure`), `Body/VisibilityZone.cs` (`Light Source`, `Multiplier When Off`, `Current`), `Flow/LevelDoor.cs` (`Locked Text`), `Editor/HorrorFPSMenu.cs` (área `Levels`, `Demo` = 110), `Editor/HorrorFPSSetup.Levels.cs` (portão de serviço e luz do corredor no Salão; alçapão e lâmpada solta no Porão), `Editor/HorrorFPSSetup.FullDemo.cs` (cinco cenas, catálogo, Build Settings), `Editor/HorrorFPSSetup.Audio.cs` (som dos novos níveis).

## 11. Limites conhecidos

- **A falha de luz é compartilhada**: todos os jogadores veem a mesma luz piscar. Só o `SanityLight` é por jogador. A gravidade por sanidade usa a **média** (ou o mínimo) dos vivos, então um jogador muito abalado pode piscar a luz de todos com `Sanity Mode = Lowest`.
- **O medo mede o batimento**, não a distância ao monstro; a luz "sente" um jogador assustado, não o monstro em si.
- A falha **não entra no save** (um apagão em andamento volta firme ao carregar). O interruptor e a energia entram.
- Luzes **baked** não falham (a luz está gravada na textura). Use luzes em tempo real, ou só o emissivo (`Emissive Renderers` do `SwitchableLights`, que o `LightFailure` também pisca).
- O atalho não é "desfeito" pelo jogador, e o perseguidor **não abre** atalhos por conta própria (só passa por eles depois de abertos, se o NavMesh deixar).
- Um atalho que **sobe** precisa de uma verga acima dele para a folha sumir; um que desliza para o lado some dentro da parede.
- Os **níveis da demo** são de caixas e cilindros, sem texturas nem modelos, e o `Create Full Game Demo` os recria do zero.
- Nada disso foi **compilado nem jogado**: ajuste intervalos, durações e a sensação (quanto a luz falha, quanto dura o apagão) ao jogar.
