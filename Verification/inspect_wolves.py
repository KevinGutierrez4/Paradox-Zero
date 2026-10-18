from PIL import Image,ImageDraw
from pathlib import Path
files=[]
for folder in ['01_lobo_blanco','05_lobo_azul']:
 files+=list(Path('Assets/OneMoreTime/Art/Originales',folder).glob('*.png'))
out=Image.new('RGB',(1200,((len(files)+7)//8)*180),(35,55,75));d=ImageDraw.Draw(out)
for i,p in enumerate(files):
 im=Image.open(p).convert('RGBA');im.thumbnail((145,140));x=(i%8)*150;y=(i//8)*180;out.paste(im,(x,y),im);d.text((x+2,y+145),p.parent.name[:2]+' '+p.stem,fill='white')
out.save('Verification/wolf-original-pieces.png')
for f in ['01_lobo_blanco','05_lobo_azul']:
 p=Path('Assets/OneMoreTime/Art/Originales',f,'torso.png');im=Image.open(p).convert('RGBA');out=Image.new('RGBA',im.size,(70,120,170,255));out.alpha_composite(im);out.resize((im.width*3,im.height*3)).save('Verification/'+f+'-torso.png')
