# Patchnotes

Was sich in M2Hub geaendert hat, neueste Version zuerst. Jede
veroeffentlichte Fassung steht hier; die Punkte sind nach **Neu**,
**Geaendert**, **Behoben** und **Entfernt** sortiert.

Diese Datei liegt der App bei und steht in den Einstellungen unter
„Was ist neu"; dieselben Punkte stehen in den Release-Notizen.

## 1.59.0

**Neu**

- **Truhen direkt im Timer-Fenster eintragen** (in den Einstellungen
  einschalten): unter den Timern steht je Lauf eine Reihe Knoepfe mit den
  Truhenzahlen, die er ueblicherweise abwirft - Hydra 0 bis 5, Razador,
  Nemere und Beran 8 bis 10, Jotun 17 bis 21, Schlangenrun ein Knopf mit 64.
  Ein Klick traegt den Lauf sofort fuer heute ein, ohne Tastatur; Run Tracker,
  Goals, Startseite und Einblendung ziehen mit.

## 1.58.0

**Neu**

- Ein vierter Ton **„Weich"** - tief und rund, mit langsamem Anschlag: faellt
  neben dem Spiel auf, ohne zu erschrecken. Er ist jetzt der Standard.

## 1.57.0

**Neu**

- Eigene Timer nehmen jetzt **Sekunden**: „5:30" sind fuenf Minuten dreissig.
  Eine blanke Zahl zaehlt weiter als Minuten.
- **Drei Toene zur Auswahl**: Glocke, Doppelton und Gong. Beim Umschalten
  hoerst du den Ton einmal.
- Die Timer-Dateien fuer OBS liegen in einem **eigenen Ordner** `timer` neben
  den Dateien der Einblendung; ein Knopf in den Einstellungen oeffnet ihn.

**Behoben**

- **Der Ton war oft nicht zu hoeren**: er wurde stillschweigend uebersprungen,
  sobald der Rechner gerade einen anderen Ton abspielte - und er war zu leise.
  Jetzt spielt er immer und deutlich lauter.

## 1.56.0

**Neu**

- **Ton am Ende einer Abklingzeit**: zwei weiche Glockentoene - hoerbar neben
  dem Spiel, aber kein Alarm. Abschaltbar in den Einstellungen; beim
  Einschalten hoerst du ihn einmal.

**Geaendert**

- Die **Einstellungen haben Reiter**: Allgemein, Streaming und Programm. Vorher
  stand alles untereinander, und wer die Sprache suchte, scrollte an der
  Stream-Einblendung vorbei.

## 1.55.0

**Geaendert**

- Das Timer-Fenster laesst sich **an der ganzen Flaeche** verschieben, nicht
  mehr nur an der schmalen Leiste oben.
- **Ein Klick schaltet um**: steht die Uhr, laeuft sie los; laeuft sie, haelt
  sie an. Vorher startete der zweite Klick sie neu.

**Behoben**

- Das Timer-Fenster hatte **schwarze Ecken**. Es zeichnet auf eine Flaeche,
  die nicht durchsichtig sein kann - die runden Ecken sind deshalb weg.

## 1.54.0 - 1.54.1

**Neu**

- **Abklingzeiten ueber dem Spiel**: ein kleines Fenster mit einem Knopf je
  Setup. Linksklick startet die Uhr, ein zweiter startet sie neu, Rechtsklick
  haelt sie an. Wer zwei Hydra-Chars hat, traegt in den Einstellungen „2" ein
  und bekommt zwei Knoepfe; dazu lassen sich eigene Timer mit eigenem Namen
  und eigener Zeit anlegen. Das Streamdeck wird dafuer nicht mehr gebraucht.
- Das Fenster liegt ueber allen Fenstern, laesst sich ziehen und merkt sich
  seine Stelle; ein Knopf in der Seitenleiste blendet es ein und aus.
- Fuer OBS schreibt M2Hub die Abklingzeiten im Sekundentakt mit: je Timer eine
  `.txt` mit der Restzeit, dazu `timer-aktiv`, `timer-alle`, `timer-naechster`
  und eine fertige `timer.html`, die sich selbst aktuell haelt.

## 1.53.0

**Neu**

- Jede Angabe der Goal-Einblendung liegt jetzt auch als eigene `.txt` im
  Stream-Ordner: Zielbetrag, Netto, Offenes, Ausgaben, Einnahmen, Fortschritt,
  Truhen, Runs, der aktive Lauf und die geschaetzten Restlaeufe. Wer die Karte
  in OBS selbst baut, muss nichts mehr nachrechnen.

**Behoben**

- In der Goal-Einblendung zaehlte ein Shop-Bestand als Ausgabe. Er wird jetzt
  wie im Bereich Goals gerechnet - abzueglich der Truhen, die ueber die Runs
  schon im Gewinn stehen.

## 1.52.1

**Neu**

- In der Shop-Hilfe steht die Markt-Seite jetzt mit Adresse und einem Knopf,
  der sie im Browser oeffnet - abtippen muss sie niemand mehr.

## 1.52.0

**Neu**

- **Hilfe-Knopf** beim Shop-Bestand: vier gezeichnete Schritte zeigen, wo der
  Gesamtwert des eigenen Ladens steht - Server waehlen, Verkaeufersuche mit dem
  Charakternamen, Laden-Symbol anklicken, Summe ablesen.

## 1.51.0

**Neu**

- **„Im Shop"** als eigener Posten bei den Zielen: was unverkauft im Shop
  liegt, zaehlt zum Ziel mit. Damit nichts doppelt zaehlt, traegt man darunter
  die Truhen ein, die im Betrag stecken - sie werden herausgerechnet, weil sie
  ueber die Runs schon im Gewinn stehen.
- Die Truhenzeilen gehen ueber **alle Laufarten**, nicht nur den aktiven Lauf,
  und der Preis je Zeile ist frei: leer nimmt den zuletzt hinterlegten Preis
  des Laufs, eine Zahl den Preis, zu dem tatsaechlich verkauft wird.

## 1.50.2

**Geaendert**

- Das Patchnotes-Fenster traegt jetzt „Patch Notes" als Ueberschrift, darunter
  die Version und erst dann den Inhalt. Die Versionszeile stand vorher doppelt.

## 1.50.1

**Behoben**

- Die **Abklingzeit sprang beim Eintragen auf Anfang**, wenn sie gerade lief.
  Sie misst den Lauf, nicht die Eingabe - und bleibt jetzt stehen.

## 1.50.0

**Neu**

- **Deckkraft der Stream-Einblendungen** in den Einstellungen frei einstellbar,
  von ganz durchsichtig bis ganz deckend. Gilt fuer beide Karten; die Schrift
  bleibt voll deckend, damit sie vor hellem Spielbild lesbar bleibt.

## 1.49.1

**Behoben**

- Das Patchnotes-Fenster war breiter als die Maske und wurde an den Raendern
  abgeschnitten.

**Geaendert**

- Die Aufklapper in den Einstellungen sehen aus wie der Rest der App: sie
  sind aus denselben Teilen gebaut, nicht aus dem mitgelieferten Expander.

## 1.49.0

**Geaendert**

- Die **Goal-Einblendung** ist jetzt eine flache Leiste in einer Zeile statt
  eines Stapels: Titel, Balken, dahinter die eingeschalteten Angaben durch
  Punkte getrennt. Sie nimmt deutlich weniger Platz und bleibt lesbar.
- Statt „noch offen" steht dort, wo es sich schaetzen laesst, **wie viele
  Laeufe noch fehlen**.
- Die **Schriftgroesse der Leiste** laesst sich in den Einstellungen in drei
  Stufen setzen - besser als in OBS kleinzuziehen.

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
