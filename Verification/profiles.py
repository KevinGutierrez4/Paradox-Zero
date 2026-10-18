from PIL import Image, ImageDraw
from pathlib import Path
profiles=[ [.59,.62,.67,.70,.74,.77,.80,.81,.82,.83,.82,.81,.80,.79,.78,.78,.76,.75,.74,.75,.77], [.57,.60,.62,.63,.65,.67,.69,.71,.72,.73,.74,.74,.74,.73,.72,.71,.70,.68,.67,.67,.68], [.56,.59,.61,.63,.66,.69,.71,.73,.75,.77,.78,.79,.80,.81,.82,.81,.80,.78,.77,.77,.77], [.60,.62,.65,.67,.70,.72,.74,.75,.76,.77,.78,.79,.79,.78,.77,.75,.73,.70,.67,.65,.64] ]
for i, ys in enumerate(profiles,1):
 im=Image.open(f'Verification/Fondo_{i:02}.png');d=ImageDraw.Draw(im)
 pts=[(j*(im.width-1)/(len(ys)-1),y*im.height) for j,y in enumerate(ys)]
 d.line(pts,fill='#22ff44',width=3)
 im.save(f'Verification/Profile_{i}.png')
