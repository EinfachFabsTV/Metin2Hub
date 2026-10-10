# Erzeugt die Toene, die am Ende einer Abklingzeit laufen:
# Assets/Sound/timer-<name>.wav - drei zur Auswahl.
#
# Absichtlich kein Alarm, aber deutlich hoerbar: Spitze bei etwa -5 dBFS.
# Die erste Fassung lag bei -11 dBFS und ging neben dem Spiel unter.
import math, struct, wave

RATE = 44100
DIR = "/home/user/M2Hub-orFabs/desktop/M2Hub.Desktop/Assets/Sound/"
PEAK = 0.55

def tone(data, freq, seconds, start, level, decay=4.2, attack=0.008, harmonic=0.22):
    """Ein Ton mit schnellem Anschlag und Ausklang, additiv gemischt."""
    for i in range(int(seconds * RATE)):
        t = i / RATE
        env = min(1.0, t / attack) * math.exp(-t * decay)
        wave_ = math.sin(2 * math.pi * freq * t) + harmonic * math.sin(4 * math.pi * freq * t)
        at = start + i
        if at < len(data):
            data[at] += level * env * wave_

def lowpass(data, cutoff):
    """Ein einfacher Tiefpass: nimmt die Schaerfe heraus, ohne den Ton zu
    verschlucken - das ist der Unterschied zwischen dumpf und stumpf."""
    a = math.exp(-2 * math.pi * cutoff / RATE)
    last = 0.0
    for i, v in enumerate(data):
        last = (1 - a) * v + a * last
        data[i] = last

def fade(data, seconds=0.05):
    """Das Ende weich auslaufen lassen - ein harter Schnitt knackt."""
    n = int(seconds * RATE)
    for i in range(n):
        data[len(data) - n + i] *= 1 - i / n

def write(name, seconds, build):
    data = [0.0] * int(seconds * RATE)
    build(data)

    # Auf die Zielspitze bringen - so klingen alle drei gleich laut.
    high = max(abs(v) for v in data) or 1.0
    scale = PEAK / high

    with wave.open(DIR + "timer-" + name + ".wav", "w") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(
            struct.pack("<h", max(-32768, min(32767, int(v * scale * 32767)))) for v in data))

    print(name, "ok")

# Weich: der Standard. Tief und rund wie ein angeschlagenes Holz, mit
# langsamem Anschlag und Tiefpass - faellt auf, ohne zu erschrecken.
def soft(d):
    # Grundton und Quinte darueber, beide ohne scharfe Oberwellen
    tone(d, 196.0, 1.5, 0, 1.0, decay=3.0, attack=0.025, harmonic=0.12)
    tone(d, 293.7, 1.3, int(0.03 * RATE), 0.55, decay=3.4, attack=0.03, harmonic=0.08)
    # Eine Terz spaeter macht aus dem Schlag einen kurzen Zweiklang
    tone(d, 246.9, 1.2, int(0.3 * RATE), 0.5, decay=3.2, attack=0.03, harmonic=0.08)
    lowpass(d, 1400)
    fade(d)

write("weich", 1.9, soft)

# Glocke: zwei weiche Toene, A5 und E6 - der Standard.
write("glocke", 1.6, lambda d: (
    tone(d, 880.0, 1.3, 0, 1.0),
    tone(d, 1318.5, 1.2, int(0.16 * RATE), 0.8)))

# Doppelton: zwei kurze, klare Signale - faellt schneller auf, ist aber
# auch schneller vorbei.
write("doppelton", 0.75, lambda d: (
    tone(d, 1046.5, 0.3, 0, 1.0, decay=11, harmonic=0.1),
    tone(d, 1396.9, 0.35, int(0.22 * RATE), 1.0, decay=11, harmonic=0.1)))

# Gong: tief und lang, geht neben hellen Spielgeraeuschen nicht unter und
# klingt trotzdem ruhig.
write("gong", 2.4, lambda d: (
    tone(d, 196.0, 2.3, 0, 1.0, decay=1.5, attack=0.012, harmonic=0.5),
    tone(d, 392.0, 2.0, 0, 0.45, decay=1.8, harmonic=0.3),
    tone(d, 587.3, 1.4, int(0.02 * RATE), 0.2, decay=2.6)))
