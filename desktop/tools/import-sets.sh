#!/usr/bin/env bash
# Importiert die Kostuem-Set-Ordner nach Assets/Sets: erzeugt sets.json und
# kopiert die Bilder. Struktur je Ring:
#   <Ring>/<Ring>.png            -> Ring-Icon
#   <Ring>/<Set>/Set-Effekt N Teile.txt
#   <Ring>/<Set>/Reittier|Pet|Waffe/<name>.png (+ .txt mit eigenen Werten)
#   <Ring>/<Set>/<Tier>/<bild>.png   (Kostuem-Varianten, Body/Frisur, m/w)
#
# Lauf:  bash desktop/tools/import-sets.sh ["Pfad zu Kosi sets"]
# Idempotent: Assets/Sets wird bei jedem Lauf neu gebaut.
set -euo pipefail

SRC="${1:-C:/Users/lufab/Documents/Kosi sets}"
HERE="$(cd "$(dirname "$0")/.." && pwd)"          # desktop/
DEST="$HERE/M2Hub.Desktop/Assets/Sets"
JSON="$DEST/sets.json"

[ -d "$SRC" ] || { echo "Quelle nicht gefunden: $SRC" >&2; exit 1; }
rm -rf "$DEST"; mkdir -p "$DEST"

# --- Helfer -----------------------------------------------------------------
slug(){ printf '%s' "$1" \
  | sed -e 's/ä/ae/g;s/ö/oe/g;s/ü/ue/g;s/Ä/ae/g;s/Ö/oe/g;s/Ü/ue/g;s/ß/ss/g' \
  | tr '[:upper:]' '[:lower:]' | sed -e 's/[^a-z0-9]\+/-/g' -e 's/^-\+//' -e 's/-\+$//'; }
js(){ printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'; }   # JSON-String-Escape
strip(){ printf '%s' "$1" | sed 's/\r$//'; }

# Datei -> JSON-Array der nichtleeren Zeilen
lines_json(){ local f="$1" out="" first=1 line
  [ -f "$f" ] || { printf '[]'; return; }
  while IFS= read -r line || [ -n "$line" ]; do line="$(strip "$line")"
    [ -z "$line" ] && continue
    if [ $first -eq 1 ]; then first=0; else out+=","; fi
    out+="\"$(js "$line")\""
  done < "$f"
  printf '[%s]' "$out"; }

# Bild kopieren -> gibt relativen Asset-Pfad zurueck (ASCII-Slug)
copy_img(){ local file="$1" ringK="$2" setK="$3" slotK="$4"
  local base ext target rel
  base="$(basename "$file")"; ext="${base##*.}"
  target_dir="$DEST/$ringK/$setK/$slotK"; mkdir -p "$target_dir"
  local name="$(slug "${base%.*}")"
  local t="$target_dir/$name.$ext"; local n=2
  while [ -e "$t" ]; do t="$target_dir/$name-$n.$ext"; n=$((n+1)); done
  cp "$file" "$t"
  printf 'Sets/%s/%s/%s/%s' "$ringK" "$setK" "$slotK" "$(basename "$t")"; }

# Geschlecht/Teil aus dem Dateinamen raten (nur fuer Labels)
guess_part(){ local low="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  case "$low" in *turban*|*tiara*|*shemagh*|*frisur*|*haar*|*krone*|*hut*|*kappe*|*helm*) echo "Frisur";; *) echo "Body";; esac; }
guess_gender(){ local b="$1" low="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  case "$b" in *"(m)"*) echo "m"; return;; *"(w)"*) echo "w"; return;; esac
  case "$low" in
    *lady*|*prinzessin*|*koenigin*|*königin*|*dame*|*tiara*) echo "w";;
    *lord*|*prinz*|*koenig*|*könig*|*herr*|*turban*) echo "m";;
    *) echo "";; esac; }

# Reittier/Pet/Waffe-Slot als JSON-Array von {name,file,stats}
variant_slot(){ local dir="$1" ringK="$2" setK="$3" slotK="$4" out="" first=1 png base rel stats
  [ -d "$dir" ] || { printf '[]'; return; }
  while IFS= read -r -d '' png; do
    base="$(basename "${png%.*}")"
    rel="$(copy_img "$png" "$ringK" "$setK" "$slotK")"
    stats="$(lines_json "${png%.*}.txt")"
    if [ $first -eq 1 ]; then first=0; else out+=","; fi
    out+="{\"name\":\"$(js "$base")\",\"file\":\"$(js "$rel")\",\"stats\":$stats}"
  done < <(find "$dir" -maxdepth 1 -type f -iname '*.png' -print0 | sort -z)
  printf '[%s]' "$out"; }

# --- Durchlauf --------------------------------------------------------------
rings_json=""; rfirst=1
while IFS= read -r -d '' ringDir; do
  ringName="$(basename "$ringDir")"; ringK="$(slug "$ringName")"
  # Ring-Icon: erste PNG direkt im Ring-Ordner
  icon=""; iconSrc="$(find "$ringDir" -maxdepth 1 -type f -iname '*.png' | head -1)"
  if [ -n "$iconSrc" ]; then mkdir -p "$DEST/$ringK"; cp "$iconSrc" "$DEST/$ringK/_ring.png"; icon="Sets/$ringK/_ring.png"; fi

  sets_json=""; sfirst=1
  while IFS= read -r -d '' setDir; do
    setName="$(basename "$setDir")"; setK="$(slug "$setName")"
    e2="$(lines_json "$setDir/Set-Effekt 2 Teile.txt")"
    e3="$(lines_json "$setDir/Set-Effekt 3 Teile.txt")"
    e4="$(lines_json "$setDir/Set-Effekt 4 Teile.txt")"

    mounts="[]"; pets="[]"; weapons="[]"; costumes=""; cfirst=1
    while IFS= read -r -d '' sub; do
      subName="$(basename "$sub")"
      case "$subName" in
        Reittier) mounts="$(variant_slot "$sub" "$ringK" "$setK" "reittier")";;
        Pet)      pets="$(variant_slot "$sub" "$ringK" "$setK" "pet")";;
        Waffe|Waffen|Waffen-Skin) weapons="$(variant_slot "$sub" "$ringK" "$setK" "waffe")";;
        *) # Kostuem-Tier: Bilder mit Label
           imgs=""; ifirst=1
           while IFS= read -r -d '' png; do
             base="$(basename "${png%.*}")"
             rel="$(copy_img "$png" "$ringK" "$setK" "$(slug "$subName")")"
             part="$(guess_part "$base")"; gen="$(guess_gender "$base")"
             if [ $ifirst -eq 1 ]; then ifirst=0; else imgs+=","; fi
             imgs+="{\"name\":\"$(js "$base")\",\"part\":\"$part\",\"gender\":\"$gen\",\"file\":\"$(js "$rel")\"}"
           done < <(find "$sub" -maxdepth 1 -type f -iname '*.png' -print0 | sort -z)
           if [ $cfirst -eq 1 ]; then cfirst=0; else costumes+=","; fi
           costumes+="{\"name\":\"$(js "$subName")\",\"images\":[$imgs]}";;
      esac
    done < <(find "$setDir" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

    if [ $sfirst -eq 1 ]; then sfirst=0; else sets_json+=","; fi
    sets_json+="{\"key\":\"$(js "$setK")\",\"name\":\"$(js "$setName")\",\"effects\":{\"2\":$e2,\"3\":$e3,\"4\":$e4},\"costumes\":[$costumes],\"mounts\":$mounts,\"pets\":$pets,\"weapons\":$weapons}"
  done < <(find "$ringDir" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

  if [ $rfirst -eq 1 ]; then rfirst=0; else rings_json+=","; fi
  rings_json+="{\"key\":\"$(js "$ringK")\",\"name\":\"$(js "$ringName")\",\"icon\":\"$(js "$icon")\",\"sets\":[$sets_json]}"
done < <(find "$SRC" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

printf '{"rings":[%s]}' "$rings_json" > "$JSON"
echo "geschrieben: $JSON"
echo "Ringe: $(find "$SRC" -mindepth 1 -maxdepth 1 -type d | wc -l), Bilder: $(find "$DEST" -type f -iname '*.png' | wc -l)"
