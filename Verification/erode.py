from PIL import Image,ImageFilter
from collections import deque
for n in ['hacha_1','cabeza_perfil','pierna_4','brazo_2']:
 im=Image.open(f'Assets/OneMoreTime/Art/Originales/02_guerrero_hacha/{n}.png').convert('RGBA'); w,h=im.size
 a=im.getchannel('A').filter(ImageFilter.MinFilter(3)); pix=a.load(); seen=set();comps=[]
 for y in range(h):
  for x in range(w):
   if (x,y) in seen or pix[x,y]<100:continue
   pts=[];q=deque([(x,y)]);seen.add((x,y))
   while q:
    u,v=q.popleft();pts.append((u,v))
    for xx,yy in [(u-1,v),(u+1,v),(u,v-1),(u,v+1)]:
     if 0<=xx<w and 0<=yy<h and (xx,yy)not in seen and pix[xx,yy]>=100:seen.add((xx,yy));q.append((xx,yy))
   comps.append(pts)
 pts=max(comps,key=len);print(n,len(pts),(min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts),max(y for x,y in pts)))
