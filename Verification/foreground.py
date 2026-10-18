from PIL import Image, ImageDraw
from collections import deque
from pathlib import Path
for n in ['hacha_1','cabeza_perfil','pierna_4','brazo_2']:
 im=Image.open(f'Assets/OneMoreTime/Art/Originales/02_guerrero_hacha/{n}.png').convert('RGBA');w,h=im.size; pix=im.load();seen=set(); comps=[]
 def fg(x,y):
  r,g,b,a=pix[x,y];return a>=100 and not (min(r,g,b)>=225 and max(r,g,b)-min(r,g,b)<8)
 for y in range(h):
  for x in range(w):
   if (x,y) in seen or not fg(x,y):continue
   pts=[];q=deque([(x,y)]);seen.add((x,y))
   while q:
    u,v=q.popleft();pts.append((u,v))
    for xx,yy in [(u-1,v),(u+1,v),(u,v-1),(u,v+1)]:
     if 0<=xx<w and 0<=yy<h and (xx,yy)not in seen and fg(xx,yy):seen.add((xx,yy));q.append((xx,yy))
   comps.append(pts)
 pts=max(comps,key=len);print(n,len(pts),(min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts),max(y for x,y in pts)))
