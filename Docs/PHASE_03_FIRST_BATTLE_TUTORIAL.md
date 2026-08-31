# Phase 03 — Primo tutorial di combattimento

La Fase 3 trasforma `SCN_W01_L01_Tutorial` dalla preview visiva della Fase 2 in un combattimento a turni giocabile. Tutta la gerarchia visiva viene creata e salvata nell'Editor prima del Play Mode; il runtime controlla soltanto stato, animazioni, testi e cambi di scena.

## Flusso

`SCN_MainMenu` carica il tutorial con **INIZIA**. Il tutorial presenta sette messaggi brevi in italiano, abilita inizialmente soltanto **ATTACCO** e poi lascia disponibili i quattro comandi. L'eroe è a sinistra e il nemico a destra, con i piedi sulla stessa linea del terreno.

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
| Danno nemico | 12 |
| Riduzione Guardia | 6 |
| Cooldown Tecnica | 2 azioni completate |
| Bonus Marchio | ×1,5 sul prossimo colpo |

- **ATTACCO** infligge danno base.
- **TECNICA** infligge più danno e poi entra in cooldown.
- **GUARDIA** riduce il prossimo danno nemico senza annullarlo.
- **MARCHIO** potenzia il prossimo Attacco o la prossima Tecnica e si consuma.

Il nemico tutorial annuncia e usa sempre Attacco. Registra esclusivamente le azioni già completate dal giocatore; quando riconosce una ripetizione può mostrare `Il nemico ti sta osservando`. Non legge il comando corrente e non usa machine learning.

## Esito

Quando gli HP nemici raggiungono zero appare **VITTORIA**. Quando gli HP di Hero01 raggiungono zero appare **SCONFITTA**. In entrambi i casi i comandi vengono bloccati e il gioco torna a `SCN_MainMenu` automaticamente dopo circa 2,5 secondi oppure subito tramite **TORNA AL MENU**.

## Authoring e verifica

1. Aprire il progetto con Unity `6000.5.5f1`.
2. Eseguire **Tools > Veyra > Tutorial > Create First Battle Tutorial**.
3. Eseguire nuovamente lo stesso comando e verificare che non vengano creati duplicati.
4. Eseguire **Tools > Veyra > Tutorial > Validate First Battle Tutorial**.
5. Provare in Play Mode il flusso `Menu → INIZIA → tutorial → VITTORIA/SCONFITTA → Menu`.

I valori numerici sono serializzati sul controller della scena e restano modificabili dall'Inspector. Il modello `TutorialBattleState` non dipende da Unity e viene verificato dal validatore Editor senza introdurre Assembly Definition o nuovi pacchetti.
