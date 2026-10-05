# Zeichnet die vier Tutorial-Bilder fuer den Hilfe-Knopf nach - eigene
# Zeichnungen, keine Bildschirmfotos.
import cairosvg, os

OUT = "/home/user/M2Hub-orFabs/desktop/M2Hub.Desktop/Assets/Help"
W, H = 760, 430

DEEP="#0b1220"; CARD="#111a2b"; PANEL="#16203353"; LINE="#1f2b40"
MAIN="#e5edf8"; MUTED="#8ea0bd"; PRIM="#3b82f6"; AMBER="#f59e0b"
GREEN="#22c55e"; MARK="#ef4444"

def head(step, title):
    return f'''
  <rect width="{W}" height="{H}" rx="14" fill="{DEEP}"/>
  <rect x="0" y="0" width="{W}" height="54" rx="14" fill="{CARD}"/>
  <rect x="0" y="40" width="{W}" height="14" fill="{CARD}"/>
  <circle cx="34" cy="27" r="13" fill="{PRIM}"/>
  <text x="34" y="32" font-family="Segoe UI,DejaVu Sans,sans-serif" font-size="14"
        font-weight="700" fill="#ffffff" text-anchor="middle">{step}</text>
  <text x="58" y="32" font-family="Segoe UI,DejaVu Sans,sans-serif" font-size="15"
        font-weight="600" fill="{MAIN}">{title}</text>
  <rect x="0" y="54" width="{W}" height="1" fill="{LINE}"/>'''

def arrow(x1,y1,x2,y2,label="",lx=0,ly=0,anchor="start"):
    return f'''
  <defs><marker id="a{int(x1)}{int(y1)}" markerWidth="9" markerHeight="9" refX="7" refY="4.5"
      orient="auto"><path d="M0,0 L9,4.5 L0,9 z" fill="{MARK}"/></marker></defs>
  <path d="M{x1},{y1} L{x2},{y2}" stroke="{MARK}" stroke-width="2.6" fill="none"
        marker-end="url(#a{int(x1)}{int(y1)})"/>
  <text x="{lx or x1}" y="{ly or y1-8}" font-family="Segoe UI,DejaVu Sans,sans-serif"
        font-size="13" font-weight="600" fill="{MARK}" text-anchor="{anchor}">{label}</text>'''

def box(x,y,w,h,fill=CARD,stroke=LINE,r=8):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}"/>'

def text(x,y,s,size=13,fill=MAIN,weight="400",anchor="start"):
    return (f'<text x="{x}" y="{y}" font-family="Segoe UI,DejaVu Sans,sans-serif" '
            f'font-size="{size}" font-weight="{weight}" fill="{fill}" text-anchor="{anchor}">{s}</text>')

def lens(cx,cy,col="#111111"):
    return (f'<circle cx="{cx-2}" cy="{cy-2}" r="6" fill="none" stroke="{col}" stroke-width="2.2"/>'
            f'<path d="M{cx+2.5},{cy+2.5} L{cx+7},{cy+7}" stroke="{col}" stroke-width="2.4" '
            f'stroke-linecap="round"/>')

def shopicon(x,y,col=AMBER):
    return (f'<path d="M{x},{y+4} L{x+3},{y} L{x+15},{y} L{x+18},{y+4} Z" fill="none" '
            f'stroke="{col}" stroke-width="1.6" stroke-linejoin="round"/>'
            f'<rect x="{x+2}" y="{y+4}" width="{14}" height="{10}" fill="none" stroke="{col}" '
            f'stroke-width="1.6"/>')

def glow(x,y,w,h,r=10):
    return (f'<rect x="{x-4}" y="{y-4}" width="{w+8}" height="{h+8}" rx="{r+3}" fill="none" '
            f'stroke="{MARK}" stroke-width="2.4"/>')

# ---- 1: Server waehlen ------------------------------------------------------
s1 = head(1, "Markt-Seite öffnen und den eigenen Server wählen")
s1 += box(30, 86, 250, 40)
s1 += text(48, 111, "&#9679;  [SAPPHIRE] Safir", 14, MAIN, "600")
s1 += text(258, 111, "&#9662;", 14, MUTED, anchor="end")
s1 += glow(30, 86, 250, 40)
s1 += arrow(150, 178, 150, 136, "Server wählen", 96, 198)
s1 += box(30, 220, 700, 170, PANEL)
s1 += text(52, 252, "Marktplatz", 20, MAIN, "700")
s1 += text(52, 276, "Die Liste zeigt immer nur den gewählten Server –", 13, MUTED)
s1 += text(52, 296, "steht dort der falsche, findest du deinen Laden nicht.", 13, MUTED)
for i, (lbl, val) in enumerate([("Inserate", "53.950"), ("Gesamtwert", "868.616W"), ("Verkäufer", "1.831")]):
    x = 52 + i*220
    s1 += box(x, 316, 200, 56, CARD)
    s1 += text(x+16, 338, lbl, 11, MUTED)
    s1 += text(x+16, 360, val, 16, AMBER if i==1 else MAIN, "700")

# ---- 2: Verkaeufersuche -----------------------------------------------------
s2 = head(2, "Verkäufersuche öffnen und den Charakternamen eintragen")
s2 += box(30, 86, 700, 46)
for i, tab in enumerate(["Alle", "Mounts | Pets", "Waffen", "Ausrüstungen", "Schmuck"]):
    s2 += text(52 + i*130, 115, tab, 13, MAIN if i == 0 else MUTED, "600" if i == 0 else "400")
s2 += box(300, 150, 400, 150)
s2 += text(322, 180, "Verkäufersuche", 14, MAIN, "600")
s2 += box(322, 196, 300, 38, DEEP, PRIM)
s2 += text(338, 221, "ShopName", 14, MAIN)
s2 += box(630, 196, 48, 38, AMBER, AMBER)
s2 += lens(654, 215)
s2 += glow(322, 196, 300, 38)
s2 += arrow(190, 240, 310, 218, "Name des Charakters,", 30, 232)
s2 += text(30, 250, "dem der Laden gehört", 13, MARK, "600")
s2 += arrow(660, 330, 660, 244, "Suchen", 628, 352)
s2 += text(322, 268, "Nicht der Ladentitel – der Charaktername.", 12, MUTED)
s2 += box(30, 330, 240, 70, PANEL)
s2 += text(48, 358, "Tipp", 12, MUTED, "600")
s2 += text(48, 380, "Gespeicherte Namen bleiben stehen.", 11, MUTED)

# ---- 3: Laden-Symbol anklicken ---------------------------------------------
s3 = head(3, "In der Trefferliste auf das Laden-Symbol klicken")
s3 += box(30, 80, 700, 30, CARD)
s3 += text(52, 100, "Name", 12, MUTED, "600")
s3 += text(430, 100, "Anzahl", 12, MUTED, "600")
s3 += text(520, 100, "Preis", 12, MUTED, "600")
s3 += text(640, 100, "Verkäufer", 12, MUTED, "600")
for i in range(4):
    y = 118 + i*68
    s3 += box(30, y, 700, 60, PANEL)
    s3 += text(52, y+26, "Legendärer Drachensaphir", 13, "#c084fc", "700")
    s3 += text(52, y+46, "Beweglichkeit +3", 11, MUTED)
    s3 += text(170, y+46, "Erdwiderstand +3%", 11, MUTED)
    s3 += text(438, y+36, "1", 13, MAIN, "700")
    s3 += text(520, y+36, "40.000.000", 13, AMBER, "700")
    s3 += box(636, y+20, 28, 24, CARD, LINE, 5)
    s3 += shopicon(641, y+25)
    s3 += text(676, y+37, "+", 14, MUTED)
s3 += glow(636, 138, 28, 24, 7)
s3 += arrow(560, 394, 646, 174, "Das Laden-Symbol anklicken", 452, 412)

# ---- 4: Gesamtwert ablesen --------------------------------------------------
s4 = head(4, "Der Laden öffnet sich – unten steht der Gesamtwert")
s4 += box(230, 76, 300, 320, "#1a1410", "#4a3626", 6)
s4 += box(230, 76, 300, 30, "#3a2a1c", "#4a3626", 6)
s4 += text(248, 97, "Laden", 13, "#e8d5b5", "600")
s4 += text(512, 98, "&#10005;", 13, "#e8d5b5", anchor="end")
for r in range(6):
    for c in range(5):
        x = 248 + c*56; y = 118 + r*42
        s4 += box(x, y, 50, 38, "#120e0a", "#2e241a", 4)
import_items = [(1,3),(1,4),(2,2),(2,3),(2,4),(3,0),(3,1),(4,2),(4,3),(4,4),(5,3)]
for r,c in import_items:
    x = 248 + c*56; y = 118 + r*42
    s4 += f'<circle cx="{x+25}" cy="{y+19}" r="11" fill="#1e3a5f" stroke="#60a5fa" stroke-width="1.5"/>'
    s4 += f'<circle cx="{x+25}" cy="{y+19}" r="4" fill="#93c5fd"/>'
s4 += text(380, 388, "156W", 18, "#22d3ee", "700", anchor="middle")
s4 += glow(330, 372, 100, 22, 6)
s4 += arrow(700, 384, 440, 384, "Das ist dein kompletter Shop-Wert", 712, 372, "end")
s4 += box(30, 110, 180, 120, PANEL)
s4 += text(48, 138, "Weiter in M2Hub", 12, MAIN, "600")
s4 += text(48, 160, "Diesen Wert als", 11, MUTED)
s4 += text(48, 178, "&#8222;Im Shop&#8220; eintragen", 11, MUTED)
s4 += text(48, 196, "und die Truhen", 11, MUTED)
s4 += text(48, 214, "darunter abziehen.", 11, MUTED)

for name, body in [("shop1", s1), ("shop2", s2), ("shop3", s3), ("shop4", s4)]:
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">{body}</svg>'
    cairosvg.svg2png(bytestring=svg.encode("utf-8"),
                     write_to=os.path.join(OUT, name + ".png"),
                     output_width=W*2, output_height=H*2)
    print(name, "ok")
