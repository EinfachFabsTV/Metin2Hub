# Erzeugt den Ton, der am Ende einer Abklingzeit laeuft:
# Assets/Sound/timer.wav - zwei weiche Glockentoene (A5, dann E6).
#
# Absichtlich kein Alarm: zwei Sinustoene mit weichem Ein- und Ausklang,
# Spitze bei etwa -12 dBFS. Laut genug, um neben dem Spiel aufzufallen,
# leise genug, um nicht zu erschrecken - und kurz, weil er oft kommt.
import math, struct, wave

RATE = 44100
OUT = "/home/user/M2Hub-orFabs/desktop/M2Hub.Desktop/Assets/Sound/timer.wav"

def bell(freq, seconds, start, data, peak):
    """Ein Ton mit schnellem Anschlag und langem Ausklang, additiv gemischt."""
    for i in range(int(seconds * RATE)):
        t = i / RATE
        # Anschlag 8 ms, danach exponentiell leiser - wie ein Anschlagen
        attack = min(1.0, t / 0.008)
        decay = math.exp(-t * 4.2)
        # Die Oktave leise dazu macht den Ton koerperhaft statt piepsig
        wave_ = math.sin(2 * math.pi * freq * t) + 0.22 * math.sin(4 * math.pi * freq * t)
        at = start + i
        if at < len(data):
            data[at] += peak * attack * decay * wave_

total = int(1.5 * RATE)
data = [0.0] * total

bell(880.0, 1.2, 0, data, 0.21)                 # A5
bell(1318.5, 1.1, int(0.16 * RATE), data, 0.17) # E6, kurz versetzt

with wave.open(OUT, "w") as f:
    f.setnchannels(1)
    f.setsampwidth(2)
    f.setframerate(RATE)
    f.writeframes(b"".join(
        struct.pack("<h", max(-32768, min(32767, int(v * 32767)))) for v in data))

print(OUT, "geschrieben, Spitze", round(max(abs(v) for v in data), 3))
