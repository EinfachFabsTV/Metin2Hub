# Patchnotes

Was sich in M2Hub geaendert hat, neueste Version zuerst. Jede
veroeffentlichte Fassung steht hier; die Punkte sind nach **Neu**,
**Geaendert**, **Behoben** und **Entfernt** sortiert.

Diese Datei liegt der App bei und steht in den Einstellungen unter
„Was ist neu"; dieselben Punkte stehen in den Release-Notizen.

## 1.48.0

**Neu**

- Patchnotes erscheinen nach einem Update **einmal als Fenster beim Start** -
  beim ersten Start nach der Installation nicht.
- Knopf **„Stream-Start"** in der Seitenleiste: setzt „seit Stream-Start" auf
  jetzt, ohne Umweg ueber die Einstellungen. Er steht nur da, wenn eine
  Einblendung laeuft.

**Geaendert**

- Der **Schulden-Rechner** ist von „Rechner" nach **Goals** umgezogen und
  steht dort neben den Zielen - beide rechnen mit denselben Laeufen.
- Die **Einstellungen** sind wieder uebersichtlich: die langen Abschnitte
  (Startseite, Stream-Einblendung, Patchnotes) stecken in Aufklappern.

## 1.47.0

**Neu**

- **Goals** als eigener Bereich: ein Ziel in Won eintragen („1500w fuer X"),
  der Gewinn aus den Laeufen zaehlt sich von selbst dagegen. Weitere Ziele
  stehen als Warteschlange dahinter.
- Ausgaben und Zusatzeinnahmen je Ziel, in vier Arten: Standard-Push,
  Zusatz-Push, Zusatzausgabe und Zusatzeinnahme. Jeder Posten einzeln,
  mit Notiz und loeschbar.
- Was beim Erreichen eines Ziels geschieht, ist waehlbar: weiter mit
  Ueberschuss, weiter bei null, oder stehen bleiben.
- Zweite Stream-Einblendung `goal.html` fuer das aktive Ziel, mit einzeln
  abschaltbaren Teilen (Fortschritt, Netto-Rechnung, fehlende Laeufe,
  Truhen und Runs, aktiver Lauf).
- Diese Patchnotes - in der App unter Einstellungen und in jedem Release.

## 1.46.0 - 1.46.1

**Neu**

- Stream-Einblendung: M2Hub schreibt Schulden, Truhen und Runs nach
  `%AppData%\M2Hub\stream\` - als fertige `overlay.html` fuer eine
  Browser-Quelle und als einzelne `.txt` fuer Text-Quellen. Kein Server,
  kein Port.
- Der Zeitraum fuer Truhen und Runs ist waehlbar: heute, seit Stream-Start
  oder gesamt.

**Behoben**

- Bau-Fehler: der Stream-Dienst wurde den Einstellungen nicht durchgereicht.

## 1.44.0 - 1.45.1

**Neu**

- **Schulden-Rechner** im Bereich Rechner: wie viele Laeufe fehlen, bis eine
  Schuld bezahlt ist. Truhen je Lauf aus den eigenen Eintraegen oder von
  Hand, Preis als Spanne oder als Durchschnitt aus der Statistik.
- Der Stand des Rechners bleibt gespeichert; ein Fortschrittsbalken zeigt,
  wie weit es ist.

**Geaendert**

- Enter bestaetigt in allen Feldern und gibt das Feld frei - die naechste
  Taste aendert nicht mehr nachtraeglich, was gerade eingetragen wurde.

## 1.41.0 - 1.43.1

**Geaendert**

- Der Truhenpreis gehoert zum **Tag**, nicht mehr zum Lauf: jeder Eintrag
  merkt sich den Preis, der an seinem Tag galt. Monatssummen stimmen damit
  auch, wenn der Preis sich aendert.
- Ueber Monat und Gesamtzeit steht statt des Eingabefelds der
  **Durchschnittspreis je Truhe**, nach Truhen gewichtet.
- Der Preis darf Nachkommastellen haben; „56,25" und „56.25" gehen beide.

**Behoben**

- Eintraege aus aelteren Fassungen folgten weiter dem Preis des Laufs;
  sie bekommen beim Laden einmalig ihren Preis eingeschrieben.
- Laeufe ohne Truhen liessen sich nicht eintragen.

## 1.36.0 - 1.40.0

**Neu**

- Monatsauswahl in der Statistik, mit Suche.
- Tage mit Eintraegen stehen gruen im Kalender.

**Geaendert**

- Die Eintraege stehen als Kacheln je Tag statt als lange Liste; sie gehen
  nicht mehr von selbst auf.
- Die Kacheln unter der Statistik zaehlen den gewaehlten Tag.
- Die Abklingzeit startet nur noch auf Knopfdruck.
- Einstellungen zweispaltig.

## 1.27.0 - 1.35.0

**Neu**

- **Run Tracker** mit sechs Laeufen, Statistik in drei Zeitraeumen,
  Abklingzeit und eigenem Tageswaehler.
- Startseite aus selbst gewaehlten Abschnitten, Seitenleiste links.
- Gilden-Rechner.

**Behoben**

- Das Fenster wird von OBS als Fensteraufnahme erkannt.
