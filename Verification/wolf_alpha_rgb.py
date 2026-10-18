from PIL import Image
from pathlib import Path
for name in ['pata_delantera_muslo','cola_base','hocico_superior']:
 p=Path('Assets/OneMoreTime/Art/Originales/01_lobo_blanco',name+'.png'); im=Image.open(p).convert('RGBA');rgb=im.convert('RGB');rgb.resize((im.width*4,im.height*4)).save('Verification/'+name+'-rgb.png');print(name,im.size)
