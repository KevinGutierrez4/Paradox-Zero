from PIL import Image
from pathlib import Path
for n in ['hacha_1','cabeza_perfil']:
 im=Image.open(f'Assets/OneMoreTime/Art/Originales/02_guerrero_hacha/{n}.png').convert('RGBA');out=Image.new('RGBA',im.size,(50,50,50,255));out.alpha_composite(im);out.resize((im.width*4,im.height*4)).save('Verification/'+n+'-zoom.png')
