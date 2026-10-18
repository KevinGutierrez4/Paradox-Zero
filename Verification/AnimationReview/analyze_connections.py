from pathlib import Path
import numpy as np
from PIL import Image
from collections import deque

root = Path(__file__).parents[1] / 'PresentationSandbox/Verification/AnimationReview'
for kind in ['Archer', 'Mage', 'WhiteWolf', 'BlueWolf']:
    pixels = np.asarray(Image.open(root / f'{kind}-clips.png').convert('RGB')).astype(int)
    bg = pixels[0, 0]
    print(kind)
    for i in range(12):
        cell = pixels[(i//4)*320:(i//4+1)*320, (i%4)*320:(i%4+1)*320]
        mask = np.max(abs(cell-bg), axis=2) > 16
        parts=[]
        for sy, sx in np.argwhere(mask):
            if not mask[sy,sx]: continue
            queue=deque([(int(sy),int(sx))]); mask[sy,sx]=False
            area=0; minx=maxx=int(sx); miny=maxy=int(sy)
            while queue:
                y,x=queue.popleft(); area+=1
                minx=min(minx,x); maxx=max(maxx,x); miny=min(miny,y); maxy=max(maxy,y)
                for dy in [-1,0,1]:
                    for dx in [-1,0,1]:
                        ny,nx=y+dy,x+dx
                        if 0<=ny<320 and 0<=nx<320 and mask[ny,nx]:
                            mask[ny,nx]=False; queue.append((ny,nx))
            if area < 35: continue
            parts.append((area, (minx,miny,maxx+1,maxy+1)))
        print(i, sorted(parts, reverse=True))
