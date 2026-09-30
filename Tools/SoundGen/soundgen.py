#!/usr/bin/env python3
"""
Generates the game's sounds with the ElevenLabs API (sound effects + music) from sounds.json.

  ELEVENLABS_API_KEY=... python3 Tools/SoundGen/soundgen.py              # everything missing
  python3 Tools/SoundGen/soundgen.py --only NitroFire,Coin --force         # regenerate some
  python3 Tools/SoundGen/soundgen.py --only Crash --takes 3                # 3 variations -> takes/, keeps take 1
  python3 Tools/SoundGen/soundgen.py --pick Crash=2                        # use take 2 instead (no API call)
  python3 Tools/SoundGen/soundgen.py --dry-run                             # show the requests, no key needed
  python3 Tools/SoundGen/soundgen.py --selftest                            # test the audio processing offline

Output: Assets/Resources/Audio/<name>.wav (effects, loops) and Music.mp3. Unity's AudioDirector plays these and falls
back to the built-in procedural sound for anything missing, so partial runs are fine. Every generation is recorded
in generated.json (prompt, model, date, file hash) as a provenance record (Steam asks about AI-generated content).

Processing (effects): trim leading / trailing silence, 8 ms fade-out, peak-normalise to -1 dBFS x gain.
Loops are generated with loop=true and only normalised. Standard library only (no pip installs).
API reference checked against the official elevenlabs-python SDK 2.70: POST /v1/sound-generation
(text, duration_seconds 0.5-30, prompt_influence 0-1, loop, model_id eleven_text_to_sound_v2; query output_format)
and POST /v1/music (prompt, music_length_ms 3000-600000, force_instrumental).
"""
import argparse, datetime, hashlib, json, math, os, shutil, struct, sys, time, urllib.error, urllib.request, wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
TAKES = os.path.join(HERE, "takes")
RECORD = os.path.join(HERE, "generated.json")
API = "https://api.elevenlabs.io"
SFX_MODEL = "eleven_text_to_sound_v2"
RATE = 32000                     # matches the procedural bank; pcm_44100 needs a Pro plan
PEAK = 10 ** (-1 / 20)           # -1 dBFS


# ------------------------------------------------------------------ audio processing (pure python, testable offline)

def pcm16_to_floats(data):
    n = len(data) // 2
    return [s / 32768.0 for s in struct.unpack("<%dh" % n, data[: n * 2])]


def process(samples, kind, gain):
    """Trim silence (effects), fade out, peak-normalise. Returns a new list."""
    x = list(samples)
    if not x:
        return x
    if kind == "sfx":
        floor = 10 ** (-50 / 20) * max(abs(v) for v in x)
        start = next((i for i, v in enumerate(x) if abs(v) > floor), 0)
        end = len(x) - next((i for i, v in enumerate(reversed(x)) if abs(v) > floor), 0)
        start = max(0, start - int(0.005 * RATE))          # keep 5 ms before the attack
        x = x[start:end] or x
        fade = min(len(x), int(0.008 * RATE))
        for i in range(fade):
            x[len(x) - 1 - i] *= i / fade
    mean = sum(x) / len(x)
    x = [v - mean for v in x]
    peak = max(abs(v) for v in x) or 1.0
    k = PEAK * gain / peak
    return [v * k for v in x]


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(round(v * 32767))))) for v in samples))


def wrap_jump(samples):
    """Loop seam check: jump across the wrap vs the 99.9th percentile of in-loop steps."""
    steps = sorted(abs(samples[i] - samples[i - 1]) for i in range(1, len(samples)))
    return abs(samples[0] - samples[-1]), steps[int(len(steps) * 0.999)] if steps else 0.0


# ------------------------------------------------------------------ API

def post(path, body, key, query="", retries=4):
    url = API + path + (("?" + query) if query else "")
    data = json.dumps(body).encode()
    for attempt in range(retries + 1):
        req = urllib.request.Request(url, data=data, method="POST",
                                     headers={"xi-api-key": key, "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")[:400]
            if e.code in (429, 500, 502, 503, 504) and attempt < retries:
                wait = 2 ** (attempt + 1)
                print(f"    HTTP {e.code}, retrying in {wait}s ...")
                time.sleep(wait)
                continue
            raise SystemExit(f"ElevenLabs {path} failed: HTTP {e.code}: {detail}")
        except urllib.error.URLError as e:
            if attempt < retries:
                time.sleep(2 ** (attempt + 1))
                continue
            raise SystemExit(f"Cannot reach {API}: {e.reason} (is api.elevenlabs.io allowed by the network policy?)")


def sfx_request(s, style):
    body = {"text": f"{s['prompt']}. Style: {style}.", "prompt_influence": s.get("influence", 0.5), "model_id": SFX_MODEL}
    if s.get("duration"):
        body["duration_seconds"] = max(0.5, min(30.0, float(s["duration"])))
    if s["kind"] == "loop":
        body["loop"] = True
    return body


def music_request(s):
    return {"prompt": s["prompt"], "music_length_ms": int(s.get("length_ms", 90000)), "force_instrumental": True}


# ------------------------------------------------------------------ main

def load_record():
    if os.path.exists(RECORD):
        with open(RECORD) as f:
            return json.load(f)
    return {}


def save_record(rec):
    with open(RECORD, "w") as f:
        json.dump(rec, f, indent=2, sort_keys=True)
        f.write("\n")


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()[:16]


def target(s):
    return os.path.join(OUT, s["name"] + (".mp3" if s["kind"] == "music" else ".wav"))


def generate(s, style, key, takes, record):
    name = s["name"]
    if s["kind"] == "music":
        body = music_request(s)
        audio = post("/v1/music", body, key)
        path = target(s)
        os.makedirs(OUT, exist_ok=True)
        with open(path, "wb") as f:
            f.write(audio)
        print(f"  {name}: music {len(audio) // 1024} KB -> {os.path.relpath(path, ROOT)}")
        model = "music (default model)"
    else:
        body = sfx_request(s, style)
        paths = []
        for t in range(1, takes + 1):
            raw = post("/v1/sound-generation", body, key, query=f"output_format=pcm_{RATE}")
            x = process(pcm16_to_floats(raw), s["kind"], float(s.get("gain", 1.0)))
            p = os.path.join(TAKES, f"{name}_{t}.wav") if takes > 1 else target(s)
            write_wav(p, x)
            paths.append(p)
            info = f"{len(x) / RATE:.2f} s"
            if s["kind"] == "loop":
                jump, p999 = wrap_jump(x)
                info += f", loop seam {jump:.3f} ({'ok' if jump <= p999 * 1.5 else 'AUDIBLE? check'})"
            print(f"  {name} take {t}: {info} -> {os.path.relpath(p, ROOT)}")
        if takes > 1:
            shutil.copyfile(paths[0], target(s))
        path = target(s)
        model = SFX_MODEL
    record[name] = {"prompt": body.get("text") or body.get("prompt"), "model": model, "kind": s["kind"],
                    "date": datetime.date.today().isoformat(), "file": os.path.relpath(path, ROOT), "sha256_16": sha(path)}


def selftest():
    global RATE
    import random
    rnd = random.Random(1)
    sig = [0.0] * 800 + [math.sin(i * 0.3) * math.exp(-i / 2000) * 0.3 for i in range(8000)] + [0.0] * 3000
    y = process(sig, "sfx", 1.0)
    assert abs(max(abs(v) for v in y) - PEAK) < 1e-6, "peak normalised to -1 dBFS"
    assert len(y) < len(sig) - 3000, "silence trimmed"
    assert abs(y[-1]) < 1e-3, "faded out"
    loop = [math.sin(2 * math.pi * 5 * i / RATE) * 0.5 + rnd.uniform(-0.01, 0.01) for i in range(RATE)]
    z = process(loop, "loop", 0.8)
    assert len(z) == len(loop), "loops are not trimmed"
    j, p = wrap_jump(z)
    assert j <= p * 1.5, "seam check works on a periodic signal"
    raw = struct.pack("<3h", 0, 16384, -32768)
    assert pcm16_to_floats(raw) == [0.0, 0.5, -1.0]
    with open(os.path.join(HERE, "sounds.json")) as f:
        cfg = json.load(f)
    names = [s["name"] for s in cfg["sounds"]]
    assert len(names) == len(set(names)), "duplicate names"
    for s in cfg["sounds"]:
        if s["kind"] != "music":
            b = sfx_request(s, cfg["style"])
            assert 0.5 <= b.get("duration_seconds", 1) <= 30 and 0 <= b["prompt_influence"] <= 1, s["name"]
    print(f"selftest ok ({len(names)} sounds in sounds.json)")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="comma-separated names")
    ap.add_argument("--force", action="store_true", help="regenerate even if the file exists")
    ap.add_argument("--takes", type=int, default=1, help="variations per effect (saved in takes/, first one used)")
    ap.add_argument("--pick", help="NAME=TAKE: use an earlier take (no API call)")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()

    if a.selftest:
        return selftest()
    if a.pick:
        name, take = a.pick.split("=")
        src = os.path.join(TAKES, f"{name}_{take}.wav")
        shutil.copyfile(src, os.path.join(OUT, name + ".wav"))
        rec = load_record()
        if name in rec:
            rec[name]["sha256_16"] = sha(os.path.join(OUT, name + ".wav"))
            rec[name]["take"] = int(take)
            save_record(rec)
        return print(f"{name}: using take {take}")

    with open(os.path.join(HERE, "sounds.json")) as f:
        cfg = json.load(f)
    only = set(a.only.split(",")) if a.only else None
    todo = [s for s in cfg["sounds"] if (only is None or s["name"] in only) and (a.force or not os.path.exists(target(s)))]
    if only:
        unknown = only - {s["name"] for s in cfg["sounds"]}
        if unknown:
            raise SystemExit("unknown names: " + ", ".join(sorted(unknown)))
    if not todo:
        return print("nothing to do (all files exist; use --force to regenerate)")

    if a.dry_run:
        for s in todo:
            body = music_request(s) if s["kind"] == "music" else sfx_request(s, cfg["style"])
            print(("POST /v1/music " if s["kind"] == "music" else f"POST /v1/sound-generation?output_format=pcm_{RATE} ")
                  + json.dumps(body))
        return print(f"{len(todo)} request(s) (x{a.takes} takes for effects)")

    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        raise SystemExit("ELEVENLABS_API_KEY is not set (add it to the environment settings, never to the repo)")
    record = load_record()
    print(f"generating {len(todo)} sound(s) ...")
    for s in todo:
        generate(s, cfg["style"], key, max(1, a.takes), record)
        save_record(record)   # after each one, so an interrupted run keeps what it made
    print("done. Unity picks the files up from Assets/Resources/Audio on the next import.")


if __name__ == "__main__":
    main()
