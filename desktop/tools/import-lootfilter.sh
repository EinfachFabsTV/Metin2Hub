#!/usr/bin/env bash
# Importiert den Loot-Filter-Ordnerbaum nach Assets/LootFilter: erzeugt
# lootfilter.json (Kategoriebaum mit Items) und kopiert die Item-Bilder.
#
# Struktur der Quelle (beliebig tief):
#   <Oberkategorie>/<Unterkategorie>/.../<Item>.png
# Der Baum spiegelt das Beuteoptionen-Fenster des Spiels.
#
# Lauf:  bash desktop/tools/import-lootfilter.sh ["Pfad zu Loot Filter"]
# Idempotent: Assets/LootFilter wird bei jedem Lauf neu gebaut. Waechst mit,
# sobald mehr Item-PNGs nachgelegt werden.
set -euo pipefail

SRC="${1:-C:/Users/lufab/Documents/weitere sets/LootFilter/Loot Filter}"
HERE="$(cd "$(dirname "$0")/.." && pwd)"          # desktop/
DEST="$HERE/M2Hub.Desktop/Assets/LootFilter"
JSON="$DEST/lootfilter.json"

[ -d "$SRC" ] || { echo "Quelle nicht gefunden: $SRC" >&2; exit 1; }
rm -rf "$DEST"; mkdir -p "$DEST"

# --- Helfer -----------------------------------------------------------------
slug(){ printf '%s' "$1" \
  | sed -e 's/ä/ae/g;s/ö/oe/g;s/ü/ue/g;s/Ä/ae/g;s/Ö/oe/g;s/Ü/ue/g;s/ß/ss/g' \
  | tr '[:upper:]' '[:lower:]' | sed -e 's/[^a-z0-9]\+/-/g' -e 's/^-\+//' -e 's/-\+$//'; }
js(){ printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }

# Bild kopieren -> relativer Asset-Pfad (ASCII-Slug). $2 ist der Slug-Pfad.
copy_img(){ local file="$1" reldir="$2" base ext name t n
  base="$(basename "$file")"; ext="${base##*.}"
  mkdir -p "$DEST/$reldir"
  name="$(slug "${base%.*}")"
  t="$DEST/$reldir/$name.$ext"; n=2
  while [ -e "$t" ]; do t="$DEST/$reldir/$name-$n.$ext"; n=$((n+1)); done
  cp "$file" "$t"
  printf 'LootFilter/%s/%s' "$reldir" "$(basename "$t")"; }

# Einen Knoten (Kategorie) rekursiv ausgeben: Name, Schluessel, Items, Unter-
# kategorien. `local` haelt die Rekursion sauber.
emit_node(){ local dir="$1" rel="$2" name="$3" key="$4"
  local items="" ifirst=1 subs="" sfirst=1 png sub pn r sn sk
  while IFS= read -r -d '' png; do
    pn="$(basename "${png%.*}")"; r="$(copy_img "$png" "$rel")"
    if [ $ifirst -eq 1 ]; then ifirst=0; else items+=","; fi
    items+="{\"name\":\"$(js "$pn")\",\"file\":\"$(js "$r")\"}"
  done < <(find "$dir" -maxdepth 1 -type f -iname '*.png' -print0 | sort -z)
  while IFS= read -r -d '' sub; do
    sn="$(basename "$sub")"; sk="$(slug "$sn")"
    if [ $sfirst -eq 1 ]; then sfirst=0; else subs+=","; fi
    subs+="$(emit_node "$sub" "$rel/$sk" "$sn" "$sk")"
  done < <(find "$dir" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)
  printf '{"key":"%s","name":"%s","items":[%s],"subs":[%s]}' \
    "$(js "$key")" "$(js "$name")" "$items" "$subs"; }

# --- Durchlauf: je Oberkategorie ein Knoten --------------------------------
cats=""; cfirst=1
while IFS= read -r -d '' top; do
  name="$(basename "$top")"; key="$(slug "$name")"
  if [ $cfirst -eq 1 ]; then cfirst=0; else cats+=","; fi
  cats+="$(emit_node "$top" "$key" "$name" "$key")"
done < <(find "$SRC" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

printf '{"categories":[%s]}' "$cats" > "$JSON"
echo "geschrieben: $JSON"
echo "Oberkategorien: $(find "$SRC" -mindepth 1 -maxdepth 1 -type d | wc -l), Bilder: $(find "$DEST" -type f -iname '*.png' | wc -l)"
