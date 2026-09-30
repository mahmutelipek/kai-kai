# Summarises reaction-distance measurements: on-screen size at 1080p and colour contrast against the road.
import json, math, sys
names = ["Cone","ConcreteBarrier","ParkedCar","ConstructionBarrier","Pothole","Crate","BrokenPiece","Divider","Coin","Diamond","Nitro","MovingCar"]
def lab(rgb):
    def lin(c):
        c /= 255.0
        return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
    r,g,b = (lin(x) for x in rgb)
    X = (0.4124*r+0.3576*g+0.1805*b)/0.95047; Y = 0.2126*r+0.7152*g+0.0722*b; Z = (0.0193*r+0.1192*g+0.9505*b)/1.08883
    f = lambda t: t**(1/3) if t > 0.008856 else 7.787*t+16/116
    return (116*f(Y)-16, 500*(f(X)-f(Y)), 200*(f(Y)-f(Z)))
road = lab((0x4B*0.85, 0x50*0.85, 0x5B*0.85))
rows = []
for d in (50, 30):
    for k, n in enumerate(names):
        s = json.load(open(f"out/react{d}_{k}.stats.json"))
        st = [x for x in s["stats"] if x["tag"] not in ("road","backdrop","board") and not x["tag"].startswith("P")][0]
        w = st["maxX"]-st["minX"]+1 if st["pixels"] else 0
        h = st["maxY"]-st["minY"]+1 if st["pixels"] else 0
        rows.append((d, n, st["pixels"], w, h, st.get("deP90", 0), st.get("contrastPixels", 0)))
print("| dist | item | visible px | bbox w x h (px @1080p) | dE p90 vs road | px with dE>30 |")
print("|---|---|---|---|---|---|")
for d,n,p,w,h,de,cp in rows: print(f"| {d} m | {n} | {p} | {w} x {h} | {de:.0f} | {cp} |")
