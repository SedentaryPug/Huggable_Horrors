# Itens de distração: garrafa, sinalizador e rádio

Três itens que o jogador **joga** para o monstro ir investigar outro lugar: a **garrafa** quebra e faz um estrondo, o **sinalizador** acende e fica chamando enquanto queima, o **rádio** chia e murmura sozinho até a pilha acabar. Funcionam em cima do `NoiseSystem` e do equipamento de mãos: o objeto jogado emite o barulho **dele mesmo** (não do jogador), então o perseguidor vai ver o que foi, e não atrás de quem jogou. Código em `Items/ItemEquip.cs` (campos), `Equipment/` (jogar e o objeto jogado), `Body/` (`NoiseSourceTag`, sons gerados), `Stalker/StalkerSenses.cs` (como o monstro ouve) e `Editor/` (itens e menus); namespace `HorrorFPS`.

> Status: escrito e revisado no código, **ainda não compilado nem testado na Unity** (o ambiente onde foi feito não tem a Unity). Os sons (vidro quebrando, sinalizador, chiado e vozes do rádio) são gerados por código e **ninguém ouviu ainda**: ajuste volumes e troque pelos seus clipes quando tiver. Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Resumo

| Item | Como funciona | Quanto chama |
|---|---|---|
| **Garrafa** (`garrafa`) | quebra ao bater (cacos e som de vidro) | **um** barulho forte, raio 28 m, no ponto da batida. Empilha até 4 |
| **Sinalizador** (`sinalizador`) | acende **ao ser jogado**, queima com luz vermelha e chiado | chama de 2,2 em 2,2 s, raio 26 m, por 20 s (a chama apaga devagar no fim). Empilha até 3 |
| **Rádio portátil** (`radio_portatil`) | ao cair começa a tocar chiado e vozes (começa sozinho em 6 s se nunca bateu em nada) | chama de 2,8 em 2,8 s, raio 22 m, por 25 s. Não empilha |

Cada arremesso gasta **uma unidade**. Acabou a pilha: a mão fica vazia e o item sai da mochila.

---

## 2. Como o jogador usa

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Equipar | atalho do item e depois clique esquerdo ou direito (mão esquerda ou direita) | como qualquer item de mão |
| Jogar perto | **tocar** o botão da mão | tocar o gatilho |
| Jogar longe | **segurar** o botão (o braço vai para trás e um estalinho avisa quando a força está cheia) e **soltar** | segurar e soltar o gatilho |

A força vai de `throwMinPower` (toque rápido, 35%) a 100% em `throwChargeSeconds` (0,55 s). A mira é a da câmera, com um pouco de arco para cima (`throwArc`); a mão **tremendo** (medo, pouca sanidade) desvia o arremesso, como nas armas. Com uma parede colada à frente o objeto sai logo antes dela, não dentro. A dica de tutorial `throwable` aparece na primeira vez que o jogador equipa um.

O HUD da mão mostra o nome e a quantidade (`Garrafa   x3`).

---

## 3. Como o monstro reage

1. O objeto jogado é um **GameObject próprio** (`ThrownDistraction`) e emite o barulho com `NoiseKind.Distraction` tendo **ele mesmo** como fonte. Por isso o perseguidor **não** conclui "o jogador está ali" (o barulho de alguém que está sob um `PlayerContext` vira foco no jogador): ele vai **investigar o ponto**.
2. O objeto leva um `NoiseSourceTag` (`Body/NoiseSourceTag.cs`) com a chave da fonte (o id do item, ou `lureKey`) e a opção `alwaysInvestigate`. `StalkerSenses` lê essa etiqueta quando a fonte não tem uma `StalkerNoiseTag` (o framework não conhece o perseguidor; é o perseguidor que lê a etiqueta do framework).
3. **Ele aprende.** Cada viagem em vão a uma fonte de som que não era um jogador soma desconfiança na memória dele (`baitSuspicionPerMiss` 0,34: três viagens e aquela chave vira isca) e esquece devagar (0,02 por minuto). Depois disso ele **ignora** a fonte com uma chance que depende do nível de atenção e do `baitDiscernment` do perfil. Jogar a **mesma** garrafa dez vezes deixa de funcionar; é de propósito.
4. Quem decide o quanto isso importa é você: na `ItemEquip` do item, `lureAlwaysInvestigate` faz o monstro **nunca** aprender (use no sinalizador de uma cena da história) e `lureKey` junta itens diferentes sob a mesma chave (ou separa dois do mesmo tipo).

Mais sobre o aprendizado de iscas: [`HorrorFPS-Perseguidor`](HorrorFPS-Perseguidor.md) (seção de memória) e `StalkerNoiseTag` (para fontes colocadas por você na cena).

---

## 4. Campos do item (`ItemEquip`, tipo **Arremessável**)

Seção "Arremessável" do Inspector (`Editor/ItemEquipDrawer.cs`). Os botões **Garrafa**, **Sinaliz.** e **Rádio** do Inspector aplicam os valores prontos.

| Campo | Para quê |
|---|---|
| `Throw Effect` | `Shatter` (garrafa: quebra ao bater), `Flare` (sinalizador: acende ao jogar), `Radio` (rádio: toca depois da primeira batida) |
| `Throw Prefab` | modelo do objeto jogado. Vazio: usa o modelo da mão, o de inspeção, o de mundo ou um bloco simples |
| `Throw Speed` / `Throw Charge Seconds` / `Throw Min Power` / `Throw Arc` | velocidade em força cheia (m/s), tempo para a força cheia, força do toque rápido (0 a 1) e o arco para cima |
| `Throw Noise` | raio do barulho da batida, que cresce com a força do arremesso: de 30% a 75% quando bate sem quebrar (sinalizador, rádio) e de 75% a 100% quando a garrafa quebra |
| `Throw Impact Sound` | seu clipe para a batida (vazio: vidro ou *tum* gerados) |
| `Lure Seconds` | por quanto tempo continua chamando depois (0 = só a batida) |
| `Lure Noise` / `Lure Interval` | raio de cada chamada e de quanto em quanto tempo (s) |
| `Lure Sound` | seu clipe em loop (vazio: chiado do sinalizador ou rádio gerado) |
| `Lure Always Investigate` / `Lure Key` | ver seção 3 |
| `Flare Color` / `Flare Range` / `Flare Intensity` | a luz do sinalizador (só `Flare`) |

O objeto jogado é temporário: **não entra no save** (trocar de nível ou carregar o jogo some com ele) e existem no máximo `ThrownDistraction.MaxAlive` (8) ao mesmo tempo; passando disso o mais antigo é removido. Eventos para o seu código: `ThrownDistraction.LureStarted` e `LureEnded` (por exemplo, para acender uma luz ou ligar uma dica).

---

## 5. Como montar

1. `Tools > Horror FPS > Items > Create Distraction Items`: cria os três itens em `Resources/HorrorFPS/Items`, com o equipamento e um modelo simples cada. O que já existe (pelo id, em qualquer pasta) **não** é tocado.
2. Para criar uma isca sua: crie um item no **Item Studio**, escolha o tipo `Arremessável` e aplique um dos botões de valores prontos; ajuste os campos da seção 4.
3. Para colocar no mapa use o `WorldItem` de sempre (ver [`HorrorFPS-Itens-Sanidade-Corpo`](HorrorFPS-Itens-Sanidade-Corpo.md)); com `SaveableEntity` no mesmo objeto o item some para sempre depois de pego.
4. **Já posto na demonstração** (`Tools > Horror FPS > Demo > Create Full Game Demo`): Salão (duas garrafas na mesa da chave e um rádio no canto sudoeste), Porão (um sinalizador perto da pilha), Túneis (duas garrafas e um sinalizador no laço) e Casa de força (duas garrafas na mesa do rádio e um sinalizador no canto). Na cena de teste do perseguidor (`Stalker > Create Stalker Test Scene`) os três ficam na mesa de armas.

---

## 6. Para o seu código

| Quero | Como |
|---|---|
| Jogar uma isca por código (um NPC, uma armadilha) | `ThrownDistraction.Launch(item, posição, velocidade, donoOuNulo)` |
| Saber o que está chamando agora | `ThrownDistraction.Alive` (cada um com `IsLuring`, `LureSecondsLeft`, `Position`, `Key`) |
| Uma isca fixa na cena (rádio da história) | `NoiseSourceTag` no objeto que emite o som (com `alwaysInvestigate` ligado se o monstro não deve aprender) |
| Ouvir as chamadas | `NoiseSystem.Emitted` filtrando `NoiseKind.Distraction`; `NoiseKind.Distraction` foi acrescentado **no fim** do enum (assets e cenas antigos continuam iguais) |

---

## 7. Limitações conhecidas

- Nada foi compilado nem jogado: os valores (raios, tempos, volumes) são uma primeira estimativa; teste na cena de teste do perseguidor e ajuste.
- Os sons e os modelos são provisórios (primitivas e áudio gerado).
- O monstro **aprende** repetições da mesma fonte; se isso atrapalhar a sua fase, use `lureAlwaysInvestigate`.
- Objetos jogados não são salvos e não podem ser pegos de volta.
- O objeto jogado usa física comum (`Rigidbody` com colisor esfera e colisão contínua): confirme em jogo que ele não atravessa colisores finos.
- Só um `AudioListener` ativo: o som do rádio e do sinalizador é ouvido a partir do primeiro jogador (limitação geral de áudio do projeto, ver [`HorrorFPS-Audio`](HorrorFPS-Audio.md)).
