from PIL import Image
from collections import deque
from pathlib import Path
for n in ['hacha_1','cabeza_perfil','pierna_4','brazo_2']:
 im=Image.open(f'Assets/OneMoreTime/Art/Originales/02_guerrero_hacha/{n}.png').convert('RGBA');w,h=im.size
 for threshold in [100,200,250]:
  seen=set(); comps=[]; pix=im.load()
  for y in range(h):
   for x in range(w):
    if (x,y) in seen or pix[x,y][3]<threshold:continue
    pts=[];q=deque([(x,y)]);seen.add((x,y))
    while q:
     a,b=q.popleft();pts.append((a,b))
     for u,v in [(a-1,b),(a+1,b),(a,b-1),(a,b+1)]:
      if 0<=u<w and 0<=v<h and (u,v)not in seen and pix[u,v][3]>=threshold:seen.add((u,v));q.append((u,v))
    comps.append(pts)
  pts=max(comps,key=len); print(n,threshold,len(pts),(min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts),max(y for x,y in pts)), sorted([len(a) for a in comps],reverse=True)[:5])
