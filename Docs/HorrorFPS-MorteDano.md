# Morte e dano: cena de morte, marcas de dano e tela de morte

O que o jogador **vê e ouve** ao levar dano e ao morrer. Três peças, todas por jogador (cada metade da tela no split-screen tem a sua): a **cena de morte** (a câmera cai até o chão, o corpo tomba, o som some), as **marcas de dano** (borda vermelha, vida baixa, seta de onde veio o golpe) e a **tela de morte** (o "game over" mais pesado, com o motivo). Código em `Body/` (`DeathSequence`, `DeathCause`, `ProceduralAudio.Death`), `UI/` (`DamageHUD`, `GameOverScreen`, `SettingsPanel`), `Animation/` (`PoseDelta.bodyEuler`) e `Editor/DeathMenu.cs`; namespace `HorrorFPS`.

> Status: escrito e revisado no código, **ainda não compilado nem testado na Unity** (o ambiente onde foi feito não tem a Unity). Os sons (baque e zumbido da morte) são gerados por código e **ninguém ouviu ainda**; os tempos e graus abaixo são chutes razoáveis para você ajustar jogando. Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Como a morte acontece, do começo ao fim

1. O jogador chega a 0 de vida (`StatsComponent.Died` → `PlayerContext.IsDead`).
2. O `DeathSequence` do jogador espera `graceSeconds` (0,3 s) antes de começar. O módulo de reanimação ([`HorrorFPS-Reanimacao`](HorrorFPS-Reanimacao.md)) desfaz a morte logo depois que ela acontece e deixa o jogador "caído"; sem essa espera a cena de morte piscaria toda vez que alguém cai. Se `IsDead` voltou a falso nesse tempo, **nada acontece**.
3. A sequência toca por `fallSeconds` (0,85 s): a câmera cai até o chão, a cabeça tomba para o lado **contrário ao golpe**, o campo de visão fecha e há um quique curto ao bater. O corpo (visto pelos colegas) tomba para longe de quem bateu. O som do baque toca ao bater no chão e o coração e a respiração do jogador ficam em silêncio.
4. O `DamageHUD` do jogador escurece a tela, fecha a vinheta e escreve **VOCÊ MORREU**, o motivo e, se alguém ainda estiver de pé, "Os outros ainda estão de pé."
5. Quando **todos** os jogadores estão mortos, o `GameOverScreen` cobre a tela depois de 2,8 s (para a cena de cada um respirar) e libera os botões 1,5 s depois de aparecer. Se alguém foi reanimado nesse intervalo, a tela **não** aparece.
6. Se o jogador for **reanimado** depois (reviver, carregar o save), a câmera e o corpo voltam suavemente (o peso da pose cai a 1,6 por segundo) e os batimentos e a respiração são religados.

---

## 2. Como morreu (a legenda)

`DeathCause.Describe(DamageInfo)` escolhe o texto, nesta ordem:

| Ordem | Fonte | Exemplo |
|---|---|---|
| 1 | `DeathCause.Resolver` (um `Func<DamageInfo, string>` que o seu jogo define; vazio ou `null` passa para o próximo) | "Esmagado pela prensa." |
| 2 | `DeathCauseLabel` no objeto que causou o dano (ou num pai dele) | o monstro: "Pego pelo perseguidor." |
| 3 | o `DamageType` | `Fall` "A queda foi fatal.", `Fire` "Você queimou.", `Supernatural` "Algo levou você.", `Physical` "Os ferimentos foram graves demais.", outro "Você não resistiu." |

- **`DeathCauseLabel`** (menu de componente `HorrorFPS/Body/Death Cause Label`): um campo de texto. Ponha em monstros, armadilhas, vazamentos... qualquer coisa que cause dano. O menu de demonstração do perseguidor e "Add Stalker To Selected Object" já adicionam um ao monstro.
- A mesma causa aparece na tela do jogador e, no fim, na tela de morte (uma linha por jogador, "J1: ...", "J2: ..."; com um jogador só, sem rótulo).
- A direção do golpe vem do `DamageInfo`, nesta ordem: a posição do `source` (quem bateu), depois a `direction` do golpe, depois o `point` do impacto. Sem nenhum dos três, a cabeça tomba para um lado sorteado e o corpo cai para trás.

---

## 3. Cena de morte (`DeathSequence`)

Módulo do jogador (`[PlayerModule("Corpo", 5)]`, instalado sozinho no prefab). Mexe só na **imagem** da câmera (via `CameraFx`: não muda a posição nem a rotação reais, então a mira, a física e o que o jogador vê dos colegas não sofrem), então funciona igual com ou sem corpo.

| Campo | Padrão | Para quê |
|---|---|---|
| `Grace Seconds` | 0,3 | espera antes de começar (ver seção 1) |
| `Fall Seconds` | 0,85 | da queda até bater no chão |
| `Roll Degrees` | 68 | quanto a cabeça tomba de lado |
| `End Pitch` | -14 | para onde o olhar termina (negativo = olhando para o teto) |
| `Ground Clearance` | 0,24 | altura do olho acima do chão quando deitado (m) |
| `Fov Close` | 14 | graus que o campo de visão fecha |
| `Bounce` | 0,07 | quique ao bater (m) |
| `Collapse Body` | ligado | o corpo tomba (só rotação do corpo; é o que os colegas veem) |
| `Body Fall Degrees` | 86 | quanto o corpo tomba (90 = deitado) |
| `Yield To Death Clip` | ligado | se o Animator do corpo tem um clipe de morte, o tombo por código **não** é somado (ver seção 6) |
| `Play Sounds` | ligado | baque ao bater e zumbido grave que some |
| `Silence Heart And Breath` | ligado | desliga o `HeartbeatComponent` e o `BreathingComponent` do jogador morto |
| `Fall Noise Radius` | 12 | barulho do corpo caindo para o monstro (m; 0 = nenhum) |

A queda mede a distância até o chão (raio para baixo, ignorando os colisores do próprio jogador), então o olho cai do jeito certo em escada ou beirada. Com a opção **Efeitos de dano suaves** ligada, rolagem, quique, mergulho e fecho do campo de visão caem para 45% (seção 5).

API para o seu código: `IsActive`, `Elapsed`, `Weight` (0 a 1, o quanto a pose está aplicada), `HasLanded`, `Cause`, `TryGetHitOrigin(out Vector3)`, `FallSeconds` e os eventos `Started`, `Landed` e `Ended`.

Sons gerados em `Body/ProceduralAudio.Death.cs`: `BodyFall()` (0,8 s) e `DeathDrone()` (3,4 s).

---

## 4. Marcas de dano e tela de morte (`DamageHUD`)

Módulo de interface do jogador (`[PlayerModule("Interface", 5)]`; ele pede o `PlayerOverlay` e o `DeathSequence`, que entram sozinhos). Desenha na camada do próprio jogador.

| Elemento | Quando | Como |
|---|---|---|
| **Vida baixa** | abaixo de `Low Health From` (40% da vida) | vinheta vermelha que pulsa e fica mais forte quanto menos vida (até `Low Health Max Alpha`, 0,7) |
| **Pancada** | ao levar dano | borda vermelha curta, mais forte quanto mais dano (`Full Hit Damage` 35 = pancada máxima) |
| **De onde veio** | ao levar dano com fonte conhecida | marca em arco ao redor da mira apontando para a origem (`Mark Radius` 235 px lógicos, fica `Mark Seconds` 1,9 s). Até 6 marcas ao mesmo tempo; um golpe do mesmo lado (menos de 25° de diferença) reaproveita a marca. `Show Direction` liga e desliga |
| **Morte** | enquanto a `DeathSequence` toca | clarão no instante da queda, a tela escurece até 90%, vinheta vermelho-escura fecha, aparece **VOCÊ MORREU**, a causa e a dica de que os outros ainda vivem |

`Show Death Screen` desliga só a parte da morte (as marcas de dano continuam).

O `GameOverScreen` (que já existia) ficou mais forte: escurece para 93%, a vinheta fecha, o título entra grande (130 px) e assenta tremendo um pouco, a causa aparece e só depois os botões (carregar o último save, se houver, e menu principal) se acendem. Os botões só respondem depois de `ButtonsAt` para um clique no meio da cena não fechar a tela sem querer.

---

## 5. Opção "Efeitos de dano suaves" (acessibilidade)

Menu de Opções > uma linha nova, **Efeitos de dano suaves** (liga e desliga; `GameSettings.SoftDamageFx`, salvo em PlayerPrefs). O padrão é **desligado** (efeito cheio, que é o que um jogo de terror quer); quem se incomoda com flashes e balanço de câmera liga. Ligada:

| Onde | Efeito cheio | Suave |
|---|---|---|
| Clarão da pancada (`PlayerHUD`) | 0,6 de opacidade | 0,2 |
| Borda da pancada (`DamageHUD`) | 0,75 | 0,25 |
| Marcas de direção | 0,95 | 0,55 |
| Vida baixa | pulsa e fica mais forte | brilho fixo e mais fraco (sem pulsar) |
| Clarão de morte | clarão vermelho no instante da queda | sem clarão |
| Cena de morte (câmera) | rolagem, quique, mergulho da cabeça e fecho do campo de visão cheios | 45% disso (a câmera **ainda cai**, só com menos balanço) |
| Título da tela de morte | entra tremendo | entra sem tremer |

---

## 6. Corpo e clipe de morte

O corpo visto pelos colegas tomba por código (`PoseDelta.bodyEuler`, que o `PlayerBodyPresenter` aplica como rotação extra do corpo inteiro): funciona com qualquer corpo, inclusive o de teste. É uma queda dura e rígida; para uma queda de verdade use um clipe do Mixamo:

1. Baixe um clipe de morte (ex.: "Dying", "Death From The Front") e ponha na pasta de locomoção.
2. O nome com **"death"** ou **"dying"** é reconhecido como o papel novo **Morte (cair e ficar no chão)** (`LocoRole.Death`; vem antes de "fall" na busca, para "Falling Back Death" não virar queda no ar). Passo 1 da janela *Mixamo: Humanoid + Locomotion Controller* converte o FBX; o clipe toca **uma vez** e fica no último quadro.
3. Monte o controller de novo (*Create Player Animator Controller* ou o passo 2 da janela): o estado **Dead** passa a tocar o clipe. Sem clipe ele continua vazio (o corpo fica na pose parada).
4. O `DeathSequence` detecta o clipe sozinho (`Yield To Death Clip`) e **não soma** o tombo por código por cima.

Os braços e o item da primeira pessoa somem na morte: o `EquipmentController` guarda as duas mãos, cancela um arremesso carregado e não deixa usar nada enquanto o jogador está morto.

---

## 7. Testar sem esperar o monstro (menus em Play mode)

`Tools > Horror FPS > Player > Death Test` (só funciona com o jogo rodando). O alvo é o jogador **selecionado** na Hierarchy (ou um filho dele); sem seleção, o jogador 1. Se você selecionar **outro objeto da cena** (o monstro, uma luz, um cubo) ele vira a **fonte do golpe**: a marca de direção, o lado da queda e o texto de causa (`DeathCauseLabel`) passam a usar esse objeto.

| Menu | O que faz |
|---|---|
| `Hurt Player (30)` | 30 de dano físico |
| `Kill Player` | dano enorme com ponto e direção (a cabeça tomba para o lado certo) |
| `Kill Player By Fall` | morte por queda, sem fonte (legenda "A queda foi fatal.") |
| `Kill All Players (Game Over)` | mata todos: a tela de morte inteira aparece 2,8 s depois |
| `Revive Player` | reanima com vida cheia (câmera e corpo voltam suavemente) |

Com o módulo de reanimação instalado, uma morte em co-op vira "caído" em vez de morte: para ver a cena completa, jogue com um jogador só ou use `Kill All Players`.

---

## 8. Receitas

**Dar uma causa de morte própria a uma armadilha:** adicione `DeathCauseLabel` ao objeto que causa o dano e escreva "Esmagado pela prensa." no campo.

**Causa que depende da situação:** no seu jogo, uma vez só (ex.: num `Awake` de um gerenciador):

```csharp
DeathCause.Resolver = info =>
    info.source != null && info.source.CompareTag("Trap") ? "Preso na armadilha." : null;   // null cai nas regras seguintes
```

**Reagir à morte** (som da história, câmera de espectador, estatística):

```csharp
DeathSequence seq = player.GetComponentInChildren<DeathSequence>();
seq.Started += s => Debug.Log("J" + (player.PlayerIndex + 1) + " morreu: " + s.Cause);
seq.Landed  += s => { /* o corpo bateu no chão */ };
```

**Cena de morte mais longa ou mais seca:** `Fall Seconds` e `Roll Degrees` no `DeathSequence` do prefab do jogador.

---

## 9. Cuidados

- **Não afeta a mira nem a física.** Tudo é só na imagem e no modelo do corpo; o `CharacterController` do jogador morto continua onde estava.
- **Graça de 0,3 s.** Quem usa um módulo que desfaz a morte (reanimação, "segunda chance") precisa fazê-lo dentro desse tempo para a cena de morte não aparecer; se for depois, a cena toca e volta (suave).
- **Desmarcar o `DeathSequence`** (o componente no prefab; ele é exigido pelo `DamageHUD` e não sai) desliga a queda de câmera, a do corpo e a cena escura da morte: o `DamageHUD` só tem a parte da morte ligada a ele, então as marcas de dano e a vida baixa continuam. O `GameOverScreen` continua funcionando, só sem a legenda de causa.
- **Tela de morte.** O `GameOverScreen` só aparece com **todos** os jogadores mortos; com mais de um jogador, a morte de um é só a cena + a legenda "Os outros ainda estão de pé."
- **Sons provisórios.** `BodyFall` e `DeathDrone` são gerados por código; troque pelos seus em `ProceduralAudio.Death.cs` ou mande o clipe pelo `SoundEmitter`.
