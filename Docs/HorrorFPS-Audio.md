# Áudio: música e ambiente por área, passos por piso e rádio

Três coisas que dão corpo ao mundo: cada **área** do mapa toca a sua música e o seu ambiente (com fade ao mudar de uma para outra), cada **piso** tem o seu som de passo e há um **rádio** que conta a história com chiado e legenda. Tudo funciona **sem nenhum arquivo de áudio** (sons gerados por código) e aceita os seus clipes no lugar. Código em `Audio/` (zonas, diretor, rádio), `Body/` (`FootstepAudio`, `ProceduralAudio.Ambience.cs`) e `Editor/` (menus); namespace `HorrorFPS`. O módulo de passos do jogador e o diretor de áudio são instalados sozinhos: não precisam estar em cena.

> Status: escrito e revisado no código, **ainda não compilado nem testado na Unity** (o ambiente onde foi feito não tem a Unity). Os sons gerados por código são um ponto de partida para o jogo não ficar mudo: **ninguém ouviu ainda**, então ajuste os volumes de cada área ao ouvido. Se aparecer erro de compilação ou algo estranho em Play, mande o texto do erro.

---

## 1. Resumo

| O quê | Como | Quem decide |
|---|---|---|
| Música e ambiente da área | `AudioZone` (caixa Trigger no mapa) com um `AreaAudio` | o primeiro jogador vivo: onde ele está, aquela zona toca |
| Camada de tensão | cordas agudas por cima da música | sobe com o batimento dos jogadores |
| Sons ocasionais | rangidos e pancadas de vez em quando, de um lado qualquer | a área atual |
| Passos por piso | `FootstepAudio` (módulo do jogador) | `NoiseSurface` sob os pés > nome do material > piso padrão da área |
| Rádio | `RadioSet` (objeto interagível) | o jogador liga; ou um evento chama `Play("id")` |
| Volumes | **Opções > Áudio**: geral, música, ambiente e efeitos | `GameSettings`, salvo no computador |

---

## 2. Música e ambiente por área

### 2.1 Como montar

1. `Tools > Horror FPS > Audio > Create Level Audio (Global Zone)`: o som padrão do nível (vale na cena inteira). **Um por cena.**
2. `Tools > Horror FPS > Audio > Create Audio Zone`: uma caixa (Box Collider **Trigger**, 8×3×8 m) no ponto que a Scene View olha. Ajuste o tamanho para cobrir a área (de altura suficiente para pegar a **câmera** do jogador) e preencha `Area`.
3. Pronto. Ao entrar na caixa o som troca com fade; ao sair, volta para a zona de baixo (ou a global).

### 2.2 O que tem em `Area` (`AreaAudio`)

| Campo | Para quê |
|---|---|
| `Music` / `Generated Music` | o **seu** clipe (loop) ou, vazio, uma música gerada: `Calm`, `Uneasy`, `Dread` (ou `None`: só ambiente) |
| `Music Volume` | volume desta música (0 a 1), multiplicado pelo volume de **Música** das opções |
| `Tension` | o quanto o susto acende a camada de tensão por cima da música (0 = não acende) |
| `Ambience` / `Generated Ambience` | o **seu** clipe (loop) ou um gerado: `Room`, `Wind`, `Drips`, `Hum`, `Rumble` (ou `None`) |
| `Ambience Volume` | volume do ambiente, multiplicado pelo volume de **Ambiente e efeitos** das opções |
| `Occasional` / `Generated Occasional` / `Occasional Every` / `Occasional Volume` | sons que pipocam de vez em quando de um lado qualquer (seus clipes, ou rangidos e pancadas gerados). Intervalo sorteado entre os dois valores (padrão 12 a 30 s) |
| `Floor` | o piso padrão dos **passos** nesta área (madeira numa casa, grama lá fora): vale onde não há `NoiseSurface` nem material reconhecido |
| `Fade Seconds` | quanto demora a troca de música e ambiente (padrão 2,5 s) |

Um `AudioProfile` (`Assets > Create > HorrorFPS > Audio > Audio Profile`, ou o menu do Editor) guarda o mesmo `Area` num arquivo, para reaproveitar em várias zonas: arraste para o campo `Profile` da zona.

### 2.3 Regras das zonas

- **Quem decide**: o primeiro jogador vivo (menor índice), pela posição da **câmera** dele; o `AudioListener` do jogo fica nele no split-screen. Todos os jogadores ouvem a mesma música e o mesmo ambiente (sons de tela, 2D). Se todos morrem, a área não muda.
- **Aninhadas**: entre as zonas que contêm o jogador vence a de maior `Priority`; empatando, a de **menor volume** (uma sala dentro de um corredor).
- **Global**: `Global` marcado vale em toda a cena, não precisa de Collider, e **perde para qualquer zona que contenha o jogador**. Sem zona global, fora de qualquer zona fica em silêncio. Sem nenhum jogador (menu principal), só as globais contam: dá para ter a música do menu.
- **Formato**: a checagem é exata para Box, Sphere, Capsule e Mesh convexo; um Mesh Collider côncavo vale só pela caixa que o envolve.
- **Eventos**: `On Entered` / `On Exited` (UnityEvent) quando a zona passa a valer ou deixa de valer; `Stinger On Enter` toca um susto musical (acorde dissonante e pancada grave) ao entrar.
- A checagem roda 4 vezes por segundo (`Check Interval` do `AudioDirector`).

### 2.4 Camada de tensão

Por cima da música, cordas agudas e dissonantes tremem. O volume é

> `Tension (da área) × Max Volume (0,55) × smoothstep(0,35 → 1, maior batimento entre os jogadores vivos)`

e sobe devagar (0,5/s) e desce mais devagar (0,2/s). Só existe se a área **tem música**: uma área só com ambiente não acende a tensão. O batimento é o do `HeartbeatComponent` (o mesmo que o medo, o monstro por perto e os sustos mexem). Ponha o seu clipe em `Tension Clip` do `AudioDirector` (opcional: ponha um `AudioDirector` na cena Core).

### 2.5 Volumes

`Opções > Áudio` (menu principal e pausa) tem três controles, salvos no computador: **Volume geral** (vai no `AudioListener`: vale para o jogo todo), **Música** (música, tensão e susto musical) e **Ambiente e efeitos** (ambiente e sons ocasionais). O volume final de cada som é `volume da área × controle`.

---

## 3. Passos por piso

### 3.1 De onde vem o piso

A cada passo da `PlayerMotionProbe` (a mesma que decide o barulho que a IA ouve) o `FootstepAudio` olha o que está sob os pés, **nesta ordem**:

1. um **`NoiseSurface`** (o piso que você marcou): vale o tipo dele (`Preset`) e, se tiver, os clipes dele (`Footsteps`);
2. senão, o **nome do material e do objeto do chão** (opcional, `Infer From Material`): "madeira", "grama", "metal"... fazem o som sair certo sem marcar nada. Terreno (`TerrainCollider`) vale como grama;
3. senão, o **piso padrão da área** (`Area > Floor`); concreto se não há área.

| Tipo | Som (gerado por código) | Palavras que o nome do material/objeto reconhece |
|---|---|---|
| Carpet (tapete) | surdo e macio | carpet, carpete, tapete, rug |
| Grass (grama) | farfalhar | grass, grama, dirt, terra, soil, leaf, folha, moss, musgo |
| Wood (madeira) | oco, com ressonância | wood, madeira, parquet, plank, tabua, tábua, floorboard, deck |
| Concrete (concreto) | seco, um estalo | concrete, concreto, cement, cimento, tile, azulejo, ladrilho, asphalt, asfalto, brick, tijolo, stone, pedra |
| Gravel (cascalho) | grãos estalando | gravel, cascalho, pebble, sand, areia, rubble, entulho |
| Metal | ressoa e toca | metal, steel, aco, aço, iron, ferro, grate, grade, catwalk |
| Broken Glass (vidro quebrado) | estalos e tilintar agudo | shard, broken, quebrad, caco |
| Water (água) | espirro e bolhas | water, agua, água, puddle, poca, poça, mud, lama |

(A primeira palavra achada vale, na ordem água, vidro, tapete, grama, madeira, metal, cascalho, concreto. O nome do material é o do **Renderer** do chão; o do objeto é o do GameObject do Collider. `Floor` ou `Chão` sozinhos **não** dizem nada: caem no piso padrão da área.)

### 3.2 Seus próprios clipes (`FootstepSet`)

`Tools > Horror FPS > Audio > Create Footstep Set` cria o arquivo. Escolha o tipo de piso (`Surface`), ponha os clipes de `Steps` (sorteia um por passo, nunca o mesmo duas vezes) e de `Landings` (opcional: vazio usa um passo mais grave e mais forte), e ajuste `Volume` e `Pitch`. Dois jeitos de usar:

- na lista **`Sets`** do `FootstepAudio` do jogador: vale em **todo piso daquele tipo**;
- no campo **`Footsteps`** de um `NoiseSurface`: vale **só naquele piso** (uma passarela de metal com som próprio).

Tipos sem conjunto continuam com o passo gerado por código. O volume final do passo é `Volume (0,55) × ganho do piso (tapete 0,4 … metal 0,95) × força do passo (agachado fraco, correndo forte)`; o tom varia ±8 % a cada passo e um pé usa uma fonte de som, o outro usa a outra.

### 3.3 Pulo e pouso

`Jump And Land Sounds` (ligado): o pulo faz um passo leve e o **pouso** toca o som de pouso do piso, mais forte conforme a velocidade da queda.

### 3.4 O som e o barulho combinam?

Só o **som** segue a inferência pelo material. O **raio de barulho para a IA** continua vindo só do `NoiseSurface` (multiplicador do `Preset`): marcar o chão como "madeira" pelo nome do material **não** deixa o passo mais barulhento para o monstro por conta própria. Para o piso abafar ou denunciar de verdade, use `Tools > Horror FPS > Player > Create Noise Surface` (ver `HorrorFPS-Diario-Ruido.md`); o mesmo `Preset` dá o som e o barulho.

### 3.5 Split-screen

O jogo tem um `AudioListener` só. No modo `Auto` (padrão) o passo é som de tela com **um** jogador e vira **som do mundo** com dois ou mais: você ouve o parceiro andando perto (e longe, mais fraco). `Local2D` e `World3D` forçam um dos dois.

---

## 4. Rádio

### 4.1 Como montar

`Tools > Horror FPS > Audio > Create Radio` cria um cubo (Box Collider) com `RadioSet` e uma fala de exemplo. Ponha sobre uma mesa, troque o modelo (precisa só de um Collider) e escreva as falas em `Transmissions`:

| Campo da fala | Para quê |
|---|---|
| `Id` | nome da fala (para `Play("id")` e para lembrar o que já foi ouvida) |
| `Speaker` / `Text` | quem fala e a legenda |
| `Voice` | a sua gravação (opcional). **Sem ela**, toca uma voz abafada gerada por código, só como "tom de fala": a legenda é o que o jogador lê |
| `Seconds` | duração; 0 = a do clipe, ou calculada pelo texto (de 2,5 a 14 s) |
| `Once` | só toca uma vez |
| `Requires Variable` | variável da partida que precisa estar ligada para a fala poder tocar (ex.: `has_cellar_key`) |
| `Set Variable` | variável ligada quando a fala termina: libera um objetivo, uma porta, um evento |
| `On Finished` | UnityEvent ao terminar |

### 4.2 Como o jogador usa

- Rádio desligado: **Ligar rádio** (e, com `Play On Turn On`, já toca a próxima fala disponível).
- Ligado e com fala pendente: **Sintonizar** toca a próxima. Sem fala pendente, ou no meio de uma fala: **Desligar rádio**.
- Cada fala: "kshh" de abrir o canal, a voz com **efeito de rádio** (graves e agudos cortados, distorção leve) e "kshh" de fechar, com a **legenda** na tela de cada jogador vivo dentro de `Max Distance`.
- As falas tocam na ordem da lista, pulando as já ouvidas (`Once`) e as que dependem de uma variável ainda desligada.

### 4.3 Barulho, interferência e energia

- **Faz barulho**: ligar e cada início de fala emitem ruído (`NoiseKind.Voice`, `Noise Radius` 8 m) que o perseguidor pode ouvir. 0 = silencioso para a IA.
- **Interferência** (`React To Threat`): com um jogador **assustado** (batimento alto) a menos de `Threat Range` (14 m), o chiado sobe e a voz **pica e abafa** (queda de sinal). É o jeito de o rádio "sentir" o monstro sem conhecer o monstro: depende do batimento.
- **Energia** (`Power Variable`): variável da partida (a mesma das luzes). Sem energia o rádio não liga e desliga sozinho quando ela cai.
- **Roteiro**: `radio.Play("id")` toca uma fala **e liga o rádio sozinho** se estiver desligado (o clássico "o rádio ligou sozinho"). Num `UnityEvent` do Inspector (gatilhos, SmartEvents, botões) use `PlayTransmission` (com o id), `PlayNextTransmission`, `TurnOn`, `TurnOff`, `SetOn` ou `Stop`: os métodos com retorno e parâmetro opcional não aparecem na lista do Inspector.
- **Save**: ponha `SaveableEntity` no mesmo objeto: salva se está ligado e quais falas já foram ouvidas.

### 4.4 Qualquer voz como rádio (`RadioFx`)

Componente na própria `AudioSource` (ou `RadioFx.Apply(audioSource, qualidade)` em código): corta graves e agudos, distorce e, com qualidade baixa, "pica" a voz. Use o `Volume` do `RadioFx` em vez de `AudioSource.volume` (ele mexe no volume da fonte para as quedas de sinal). Serve para fala de cutscene, walkie-talkie de NPC, interfone.

---

## 5. Sons gerados por código

Tudo sai de `ProceduralAudio` (a mesma classe dos batimentos e respirações), gerado uma vez na primeira vez que é pedido e guardado. O `AudioDirector` gera os sons das zonas da cena **um por quadro** ao entrar em jogo, para não engasgar na hora de cruzar a porta.

| Grupo | Sons |
|---|---|
| Ambientes (loops de 8 s) | `Room` (ar parado e zumbido baixo), `Wind` (rajadas e assobio), `Drips` (gotas com eco), `Hum` (zumbido elétrico de 60 Hz), `Rumble` (grave com canos rangendo) |
| Músicas (loops de 8 s) | `Calm` (acorde aberto), `Uneasy` (segunda menor e trítono), `Dread` (graves em batimento) |
| Tensão e susto | camada de tensão (cordas agudas tremendo), `Stinger` (acorde dissonante + pancada, 2,6 s) |
| Sons ocasionais | `Creak(0..2)` (rangidos), `Thud`, `Scrape` |
| Passos | `Step(piso, 0..2)`, `Land(piso)`, para cada um dos oito pisos |
| Rádio | chiado em loop (3 s), "kshh" de abrir e fechar, dois bipes, voz abafada (4 variações) |

Os loops são **emendados** (sem estalo na volta). Para trocar por som de verdade, é só pôr o clipe no campo certo (`Music`, `Ambience`, `Occasional`, `Voice`, `Footstep Set`): quando há clipe seu, o gerado não toca.

---

## 6. Menus do Editor

| Menu | O que faz |
|---|---|
| `Tools > Horror FPS > Audio > Create Audio Zone` | zona de som por área (caixa Trigger) |
| `Tools > Horror FPS > Audio > Create Level Audio (Global Zone)` | o som padrão da cena |
| `Tools > Horror FPS > Audio > Create Radio` | rádio com uma fala de exemplo |
| `Tools > Horror FPS > Audio > Create Footstep Set` | arquivo de clipes de passo por piso |
| `Tools > Horror FPS > Audio > Create Audio Profile` | arquivo com o som de uma área, para reaproveitar |

Os arquivos criados vão para `Generated/Audio` (dentro da pasta do framework). Pisos: `Tools > Horror FPS > Player > Create Noise Surface`.

### O que a demonstração completa ganhou

`Tools > Horror FPS > Demo > Create Full Game Demo` agora monta: música e vento no **menu**; no **Salão** madeira, rangidos, música calma, um tapete e um **rádio** numa mesa com duas falas (a segunda só depois de pegar a chave); no **corredor** (atrás das portas) zumbido e música inquieta; no **Porão** gotejar, música sombria, susto musical ao descer e uma poça; no **Anexo** rumor, música inquieta e uma grade de metal; nos **Túneis** gotejar, música sombria, uma poça e uma grade; na **Casa de força** zumbido elétrico, música inquieta, piso de metal e outro rádio (ver `HorrorFPS-Iluminacao-Niveis.md`).

---

## 7. Em código

```csharp
// quem toca agora
AreaAudio atual = AudioDirector.CurrentArea;                   // nulo = silêncio
NoiseSurface.Preset piso = AudioDirector.DefaultFloor;
AudioDirector.AreaChanged += area => Debug.Log(area != null ? area.label : "silêncio");

// cenas especiais (perseguição, chefe): assume o som até soltar
AreaAudio chase = new AreaAudio();
chase.generatedMusic = MusicKind.Dread;
chase.generatedAmbience = AmbienceKind.None;
AudioDirector.Push(chase, this);        // o último Push vale
AudioDirector.Pop(this);                // volta ao normal (zona do jogador)
AudioDirector.Stinger();                // susto musical (segue o volume de Música)

// rádio
RadioSet radio = ...;
radio.Play("radio_aviso");              // liga sozinho se estiver desligado (num UnityEvent: PlayTransmission)
radio.SetOn(false); radio.Stop();
bool ligado = radio.IsOn, falando = radio.IsPlaying, temFala = radio.HasPending;
RadioFx.Apply(audioSource, 0.6f);       // uma voz qualquer virando rádio

// piso e volumes
NoiseSurface s = NoiseSurface.Nearest(pePosicao, ~0);          // o piso marcado sob os pés (ou nulo)
GameSettings.MusicVolume = 0.5f;        // 0..1; também MasterVolume e AmbienceVolume
AudioClip vento = ProceduralAudio.Ambience(AmbienceKind.Wind);
```

---

## 8. Se algo não funciona

| Sintoma | Causa provável |
|---|---|
| Nenhuma música nem ambiente | a cena não tem zona de áudio (ou o jogador não está dentro de nenhuma e não há zona global); a área tem `Generated Music`/`Generated Ambience` = `None` e nenhum clipe; ou os volumes das Opções estão em 0 |
| A zona não troca ao entrar | o Collider não cobre a **câmera** do jogador (altura!), não é Trigger/está desligado, ou outra zona de maior `Priority` também contém o jogador; um Mesh Collider côncavo só vale pela caixa que o envolve |
| A tensão nunca aparece | a área não tem música, `Tension` = 0, ou o batimento do jogador não passa de 0,35 (o monstro não está por perto/o jogador não se assusta) |
| Os passos soam todos iguais | não há `NoiseSurface` nem material reconhecido: cai no `Area > Floor`; marque o piso ou nomeie o material (3.1). Ou o jogo usa outro som de passo do FirstPersonController (desligue-o) |
| O passo toca em dobro | outro script já toca os passos do `FirstPersonController`; o `FootstepAudio` ouve o mesmo evento da `PlayerMotionProbe` |
| O passo é inaudível | volumes: `FootstepAudio > Volume`, o tipo (tapete é de propósito baixo) e o **Volume geral** das Opções |
| O rádio não liga | sem energia (`Power Variable` desligada) ou sem Collider; a dica mostra "Sem energia" |
| O rádio liga e fica só no chiado | não há fala disponível: todas `Once` já ouvidas, ou `Requires Variable` ainda desligada |
| A legenda não aparece | o jogador está além de `Max Distance`, está morto, ou o módulo de legendas (`SanitySubtitles`) não foi instalado |
| A voz do rádio parece "picada" | é a interferência (`React To Threat`): há um jogador assustado perto; desligue o campo se não quiser |

---

## 9. Arquivos

| Arquivo | O que faz |
|---|---|
| `Audio/AreaAudio.cs` | o "som de um lugar" (música, ambiente, ocasionais, piso, fade) |
| `Audio/AudioProfile.cs` | arquivo com um `AreaAudio` |
| `Audio/AudioZone.cs` | zona no mapa, `Find(ponto)`, eventos de entrada e saída |
| `Audio/AudioDirector.cs` | toca a área atual com fade, tensão, sons ocasionais, `Push/Pop`, susto musical |
| `Audio/FootstepSet.cs` | clipes de passo e pouso por piso |
| `Body/FootstepAudio.cs` | módulo do jogador: o passo do piso sob os pés (`[PlayerModule("Corpo", 6)]`) |
| `Audio/RadioSet.cs`, `RadioTransmission.cs` | rádio interagível e suas falas |
| `Audio/RadioFx.cs` | efeito de rádio em qualquer `AudioSource` |
| `Body/ProceduralAudio.Ambience.cs` | ambientes, músicas, tensão, rangidos, passos e kit de rádio gerados por código |
| `Editor/Audio/AudioMenu.cs` | menus `Tools > Horror FPS > Audio` |
| `Editor/HorrorFPSSetup.Audio.cs` | o áudio da demonstração |

Alterados: `Body/NoiseSurface.cs` (`Kind`, `Footsteps`, `Nearest`), `Body/PlayerMotionProbe.cs` (`FeetPosition`, `GroundMask`), `Body/ProceduralAudio.cs` (agora `partial`), `Core/GameSettings.cs` (três volumes), `Core/UIText.cs`, `UI/SettingsPanel.cs` (subpágina Áudio; as linhas ficaram um pouco mais baixas para caber), `Core/PlayerModules.cs` (instala `FootstepAudio`), `Editor/HorrorFPSMenu.cs` (área Audio).

## 10. Limites conhecidos

- **Tudo gerado por código é provisório**: nada foi ouvido ainda. Ajuste os volumes por área ao ouvido e troque por gravações quando tiver.
- Música e ambiente são **de tela**, iguais para todos os jogadores; não há mixagem por jogador (o jogo tem um `AudioListener` só). Quem decide a área é o primeiro jogador vivo.
- Não há **reverberação por ambiente** nem abafamento por parede/porta; é troca de zona, não propagação do som.
- Os ambientes gerados são loops de 8 s de ruído e tons: em fones, um olho atento pode perceber a repetição. O ambiente de verdade (clipe seu) resolve.
- A **voz do rádio sem gravação** é só um murmúrio com tom de fala; a legenda é o conteúdo.
- A interferência do rádio mede o **medo do jogador** (batimento), não a distância ao monstro.
- O som dos passos de **outros** jogadores usa o piso sob os pés **dele**, mas o piso padrão da área é o da zona do primeiro jogador vivo.
