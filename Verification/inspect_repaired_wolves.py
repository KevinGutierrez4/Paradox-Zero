from PIL import Image, ImageDraw
from pathlib import Path
import re
files=list(Path('Assets/OneMoreTime/Generated_Hacha_Final/Sprites').glob('*fur_v6.asset'))
out=Image.new('RGB',(1000,((len(files)+4)//5)*210),(40,55,70));d=ImageDraw.Draw(out)
for i,p in enumerate(files):
 s=p.read_text();w=int(re.search(r'm_Width: (\d+)',s)[1]);h=int(re.search(r'm_Height: (\d+)',s)[1]);b=bytes.fromhex(max(re.findall(r'_typelessdata: ([a-f0-9]+)',s),key=len));rgba=bytearray(len(b))
 for k in range(0,len(b),4):rgba[k:k+4]=bytes([b[k+1],b[k+2],b[k+3],b[k]])
 im=Image.frombytes('RGBA',(w,h),bytes(rgba)).transpose(Image.Transpose.FLIP_TOP_BOTTOM);im=im.crop(im.getbbox());im.thumbnail((190,170));x=i%5*200;y=i//5*210;out.paste(im,(x,y),im);d.text((x,y+175),p.stem[:45],fill='white')
out.save('Verification/wolf-repaired-pieces.png')



