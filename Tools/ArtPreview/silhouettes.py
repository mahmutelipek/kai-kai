# M4 acceptance 1: six riders at gameplay camera distance. Silhouette IoU (same spot) and colour distance (side by side).
import json, math, itertools
def lab(rgb):
    def lin(c):
        c /= 255; return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4
    r,g,b = (lin(x) for x in rgb)
    X=(0.4124*r+0.3576*g+0.1805*b)/0.95047; Y=0.2126*r+0.7152*g+0.0722*b; Z=(0.0193*r+0.1192*g+0.9505*b)/1.08883
    f=lambda t: t**(1/3) if t>0.008856 else 7.787*t+16/116
    return (116*f(Y)-16,500*(f(X)-f(Y)),200*(f(Y)-f(Z)))
sil=json.load(open('out/chars_silhouette.stats.json'))
col=json.load(open('out/chars_colors.stats.json'))
tags=sil['maskTags']; iou=sil['iou']
P=[s for s in col['stats'] if s['tag'].startswith('P')]
print("rider sizes on screen (1920x1080, gameplay camera, rider at deck centre):")
for s in P: print(f"  {s['tag']}: {s['maxX']-s['minX']+1} x {s['maxY']-s['minY']+1} px, mean colour ({s['r']:.0f},{s['g']:.0f},{s['b']:.0f})")
print("pairwise: silhouette IoU (1 = identical outline) / colour dE76 of mean colours")
worst_iou=0; worst_de=999
for a,b in itertools.combinations(range(6),2):
    de=math.dist(lab((P[a]['r'],P[a]['g'],P[a]['b'])),lab((P[b]['r'],P[b]['g'],P[b]['b'])))
    worst_iou=max(worst_iou,iou[a][b]); worst_de=min(worst_de,de)
    print(f"  {tags[a]}-{tags[b]}: IoU {iou[a][b]:.2f}  dE {de:.0f}")
print(f"max IoU {worst_iou:.2f}, min colour dE {worst_de:.0f}")
