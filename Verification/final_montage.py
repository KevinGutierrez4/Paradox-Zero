from PIL import Image,ImageDraw
poses=[('Idle','Reposo'),('WalkContact','Carrera'),('Jump','Salto'),('Crouch','Agachado'),('Windup','Anticipación'),('Impact','Corte')]
out=Image.new('RGB',(1056,744),(30,34,42));d=ImageDraw.Draw(out)
for i,(pose,label) in enumerate(poses):
 im=Image.open(f'Verification/Poses/Player_{pose}.png'); im=im.resize((352,352)); x=(i%3)*352;y=(i//3)*372;out.paste(im,(x,y+20));d.text((x+14,y+5),label,fill='white')
out.save('Verification/player-final-poses.png')
