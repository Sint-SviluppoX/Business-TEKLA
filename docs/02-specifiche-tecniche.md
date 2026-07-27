# Specifiche tecniche

## Creazione codici articolo da file FPpro/Tekla

La personalizzazione `BOCRGSOF` legge file CSV con nome:

```text
<codice_commessa>_<suffisso>.CSV
```

I CSV possono avere o non avere l'intestazione. Per il file offerta e per tutti
i file del pacchetto `TXJ/TXB/TXF/TXG/TXO/TXP`, la prima riga viene ignorata
solo quando è riconosciuta come intestazione. Il riconoscimento considera:

- il valore `TOL` nella prima colonna;
- oppure la presenza di almeno due etichette tipiche, per esempio
  `CATEGORIA`, `CODICE`, `DESCRIZIONE`, `NUMEROVOCE`, `QTA`,
  `CAMPO INSERIMENTO` e `DATA/ORA ESPORTAZIONE`.

Se questi indicatori non sono presenti, la prima riga viene importata come
riga dati.

Il valore della prima colonna (`F1`, rinominata `NomeCommessa`) viene sempre
ricavato dal prefisso del nome del file. L'eventuale valore presente nella prima
colonna del CSV non viene considerato attendibile e viene sostituito.

`CLHCRGSOF.CaricaPacchettoCSV` carica i file, mentre `CLFCRGSOF` compone e
crea gli articoli usando le regole dell'import da file separati di `BNHHIMEX`:

| Suffisso | Tabella interna | Tipologia | Composizione codice | Descrizione | Unità di misura |
|---|---|---|---|---|---|
| `TXB` | `BAR` | Materie prime profilate | `F2 + " " + F3 + " " + F6` | `F4` | `BAR` |
| `TXF` | `FITMENT` | Accessori | `F7` | `F8` | `PZ` |
| `TXG` | `GLASS` | Vetri | `F6 + " " + F8` | `F7` | `MQ` |
| `TXO` | `OPTION` | Dettaglio tagli/consumi dei profili TXB | Nessun nuovo articolo | — | — |
| `TXP` | `PANEL` | Pannelli | `F6` | `F7` | `MQ` |

Il file `TXB` costituisce l'anagrafica delle materie prime profilate. Il relativo
schema applicativo comprende tutte le colonne necessarie, incluso il codice
colore `F6`.

Il file `TXO` non genera articoli distinti né distinte intermedie. Le sue righe
descrivono i pezzi tagliati e vengono associate alla materia prima `TXB`
mediante serie e codice. Se nel `TXB` esistono più finiture per la stessa coppia
serie/codice, viene utilizzato anche il codice colore. Un'associazione mancante
o non univoca interrompe l'importazione.

Il consumo teorico, espresso in barre, è calcolato per ogni riga `TXO` come:

```text
NumeroPezzi * LunghezzaPezzo / LunghezzaBarra
```

Il consumo viene inserito direttamente nella distinta del prodotto finito e
nei fabbisogni dell'impegno di commessa. Non vengono creati gli articoli tecnici
legacy `BAR_OPTION` né le distinte artificiali `OPTION -> BAR_OPTION`.

I codici generati dalle cinque tipologie conservano gli eventuali spazi,
coerentemente con `BNHHIMEX`. Rimane attivo il limite massimo
`CLN__STD.CodartMaxLen`; un codice più lungo viene troncato e l'operazione viene
registrata nel log.

Le altre tipologie (`JOB`, `LIST` e le eventuali tipologie legacy)
mantengono le precedenti regole di composizione e normalizzazione.

## Collegamento tra offerta e filiera

La tabella `MOVOFF` contiene il campo custom:

```text
mo_hhnumvoce varchar(50) NULL
```

Nel `DataSet` applicativo il campo è esposto come `ec_hhnumvoce`.

Il campo è nullable sia su `MOVOFF` sia su `MOVORD`. Il valore `NULL` consente
di rappresentare righe non ancora collegate oppure righe aggregate prive di
una singola voce di origine. La generazione della filiera continua comunque a
richiedere una `NumeroVoce` valida sulle righe offerta attive con quantità
diversa da zero.

Durante l'importazione dell'offerta, `BOCRGSOF` salva in questo campo il valore
della terza colonna, corrispondente a `NumeroVoce`. L'associazione viene
utilizzata solo dopo avere verificato che il codice commessa dell'offerta
coincida con nome e contenuto del `TXJ`.

Quando viene generata la filiera:

- codice prodotto e struttura tecnica provengono dai file Tekla;
- descrizione, quantità, larghezza, lunghezza e colore dell'impegno cliente
  provengono dalle righe correnti dell'offerta;
- i fabbisogni vengono moltiplicati per:

```text
quantità offerta / MoltiplicatoreStrutture TXJ
```

Le righe eliminate dall'offerta non vengono elaborate. Le righe con quantità
zero non generano righe di impegno né fabbisogni e possono avere
`ec_hhnumvoce` nullo. La procedura si blocca se una riga attiva con quantità
diversa da zero:

- non contiene `ec_hhnumvoce`;
- duplica una `NumeroVoce` già presente;
- indica una `NumeroVoce` assente nel `TXJ`;
- trova la stessa `NumeroVoce` più volte nel `TXJ`.

Le offerte importate prima dell'introduzione del campo devono essere reimportate
oppure valorizzate manualmente.

Il campo `mo_hhnumvoce` è presente anche su `MOVORD`:

- sulle righe dell'impegno cliente `R` contiene la `NumeroVoce` collegata;
- sulle righe dell'ordine di produzione `H` contiene la `NumeroVoce`
  collegata;
- sulle righe aggregate dell'impegno di commessa `#` resta `NULL`, perché una
  riga può aggregare fabbisogni provenienti da più voci e non esiste quindi un
  collegamento univoco.

## Documenti generati

Per ogni commessa la procedura completa la seguente filiera:

| Documento | `tipork` | Conto | `tipobf` | Contenuto |
|---|---|---:|---:|---|
| Impegno cliente | `R` | conto ricavato dal lead, con fallback configurato | `1` | prodotti finiti secondo l'offerta corrente |
| Impegno di commessa | `#` | conto ricavato dal lead, con fallback configurato | `1` | fabbisogni materiali aggregati |
| Ordine di produzione | `H` | `9019999` | `3` | prodotti finiti secondo l'offerta corrente |

La presenza dei tre documenti viene controllata separatamente per commessa. Se
un'elaborazione precedente ne ha già creato uno, il documento esistente non
viene duplicato e vengono generati soltanto quelli mancanti.

Il conto `9019999` e il `tipobf = 3` dell'ordine `H` vengono ripristinati anche
dopo la validazione della testata e immediatamente prima del salvataggio, così
eventuali ricalcoli standard di Business non possono sostituirli con il conto
associato al lead.

## Aggiornamento delle distinte base

Articoli e distinte vengono elaborati a ogni generazione della filiera, anche
quando i documenti `R`, `#` e `H` risultano già presenti. Per ogni prodotto
finito `TXJ`:

- se la distinta non esiste, viene creata;
- se esiste, viene aperta e viene generata una nuova versione;
- i componenti ricavati dal pacchetto corrente vengono inseriti nella nuova
  versione;
- la nuova versione viene impostata come versione corrente.

I profili del `TXO` sono collegati direttamente agli articoli materia prima
creati dal `TXB`; il `TXO` è un dettaglio di taglio e consumo, non una sorgente
di semilavorati.

Se non è possibile creare e salvare la nuova versione, l'elaborazione si
interrompe prima della generazione dei documenti mancanti.

## Controllo di coerenza del pacchetto

Il codice commessa della prima colonna del file usato per importare l'offerta
viene salvato in `TESTOFF.et_riferim`. Tutte le righe dello stesso file offerta
devono riportare il medesimo codice.

Prima di generare la filiera, `BOCRGSOF` verifica che `et_riferim` coincida con:

1. il prefisso del nome `<codice_commessa>_TXJ.CSV`;
2. la prima colonna di tutte le righe dati del `TXJ`.

La prima riga è esclusa dal controllo soltanto se viene riconosciuta come
intestazione; in caso contrario viene controllata come normale riga dati. Una
discordanza o un codice vuoto interrompono la procedura prima della creazione
di commessa, articoli, distinte e documenti.

Prima di creare il primo documento vengono ricavate e validate sia le righe
dell'impegno cliente `R` sia quelle aggregate dell'impegno di commessa `#`. In
questo modo l'assenza di fabbisogni non lascia il solo impegno `R` creato
parzialmente.
