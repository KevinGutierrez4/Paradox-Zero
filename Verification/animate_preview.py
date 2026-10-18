from PIL import Image,ImageDraw
from pathlib import Path
frames=[]; durations=[]
for _ in range(2):
 for p in sorted(Path('Verification/Motion').glob('walk_*.png')):
  im=Image.open(p).convert('RGB'); ImageDraw.Draw(im).text((18,18),'CARRERA',fill='white');frames.append(im);durations.append(20)
for i,p in enumerate(sorted(Path('Verification/Motion').glob('axe_*.png'))):
 im=Image.open(p).convert('RGB');ImageDraw.Draw(im).text((18,18),'HACHA CUERPO A CUERPO',fill='white');frames.append(im);durations.append(30 if i % 8 in (0,1,3,5,7) else 20)
frames[0].save('Verification/player-animation.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2)
