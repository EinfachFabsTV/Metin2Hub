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

# ---- 1: Laden im Spiel oeffnen ---------------------------------------------
s1 = head(1, "Im Spiel den eigenen Laden anklicken")
s1 += box(60, 86, 300, 300, "#1a1410", "#4a3626", 6)
s1 += box(60, 86, 300, 30, "#3a2a1c", "#4a3626", 6)
s1 += text(78, 107, "Laden", 13, "#e8d5b5", "600")
s1 += text(342, 108, "&#10005;", 13, "#e8d5b5", anchor="end")
for r in range(5):
    for c in range(5):
        x = 78 + c*56; y = 128 + r*42
        s1 += box(x, y, 50, 38, "#120e0a", "#2e241a", 4)
for r, c in [(0,3),(0,4),(1,2),(1,3),(2,0),(2,1),(3,2),(3,3),(4,3)]:
    x = 78 + c*56; y = 128 + r*42
    s1 += f'<circle cx="{x+25}" cy="{y+19}" r="11" fill="#1e3a5f" stroke="#60a5fa" stroke-width="1.5"/>'
    s1 += f'<circle cx="{x+25}" cy="{y+19}" r="4" fill="#93c5fd"/>'
s1 += glow(60, 86, 300, 300, 8)
s1 += arrow(470, 230, 374, 230, "Dein Laden, wie ihn jeder sieht", 484, 224)
s1 += box(440, 280, 280, 110, PANEL)
s1 += text(460, 308, "Kein Umweg über eine Internetseite", 12, MAIN, "600")
s1 += text(460, 332, "Die Summe steht im Spiel selbst –", 11, MUTED)
s1 += text(460, 350, "du brauchst nur dein eigenes", 11, MUTED)
s1 += text(460, 368, "Laden-Fenster.", 11, MUTED)

# ---- 2: Summe unten ablesen -------------------------------------------------
s2 = head(2, "Unten steht der Gesamtwert - diese Zahl eintragen")
s2 += box(230, 76, 300, 320, "#1a1410", "#4a3626", 6)
s2 += box(230, 76, 300, 30, "#3a2a1c", "#4a3626", 6)
s2 += text(248, 97, "Laden", 13, "#e8d5b5", "600")
s2 += text(512, 98, "&#10005;", 13, "#e8d5b5", anchor="end")
for r in range(6):
    for c in range(5):
        x = 248 + c*56; y = 118 + r*42
        s2 += box(x, y, 50, 38, "#120e0a", "#2e241a", 4)
for r, c in [(1,3),(1,4),(2,2),(2,3),(2,4),(3,0),(3,1),(4,2),(4,3),(4,4),(5,3)]:
    x = 248 + c*56; y = 118 + r*42
    s2 += f'<circle cx="{x+25}" cy="{y+19}" r="11" fill="#1e3a5f" stroke="#60a5fa" stroke-width="1.5"/>'
    s2 += f'<circle cx="{x+25}" cy="{y+19}" r="4" fill="#93c5fd"/>'
s2 += text(380, 388, "156W", 18, "#22d3ee", "700", anchor="middle")
s2 += glow(330, 372, 100, 22, 6)
s2 += arrow(700, 384, 440, 384, "Das ist dein kompletter Shop-Wert", 712, 372, "end")
s2 += box(30, 110, 180, 150, PANEL)
s2 += text(48, 138, "Weiter in M2Hub", 12, MAIN, "600")
s2 += text(48, 160, "Diesen Wert bei Goals", 11, MUTED)
s2 += text(48, 178, "als &#8222;Im Shop&#8220; eintragen", 11, MUTED)
s2 += text(48, 196, "und darunter die Truhen", 11, MUTED)
s2 += text(48, 214, "angeben, die schon über", 11, MUTED)
s2 += text(48, 232, "die Runs gezaehlt sind.", 11, MUTED)

for name, body_ in [("shop1", s1), ("shop2", s2)]:
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">{body_}</svg>'
    cairosvg.svg2png(bytestring=svg.encode("utf-8"),
                     write_to=os.path.join(OUT, name + ".png"),
                     output_width=W*2, output_height=H*2)
    print(name, "ok")
