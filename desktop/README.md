# M2Hub Desktop

Native Desktop-App für M2Hub – **ohne WebView und ohne mitgelieferten Browser**.
Die Oberfläche ist [Avalonia](https://avaloniaui.net/) (C# / .NET 9), gezeichnet
wird direkt über Skia. Es gibt kein HTML, kein Electron, kein Chromium und
keinen System-WebView.

Umfang: **Accounts**, **Events** (global und serverspezifisch) und
**Itemshop**. Keine Rechner, kein Admin-Bereich.

Die App ist **eigenständig**: kein Server, kein Konto, keine Anmeldung, keine
Registrierung. Alles liegt lokal im Nutzerprofil.

## Woher die Daten kommen

| Bereich | Quelle | Ablage |
|---|---|---|
| Accounts, Charaktere, Gilden, Schnellwahl | nur lokal, von Hand gepflegt | `accounts.json` |
| Events (global) | Forum-Board 1167 „News - Events“ | `cache.json` |
| Eventkalender je Server | Forum-Thread 90381 „Eventkalender“ | `cache.json` |
| Itemshop | Forum-Board 303 „News - Itemshop“ | `cache.json` |

Geladene Forum-Daten werden **sieben Tage** aufbewahrt und danach beim nächsten
Start verworfen. Solange etwas im Speicher liegt, funktioniert die App auch
ohne Netz.

### Wann geladen wird

- einmal beim Start des Programms,
- danach alle **fünf Minuten**, solange das Programm läuft,
- zusätzlich fest um **18:01** und **18:02** (Berliner Zeit),
- jederzeit über **„Jetzt laden“** in der Kopfzeile.

### Schonender Abruf

Die App stellt die Forum-Requests selbst, deshalb gelten dieselben Bremsen wie
wie früher im Backend (`Veraltet/server/utils/board-sync.ts`) — **nicht ohne Rücksprache lockern**:

1. Die Board-Übersicht wird **bedingt** abgerufen (ETag/Last-Modified) — der
   Fünf-Minuten-Takt kostet in aller Regel nur ein `304 Not Modified`.
2. Nur Seite 1 der Übersicht.
3. Thread-Seiten ausschließlich für **unbekannte** Thread-IDs.
4. Höchstens **5** neue Threads je Lauf, der Rest folgt beim nächsten.
5. **1,5 s** Pause zwischen zwei Requests.
6. Bei 429/5xx ein Cooldown mit exponentiellem Backoff (15 min bis 12 h).
7. Der Eventkalender wird höchstens alle vier Stunden geladen.

Die Parser sind die Portierung von `Veraltet/shared/utils/scraper/` nach C#
(`Services/Forum/Html.cs`, `EventCalendar.cs`, `Board.cs`). Ändert sich dort
etwas, muss es hier nachgezogen werden. HTML wird nie gerendert — die App zeigt
ausschließlich den Text der Beiträge.

## Bilder zu den Events

Der Eventkalender im Forum wird von Hand geschrieben. Die Namen weichen deshalb
ständig ab – Abkürzungen („Konz. Lesen“), andere Wortwahl
(„Mondlichtschatzkisten“ statt „Mondlicht-Schatztruhe“) und schlichte
Tippfehler („Kleine Segnug“). Ein Vergleich auf Gleichheit findet so gut wie
nichts.

`Services/EventIcons.cs` ordnet die mitgelieferten Bilder aus `Assets/Events/`
deshalb in vier Stufen zu, jeweils auf einer Vergleichsform (klein, ohne
Satzzeichen, Umlaute ausgeschrieben, Klammerzusatz entfernt):

| Stufe | greift bei | Wert |
|---|---|---|
| exakter Treffer auf Name oder Alias | „Kleine Segnung“ | 1,00 |
| ein Name steckt im anderen | „Cor Draconis“ → „Cor Draconis Roh“ | 0,90 |
| Wortabgleich, inkl. Abkürzung und Tippfehler je Wort | „Konz. Lesen“, „Kleine Segnug“ | bis 0,95 |
| Jaro-Winkler über die zusammengezogene Form | „Segensschriftrole“ | bis 0,93 |

Genommen wird der beste Treffer ab **0,82**; darunter bleibt die Zelle ohne
Bild. Lieber kein Symbol als ein falsches – ein falsches ist im Kalender sofort
irreführend.

Die letzte Stufe ist doppelt gesichert: sie greift erst ab 0,92 Ähnlichkeit und
nur, wenn auch die Wörter zueinander passen. Ohne diese Bremse würde „Elixier
des Lebens“ auf „Elixier des Mondes“ zeigen – für den bloßen
Buchstabenvergleich fast dasselbe, gemeint aber etwas anderes. Füllwörter
(„des“, „der“, „B“) zählen dabei nicht mit.

Ein neues Bild kommt nach `Assets/Events/` (ASCII-Dateiname) und bekommt einen
Eintrag im Katalog in `EventIcons.cs`; taucht im Forum regelmäßig eine andere
Schreibweise auf, wird sie dort als Alias ergänzt.

## Accounts

Zweispaltig statt der langen Liste aufklappbarer Karten:

- **Zweispaltig**: links die Accounts (mit Charakter- und Medaillenzahl), rechts
  die Charaktere des gewählten Accounts.
- **Suche über beides** – Accountname, Notiz *und* Charakternamen.
- **Gildenfilter** auf die rechte Tabelle.
- **Zeilenweises Speichern**: geänderte Zeilen sind erkennbar, der
  Speichern-Knopf ist nur bei echten Änderungen aktiv.
- **Schnellwahl-Knöpfe** (`+12`, `+21`, …) direkt an jeder Zeile, im gleichen
  Fenster editierbar.
- **Sammelvergabe** mit Rückfrage, wahlweise auf eine Gilde begrenzt.
- **Gildenverwaltung** (anlegen, umbenennen, Level, löschen) in derselben Ansicht.

Löschvorgänge fragen immer nach.

## Herunterladen

Fertig gebaut, ohne selbst etwas zu installieren:

**<https://github.com/EinfachFabsTV/Metin2Hub/releases/latest>** →
`M2Hub.exe`

Die Downloads liegen in einem eigenen **öffentlichen** Repo, der Quellcode
bleibt im privaten Hauptrepo. Nur so kann die App ohne Zugangsschlüssel nach
Aktualisierungen sehen – ein eingebauter Schlüssel wäre ein Schlüssel für
jeden, der die Exe bekommt.

Die Datei ist self-contained (kein .NET noetig) und wird von
`.github/workflows/desktop-build.yml` bei jeder Aenderung unter `desktop/` neu
gebaut und dort ersetzt. Dasselbe Ergebnis haengt auch am jeweiligen
Actions-Lauf unter „Artifacts“.

Beim ersten Start meldet sich der SmartScreen-Filter, weil die Exe nicht
signiert ist – „Weitere Informationen“ → „Trotzdem ausführen“.

## Bauen

> Hinweis: In der Umgebung, in der dieser Code entstanden ist, war kein
> .NET-SDK verfügbar (der Download ist dort netzseitig gesperrt). Der erste
> `dotnet build` auf deinem Rechner ist also der erste überhaupt – kleinere
> Anpassungen (Paketversionen, einzelne Bindings) können dabei anfallen.

Voraussetzung ist das **.NET 9 SDK** (https://dotnet.microsoft.com/download).

```bash
cd desktop/M2Hub.Desktop
dotnet restore
dotnet run
```

Fertiges Windows-Programm als eine einzelne Datei:

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Das Ergebnis liegt unter
`bin/Release/net9.0/win-x64/publish/M2Hub.exe`. Für Linux bzw. macOS
`-r linux-x64` oder `-r osx-arm64` setzen – derselbe Code, kein Fork.

## Wo die Daten liegen

`%AppData%\M2Hub\settings.json` (Linux/macOS: entsprechendes Profilverzeichnis):

```
accounts.json   Accounts, Charaktere, Gilden, Schnellwahl
cache.json      Events, Eventkalender, Itemshop (max. 7 Tage)
images/         heruntergeladene Ankündigungsbilder
```

Zum Zurücksetzen genügt es, den Ordner zu löschen. `accounts.json` ist die
Datei, die man sichern sollte – alles andere lädt sich neu.

## Aufbau

```
Services/       LocalStore (Ablage), ForumService (Abruf + Drosselung), ImageCache
Services/Forum/ Portierte Parser: Html, EventCalendar, Board
Models/      Datenmodell der App
ViewModels/  Zustand und Logik je Bereich (INotifyPropertyChanged, ohne Framework-Zusatz)
Views/       XAML-Oberflächen, ein UserControl je Bereich
Styles/         Theme.axaml – die Tokens aus Veraltet/app/assets/css/main.css
```

Farben stehen ausschließlich in `Styles/Theme.axaml`; in den Ansichten werden
nur die Namen verwendet. Fehlt eine Farbe, wird sie dort ergänzt – analog zur
Regel für `main.css` in der alten Web-Version.

Das Programmsymbol entsteht aus `fav.png` im Projektstamm und liegt als
`Assets/m2hub.ico` (Fenster, Taskleiste, Exe) und `Assets/m2hub.png`
(Kopfzeile) bei.
