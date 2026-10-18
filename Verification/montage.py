from PIL import Image, ImageDraw
from pathlib import Path
poses=['Idle','WalkContact','WalkPassing','Jump','Fall','Crouch','Windup','Impact','Recovery','Death']
for group,kinds in [('humans',['Player','Archer','Mage']),('animals',['WhiteWolf','BlueWolf','Boss'])]:
 out=Image.new('RGB',(1800,3*390),(30,33,40));d=ImageDraw.Draw(out)
 for row,kind in enumerate(kinds):
  for j,pose in enumerate(poses):
   im=Image.open(f'Verification/Poses/{kind}_{pose}.png');im.thumbnail((180,350));x=j*180;y=row*390
   out.paste(im,(x,y+25));d.text((x+3,y+5),kind+' '+pose,fill='white')
 out.save(f'Verification/{group}-poses.png')
