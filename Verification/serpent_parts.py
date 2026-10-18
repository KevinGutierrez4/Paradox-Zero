from PIL import Image,ImageDraw
from pathlib import Path
files=list(Path('Assets/OneMoreTime/Art/Originales/06_serpiente_azul').glob('*.png'))
out=Image.new('RGB',(1100,((len(files)+7)//8)*180),(45,50,60));d=ImageDraw.Draw(out)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGBA');im=im.crop(im.getbbox());im.thumbnail((130,145));x=(i%8)*137;y=(i//8)*180;out.paste(im,(x,y),im);d.text((x,y+147),p.stem,fill='white')
out.save('Verification/serpent-art.png')
