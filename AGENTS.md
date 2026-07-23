# AGENTS.md

## Scopo

Questo repository contiene la personalizzazione Business CUBE/Tekla/FPpro.

Tekla è una **personalizzazione di primo livello**. La struttura ammessa è:

```text
BN standard -> BO custom
```

I componenti custom ex-novo della commessa usano il prefisso `BNHH*`. In questo
progetto sono quindi modificabili esclusivamente i componenti applicativi
`BO*`, i componenti ex-novo `BNHH*` e gli altri file Tekla/FPpro esplicitamente
indicati in questo documento.

Non introdurre componenti `BP*` né logiche di secondo livello. Le convenzioni
generali di Business CUBE, inclusa la distinzione tra standard e custom, sono
raccolte nel documento condiviso
[`../standard-business-cube.md`](../standard-business-cube.md).

## Perimetro modificabile

Possono essere consultati e modificati:

- componenti ex-novo `BNHH*`;
- componenti di primo livello `BO*`;
- file specifici della personalizzazione Tekla/FPpro;
- documentazione nella cartella `docs`;
- file `.MODXP` della personalizzazione, se presenti;
- profili o procedure Import/Export collegati alla personalizzazione, se
  esplicitamente richiesto.

Prima di modificare un file, verificarne il ruolo. Se non appartiene con
certezza al perimetro sopra indicato, consultare
[`../standard-business-cube.md`](../standard-business-cube.md) e trattarlo
come sola consultazione fino a quando la proprietà custom non sia stata
accertata.

## Componenti principali della personalizzazione Tekla

- `BNHHIMDB`: importazione commessa FPpro/Tekla;
- `BNHHCOTK`: console controllo lavorazioni;
- profilo Import/Export `1010 - IMPORTA BOLLA`;
- procedure Import/Export `5001-5005`;
- campi custom su `MOVORD`:
  - `mo_hhislavo`;
  - `mo_HhCodMacchina`;
  - `mo_HhdatLavo`.

## Comportamento di BNHHIMDB

`BNHHIMDB` importa una commessa esportata da FPpro/Tekla e crea in Business
CUBE:

- articoli mancanti;
- commessa Business;
- distinta base;
- versioni distinta;
- eventuale ordine di produzione tipo `H`.

La procedura legge file CSV senza intestazione, con nomi nel formato:

```text
<codice_commessa>_<suffisso>.CSV
```

## Comportamento di BNHHCOTK

`BNHHCOTK` è una console manuale di controllo avanzamento produzione. Legge i
file generati dai macchinari nelle cartelle configurate; il nome del file deve
coincidere con il codice articolo.

Quando trova un file valido, aggiorna le righe `MOVORD` di tipo `H` impostando:

```text
mo_hhislavo = S
mo_HhCodMacchina = <macchina>
mo_HhdatLavo = <data/ora>
```

### Limite tecnico importante

`BNHHCOTK` aggiorna le righe `MOVORD` filtrando per codice articolo e tipo
documento `H`. Non filtra per singolo ordine, anno, serie, numero documento o
commessa.

Prima di modificare questa logica, leggere
[`docs/03-limiti-paletti-prerequisiti.md`](docs/03-limiti-paletti-prerequisiti.md).

## Documentazione: percorso, scopo e utilizzo

Tutta la documentazione di progetto è nella cartella [`docs`](docs). Prima di
analizzare o modificare codice, Codex deve aprire i documenti pertinenti usando
questa tabella come indice.

| Percorso | Scopo | Quando consultarlo o aggiornarlo |
|---|---|---|
| [`docs/00-obiettivo-commessa.md`](docs/00-obiettivo-commessa.md) | Contesto cliente, problema, obiettivi, perimetro e risultato atteso | Per comprendere il perché della personalizzazione; aggiornare se cambia il perimetro funzionale |
| [`docs/01-gestione-operativa.md`](docs/01-gestione-operativa.md) | Flusso utente da FPpro/Tekla a Business, file, importazione, controlli e log | Per modifiche al flusso operativo o alle istruzioni per gli utenti |
| [`docs/02-specifiche-tecniche.md`](docs/02-specifiche-tecniche.md) | Componenti, classi, tabelle, campi, CSV e logiche tecniche | Prima di intervenire sul codice; aggiornare quando cambia il comportamento tecnico |
| [`docs/03-limiti-paletti-prerequisiti.md`](docs/03-limiti-paletti-prerequisiti.md) | Prerequisiti, vincoli, assunzioni e rischi noti | Sempre prima di cambiare codifiche, quantità, distinte, ordini o console lavorazioni |
| [`docs/04-installazione-configurazione.md`](docs/04-installazione-configurazione.md) | Installazione, database, tipi documento, numerazioni, cartelle e configurazioni | Per attività di distribuzione/configurazione; aggiornare se cambiano requisiti o impostazioni |
| [`../standard-business-cube.md`](../standard-business-cube.md) | Regole generali condivise Business CUBE: standard NTS, livelli di personalizzazione, form/entity/DAL, mapping e configurazioni | Per riconoscere componenti standard, comprendere il framework e progettare estensioni senza alterare lo standard |

I documenti specifici Tekla prevalgono sulle indicazioni generali di
`../standard-business-cube.md` quando descrivono una scelta locale esplicita. La
regola di primo livello definita in questo `AGENTS.md` è sempre vincolante.

## Linee guida per le modifiche

Prima di modificare codice:

1. leggere i documenti pertinenti nell'indice precedente;
2. confermare che il file sia un componente custom `BNHH*` o `BO*`;
3. non modificare componenti standard NTS;
4. mantenere la personalizzazione al primo livello e non creare `BP*`;
5. preferire modifiche isolate nei componenti custom;
6. mantenere compatibilità con Business CUBE SR8 CU4, salvo indicazioni diverse;
7. non cambiare codifiche articoli senza verificare l'impatto su distinta,
   ordine e console;
8. non cambiare i tipi documento `H`, `Z`, `Y` senza verificare la
   configurazione cliente;
9. non cambiare i campi custom `MOVORD` senza verificare `.MODXP` e database;
10. aggiornare nello stesso intervento la documentazione interessata.

## Stile della documentazione

Mantenere la documentazione chiara anche per chi non conosce il progetto.
Preferire sezioni brevi, tabelle, esempi concreti, nomi esatti di componenti,
tabelle e campi e avvertenze esplicite sui limiti tecnici.

Evitare modifiche implicite o silenziose ai comportamenti legacy. Il progetto
nasce in un contesto cliente poco standardizzato: ogni cambio apparentemente
piccolo può avere effetti su articoli, distinte, ordini e magazzino.
