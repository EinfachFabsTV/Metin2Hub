# Patchnotes

Was sich in M2Hub geaendert hat, neueste Version zuerst. Jede
veroeffentlichte Fassung steht hier; die Punkte sind nach **Neu**,
**Geaendert**, **Behoben** und **Entfernt** sortiert.

Diese Datei liegt der App bei und steht in den Einstellungen unter
„Was ist neu"; dieselben Punkte stehen in den Release-Notizen.

## 1.79.2

**Neu**

- **Abklingzeiten überstehen einen Neustart:** Laufende Timer merken sich ihren
  Ablaufzeitpunkt und laufen nach einem Neustart der App einfach weiter. War die
  Zeit zwischenzeitlich schon um, steht der Knopf wieder bereit (ohne Ton).

## 1.79.1

**Neu**

- **Set „Nezeraths Kopf":** Das Set zeigt jetzt die enthaltenen Push-Ringe und
  Amulette (Ring der Freude, Segen der Macht, Nazar-Amulett, …) jeweils mit
  eigenem Icon und ihren Werten – vorher standen sie leer da.

**Geändert**

- **Auto-Res** hat jetzt ein eigenes Symbol auf der Helper-Kachel.
- Alle Event-Daten (Kostüm-Sets, Loot-Filter, Okey-Loot) neu eingelesen.

## 1.79.0

**Neu**

- **Auto-Res (neuer Helfer):** Setzt die Auflösung (`WIDTH`/`HEIGHT`) in der
  `metin2.cfg` deines Clients per **Preset** (z. B. „Meley Res", „Hydra Res",
  „Single Client"). Du wählst die Ziel-`metin2.cfg` einmal aus; beim Anlegen
  eines Presets wird die komplette Config als Vorlage gespeichert, und „Setzen"
  schreibt sie – nur mit geänderter Auflösung – zurück. Den Client startest du
  selbst; er übernimmt die Auflösung beim nächsten Start.

**Geändert**

- **Okey-Helfer aufgeräumt:** Der Truhen-Loot sitzt jetzt als schmale Spalte
  **rechts**, der Helfer selbst links. Der **Hilfe-Knopf** öffnet ein **Popup**
  (schließt beim Klick daneben) statt eines festen Abschnitts. Truhen-Icons
  oben rechts.

## 1.78.1

**Behoben**

- **Okey-Helfer kompakter:** Die Kartenpalette sitzt jetzt linksbündig und
  enger beieinander, die Loot-Truhen und ihre Sidebar stehen direkt
  nebeneinander, und der Bereich hat eine Maximalbreite – das Layout wirkt auf
  breiten Bildschirmen nicht mehr auseinandergezogen.

## 1.78.0

**Neu**

- **Okey-Helfer: Truhen-Loot:** Ein neuer goldener Knopf **„Loot"** listet die
  Truhen (Bronze/Silber/Gold, Sommer- und Winter-Version). Klickst du eine
  Truhe an, erscheint rechts eine Sidebar mit ihren Items und der
  **Drop-Wahrscheinlichkeit**, sortiert nach Seltenheit (kleinste % oben).
- **Truhen-Icons**: oben rechts zeigt der Helfer die zum Punktestand passende
  Truhe als Bild; das **Okey-Symbol** ziert die Helper-Kachel.

## 1.77.0

**Neu**

- **Helper-Bereich mit Okey-Helfer:** Neuer Sidebar-Eintrag „Helper" mit einer
  Startseite für kleine Event-Helfer. Erster Helfer ist der **Okey-Helfer**: Du
  setzt deine 5 offenen Karten wie im Spiel, der Helfer **markiert die beste
  Kombo grün** (werten) bzw. die wegzuwerfende Karte **rot** – berechnet aus den
  noch möglichen Punkten mit dem Restdeck, du musst nicht selbst entscheiden. Ein
  Klick wertet die Kombo oder wirft die Karte weg, **Punkte und Truhe** laufen
  oben rechts mit, und die **Chance auf Gold/Silber** rechnet sich **live** ab
  deinem aktuellen Stand. Ein **Hilfe-Knopf** erklärt das Event kompakt und zeigt
  die Punkte je Kombo an durchwechselnden Beispielkarten. Ist Silber/Gold nicht
  mehr erreichbar, weist der Helfer darauf hin, dass man beenden kann.
- **Neue Kostüm-Sets:** Der Katalog wurde um weitere Sets ergänzt (u. a. der
  Push-Ring „Nezeraths Kopf").

**Geändert**

- **Loot-Filter aufgeräumt:** Das **Filter-Fenster** ist vorerst als „bald"
  gekennzeichnet (wird noch gebaut). Außerdem zeigt der Loot-Filter zunächst
  alle Bereiche außer **Waffe, Rüstung, Helm und Sonstige Ausrüstung** – die
  kommen später dazu.

## 1.76.1

**Behoben**

- **Test-Updates nur in der Dev-Version:** Das Feld für den Dev-Token
  („Test-Updates (M2hubDev)") erscheint jetzt nur noch im Test-Build, nicht im
  offiziellen Release. Auch die Suche nach Test-Builds läuft nur dort.

## 1.76.0

**Neu**

- **Loot-Filter (neuer Bereich):** Tippe einen Item-Namen und sieh sofort, unter
  welche Kategorie es im Beuteoptionen-Fenster fällt (mit Bild), oder blättere
  den Kategoriebaum durch. Dazu ein **Filter-Fenster** als interaktiver Editor –
  Nachbildung der Beuteoptionen, standardmäßig alles an, dein Stand bleibt
  gemerkt. Die Item-Daten wachsen mit, sobald mehr Bilder nachgelegt werden.
  (Import/Export einer Char-Datei folgt später.)
- **Test-Updates (M2hubDev):** In den Einstellungen unter „Programm" lässt sich
  ein Zugangsschlüssel zum Dev-Repo eintragen. Ist einer hinterlegt, sucht die
  App auch nach dem neuesten Test-Build und tauscht sich auf Wunsch darauf aus –
  erkannt am Commit-Stand, nicht an der Versionsnummer. Der Schlüssel bleibt nur
  lokal auf dem Rechner.

**Behoben**

- **Timer-Ton bei mehreren Audio-Ausgaben:** Die Ausgabe läuft jetzt über
  WASAPI und folgt demselben Audiogerät wie deine anderen Programme. Vorher
  ging der Ton über den alten Windows-Weg immer aufs Standardgerät – wer
  mehrere Ausgänge nutzt, hörte dann nichts, obwohl alles richtig eingestellt
  war.
- **Katalog-Übersicht nutzt die Breite besser:** die Set-Karten skalieren jetzt
  mit der Fensterbreite. War das Fenster knapp zu schmal, brach bisher eine
  Spalte um und rechts blieb eine Leerfläche – jetzt werden die Karten etwas
  schmaler, statt die Spalte zu verlieren.

## 1.75.0

**Geändert**

- **Accounts-Bereich als Master-Detail:** links die Account-Liste (durchsuchbar,
  mit Filtern wie gehabt), rechts der gewählte Account mit seinen Charakteren
  (Level, Medaillen, Schnellwahl) – übersichtlicher als das bisherige
  Kachelraster.

**Neu**

- **Startseite überarbeitet:** oben ein Hero mit dem aktiven **Ziel als
  Fortschrittsring** (Erreicht / Offen / Ziel / noch X Läufe) und Kacheln für
  **Truhen & Läufe der Woche und des Monats**. Darunter die bisherigen,
  umschaltbaren Abschnitte. Der Ziel-Abschnitt ist ab-/anwählbar.

## 1.74.0

**Geändert**

- Die **Katalog-Karten** zeigen jetzt die Teile eines Sets als Reihe – Waffe,
  Rüstung, Frisur, Haustier, Reittier, Ring. Hat ein Slot mehrere Varianten
  (z. B. Atlantisfürst und Atlantiskrieger), **wechseln sie sich in der Vorschau
  ab**. Darunter die Teile-Stufen (2–6) zum Umschalten und die Effekte der
  gewählten Stufe.
- Der **Simulator**-Reiter ist vorerst deaktiviert („bald") – er wird noch
  überarbeitet.

**Intern**

- Im Release-Repo bleibt nur noch die **aktuellste Version** stehen; ältere
  Releases werden beim Veröffentlichen automatisch entfernt.

## 1.73.0

**Behoben**

- **Nazar-Amulett** hat jetzt Ring-Werte (u. a. +50 % EXP) und zählt damit im
  Simulator mit.
- **Weibliche Kostümteile** werden besser erkannt: Namensformen auf „-in"
  (Kriegerin, Königin, Fürstin …) gelten jetzt als weiblich und erscheinen nicht
  mehr unter „Männlich".

## 1.72.0

**Geändert**

- Der **Set-Simulator** wählt die Teile jetzt über **Kachel-Raster** statt
  Dropdowns: je Slot (Ring, Body, Frisur, Waffe, Pet, Reittier) ein Raster zum
  Anklicken, mit **Männlich/Weiblich-Umschalter** (für Body/Frisur) und Suche.
  Rechts bleibt das Ergebnis-Panel mit Gesamt-EXP und den aktiven Boni.
- **„Beste EXP"** optimiert jetzt für den **gewählten Push-Ring**; ist keiner
  gewählt, sucht es weiter über alle Ringe.

## 1.71.0

**Neu**

- **Set-Simulator** im Kostüme-Bereich (neuer Reiter neben dem Katalog). Für
  jeden der sechs Slots – Ring, Body, Frisur, Waffe, Pet, Reittier – wählt man
  ein Teil, und die **Gesamt-EXP rechnet sich live**: Eigen-EXP der Teile plus
  Set-Boni (höchste erreichte Stufe je Set, der Ring zählt mit). Der Knopf
  **„Beste EXP"** sucht die stärkste Kombination über Ring und bis zu zwei
  Fokus-Sets.
- Dafür liest der Importer jetzt auch die **Ring-Werte** (`<Ringname>.txt`, u. a.
  die EXP des Rings) ein.

## 1.70.0

**Neu**

- In den **Kostümen** springt ein runder Knopf unten rechts wieder ganz nach
  oben. Er erscheint erst, wenn man ein Stück gescrollt hat – praktisch in der
  langen „Alle"-Liste.

## 1.69.0

**Neu**

- Push-Ring **Ring der Freude** im Katalog.

**Geändert**

- Der Importer übernimmt die neue Quellstruktur: Kostümteile liegen jetzt in
  getrennten **`Body/`- und `Frisur/`-Unterordnern**. Die Zuordnung Body/Frisur
  ist damit zuverlässig, statt sie über den Dateinamen zu raten.

## 1.68.0

**Neu**

- Zwei weitere Push-Ringe im Katalog: **Lolli der Stärke** und **Lolli der
  Magie**. Beide teilen sich dieselben Sets (18), erscheinen aber als eigene
  Ringe mit eigenem Icon.

**Geändert**

- Der Importer liest jetzt auch einen Ordner, den sich **zwei Ringe teilen**
  (Ordnername „A & B" → zwei Ringe, gemeinsame Sets), und erkennt die
  Effekt-Dateien mit und ohne Bindestrich („Set-Effekt" / „Set Effekt").

## 1.67.0

**Geändert**

- Im Katalog gibt es jetzt **„Alle"** (standardmäßig aktiv): zeigt die Sets aller
  Ringe auf einmal; die einzelnen Ringe filtern weiterhin.
- Die **Suche** findet ein Set auch über seine **Teil-Namen** – tippe einen
  Waffen-Skin-, Pet-, Reittier- oder Kostümteil-Namen, erscheint das Set.
- Das **Vorschaubild** ist einheitlich das Body-Bild. In der Detailansicht stehen
  die Kostüm-Varianten in fester Reihenfolge: **Frisur (m/w), dann Body (m/w)**.
- Der 4. Slot zeigt **Waffen links, Pet und Reittier rechts**.
- Die Effektstufe ist nicht mehr fest „4": die Karte zeigt die **höchste
  vorhandene Stufe**, und der Importer liest beliebige „Set-Effekt N Teile"-Dateien
  (auch 5/6 …), sobald sie vorliegen.

## 1.66.0

**Neu**

- Neuer Bereich **Kostüme**: ein Katalog aller Push-Ringe und ihrer Sets. Oben
  stehen die Ringe (wählbar), eine **Live-Suche** filtert die Sets beim Tippen.
  Jedes Set zeigt die **Effektstufen** (2/3/4 Teile), die **Kostüm-Varianten**
  (Body/Frisur, m/w, Tiers) und den **4. Teil** (Reittier/Pet/Waffe) mit eigenen
  Werten.
- Die Set-Daten pflegt ein Importer (`desktop/tools/import-sets.sh`), der einen
  Ordnerbaum einliest; Bilder und Werte werden gebündelt mitgeliefert. Der
  EXP-Simulator über die Sets folgt später.

## 1.65.0

**Behoben**

- Der **Biologen-Rechner** rechnete die Akzeleratoren je Skip mit festen Werten.
  Richtig ist, dass sie am **Cooldown** hängen: wie oft man beschleunigen muss,
  bis der Grund-Cooldown (24 h, bei Lv 92/94 12 h) auf den gewünschten Wert
  fällt. Jetzt wie in der laufenden Referenz.

**Geändert**

- Die Preise im Biologen-Rechner sind nach Höhe eingefärbt: **Drachenmünzen rot**
  (teuerste), **im Angebot orange**, **Flash-Sale grün** (günstigste).
- Die **Lv-92/94-Reihe** bei „Akzeleratoren je Skip" erscheint nur noch, wenn
  die Auswahl eine solche Abgabe enthält.

## 1.64.0

**Geändert**

- Der **Pet-Rechner** stellt jetzt die richtige Frage: **Waffe +7 gegen
  Leckerli+** - welche weniger kostet und **ab welchem Level** sich das umkehrt
  (nach Stückzahl immer, nach Preis sobald Preise stehen). Das Köstliche
  Leckerli stand gleichrangig daneben, ist aber ein optionaler Weg über die
  Pet-EXP und jetzt so gekennzeichnet.
- In den Rechnern bestätigt **Enter** eine Eingabe, ebenso ein **Klick irgendwo**
  außerhalb des Feldes.

## 1.63.0

**Neu**

- Der **Pet-Rechner** im Bereich „Rechner". Zwei Teile: **Fütterbedarf** zeigt,
  wie viele Waffen +7, Leckerli+ oder Köstliche Leckerli ein Levelaufstieg
  kostet - mit Preisen auch, welcher Weg günstiger ist. **Petwerte** schätzt die
  Werte nach Pettyp, Level und Alter, für normale Level und Championlevel.
- Der **Biologen-Rechner**. Für einen Bereich von Abgaben rechnet er die
  nötigen Akzeleratoren, Drachenmünzen (regulär, im Angebot, Flash-Sale) und
  Elixier-Packs aus. Die Level-92/94-Abgaben zählen mit dem günstigeren Bedarf.
- Eine **Knopfreihe im Rechner-Bereich** wählt zwischen Gilde, Pet und Biologen.

**Geändert**

- Der Rechner-Bereich zeigte bisher nur den Gilden-Rechner; er fasst jetzt alle
  Rechner unter einer Knopfreihe zusammen.

## 1.62.0

**Entfernt**

- Der **Hilfe-Knopf beim Shop-Bestand**. Er zeigte einen Weg, den es so nicht
  gibt: weder die Markt-Seite (sie schliesst) noch das Laden-Fenster im Spiel
  nennen den Gesamtwert. Den rechnet man selbst zusammen - der Hinweis am Feld
  sagt das jetzt.

## 1.61.0

**Entfernt**

- Die Knoepfe zu **Wiki und Handelsglas** in der Kopfzeile. Die Seite dahinter
  schliesst, und ein toter Link ist schlechter als kein Link.

**Geaendert**

- Die **Hilfe zum Shop-Wert** fuehrt jetzt durch das Spiel statt ueber eine
  Internetseite: eigenen Laden anklicken, unten steht die Summe. Zwei Bilder
  statt vier, kein Link mehr.

## 1.60.1

**Geaendert**

- Die Beschreibungen (README) nennen jetzt auch Goals, die Abklingzeiten ueber
  dem Spiel, die Stream-Einblendungen und die Patchnotes - sie standen noch auf
  dem Stand von vor einem Dutzend Versionen.

## 1.60.0

**Neu**

- Die Truhen-Knoepfe im Timer-Fenster **blinken nach dem Klick kurz gruen** -
  so sieht man, dass der Lauf eingetragen ist, ohne ins Hauptfenster zu
  wechseln.

## 1.59.1

**Behoben**

- Die **Timer-Dateien wurden nie aufgeraeumt**: wer die Zahl der Setups
  verkleinerte oder einen eigenen Timer loeschte, behielt dessen `.txt` im
  Ordner - sie blieb auf der letzten Zeit stehen und zeigte im Stream etwas
  Falsches. Jetzt verschwindet mit dem Timer auch seine Datei.
- Ebenso weg sind die Dateien aus den Fassungen vor 1.57.0, die noch neben
  dem Ordner lagen. **Wer damals eine Browser-Quelle auf `stream\timer.html`
  gerichtet hat, stellt sie auf `stream\timer\timer.html` um** - seit dem
  Umzug in den eigenen Ordner wurde die alte Datei nicht mehr beschrieben.

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
