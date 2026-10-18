from PIL import Image
from collections import Counter
from pathlib import Path
for n in ['pierna_2','brazo_2','torso_frente','hacha_1']:
 im=Image.open(Path('Assets/OneMoreTime/Art/Originales/02_guerrero_hacha')/(n+'.png')).convert('RGBA');print(n,Counter(im.getdata()).most_common(3),im.getpixel((0,0)))
