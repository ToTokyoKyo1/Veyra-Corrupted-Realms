# Phase 03 — Primo tutorial di combattimento

La Fase 3 trasforma `SCN_W01_L01_Tutorial` dalla preview visiva della Fase 2 in un combattimento a turni giocabile. Tutta la gerarchia visiva viene creata e salvata nell'Editor prima del Play Mode; il runtime controlla soltanto stato, animazioni, testi e cambi di scena.

## Flusso

`SCN_MainMenu` carica il tutorial con **INIZIA**. Il tutorial presenta una sola informazione alla volta e guida, nell'ordine, **ATTACCO**, **GUARDIA**, **TECNICA** e **ANALIZZA**. Durante ogni prova resta attivo soltanto il comando richiesto. L'eroe è a sinistra e il nemico a destra, con i piedi sulla stessa linea del terreno.

Ogni turno segue questa sequenza:

1. il giocatore sceglie un comando;
2. l'eroe completa l'azione;
3. il nemico, se ancora vivo, esegue l'attacco annunciato;
4. il controllo ritorna al giocatore.

## Regole iniziali

| Elemento | Valore tutorial |
|---|---:|
| HP Hero01 | 100 |
| HP Enemy01 | 100 |
| Danno Attacco | 20 |
| Danno Tecnica | 32 |
| Danno nemico | 25 |
| Protezione Guardia | 100% del prossimo colpo |
| Cooldown Tecnica | 2 azioni completate |
| Corruzione nemico | 70% |

- **ATTACCO** infligge danno base.
- **TECNICA** infligge più danno e poi entra in cooldown.
- **GUARDIA** consuma il turno, annulla il prossimo danno nemico e poi si consuma.
- **ANALIZZA** apre la scheda del nemico senza infliggere danni, consumare il turno o avviare il contrattacco.

Il bilanciamento insegna a combinare le azioni: ignorare Guardia e Tecnica può portare alla sconfitta, mentre usarle con criterio permette di vincere.

La scheda di **ANALIZZA** mostra soltanto nome, razza, percentuale di corruzione e stato emotivo. Nel tutorial i valori iniziali sono `Creatura Corrotta`, `Creatura delle Radici`, `70%` e `Arrabbiato`; sono configurabili dall'Inspector e la corruzione viene sempre limitata tra 0 e 100.

Il nemico tutorial annuncia e usa sempre Attacco. Registra esclusivamente le azioni di combattimento già completate dal giocatore; aprire **ANALIZZA** non entra nella cronologia. Quando riconosce una ripetizione può mostrare `Il nemico ti sta osservando`. Non legge il comando corrente e non usa machine learning.

## Esito

Quando gli HP nemici raggiungono zero appare **VITTORIA**. Quando gli HP di Hero01 raggiungono zero appare **SCONFITTA**. In entrambi i casi i comandi vengono bloccati e il gioco torna a `SCN_MainMenu` automaticamente dopo circa 2,5 secondi oppure subito tramite **TORNA AL MENU**.

## Authoring e verifica

1. Aprire il progetto con Unity `6000.5.5f1`.
2. Eseguire **Tools > Veyra > Tutorial > Create First Battle Tutorial**.
3. Eseguire nuovamente lo stesso comando e verificare che non vengano creati duplicati.
4. Eseguire **Tools > Veyra > Tutorial > Validate First Battle Tutorial**.
5. Provare in Play Mode il flusso `Menu → INIZIA → ATTACCO → GUARDIA → TECNICA → ANALIZZA → VITTORIA → Menu` e verificare separatamente anche l'esito di sconfitta.

I valori numerici sono serializzati sul controller della scena e restano modificabili dall'Inspector. Il modello `TutorialBattleState` non dipende da Unity e viene verificato dal validatore Editor senza introdurre Assembly Definition o nuovi pacchetti.
