#!/usr/bin/env bash
# Importiert die Okey-Event-Daten nach Assets/Okey: das Helfer-Icon und die
# Truhen-Loot-Tabellen (Sommer/Winter, je Truhe Items mit Wahrscheinlichkeit).
#
# Quelle:
#   <SRC>/Okey-Helper.png
#   <SRC>/Truhen/Loot/<Sommer-Version|Winter-Version>/<Truhe>/<Item>.png
#   .../<Truhe>/Wahrscheinlichkeit.txt   (je Zeile: <Name>\t<Prozent>)
# Die Namen in der .txt sind teils verdoppelt ("YangYang (50.000)"); sie werden
# per Endungsvergleich an die PNG-Namen gebunden.
#
# Lauf:  bash desktop/tools/import-okey-loot.sh ["Pfad zu Okey-Helper"]
set -euo pipefail

SRC="${1:-C:/Users/lufab/Documents/Event-Helper/Okey-Helper}"
HERE="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$HERE/M2Hub.Desktop/Assets/Okey"
JSON="$DEST/okey-loot.json"

[ -d "$SRC" ] || { echo "Quelle nicht gefunden: $SRC" >&2; exit 1; }
rm -rf "$DEST"; mkdir -p "$DEST"

slug(){ printf '%s' "$1" \
  | sed -e 's/ä/ae/g;s/ö/oe/g;s/ü/ue/g;s/Ä/ae/g;s/Ö/oe/g;s/Ü/ue/g;s/ß/ss/g' \
  | tr '[:upper:]' '[:lower:]' | sed -e 's/[^a-z0-9]\+/-/g' -e 's/^-\+//' -e 's/-\+$//'; }
js(){ printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }
strip(){ printf '%s' "$1" | sed 's/\r$//'; }

# Icon
icon=""
if [ -f "$SRC/Okey-Helper.png" ]; then cp "$SRC/Okey-Helper.png" "$DEST/icon.png"; icon="Okey/icon.png"; fi

# Wahrscheinlichkeit einer Karte: die .txt-Zeile, deren Name auf den PNG-Namen
# endet (faengt die Verdopplung ab).
chance_for(){ local base="$1" txt="$2" name pct
  [ -f "$txt" ] || { printf ''; return; }
  while IFS=$'\t' read -r name pct || [ -n "$name" ]; do
    name="$(strip "$name")"; [ -z "$name" ] && continue
    if [ "${name%"$base"}" != "$name" ] || [ "$name" = "$base" ]; then
      printf '%s' "$(strip "$pct")"; return
    fi
  done < "$txt"
  printf ''; }

tier_of(){ case "$1" in *[Gg]old*) echo 2;; *[Ss]ilber*) echo 1;; *) echo 0;; esac; }
key_of(){ case "$1" in *[Gg]old*) echo gold;; *[Ss]ilber*) echo silber;; *) echo bronze;; esac; }

# Truhen-Icons (Bronze/Silber/Gold) - fuer Sommer/Winter gleich, liegen direkt
# unter Truhen/.
chest_icons=""; cifirst=1
for f in "$SRC/Truhen/"*.png; do
  [ -f "$f" ] || continue
  k="$(key_of "$(basename "$f")")"
  cp "$f" "$DEST/chest-$k.png"
  if [ $cifirst -eq 1 ]; then cifirst=0; else chest_icons+=","; fi
  chest_icons+="\"$k\":\"Okey/chest-$k.png\""
done

seasons_json=""; seafirst=1
for season in "Sommer-Version" "Winter-Version"; do
  sdir="$SRC/Truhen/Loot/$season"
  [ -d "$sdir" ] || continue
  skey="$(slug "$season")"

  chests_json=""; cfirst=1
  while IFS= read -r -d '' chestDir; do
    chestName="$(basename "$chestDir")"; ckey="$(key_of "$chestName")"; tier="$(tier_of "$chestName")"
    txt="$chestDir/Wahrscheinlichkeit.txt"

    items=""; ifirst=1
    while IFS= read -r -d '' png; do
      base="$(basename "${png%.*}")"
      islug="$(slug "$base")"
      rel="Okey/$skey/$ckey/$islug.png"
      mkdir -p "$DEST/$skey/$ckey"
      cp "$png" "$DEST/$skey/$ckey/$islug.png"
      chance="$(chance_for "$base" "$txt")"
      if [ $ifirst -eq 1 ]; then ifirst=0; else items+=","; fi
      items+="{\"name\":\"$(js "$base")\",\"file\":\"$(js "$rel")\",\"chance\":\"$(js "$chance")\"}"
    done < <(find "$chestDir" -maxdepth 1 -type f -iname '*.png' -print0 | sort -z)

    if [ $cfirst -eq 1 ]; then cfirst=0; else chests_json+=","; fi
    chests_json+="{\"key\":\"$(js "$ckey")\",\"name\":\"$(js "$chestName")\",\"tier\":$tier,\"items\":[$items]}"
  done < <(find "$sdir" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

  if [ $seafirst -eq 1 ]; then seafirst=0; else seasons_json+=","; fi
  seasons_json+="{\"key\":\"$(js "$skey")\",\"name\":\"$(js "$season")\",\"chests\":[$chests_json]}"
done

printf '{"icon":"%s","chestIcons":{%s},"seasons":[%s]}' "$(js "$icon")" "$chest_icons" "$seasons_json" > "$JSON"
echo "geschrieben: $JSON"
echo "Bilder: $(find "$DEST" -type f -iname '*.png' | wc -l)"
