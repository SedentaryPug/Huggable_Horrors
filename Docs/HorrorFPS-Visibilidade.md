# Visibilidade do jogador (agachar e sombra também escondem)

Agachar já fazia o jogador **menos ouvido** (ver `HorrorFPS-Diario-Ruido.md`). Agora também o deixa **menos visto**: o Perseguidor demora mais para notá-lo e enxerga de menos longe. Sombra e escuridão ajudam, e a lanterna ligada atrapalha. Código em `Body/` (`PlayerVisibility`, `VisibilityZone`), `UI/` (`VisibilityHUD`) e `Stalker/` (sentidos); namespace `HorrorFPS`. O módulo do jogador é instalado sozinho: não precisa estar em cena.

> Status: escrito e revisado no código, **ainda não compilado nem testado na Unity** (o ambiente onde foi feito não tem a Unity). Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. O que o jogador vê

- **Indicador de visibilidade** no canto superior direito, logo abaixo do medidor de ruído: um nome (`Escondido`, `Pouco visível`, `Visível`, `Muito visível`), uma barrinha com o fator (a marca fina é o "normal") e o motivo embaixo do nome (`agachado · na sombra`, `lanterna ligada`...).
- Ele aparece **enquanto o jogador está agachado ou escondido** e por alguns segundos sempre que o nível muda (acendeu a lanterna, entrou numa sombra). **Opções > Indicadores de furtividade** liga e desliga os dois indicadores (ruído e visibilidade) de uma vez; vale para todos os jogadores deste computador.
- A dica **Agachar** agora explica as duas coisas (menos barulho, mais difícil de ver) e a dica **Lanterna** avisa que a luz também denuncia.

---

## 2. Como o fator é calculado

`PlayerVisibility` (um por jogador) guarda um **fator**: 1 = normal, abaixo de 1 = mais difícil de notar, acima de 1 = mais fácil.

> **fator = agachado × lanterna ligada × zona × modificadores** (dentro de um esconderijo é 0)

| Parte | Valor padrão | Observação |
|---|---|---|
| Agachado | × 0,55 (`Crouch Factor`) | lido direto do `PlayerMotionProbe` (o mesmo "agachado" do medidor de ruído) |
| Lanterna ligada | × 1,3 (`Lamp Factor`) | vale a lanterna **equipada e acesa** (flag `ligada`); sem pilhas apaga sozinha |
| Zona | `VisibilityZone` em volta do corpo | Escuridão 0,4, Sombra 0,65, Normal 1, Iluminado 1,3, Holofote 1,6 |
| Modificadores | `SetVisibilityModifier(chave, mult)` | capa escura, neblina, refletor... os valores se multiplicam |

O fator **sobe quase na hora** (levantou, acendeu a lanterna: `Rise Speed` 8/s) e **desce em um instante** (agachou, entrou na sombra: `Fall Speed` 1,5/s), para o agachar não ser um interruptor. O nível do indicador sai do fator: abaixo de `Low Below` (0,75) é `Pouco visível`, acima de `High From` (1,2) é `Muito visível`.

---

## 3. Como o Perseguidor usa

Na visão dele (`StalkerSenses`), com `escala` = fator do jogador pesado por `playerVisibilityWeight`:

- **Alcance**: `visionRange × Lerp(1, escala, visibilityRangeInfluence)`. Com a influência padrão de 0,5, agachado (0,55) ele passa de 14 m para uns 10,9 m no nível Calm.
- **Velocidade de notar**: a suspeita enche `× escala` mais devagar (ou mais depressa, com luz).
- Continuam valendo os fatores que já existiam: parado (`stillFactor` 0,6), correndo (`runningFactor` 1,25), cone de visão, distância, e os **3 raios** (olho, peito, pés). **Esconder-se agachado atrás de algo baixo** (um sofá, uma bancada) já funcionava sem este sistema: o agachar abaixa os três pontos e a parede baixa os tapa.
- `nearSenseRange` (1,8 m) e a **audição** não mudam: quem esbarra no monstro continua sendo notado, e o barulho dos passos segue as regras do ruído.

Exemplos no nível Calm (14 m, antes de distância, parado e correndo):

| Situação | Fator | Alcance | Ritmo de suspeita |
|---|---|---|---|
| Em pé, lugar comum | 1,00 | 14 m | 100 % |
| Agachado | 0,55 | ~10,9 m | 55 % |
| Em pé, na sombra | 0,65 | ~11,5 m | 65 % |
| Agachado e na sombra | 0,36 | ~9,5 m | 36 % |
| Agachado e parado na sombra | 0,36 × 0,6 | ~9,5 m | ~21 % |
| Em pé, lanterna ligada | 1,30 | ~16 m | 130 % |

Os níveis mais atentos (Hunting, Frenzy) têm `confirmSeconds` bem menor: agachar ajuda, mas não esconde de quem já está caçando.

### Ajustes por monstro (`StalkerProfile`, grupo *Visão e audição*)

| Campo | Para quê |
|---|---|
| `Player Visibility Weight` (1) | quanto o fator conta para este monstro. `0` ignora (um monstro que não depende de olhos); `1` por inteiro |
| `Visibility Range Influence` (0,5) | quanto o fator também muda o **alcance**. `0` só muda a velocidade de notar; `1` o alcance muda na mesma proporção |

---

## 4. Zonas de visibilidade (`VisibilityZone`)

`Tools > Horror FPS > Player > Create Visibility Zone` cria uma caixa (Box Collider **Trigger**, 4×3×4 m) no ponto que a Scene View olha. Ajuste o tamanho e escolha o `Preset` (Escuridão, Sombra, Normal, Iluminado, Holofote) ou `Custom` com o multiplicador.

- Ela **não muda a luz da cena**: só diz à IA como enxergar quem está dentro. Combine com uma luz de verdade (ou a falta dela) para o jogador *ver* a sombra.
- Zonas **aninhadas**: vale a de **menor volume** (uma poça de luz dentro de um corredor escuro).
- Vale o ponto do **centro do corpo** do jogador (o `CharacterController`); a caixa precisa cobrir essa altura.
- A caixa aparece colorida na Scene View (azul = esconde, amarelo = denuncia).
- Também existe `Tools > Horror FPS > Player > Create Noise Surface` (piso que abafa ou denuncia os passos, ver doc de ruído).

---

## 5. Em código

```csharp
PlayerVisibility vis = PlayerVisibility.Of(player);     // ou PlayerRef.Find<PlayerVisibility>(this)
float f = vis.Factor;                  // 1 normal, 0 escondido (suavizado)
VisibilityLevel nivel = vis.Level;     // Hidden / Low / Normal / High
bool agachado = vis.Crouching, lanterna = vis.LampOn;
vis.SetVisibilityModifier(this, 0.7f); // capa escura; 1 remove
vis.SetVisibilityModifier(this, 1f);
float z = VisibilityZone.MultiplierAt(ponto, ~0);       // multiplicador da zona em um ponto

// do lado do monstro:
float escala = brain.Senses.VisibilityScale(target);    // já pesado pelo perfil
float alcance = brain.Senses.VisionRangeFor(target, brain.Tune);
```

Um módulo do perseguidor com `ModifyVisibility(target, v)` ainda funciona e roda **depois** desta conta (o monstro cego à luz, por exemplo).

---

## 6. Depurar

No Editor e em Development Build, a visão de percepção (**F8** tudo, **F9** visão) mostra, em cada jogador, `visibilidade x0,55 (alcance 11 m)` quando o fator não é 1, e a linha passa a dizer "fora do alcance" com o alcance já encurtado. O cone desenhado é o do nível de atenção (sem encurtar).

### Se algo não funciona

| Sintoma | Causa provável |
|---|---|
| Agachar não muda nada no monstro | o jogador não tem o módulo (`Player Visibility`; instala sozinho se `PlayerModules.AutoInstall` está ligado), ou o perfil tem `Player Visibility Weight` = 0 |
| A zona não faz efeito | o Collider não cobre o **centro do corpo** do jogador, ou está desligado; veja o multiplicador no `Preset` (Normal vale 1) |
| O indicador não aparece | Opções > Indicadores de furtividade desligado, ou o jogador está em pé, em lugar comum e nada mudou nos últimos segundos |
| O monstro o vê agachado atrás de uma parede baixa | a parede é mais baixa que os 3 pontos do corpo agachado (olho ~1 m, peito, pés), ou a camada dela não está em `sightMask` |
| Com a lanterna ligada quase não muda | o fator só sobe até `Lamp Factor`; perto do monstro a suspeita já enche no máximo (a conta é limitada a 1) |

---

## 7. Arquivos

| Arquivo | O que faz |
|---|---|
| `Body/PlayerVisibility.cs` | módulo do jogador: fator, nível, modificadores (`[PlayerModule("Corpo", 5)]`) |
| `Body/VisibilityZone.cs` | zona de sombra/luz (Trigger), `MultiplierAt(ponto)` |
| `UI/VisibilityHUD.cs` | indicador na tela (`[PlayerModule("Interface", 7)]`) |
| `Stalker/StalkerSenses.cs`, `StalkerBridge.cs`, `StalkerProfile.cs` | a conta na visão do monstro (`VisibilityScale`, `VisionRangeFor`) e os dois campos do perfil |
| `Stalker/StalkerDebugView.cs` | texto de depuração |
| `Editor/StealthMenu.cs` | menus Create Visibility Zone / Create Noise Surface |

Alterados: `Core/PlayerModules.cs` (instala `PlayerVisibility` e `VisibilityHUD`), `Core/UIText.cs` (textos), `Core/GameSettings.cs` (o mesmo ajuste liga os dois indicadores), `Tutorials/TutorialDefaults.cs` (textos das dicas Agachar e Lanterna).

## 8. Limites conhecidos

- **Não há medição automática da luz da cena**: a lanterna e as `VisibilityZone` são regras. Uma sala escura sem zona vale como lugar comum para a IA.
- Só a lanterna **do próprio jogador** denuncia; luz de outros jogadores ou de objetos não entra.
- No co-op local, cada jogador tem o seu fator; o monstro pondera cada um separadamente.
- O fator não muda a rapidez com que o monstro **escolhe** quem perseguir (o Diretor); só o que os olhos dele percebem.
