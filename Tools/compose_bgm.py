"""Gem Trail: original 32-bar, 108 BPM casual-game music.
Rebuild: python Tools/compose_bgm.py [output.wav]
Requires numpy. Notes and room tails wrap around a seamless loop.
A sustained harmonic bed prevents the percussive lead from sounding interrupted.
"""
from pathlib import Path
import json
import sys
import wave
import numpy as np

SR = 44100
BPM = 108
BEAT = 60 / BPM
BARS = 32
N = round(BARS * 4 * BEAT * SR)
rng = np.random.default_rng(17081)
mix = np.zeros((N, 2), np.float64)


def add(midi, beat, duration, amp, kind='marimba', pan=0):
    t = np.arange(round(duration * SR)) / SR
    f = 440 * 2 ** ((midi - 69) / 12)
    if kind == 'pad':
        # Overlapping chord voices stay audible between melody attacks.
        sig = (np.sin(2 * np.pi * f * t)
               + .16 * np.sin(2 * np.pi * f * 1.0015 * t)
               + .08 * np.sin(4 * np.pi * f * t))
        attack = np.sin(np.minimum(1, t / .24) * np.pi / 2) ** 2
        release = np.sin(np.clip((duration - t) / .48, 0, 1) * np.pi / 2) ** 2
    else:
        if kind == 'marimba':
            sig = (np.sin(2 * np.pi * f * t + .32 * np.sin(6 * np.pi * f * t) * np.exp(-t * 12))
                   + .10 * np.sin(8 * np.pi * f * t) * np.exp(-t * 12)) * np.exp(-t * 2.6)
        elif kind == 'pluck':
            sig = sum(np.sin(2 * np.pi * f * k * t + .08 * k)
                      * np.exp(-t * (2.1 + k * .65)) / k ** 1.8 for k in range(1, 6))
        elif kind == 'bass':
            sig = (np.sin(2 * np.pi * f * t) + .10 * np.sin(4 * np.pi * f * t)) * (.22 + .78 * np.exp(-t * 2.2))
        else:
            sig = (np.sin(2 * np.pi * f * t) + .12 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 3)) * np.exp(-t * 2.0)
        attack = np.minimum(1, t / .012)
        release = np.minimum(1, (duration - t) / .12)
    sig *= attack * release * amp
    weights = np.sqrt(np.array([1 - pan, 1 + pan]) / 2)
    indices = (round(beat * BEAT * SR) + np.arange(len(t))) % N
    for channel in range(2):
        np.add.at(mix[:, channel], indices, sig * weights[channel])


def shake(beat, amp):
    t = np.arange(round(.11 * SR)) / SR
    noise = rng.normal(0, 1, len(t))
    noise = np.convolve(noise, [1, -.8], mode='same')
    sig = noise * np.exp(-t * 55) * np.minimum(1, t / .003) * amp
    indices = (round(beat * BEAT * SR) + np.arange(len(t))) % N
    mix[indices] += sig[:, None] * np.array([.62, .70])


chords = [[62,66,69,73], [61,64,69,71], [59,62,66,69], [55,59,62,69],
          [54,62,66,69], [52,55,59,62], [55,59,62,69], [57,61,64,69]]
# Repeat the opening phrase's rhythm throughout the song. This preserves
# the first seven seconds and avoids later slow gaps or double-time passages.
rhythm = [
    [(0,1),(1,.5),(1.5,.5),(2.5,.5),(3,.7)],
    [(0,.7),(1,.5),(2,.8),(3.5,.4)],
    [(0,.5),(.75,.5),(1.5,.7),(2.5,.5),(3.25,.6)],
    [(.25,.8),(1.5,.5),(2.25,.6),(3.25,.6)],
]
pitch_phrases = [
    [74,78,81,78,76], [73,76,81,78],
    [78,76,74,73,71], [74,78,76,74],
    [81,78,83,81,78], [79,78,76,74],
    [74,76,78,74,71], [76,73,69,73],
]
melodies = [[(beat, midi, duration) for (beat, duration), midi
             in zip(rhythm[i % len(rhythm)], pitches)]
            for i, pitches in enumerate(pitch_phrases)]

for bar in range(BARS):
    chord = chords[bar % 8]
    base = bar * 4
    for j, midi in enumerate(chord):
        add(midi, base - .24 / BEAT, 4 * BEAT + .72, .035 if j else .026, 'pad', (j - 1.5) * .18)
    for beat in [0, 1.5, 2, 3.5]:
        for j, midi in enumerate(chord):
            add(midi, base + beat + j * .025 + rng.uniform(-.006,.006), 1.15,
                .014 * rng.uniform(.93,1.06), 'pluck', -.30)
    # A quiet continuous arpeggio connects phrases without loud percussion.
    for eighth in range(8):
        add(chord[eighth % 4] + 12, base + eighth * .5, .92, .008, 'pluck', .28)
    for beat in [0, 2]:
        add(chord[0] - 12, base + beat, 2 * BEAT + .10, .047, 'bass', -.03)
    # Keep the same rhythmic pattern and lead strength in every section.
    for beat, midi, duration in melodies[bar % 8]:
        add(midi, base + beat, duration * BEAT + .90,
            .039, 'marimba', .14)
    for beat in np.arange(.5, 4, .5):
        shake(base + beat, .004 if beat % 1 else .003)
    if bar % 4 == 0:
        add(chord[2] + 24, base + .02, 2.0, .008, 'bell', .35)

# Circular room reflections retain tails across the end/start of the file.
dry = mix.copy()
for seconds, gain in [(.071,.12), (.113,.09), (.179,.07), (.257,.055)]:
    mix += np.roll(dry, round(seconds * SR), axis=0) * gain
mix -= mix.mean(axis=0)
rms = float(np.sqrt(np.mean(mix ** 2)))
gain = min(10 ** (-18 / 20) / max(rms, 1e-9), .82 / max(float(abs(mix).max()), 1e-9))
mix *= gain
# Correct only the tiny residual endpoint mismatch, without a fade to silence.
k = 256
delta = mix[-1] - mix[0]
mix[-k:] -= np.linspace(0,1,k)[:,None] * delta
out = Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1] / 'Assets/LoopSort/Resources/LoopSort/Audio/music.wav'
out.parent.mkdir(parents=True, exist_ok=True)
tmp = out.with_suffix('.new.wav')
with wave.open(str(tmp),'wb') as wav:
    wav.setnchannels(2)
    wav.setsampwidth(2)
    wav.setframerate(SR)
    wav.writeframes((np.clip(mix,-1,1) * 32767).astype('<i2').tobytes())
tmp.replace(out)
window = round(SR * .1)
blocks = mix[:len(mix)//window*window].reshape(-1,window,2)
levels = 20 * np.log10(np.maximum(np.sqrt(np.mean(blocks**2,axis=(1,2))), 1e-10))
print(json.dumps({'title':'Gem Trail - continuous arrangement','bpm':BPM,'bars':BARS,
    'seconds':N/SR,'peak':float(abs(mix).max()),
    'rmsDb':float(20*np.log10(np.sqrt(np.mean(mix**2)))),
    'quietest100msDb':float(levels.min()),'loudest100msDb':float(levels.max()),
    'seamDifference':float(abs(mix[-1]-mix[0]).max()),'path':str(out)},indent=2))
