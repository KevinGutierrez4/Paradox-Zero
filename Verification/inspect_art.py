from PIL import Image,ImageOps,ImageDraw
from pathlib import Path
root=Path('Assets/OneMoreTime/Art')
files=list((root/'Originales/02_guerrero_hacha').glob('*.png'))+list((root/'Originales/03_guerrero_arco').glob('*.png'))+list((root/'Originales/04_mago').glob('*.png'))
w=1200; cellw=150; cellh=190
out=Image.new('RGB',(w,((len(files)+7)//8)*cellh),(48,53,65)); d=ImageDraw.Draw(out)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGBA'); im=im.crop(im.getbbox()); im.thumbnail((140,150)); x=(i%8)*cellw; y=(i//8)*cellh
 out.paste(im,(x+(140-im.width)//2,y),im); d.text((x+3,y+151),p.parent.name[:2]+' '+p.stem,fill='white')
out.save('Verification/art-contact.png')
out=Image.new('RGB',(1000,600*2),(20,20,30))
for i,p in enumerate(sorted((root/'Fondos').glob('*.png'))):
 im=Image.open(p).convert('RGB'); im.thumbnail((500,560)); out.paste(im,((i%2)*500,(i//2)*600)); ImageDraw.Draw(out).text(((i%2)*500,(i//2)*600+560),p.name,fill='white')
out.save('Verification/backgrounds.png')
