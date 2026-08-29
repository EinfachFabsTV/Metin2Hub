using System.ComponentModel;
using System.Globalization;

namespace M2Hub.Desktop.Services;

/// Oberflaechentexte in allen unterstuetzten Sprachen.
///
/// Die Texte stehen fest im Programm - eine fehlende Datei kann es damit nicht
/// geben. Deutsch ist die Quelle; fehlt ein Schluessel in einer Sprache, wird
/// der deutsche Text gezeigt statt einer leeren Stelle.
///
/// Gebunden wird ueber den Indexer, damit ein Sprachwechsel sofort auf alle
/// Ansichten durchschlaegt, ohne dass das Fenster neu aufgebaut werden muss:
///
///     Text="{loc:T events.subtitle}"
///
/// Die Uebersetzungen ins Englische, Tuerkische und Italienische stammen nicht
/// von Muttersprachlern - sie gehoeren gegengelesen.
public sealed class Loc : INotifyPropertyChanged
{
    /// Reihenfolge wie in der Auswahlliste.
    public static readonly string[] Languages = ["de", "en", "tr", "it"];

    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["de"] = "Deutsch",
        ["en"] = "English",
        ["tr"] = "Türkçe",
        ["it"] = "Italiano",
    };

    public static Loc I { get; } = new();

    private string _language = "de";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// Aktuelle Sprache als Kuerzel.
    public string Language => _language;

    /// Sprache aus den Einstellungen setzen. "auto" folgt Windows.
    public void SetLanguage(string? code)
    {
        var next = Resolve(code);
        if (next == _language) return;

        _language = next;
        // Der leere Name erneuert jede Bindung auf den Indexer.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    /// "auto" oder etwas Unbekanntes wird zur Sprache von Windows, sonst Englisch.
    public static string Resolve(string? code)
    {
        if (!string.IsNullOrEmpty(code) && Languages.Contains(code)) return code;

        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Contains(system) ? system : "en";
    }

    public string this[string key] => Get(key);

    /// Text mit Platzhaltern: T("update.title", "1.5.0").
    public static string T(string key, params object[] args)
    {
        var text = I.Get(key);
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    private string Get(string key)
    {
        if (!Texts.TryGetValue(key, out var row)) return key;

        var index = Array.IndexOf(Languages, _language);
        if (index < 0) index = 0;

        // Fehlt die Uebersetzung, gilt der deutsche Text
        var value = row[index];
        return string.IsNullOrEmpty(value) ? row[0] : value;
    }

    /// Schluessel -> Texte in der Reihenfolge de, en, tr, it.
    private static readonly Dictionary<string, string[]> Texts = new(StringComparer.Ordinal)
    {
        ["nav.accounts"] = ["Accounts", "Accounts", "Hesaplar", "Account"],
        ["nav.events"] = ["Events", "Events", "Etkinlikler", "Eventi"],
        ["nav.itemshop"] = ["Itemshop", "Item Shop", "Nesne Market", "Item Shop"],
        ["nav.wiki"] = ["Wiki öffnen (metin2alerts.com)", "Open wiki (metin2alerts.com)", "Wiki’yi aç (metin2alerts.com)", "Apri il wiki (metin2alerts.com)"],
        ["nav.tradingGlass"] = ["Handelsglas öffnen (metin2alerts.com)", "Open trading glass (metin2alerts.com)", "Ticaret camını aç (metin2alerts.com)", "Apri il trading glass (metin2alerts.com)"],
        ["nav.calc"] = ["Rechner", "Calculators", "Hesaplayıcı", "Calcolatori"],
        ["nav.settings"] = ["Einstellungen", "Settings", "Ayarlar", "Impostazioni"],
        ["header.refresh"] = ["Jetzt laden", "Refresh now", "Şimdi yükle", "Aggiorna ora"],
        ["header.refresh.tip"] = ["Events und Itemshop sofort aus dem Forum laden", "Load events and item shop from the forum right away", "Etkinlikleri ve nesne marketi forumdan hemen yükle", "Carica subito eventi e item shop dal forum"],
        ["header.loading"] = ["lädt …", "loading …", "yükleniyor …", "caricamento …"],
        ["header.notLoaded"] = ["noch nicht geladen", "not loaded yet", "henüz yüklenmedi", "non ancora caricato"],
        ["header.lastLoaded"] = ["zuletzt geladen {0}", "last loaded {0}", "son yükleme {0}", "ultimo caricamento {0}"],
        ["window.minimize"] = ["Minimieren", "Minimise", "Simge durumuna küçült", "Riduci a icona"],
        ["window.maximize"] = ["Maximieren", "Maximise", "Ekranı kapla", "Ingrandisci"],
        ["window.close"] = ["Schließen", "Close", "Kapat", "Chiudi"],
        ["common.save"] = ["Speichern", "Save", "Kaydet", "Salva"],
        ["common.cancel"] = ["Abbrechen", "Cancel", "İptal", "Annulla"],
        ["common.delete"] = ["Löschen", "Delete", "Sil", "Elimina"],
        ["common.apply"] = ["Anwenden", "Apply", "Uygula", "Applica"],
        ["common.later"] = ["Später", "Later", "Daha sonra", "Più tardi"],
        ["common.edit"] = ["Bearbeiten", "Edit", "Düzenle", "Modifica"],
        ["common.name"] = ["Name", "Name", "Ad", "Nome"],
        ["common.level"] = ["Level", "Level", "Seviye", "Livello"],
        ["common.medals"] = ["Medaillen", "Medals", "Madalya", "Medaglie"],
        ["common.guild"] = ["Gilde", "Guild", "Lonca", "Gilda"],
        ["common.search"] = ["Suchen …", "Search …", "Ara …", "Cerca …"],
        ["common.openForum"] = ["Im Forum öffnen", "Open in forum", "Forumda aç", "Apri nel forum"],
        ["accounts.search"] = ["Account oder Charakter suchen …", "Search account or character …", "Hesap veya karakter ara …", "Cerca account o personaggio …"],
        ["accounts.sort"] = ["Sortierung", "Sort by", "Sıralama", "Ordinamento"],
        ["accounts.language"] = ["Sprache", "Language", "Dil", "Lingua"],
        ["accounts.manage"] = ["Verwaltung", "Manage", "Yönetim", "Gestione"],
        ["accounts.manage.tip"] = ["Gilden, Client-Sprachen, Schnellwahl und Sammelvergabe", "Guilds, client languages, quick buttons and bulk awards", "Loncalar, istemci dilleri, hızlı düğmeler ve toplu dağıtım", "Gilde, lingue del client, pulsanti rapidi e assegnazione multipla"],
        ["accounts.new"] = ["Account anlegen", "Add account", "Hesap ekle", "Aggiungi account"],
        ["accounts.newTitle"] = ["Neuen Account anlegen", "Add a new account", "Yeni hesap ekle", "Aggiungi un nuovo account"],
        ["accounts.edit"] = ["Account bearbeiten", "Edit account", "Hesabı düzenle", "Modifica account"],
        ["accounts.delete"] = ["Account löschen", "Delete account", "Hesabı sil", "Elimina account"],
        ["accounts.addChar"] = ["+ Char", "+ Char", "+ Karakter", "+ Pers."],
        ["accounts.deleteChar"] = ["Charakter löschen", "Delete character", "Karakteri sil", "Elimina personaggio"],
        ["accounts.moveUp"] = ["Nach vorn", "Move up", "Öne al", "Sposta avanti"],
        ["accounts.moveDown"] = ["Nach hinten", "Move down", "Arkaya al", "Sposta indietro"],
        ["accounts.col.char"] = ["Char", "Char", "Karakter", "Pers."],
        ["accounts.stats.medals"] = ["Medaillen gesamt", "Medals total", "Toplam madalya", "Medaglie totali"],
        ["accounts.stats.average"] = ["Durchschnitt pro Char", "Average per character", "Karakter başına ortalama", "Media per personaggio"],
        ["accounts.stats.accounts"] = ["Accounts", "Accounts", "Hesaplar", "Account"],
        ["accounts.stats.chars"] = ["Charaktere", "Characters", "Karakterler", "Personaggi"],
        ["accounts.stats.bio"] = ["Bio erledigt", "Bio done", "Bio tamamlandı", "Bio completata"],
        ["accounts.bio.done"] = ["Bio fertig", "Bio done", "Bio tamam", "Bio fatta"],
        ["accounts.bio.pending"] = ["Bio nicht fertig", "Bio not done", "Bio tamam değil", "Bio non fatta"],
        ["accounts.legend"] = ["Legende: Level grün = Meley-Char · Name rot = Grotte · * = Bio offen · „Bio“ = Bio erledigt", "Legend: green level = Meley char · red name = cave · * = bio open · “Bio” = bio done", "Açıklama: yeşil seviye = Meley karakteri · kırmızı ad = mağara · * = bio açık · “Bio” = bio tamam", "Legenda: livello verde = pers. Meley · nome rosso = grotta · * = bio aperta · “Bio” = bio fatta"],
        ["accounts.empty"] = ["Links einen Account auswählen oder einen neuen anlegen.", "Pick an account on the left or add a new one.", "Soldan bir hesap seç veya yeni bir tane ekle.", "Scegli un account a sinistra o aggiungine uno nuovo."],
        ["accounts.bulk"] = ["Sammelvergabe", "Bulk award", "Toplu dağıtım", "Assegnazione multipla"],
        ["calc.char.one"] = ["Charakter", "character", "karakter", "personaggio"],
        ["calc.char.many"] = ["Charaktere", "characters", "karakter", "personaggi"],
        ["calc.account.one"] = ["Account", "account", "hesap", "account"],
        ["calc.account.many"] = ["Accounts", "accounts", "hesap", "account"],
        ["calc.guild.title"] = ["Gilden-Rechner", "Guild calculator", "Lonca hesaplayıcı", "Calcolatore gilda"],
        ["calc.guild.subtitle"] = ["Spenden, Tapferkeitsmedaillen und Tage bis zum Ziel-Level.", "Donations, valour medals and days to the target level.", "Hedef seviyeye kadar bağışlar, cesaret madalyaları ve günler.", "Donazioni, medaglie del valore e giorni fino al livello obiettivo."],
        ["calc.guild.currentLevel"] = ["Aktuelles Gilden-Level", "Current guild level", "Mevcut lonca seviyesi", "Livello attuale della gilda"],
        ["calc.guild.targetLevel"] = ["Ziel-Gilden-Level", "Target guild level", "Hedef lonca seviyesi", "Livello obiettivo della gilda"],
        ["calc.guild.levelBadge"] = ["Level {0}", "Level {0}", "Seviye {0}", "Livello {0}"],
        ["calc.guild.donationType"] = ["Spendenart", "Donation type", "Bağış türü", "Tipo di donazione"],
        ["calc.guild.donation.small"] = ["Klein", "Small", "Küçük", "Piccola"],
        ["calc.guild.donation.medium"] = ["Mittel", "Medium", "Orta", "Media"],
        ["calc.guild.donation.high"] = ["Hoch", "High", "Yüksek", "Alta"],
        ["calc.guild.expSuffix"] = ["EXP", "EXP", "TP", "EXP"],
        ["calc.guild.donatingChars"] = ["Spendende Charaktere", "Donating characters", "Bağış yapan karakterler", "Personaggi che donano"],
        ["calc.guild.perDayNote"] = ["{0} Spenden je Charakter und Tag. Eine Gilde fasst höchstens {1} Mitglieder.", "{0} donations per character and day. A guild holds at most {1} members.", "Karakter başına günde {0} bağış. Bir lonca en fazla {1} üye alır.", "{0} donazioni per personaggio al giorno. Una gilda ospita al massimo {1} membri."],
        ["calc.guild.resultTitle"] = ["Ergebnis", "Result", "Sonuç", "Risultato"],
        ["calc.guild.resultSubtitle"] = ["Aufwand für Level {0} → {1}.", "Effort for level {0} → {1}.", "Seviye {0} → {1} için gereken çaba.", "Impegno per il livello {0} → {1}."],
        ["calc.guild.donationsFor"] = ["Spenden „{0}“", "“{0}” donations", "„{0}“ bağışları", "Donazioni «{0}»"],
        ["calc.guild.drPerAccount"] = ["Drachenmünzen je Account", "Dragon coins per account", "Hesap başına ejderha parası", "Monete del drago per account"],
        ["calc.guild.accountsNote"] = ["{0} à {1} Charaktere · {2} Scheine je Account", "{0} with {1} characters each · {2} tickets per account", "{0}, her biri {1} karakter · hesap başına {2} pusula", "{0} da {1} personaggi · {2} biglietti per account"],
        ["calc.guild.totalExp"] = ["Erforderliche Gesamt-EXP", "Total EXP required", "Gereken toplam TP", "EXP totali necessari"],
        ["calc.guild.ticketsPerChar"] = ["Scheine „Mittel“ je Charakter", "“Medium” tickets per character", "Karakter başına „Orta“ pusula", "Biglietti «Media» per personaggio"],
        ["calc.guild.donationsPerChar"] = ["Spenden je Charakter", "Donations per character", "Karakter başına bağış", "Donazioni per personaggio"],
        ["calc.guild.donationsPerAccount"] = ["Spenden je Account", "Donations per account", "Hesap başına bağış", "Donazioni per account"],
        ["calc.guild.daysRow"] = ["Tage ({0})", "Days ({0})", "Gün ({0})", "Giorni ({0})"],
        ["calc.guild.daysValue"] = ["~ {0} Tage", "~ {0} days", "~ {0} gün", "~ {0} giorni"],
        ["calc.guild.medals"] = ["Tapferkeitsmedaillen", "Valour medals", "Cesaret madalyaları", "Medaglie del valore"],
        ["calc.guild.drPerChar"] = ["Drachenmünzen je Charakter", "Dragon coins per character", "Karakter başına ejderha parası", "Monete del drago per personaggio"],
        ["calc.guild.totalCost"] = ["Kosten insgesamt", "Total cost", "Toplam maliyet", "Costo totale"],
        ["calc.guild.progress"] = ["Fortschritt zu Level {0}", "Progress to level {0}", "Seviye {0} ilerlemesi", "Progresso verso il livello {0}"],
        ["calc.guild.footnote"] = ["Nur der mittlere Spendenschein ist im Itemshop kaufbar. Der hohe Schein fällt als Beute, die kleine Spende kostet Yang.", "Only the medium donation ticket is sold in the item shop. The high ticket drops as loot, the small donation costs yang.", "Nesne markette yalnızca orta bağış pusulası satılır. Yüksek pusula ganimet olarak düşer, küçük bağış yang ister.", "Solo il biglietto di donazione medio è in vendita nell’item shop. Quello alto cade come bottino, la donazione piccola costa yang."],
        ["accounts.roles.title"] = ["Rollen vergeben", "Assign roles", "Rolleri ata", "Assegna ruoli"],
        ["accounts.roles.hint"] = ["Eine Rolle bei allen passenden Charakteren auf einmal setzen oder abnehmen.", "Set or remove a role on all matching characters at once.", "Bir rolü uyan tüm karakterlere aynı anda ver veya kaldır.", "Imposta o rimuovi un ruolo su tutti i personaggi corrispondenti in una volta."],
        ["accounts.roles.set"] = ["Setzen", "Set", "Ver", "Imposta"],
        ["accounts.roles.unset"] = ["Abnehmen", "Remove", "Kaldır", "Rimuovi"],
        ["accounts.roles.level"] = ["Level von / bis", "Level from / to", "Seviye – / –", "Livello da / a"],
        ["accounts.roles.scope.level"] = ["alle Charaktere Level {0} bis {1}", "all characters level {0} to {1}", "seviye {0} ile {1} arasındaki tüm karakterler", "tutti i personaggi di livello da {0} a {1}"],
        ["accounts.roles.scope.levelGuild"] = ["die Charaktere Level {0} bis {1} der Gilde „{2}“", "the level {0} to {1} characters of guild “{2}”", "„{2}“ loncasının seviye {0}–{1} karakterleri", "i personaggi di livello da {0} a {1} della gilda «{2}»"],
        ["accounts.roles.confirm.set"] = ["Rolle „{0}“ auf {1} setzen?", "Set role “{0}” on {1}?", "„{0}“ rolü {1} için verilsin mi?", "Impostare il ruolo «{0}» su {1}?"],
        ["accounts.roles.confirm.unset"] = ["Rolle „{0}“ bei {1} abnehmen?", "Remove role “{0}” from {1}?", "„{0}“ rolü {1} için kaldırılsın mı?", "Rimuovere il ruolo «{0}» da {1}?"],
        ["accounts.roles.done"] = ["{0} Charaktere angepasst.", "{0} characters updated.", "{0} karakter güncellendi.", "{0} personaggi aggiornati."],
        ["accounts.bulk.scope"] = ["Wer bekommt sie", "Who receives them", "Kimler alacak", "Chi le riceve"],
        ["accounts.bulk.scope.all"] = ["Alle Charaktere", "All characters", "Tüm karakterler", "Tutti i personaggi"],
        ["accounts.bulk.scope.donate"] = ["Nur Spenden-Chars", "Donation chars only", "Yalnızca bağış karakterleri", "Solo personaggi donazioni"],
        ["accounts.bulk.title"] = ["Medaillen vergeben", "Award medals", "Madalya dağıt", "Assegna medaglie"],
        ["accounts.bulk.confirm"] = ["{0} Medaillen auf {1} anwenden?", "Apply {0} medals to {1}?", "{0} madalya {1} için uygulansın mı?", "Applicare {0} medaglie a {1}?"],
        ["accounts.bulk.scope.allGuild"] = ["die Charaktere der Gilde „{0}“", "the characters of guild “{0}”", "„{0}“ loncasının karakterleri", "i personaggi della gilda «{0}»"],
        ["accounts.bulk.scope.donateGuild"] = ["die Spenden-Chars der Gilde „{0}“", "the donation chars of guild “{0}”", "„{0}“ loncasının bağış karakterleri", "i personaggi donazioni della gilda «{0}»"],
        ["accounts.bulk.scope.allChars"] = ["alle Charaktere", "all characters", "tüm karakterler", "tutti i personaggi"],
        ["accounts.bulk.scope.donateChars"] = ["alle Spenden-Chars", "all donation chars", "tüm bağış karakterleri", "tutti i personaggi donazioni"],
        ["accounts.bulk.done"] = ["{0} Charaktere angepasst.", "{0} characters updated.", "{0} karakter güncellendi.", "{0} personaggi aggiornati."],
        ["accounts.bulk.needAmount"] = ["Bitte einen Betrag angeben.", "Please enter an amount.", "Lütfen bir miktar girin.", "Inserisci un importo."],
        ["form.needName"] = ["Bitte einen Namen angeben.", "Please enter a name.", "Lütfen bir ad girin.", "Inserisci un nome."],
        ["accounts.bulk.hint"] = ["Medaillen auf viele Charaktere gleichzeitig anrechnen.", "Add medals to many characters at once.", "Madalyaları birçok karaktere aynı anda ekle.", "Aggiungi medaglie a più personaggi in una volta."],
        ["accounts.languages"] = ["Client-Sprachen", "Client languages", "İstemci dilleri", "Lingue del client"],
        ["accounts.languages.hint"] = ["Der Accountname erscheint in dieser Farbe. Farbwert als #RRGGBB.", "The account name appears in this colour. Colour value as #RRGGBB.", "Hesap adı bu renkte görünür. Renk değeri #RRGGBB olarak.", "Il nome dell’account appare in questo colore. Valore come #RRGGBB."],
        ["accounts.languages.new"] = ["Neue Sprache", "New language", "Yeni dil", "Nuova lingua"],
        ["accounts.guilds"] = ["Gilden", "Guilds", "Loncalar", "Gilde"],
        ["accounts.guilds.new"] = ["Neue Gilde", "New guild", "Yeni lonca", "Nuova gilda"],
        ["accounts.presets"] = ["Schnellwahl", "Quick buttons", "Hızlı düğmeler", "Pulsanti rapidi"],
        ["accounts.presets.hint"] = ["Diese Knöpfe stehen an jedem Charakter.", "These buttons appear on every character.", "Bu düğmeler her karakterde görünür.", "Questi pulsanti compaiono su ogni personaggio."],
        ["accounts.presets.row"] = ["Zeile", "Row", "Satır", "Riga"],
        ["accounts.presets.label"] = ["Beschriftung", "Label", "Etiket", "Etichetta"],
        ["accounts.coins"] = ["Drachenmünzen", "Dragon coins", "Ejder paraları", "Monete del drago"],
        ["accounts.stats.coins"] = ["Drachenmünzen gesamt", "Dragon coins total", "Toplam ejder parası", "Monete del drago in totale"],
        ["accounts.level.edit"] = ["Doppelklick zum Ändern", "Double-click to edit", "Değiştirmek için çift tıkla", "Doppio clic per modificare"],
        ["accounts.medals.edit"] = ["Doppelklick zum Ändern", "Double-click to edit", "Değiştirmek için çift tıkla", "Doppio clic per modificare"],
        ["accounts.server"] = ["Server", "Server", "Sunucu", "Server"],
        ["accounts.noServer"] = ["Kein Server", "No server", "Sunucu yok", "Nessun server"],
        ["accounts.filter.allRuns"] = ["Alle Läufe", "All runs", "Tüm koşular", "Tutti i run"],
        ["accounts.run"] = ["Lauf", "Run", "Koşu", "Run"],
        ["accounts.sort.coins"] = ["Drachenmünzen", "Dragon coins", "Ejder paraları", "Monete del drago"],
        ["accounts.filter.onlyCoins"] = ["Nur mit Drachenmünzen", "Only with dragon coins", "Yalnızca ejder parası olanlar", "Solo con monete del drago"],
        ["accounts.filter.allServers"] = ["Alle Server", "All servers", "Tüm sunucular", "Tutti i server"],
        ["accounts.filter.allLanguages"] = ["Alle Sprachen", "All languages", "Tüm diller", "Tutte le lingue"],
        ["accounts.filter.allGuilds"] = ["Alle Gilden", "All guilds", "Tüm loncalar", "Tutte le gilde"],
        ["accounts.noGuild"] = ["ohne Gilde", "no guild", "loncasız", "senza gilda"],
        ["accounts.sort.own"] = ["eigene", "custom", "kendi", "personale"],
        ["accounts.sort.name"] = ["Name", "Name", "Ad", "Nome"],
        ["accounts.sort.medals"] = ["Medaillen", "Medals", "Madalya", "Medaglie"],
        ["accounts.sort.level"] = ["Level", "Level", "Seviye", "Livello"],
        ["accounts.sort.language"] = ["Sprache", "Language", "Dil", "Lingua"],
        ["account.form.name"] = ["Accountname", "Account name", "Hesap adı", "Nome account"],
        ["account.form.nameHint"] = ["Endet der Name auf Ziffern, zeigt die Kachel die letzten vier groß an.", "If the name ends in digits, the tile shows the last four in large type.", "Ad rakamla bitiyorsa, kart son dördünü büyük gösterir.", "Se il nome finisce con cifre, la scheda mostra le ultime quattro in grande."],
        ["account.form.language"] = ["Client-Sprache", "Client language", "İstemci dili", "Lingua del client"],
        ["account.form.coins"] = ["Drachenmünzen", "Dragon coins", "Ejder paraları", "Monete del drago"],
        ["account.form.server"] = ["Server", "Server", "Sunucu", "Server"],
        ["account.form.ownServer"] = ["Eigener Server", "Own server", "Kendi sunucun", "Server proprio"],
        ["account.form.note"] = ["Notiz (optional)", "Note (optional)", "Not (isteğe bağlı)", "Nota (facoltativa)"],
        ["char.form.roles"] = ["Rollen", "Roles", "Roller", "Ruoli"],
        ["char.form.meley"] = ["Meley-Char (Level grün hinterlegt)", "Meley char (level highlighted green)", "Meley karakteri (seviye yeşil)", "Personaggio Meley (livello in verde)"],
        ["char.form.grotte"] = ["Levelt (Name rot)", "Levelling (name in red)", "Seviye atlıyor (ad kırmızı)", "Sale di livello (nome in rosso)"],
        ["char.form.balathor"] = ["Balathor-Char", "Balathor character", "Balathor karakteri", "Personaggio Balathor"],
        ["char.form.serpent"] = ["Schlangenrun-Char", "Snake run character", "Yılan koşusu karakteri", "Personaggio corsa del serpente"],
        ["accounts.role.balathor.short"] = ["Ba", "Ba", "Ba", "Ba"],
        ["accounts.role.serpent.short"] = ["Se", "Se", "Se", "Se"],
        ["char.form.donate"] = ["Spenden-Char (bekommt die Medaillen gutgeschrieben)", "Donation char (medals are credited to them)", "Bağış karakteri (madalyalar ona yazılır)", "Personaggio donazioni (le medaglie vengono accreditate a lui)"],
        ["accounts.role.donate.short"] = ["Sp", "Do", "Bğ", "Do"],
        ["accounts.role.runs"] = ["Läufe dieses Chars", "Runs of this character", "Bu karakterin koşuları", "Run di questo personaggio"],
        ["char.form.bio"] = ["Trägt die Orkzahn-Bio dieses Accounts", "Carries this account’s orc tooth bio", "Bu hesabın ork dişi biyoloğunu taşıyor", "Porta la bio del dente d’orco di questo account"],
        ["char.form.bioDone"] = ["Bio erledigt", "Bio done", "Bio tamamlandı", "Bio completata"],
        ["events.subtitle"] = ["Globale Ankündigungen und der Kalender je Server, direkt aus dem offiziellen Forum. Der Stand liegt lokal und wird nach sieben Tagen verworfen.", "Global announcements and the calendar per server, straight from the official forum. Everything is kept locally and discarded after seven days.", "Genel duyurular ve sunucu bazlı takvim, doğrudan resmî forumdan. Veriler yerelde tutulur ve yedi gün sonra silinir.", "Annunci globali e il calendario per server, direttamente dal forum ufficiale. Tutto resta in locale e viene scartato dopo sette giorni."],
        ["events.tab.global"] = ["Global", "Global", "Genel", "Globale"],
        ["events.hideExpired"] = ["Abgelaufene ausblenden", "Hide expired", "Süresi dolanları gizle", "Nascondi scaduti"],
        ["events.showUndated"] = ["Ohne Zeitraum anzeigen", "Show without period", "Tarihsizleri göster", "Mostra senza periodo"],
        ["events.showUndated.tip"] = ["Im Event-Board stehen auch Beiträge, die kein Event ankündigen", "The event board also holds posts that announce no event", "Etkinlik panosunda etkinlik duyurmayan gönderiler de var", "Nella sezione eventi ci sono anche post che non annunciano eventi"],
        ["events.hint"] = ["Laufende und kommende Events stehen immer oben.", "Running and upcoming events always come first.", "Devam eden ve yaklaşan etkinlikler her zaman üstte.", "Gli eventi in corso e in arrivo sono sempre in alto."],
        ["events.running"] = ["läuft", "running", "devam ediyor", "in corso"],
        ["events.upcoming"] = ["kommt", "upcoming", "yaklaşıyor", "in arrivo"],
        ["events.noCalendar"] = ["Für diesen Server liegt noch kein Kalender vor. „Jetzt laden“ holt ihn aus dem Forum.", "No calendar for this server yet. “Refresh now” fetches it from the forum.", "Bu sunucu için henüz takvim yok. “Şimdi yükle” forumdan getirir.", "Nessun calendario per questo server. “Aggiorna ora” lo recupera dal forum."],
        ["month.1"] = ["Januar", "January", "Ocak", "Gennaio"],
        ["month.2"] = ["Februar", "February", "Şubat", "Febbraio"],
        ["month.3"] = ["März", "March", "Mart", "Marzo"],
        ["month.4"] = ["April", "April", "Nisan", "Aprile"],
        ["month.5"] = ["Mai", "May", "Mayıs", "Maggio"],
        ["month.6"] = ["Juni", "June", "Haziran", "Giugno"],
        ["month.7"] = ["Juli", "July", "Temmuz", "Luglio"],
        ["month.8"] = ["August", "August", "Ağustos", "Agosto"],
        ["month.9"] = ["September", "September", "Eylül", "Settembre"],
        ["month.10"] = ["Oktober", "October", "Ekim", "Ottobre"],
        ["month.11"] = ["November", "November", "Kasım", "Novembre"],
        ["month.12"] = ["Dezember", "December", "Aralık", "Dicembre"],
        ["events.column.day"] = ["Tag", "Day", "Gün", "Giorno"],
        ["events.column.weekday"] = ["Wochentag", "Weekday", "Haftanın günü", "Giorno della settimana"],
        ["events.now"] = ["Jetzt ({0}–{1})", "Now ({0}–{1})", "Şimdi ({0}–{1})", "Ora ({0}–{1})"],
        ["events.source"] = ["Aus dem deutschen Forum; Eventnamen übersetzt, Ankündigungstexte im Original.", "From the German forum; event names translated, announcement texts in the original.", "Alman forumundan; etkinlik adları çevrildi, duyuru metinleri orijinal.", "Dal forum tedesco; nomi degli eventi tradotti, testi degli annunci in originale."],
        ["events.from"] = ["ab {0}", "from {0}", "{0} itibarıyla", "dal {0}"],
        ["events.until"] = ["bis {0}", "until {0}", "{0} tarihine kadar", "fino al {0}"],
        ["events.specials"] = ["Zusätzliche Events", "Additional events", "Ek etkinlikler", "Eventi aggiuntivi"],
        ["events.unknownPeriod"] = ["Zeitraum unbekannt", "Period unknown", "Süre bilinmiyor", "Periodo sconosciuto"],
        ["itemshop.subtitle"] = ["Aktionen direkt aus dem offiziellen Forum. Der Stand liegt lokal und wird nach sieben Tagen verworfen.", "Offers straight from the official forum. Everything is kept locally and discarded after seven days.", "Kampanyalar doğrudan resmî forumdan. Veriler yerelde tutulur ve yedi gün sonra silinir.", "Offerte direttamente dal forum ufficiale. Tutto resta in locale e viene scartato dopo sette giorni."],
        ["itemshop.running"] = ["Laufende", "Running", "Devam eden", "In corso"],
        ["itemshop.upcoming"] = ["Kommende", "Upcoming", "Yaklaşan", "In arrivo"],
        ["itemshop.expired"] = ["Abgelaufene", "Expired", "Süresi dolan", "Scaduti"],
        ["itemshop.empty"] = ["Keine Aktionen gefunden.", "No offers found.", "Kampanya bulunamadı.", "Nessuna offerta trovata."],
        ["itemshop.new"] = ["neu", "new", "yeni", "nuovo"],
        ["settings.subtitle"] = ["Alles hier gilt nur auf diesem Rechner.", "Everything here applies to this computer only.", "Buradaki her şey yalnızca bu bilgisayar için geçerlidir.", "Tutto qui vale solo per questo computer."],
        ["settings.language"] = ["Sprache", "Language", "Dil", "Lingua"],
        ["settings.language.hint"] = ["Beim ersten Start richtet sich die App nach Windows. Geladen wird immer aus dem deutschen Forum; Eventnamen werden hier übersetzt.", "On first start the app follows Windows. Data always comes from the German forum; event names are translated here.", "İlk açılışta uygulama Windows’u temel alır. Veriler her zaman Alman forumundan gelir; etkinlik adları burada çevrilir.", "Al primo avvio l’app segue Windows. I dati vengono sempre dal forum tedesco; i nomi degli eventi sono tradotti qui."],
        ["settings.language.auto"] = ["Automatisch (Windows)", "Automatic (Windows)", "Otomatik (Windows)", "Automatico (Windows)"],
        ["settings.servers"] = ["Server", "Servers", "Sunucular", "Server"],
        ["settings.servers.hint"] = ["Nur angehakte Server erscheinen als Reiter bei den Events und in der Auswahl bei den Accounts. Die Daten bleiben unberührt.", "Only ticked servers appear as tabs under Events and in the account picker. The data stays untouched.", "Yalnızca işaretli sunucular Etkinlikler sekmelerinde ve hesap seçiminde görünür. Veriler etkilenmez.", "Solo i server selezionati compaiono come schede in Eventi e nella scelta degli account. I dati restano intatti."],
        ["settings.header"] = ["Kopfzeile", "Header", "Başlık çubuğu", "Intestazione"],
        ["settings.header.hint"] = ["Neben den globalen Events und Happy Hours kann auch das laufende Event eines Serverkalenders oben stehen.", "Besides global events and happy hours, the running event of a server calendar can appear at the top.", "Genel etkinlikler ve happy hour’ların yanında bir sunucu takviminin devam eden etkinliği de üstte görünebilir.", "Oltre a eventi globali e happy hour, in alto può comparire l’evento in corso di un calendario server."],
        ["settings.header.none"] = ["Keiner", "None", "Hiçbiri", "Nessuno"],
        ["settings.update"] = ["Aktualisierung", "Updates", "Güncelleme", "Aggiornamenti"],
        ["settings.update.check"] = ["Beim Start nach einer neueren Version sehen", "Check for a newer version on start", "Açılışta yeni sürüm ara", "Cerca una nuova versione all’avvio"],
        ["settings.update.hint"] = ["Auf Knopfdruck lädt die App die neue Version, ersetzt sich selbst und startet neu.", "At the press of a button the app downloads the new version, replaces itself and restarts.", "Tek tuşla uygulama yeni sürümü indirir, kendini değiştirir ve yeniden başlar.", "Con un clic l’app scarica la nuova versione, si sostituisce e si riavvia."],
        ["settings.update.now"] = ["Jetzt nach Update sehen", "Check for updates now", "Şimdi güncelleme ara", "Cerca aggiornamenti ora"],
        ["settings.update.page"] = ["Release-Seite öffnen", "Open release page", "Sürüm sayfasını aç", "Apri la pagina delle release"],
        ["settings.data"] = ["Daten", "Data", "Veriler", "Dati"],
        ["settings.data.hint"] = ["accounts.json trägt deine Accounts – die Datei ist das, was man sichern sollte. cache.json hält den Forum-Stand und wird nach sieben Tagen ohnehin verworfen.", "accounts.json holds your accounts – that is the file worth backing up. cache.json holds the forum data and is discarded after seven days anyway.", "accounts.json hesaplarını tutar – yedeklenmesi gereken dosya budur. cache.json forum verisini tutar ve yedi gün sonra zaten silinir.", "accounts.json contiene i tuoi account – è il file da salvare. cache.json contiene i dati del forum e viene comunque scartato dopo sette giorni."],
        ["settings.data.open"] = ["Ordner öffnen", "Open folder", "Klasörü aç", "Apri cartella"],
        ["settings.data.clear"] = ["Zwischenspeicher leeren", "Clear cache", "Önbelleği temizle", "Svuota la cache"],
        ["settings.version"] = ["Version {0}", "Version {0}", "Sürüm {0}", "Versione {0}"],
        ["update.title"] = ["Version {0} verfügbar", "Version {0} available", "Sürüm {0} mevcut", "Versione {0} disponibile"],
        ["update.current"] = ["Installiert ist {0}.", "You have {0} installed.", "Kurulu sürüm {0}.", "Hai installato {0}."],
        ["update.size"] = ["Der Download ist etwa {0} groß.", "The download is about {0}.", "İndirme yaklaşık {0}.", "Il download è di circa {0}."],
        ["update.install"] = ["Jetzt aktualisieren", "Update now", "Şimdi güncelle", "Aggiorna ora"],
        ["update.page"] = ["Release-Seite", "Release page", "Sürüm sayfası", "Pagina release"],
        ["update.downloading"] = ["Wird geladen …", "Downloading …", "İndiriliyor …", "Download in corso …"],
        ["update.replacing"] = ["Wird ersetzt …", "Replacing …", "Değiştiriliyor …", "Sostituzione …"],
        ["update.noInstall"] = ["An diesem Ort lässt sich das Programm nicht selbst austauschen. Lade die neue Version über die Release-Seite herunter und ersetze die Datei von Hand.", "At this location the program cannot replace itself. Download the new version from the release page and replace the file by hand.", "Bu konumda program kendini değiştiremez. Yeni sürümü sürüm sayfasından indirip dosyayı elle değiştir.", "In questa posizione il programma non può sostituirsi. Scarica la nuova versione dalla pagina release e sostituisci il file a mano."],
        ["settings.update.none"] = ["Kein Update gefunden – {0} ist aktuell.", "No update found – {0} is current.", "Güncelleme yok – {0} güncel.", "Nessun aggiornamento – {0} è aggiornato."],
        ["settings.data.clearHint"] = ["Events, Eventkalender und Itemshop werden verworfen und beim nächsten Laden neu geholt. Accounts bleiben unberührt.", "Events, event calendar and item shop are discarded and fetched again on the next load. Accounts stay untouched.", "Etkinlikler, etkinlik takvimi ve nesne market silinir ve bir sonraki yüklemede yeniden alınır. Hesaplar etkilenmez.", "Eventi, calendario e item shop vengono scartati e recuperati al prossimo caricamento. Gli account restano intatti."],
        ["settings.data.clearOk"] = ["Leeren", "Clear", "Temizle", "Svuota"],
        ["settings.data.cleared"] = ["Zwischenspeicher geleert. „Jetzt laden“ holt die Daten neu.", "Cache cleared. “Refresh now” fetches the data again.", "Önbellek temizlendi. “Şimdi yükle” verileri yeniden getirir.", "Cache svuotata. “Aggiorna ora” recupera di nuovo i dati."],
        ["update.failedDownload"] = ["Der Download hat nicht geklappt. Versuch es später noch einmal oder lade die Datei über die Release-Seite.", "The download failed. Try again later or get the file from the release page.", "İndirme başarısız oldu. Daha sonra tekrar dene veya dosyayı sürüm sayfasından al.", "Il download non è riuscito. Riprova più tardi o scarica il file dalla pagina release."],
        ["update.failedReplace"] = ["Die Datei ließ sich nicht ersetzen. Lade die neue Version über die Release-Seite herunter.", "The file could not be replaced. Download the new version from the release page.", "Dosya değiştirilemedi. Yeni sürümü sürüm sayfasından indir.", "Impossibile sostituire il file. Scarica la nuova versione dalla pagina release."],
        ["itemshop.over"] = ["vorbei", "over", "bitti", "concluso"],
    };
}
