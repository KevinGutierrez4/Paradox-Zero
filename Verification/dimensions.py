from PIL import Image,ImageDraw
from pathlib import Path
for name in ['02_guerrero_hacha','03_guerrero_arco','04_mago']:
 files=list(Path('Assets/OneMoreTime/Art/Originales',name).glob('*.png'))
 for p in files:
  im=Image.open(p)
  print(str(p),im.size)
for p in Path('Assets/OneMoreTime/Art/Fondos').glob('*.png'):
 im=Image.open(p); im.resize((1500,500)).save('Verification/'+p.name)
