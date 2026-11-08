#!/usr/bin/env python3
"""Game audio from the Higgsfield takes in art-src/audio (free; needs ffmpeg).

  python3 Tools/art/audio.py

Sound effects are trimmed to the useful hit, faded and peak-normalised, mono; the ambience beds and the
music are cut to a seamless loop (the tail crossfaded into the head). Everything is written as OGG Vorbis to
Assets/Resources/Audio/<name>.ogg, which Sfx loads by name.

Sources: Seed Audio 1.0 (Higgsfield) for the island effects and ambience (2026-11-08); the UI takes are shared
with Ironhorse Frontier. Sfx.cs loads them; missing clips are skipped.
"""
import os
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "art-src", "audio")
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 44100

# name: (source file, start s, length s, peak, fade-out s). start=None finds the first hit.
# Several names can share a take (the code asks for sounds by what happens, not by file).
SFX = {
    # Generic UI and reward sounds (takes shared with Ironhorse Frontier).
    "click":   ("click.wav",   None, 0.12, 0.55, 0.05),
    "beep":    ("click.wav",   None, 0.12, 0.45, 0.05),
    "map":     ("click.wav",   None, 0.12, 0.55, 0.05),
    "coin":    ("coin.wav",    0.78, 0.70, 0.80, 0.30),
    "buy":     ("coin.wav",    0.78, 0.70, 0.85, 0.30),
    "quest":   ("coin.wav",    0.78, 0.70, 0.80, 0.30),
    "built":   ("build.wav",   None, 1.45, 0.85, 0.20),
    "craft":   ("build.wav",   None, 0.90, 0.70, 0.30),
    "error":   ("error.wav",   None, 0.40, 0.55, 0.12),
    "levelup": ("levelup.wav", None, 2.90, 0.85, 0.45),
    "deliver": ("deliver.wav", None, 0.45, 0.55, 0.20),
    # Island sounds (Seed Audio, 2026-11-08).
    "chop":     ("chop.wav",     None, 0.70, 0.90, 0.25),
    "mine":     ("mine.wav",     None, 0.80, 0.85, 0.30),
    "gather":   ("gather.wav",   None, 0.80, 0.70, 0.30),
    "drink":    ("gather.wav",   None, 0.60, 0.55, 0.30),
    "treefall": ("treefall.wav", None, 2.80, 0.90, 0.80),
    "hit":      ("hit.wav",      None, 0.50, 0.85, 0.20),
    "hurt":     ("hurt.wav",     None, 0.65, 0.75, 0.20),
    "pirate_die": ("hurt.wav",   None, 0.65, 0.80, 0.20),
    "swing":    ("swing.wav",    None, 0.45, 0.55, 0.15),
    "grunt":    ("grunt.wav",    None, 1.40, 0.75, 0.40),
    "boardie":  ("grunt.wav",    None, 1.80, 0.80, 0.80),
    "static":   ("static.wav",   None, 2.20, 0.55, 0.50),
    "eat":      ("eat.wav",      None, 0.90, 0.70, 0.30),
}
# name: (source, start s, end s, crossfade s, peak, channels, ffmpeg filter or None)
LOOPS = {
    "amb_surf":   ("amb_surf.wav",   0.5, 26.0, 2.5, 0.70, 1, None),
    "amb_jungle": ("amb_jungle.wav", 0.5, 33.0, 2.5, 0.65, 1, None),
}


def load(name, channels, af=None):
    cmd = ["ffmpeg", "-v", "error", "-i", os.path.join(SRC, name)] + (["-af", af] if af else []) + ["-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"]
    raw = subprocess.run(cmd, check=True, capture_output=True).stdout
    a = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    return a.reshape(-1, channels)


def save(name, a):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(a.shape[1]), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "5", path], input=a.astype(np.float32).tobytes(), check=True)
    return path


def normalise(a, peak):
    m = np.abs(a).max()
    return a * (peak / m) if m > 0 else a


def sfx(name, src, start, length, peak, fade):
    a = load(src, 1)
    if start is None:
        env = np.abs(a[:, 0])
        hit = np.argmax(env > env.max() * 0.12)
        s = max(0, hit - int(0.008 * SR))
    else:
        s = int(start * SR)
    a = a[s:s + int(length * SR)].copy()
    n = len(a)
    a[:int(0.004 * SR)] *= np.linspace(0, 1, int(0.004 * SR))[:, None]  # no click at the cut
    f = min(n, int(fade * SR))
    a[n - f:] *= np.linspace(1, 0, f)[:, None] ** 2
    return normalise(a, peak)


def loop(src, start, end, xfade, peak, channels, af=None):
    a = load(src, channels, af)
    a = a[int(start * SR):int(end * SR)].copy()
    x = int(xfade * SR)
    head, tail = a[:x].copy(), a[-x:].copy()
    t = np.linspace(0, 1, x)[:, None]
    body = a[:-x].copy()
    body[:x] = head * np.sqrt(t) + tail * np.sqrt(1 - t)  # equal-power crossfade: the end flows into the start
    return normalise(body, peak)


def main():
    if not os.path.isdir(SRC):
        sys.exit("missing " + SRC)
    for name, spec in SFX.items():
        print(name, save(name, sfx(name, *spec)))
    for name, spec in LOOPS.items():
        print(name, save(name, loop(*spec)))


if __name__ == "__main__":
    main()
