from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json

root = Path('Verification/ShieldHeroV4')
asset = Path('Assets/OneMoreTime/Art/HeroShieldV4')
rows = ['Idle', 'Walk', 'Crouch', 'Attack', 'Jump', 'Fall', 'Block', 'Die']
names = ['Respiración', 'Caminar', 'Agachado', 'Hachazo', 'Salto', 'Caída', 'Bloqueo', 'Muerte']
durations = [1.2, .8, 1.2, .625, .4, .6, 1.2, .9]
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 18)
frames = {row: [Image.open(root / f'{row}_{i:02}.png').convert('RGBA') for i in range(12)] for row in rows}

def tile(im, label, mirror=False):
    canvas = Image.new('RGB', (320, 325), (35, 40, 49))
    draw = ImageDraw.Draw(canvas)
    draw.text((10, 3), label, font=font, fill=(235, 240, 245))
    im = im.crop((32, 48, 352, 368))
    if mirror:
        im = im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    canvas.paste(im, (0, 26), im)
    return canvas

for row, label, duration in zip(rows, names, durations):
    images = [tile(im, label) for im in frames[row]]
    delays = [round(duration * 1000 / 12)] * 12
    if row == 'Die': delays[-1] = 1000
    images[0].save(root / f'{row}.gif', save_all=True, append_images=images[1:], duration=delays, loop=0, disposal=2)

for mirror in [False, True]:
    images = []
    for f in range(64):
        canvas = Image.new('RGB', (960, 975), (35, 40, 49))
        for n, (row, label, duration) in enumerate(zip(rows, names, durations)):
            t = (f * .05) % (duration + (.55 if row in ['Attack', 'Die'] else 0))
            index = min(11, int(t / duration * 12))
            if row == 'Attack' and t > duration: index = 0
            canvas.paste(tile(frames[row][index], label, mirror), (n % 3 * 320, n // 3 * 325))
        images.append(canvas)
    suffix = '_Left' if mirror else ''
    images[0].save(root / f'Hero_All{suffix}.gif', save_all=True, append_images=images[1:], duration=50, loop=0, disposal=2)
    if not mirror:
        images[0].save(root / 'Hero_Slow.gif', save_all=True, append_images=images[1:], duration=150, loop=0, disposal=2)

manifest = dict(image='Hero_Shield_96.png', width=4608, height=3072, columns=12, rows=8, cell=[384,384], pixels_per_unit=96, pivot=[.5,.1875], facing='right', animations=[])
for n, (row, duration) in enumerate(zip(rows, durations)):
    manifest['animations'].append(dict(name=row, row_from_top=n, duration_seconds=duration, loop=n in [0,1,2,6], frames=[dict(name=f'{row}_{i:02}',rect_from_bottom_left=[i*384,(7-n)*384,384,384]) for i in range(12)]))
(asset / 'animations.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print('Verified RGBA sheet:', Image.open(asset / manifest['image']).size, Image.open(asset / manifest['image']).getchannel('A').getextrema())
print('Native render previews:', len(rows), 'actions, normal/slower, both directions.')
