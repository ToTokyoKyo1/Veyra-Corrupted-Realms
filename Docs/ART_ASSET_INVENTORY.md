# Veyra — inventario integrazione grafica

## Regole di provenienza

Gli originali restano invariati in `C:\Users\rilom\OneDrive\Desktop\Veyra`.
Le copie integrali di lavoro sono in `Assets/_Veyra/SourceArt/UserProvided`; gli asset usati dal gioco sono copie separate nelle cartelle `Art` e `Audio`.

Non erano presenti file di licenza o attribuzione nelle tre cartelle consegnate. Prima di una distribuzione pubblica occorre conservare o recuperare la licenza associata ai pacchetti originali.

## Hero01

- Origine: `Hero/idle` e `Hero/walk`.
- Runtime: `Assets/_Veyra/Art/Sprites/Characters/Hero01/UserProvided`.
- Import: Sprite Multiple, Point, Clamp, mipmap disattivati, non compresso, 32 PPU, pivot in basso al centro.
- Animazioni create: Idle (10 frame) e Walk da battaglia (prima riga coerente, 4 frame).
- Limite dichiarato: non esistono frame Attack, Guard, Technique, Hit o Death. L'attacco usa il movimento breve e gli effetti già presenti nel combattimento, senza inventare frame e senza applicare danni aggiuntivi.

## Knight

- Origine: `Nemici/Knight/Knight`.
- Runtime: solo file trasparenti `noBKG_` in `Assets/_Veyra/Art/Sprites/Enemies/World01/Knight/UserProvided`.
- Animazioni create: Idle, Attack, Death, Jump/Fall, Roll, Run e Shield.
- Impiego: un solo nemico terrestre compatibile del Livello 4. Gli altri ruoli, inclusi quelli in volo, restano segnaposto per non presentare falsi personaggi unici.

## UI

- Origine: `UI/ui.png` e `UI/icons_32x32.png`.
- Runtime: `Assets/_Veyra/Art/Sprites/UI/UserProvided`.
- Sono usati i pannelli/pulsanti senza testo dimostrativo e icone dedicate ad Attacco, Guardia, Tecnica e Analizza.
- La scritta dimostrativa `METROIDVANIA` e le etichette del pacchetto non vengono usate nel gioco.

## Audio

- Origine: `UI/sfx/sfx`.
- Runtime selezionato: `select.wav`, `confirmation.wav`, `save.wav`, `error.wav` in `Assets/_Veyra/Audio/SFX/UI/UserProvided`.
- Il feedback standard dei pulsanti usa `select.wav` a volume contenuto tramite riferimenti serializzati; nessun caricamento dinamico da `Resources`.
- I file lunghi o semanticamente non necessari non vengono collegati al runtime.

## Palette centrale

L'asset `Assets/_Veyra/Data/UI/VeyraThemePalette.asset` contiene:

- background `#090B15`
- panel `#14182E`
- secondary `#2C354D`
- border `#404973`
- disabled `#686F99`
- primary text `#F5FFE8`
- secondary text `#A3A7C2`
- info/save `#92E8C0`
- action/select `#FFAE70`
- danger/kill `#AD2F45`
- damage `#BD6A62`
- corruption/technique `#692464`

## Rigenerazione idempotente

In Unity, in Edit Mode: `Tools > Veyra > Visuals > Integrate Provided Art`.
Il comando reimporta gli asset con impostazioni coerenti, aggiorna clip/controller/prefab generati e rigenera le scene esistenti tramite le factory consolidate. Non crea livelli, eroi o meccaniche nuove.
