// Pack generated pixel data only: no repainting, resampling, or palette conversion.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const {PNG} = require('C:/Users/gehbd/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const source = process.argv[2];
const project = path.resolve(__dirname, '../..');
const outputDir = path.join(project, 'Assets/OneMoreTime/Art/WarriorAnimation_v2');
const outputFile = path.join(outputDir, 'warrior_animations_v2.png');
if (fs.existsSync(outputFile) && !process.argv.includes('--repack')) throw new Error('Output already exists; refusing to overwrite.');
const im = PNG.sync.read(fs.readFileSync(source));
const {width:w,height:h,data:d} = im;
if(w!==1448 || h!==1086) throw new Error('Unexpected source dimensions.');
const labels = new Int32Array(w*h); labels.fill(-1);
const components = [];
for(let p=0;p<w*h;p++) {
 if(labels[p]!==-1 || d[p*4+3]<32) continue;
 const id=components.length, pixels=[p]; labels[p]=id;
 let x0=w,y0=h,x1=0,y1=0;
 for(let k=0;k<pixels.length;k++) {
  const v=pixels[k], x=v%w, y=Math.floor(v/w);
  x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);
  for(let dy=-1;dy<=1;dy++) for(let dx=-1;dx<=1;dx++) {
   const xx=x+dx,yy=y+dy;
   if(xx<0||xx>=w||yy<0||yy>=h) continue;
   const n=yy*w+xx;
   if(labels[n]===-1 && d[n*4+3]>=32) {labels[n]=id;pixels.push(n);}
  }
 }
 components.push({id,n:pixels.length,x0,y0,x1,y1});
}
const mains=components.filter(c=>c.n>1000);
if(mains.length!==48) throw new Error(`Expected 48 full sprites, found ${mains.length}.`);
const rows=[[],[],[],[],[],[]];
for(const c of mains) {
 const cy=(c.y0+c.y1)/2;
 const row=cy<225?0:cy<395?1:cy<590?2:cy<730?3:cy<915?4:5;
 rows[row].push(c);
}
const clips=[
 {name:'Idle',fps:8,loop:true},
 {name:'Walk',fps:12,loop:true},
 {name:'AxeSlash',fps:12,loop:false},
 {name:'Crouch',fps:12,loop:false,holdFrame:3},
 {name:'JumpUp',fps:12,loop:false},
 {name:'FallDown',fps:10,loop:true}
];
const mainToFrame=new Map(), frames=[];
for(let r=0;r<6;r++) {
 rows[r].sort((a,b)=>a.x0-b.x0);
 if(rows[r].length!==8) throw new Error(`Row ${r} has ${rows[r].length} sprites.`);
 for(let c=0;c<8;c++) {
  const component=rows[r][c], i=r*8+c;
  mainToFrame.set(component.id,i);
  frames.push({name:`${clips[r].name}_${String(c).padStart(2,'0')}`,row:r,column:c,
   x:c*256,y:r*256,width:256,height:256,
   unityRect:{x:c*256,y:(5-r)*256,width:256,height:256},
   pivot:{x:0.5,y:26/256},
   sourceAnchor:{x:100+181*c,y:[215,389,572,733,902,1054][r]},
   bounds:{x0:256,y0:256,x1:-1,y1:-1},pixels:0});
 }
}
function nearestMain(x,y) {
 let best=-1,score=Infinity;
 const column=Math.max(0,Math.min(7,Math.round((x-100)/181)));
 const airBoundary=[908,910,908,900,905,908,911,919][column];
 const row=y<228?0:y<394?1:y<590?2:y<734?3:y<airBoundary?4:5;
 for(const c of mains) {
  if(frames[mainToFrame.get(c.id)].row!==row) continue;
  const dx=Math.max(c.x0-x,0,x-c.x1),dy=Math.max(c.y0-y,0,y-c.y1),s=dx*dx+dy*dy;
  if(s<score) {score=s;best=mainToFrame.get(c.id);}
 }
 return {frame:best,distance:score};
}
const assignment=new Map(mainToFrame);
for(const c of components) if(!assignment.has(c.id)) {
 const n=nearestMain((c.x0+c.x1)/2,(c.y0+c.y1)/2);
 assignment.set(c.id,n.distance<=400?n.frame:-1);
}
const atlas=new PNG({width:2048,height:1536});atlas.data.fill(0);
let preserved=0,discarded=0,transparent=0;
for(let p=0;p<w*h;p++) {
 const a=d[p*4+3];if(!a)continue;
 const x=p%w,y=Math.floor(p/w);
 let fi=labels[p]>=0?assignment.get(labels[p]):-1;
 if(labels[p]<0) {const n=nearestMain(x,y);fi=n.distance<=400?n.frame:-1;}
 if(fi<0) {discarded++;continue;}
 const f=frames[fi],lx=x-f.sourceAnchor.x+128,ly=y-f.sourceAnchor.y+230;
 if(lx<8||lx>=248||ly<8||ly>=248) throw new Error(`Insufficient padding in ${f.name}: ${lx},${ly}`);
 const target=((f.y+ly)*2048+f.x+lx)*4;
 if(atlas.data[target+3]!==0)throw new Error('Unexpected overlapping source pixels.');
 d.copy(atlas.data,target,p*4,p*4+4);preserved++;f.pixels++;
 f.bounds.x0=Math.min(f.bounds.x0,lx);f.bounds.y0=Math.min(f.bounds.y0,ly);
 f.bounds.x1=Math.max(f.bounds.x1,lx);f.bounds.y1=Math.max(f.bounds.y1,ly);
}
for(let p=3;p<atlas.data.length;p+=4)if(atlas.data[p]===0)transparent++;
fs.mkdirSync(outputDir,{recursive:true});
fs.writeFileSync(outputFile,PNG.sync.write(atlas));
const manifest={image:'warrior_animations_v2.png',size:{width:2048,height:1536},columns:8,rows:6,cell:{width:256,height:256},
 coordinates:'x/y from top-left; unityRect from bottom-left',pixelsPerUnit:100,
 pivot:{x:0.5,y:26/256},groundLineFromTop:230,
 clips:clips.map((clip,row)=>({...clip,row,frames:frames.filter(f=>f.row===row).map(f=>f.name)})),frames};
fs.writeFileSync(path.join(outputDir,'animations.json'),JSON.stringify(manifest,null,2));
// Start with this project's existing Unity 6 TextureImporter defaults.
const templatePath=path.join(project,'Assets/OneMoreTime/Art/Originales');
function findMeta(dir){for(const e of fs.readdirSync(dir,{withFileTypes:true})) {const p=path.join(dir,e.name);if(e.isDirectory()){const m=findMeta(p);if(m)return m;}else if(e.name.endsWith('.png.meta'))return p;}}
const existingMeta=fs.existsSync(outputFile+'.meta')?fs.readFileSync(outputFile+'.meta','utf8'):'';
const guid=existingMeta.match(/^guid: (\w+)$/m)?.[1]||crypto.randomUUID().replaceAll('-','');
let meta=fs.readFileSync(findMeta(templatePath),'utf8').replaceAll('\r\n','\n');
meta=meta.replace(/^guid: .+$/m,`guid: ${guid}`).replace('  spriteMode: 1','  spriteMode: 2');
meta=meta.replace('  alignment: 0','  alignment: 9').replace('  spritePivot: {x: 0.5, y: 0.5}','  spritePivot: {x: 0.5, y: 0.1015625}');
meta=meta.replace('  spriteGenerateFallbackPhysicsShape: 1','  spriteGenerateFallbackPhysicsShape: 0');
const ids=frames.map((f,i)=>({name:f.name,id:21300000+i*2}));
meta=meta.replace('  internalIDToNameTable: []','  internalIDToNameTable:\n'+ids.map(f=>`  - first:\n      213: ${f.id}\n    second: ${f.name}`).join('\n'));
const sprites=frames.map((f,i)=>`    - serializedVersion: 2\n      name: ${f.name}\n      rect:\n        serializedVersion: 2\n        x: ${f.unityRect.x}\n        y: ${f.unityRect.y}\n        width: 256\n        height: 256\n      alignment: 9\n      pivot: {x: 0.5, y: 0.1015625}\n      border: {x: 0, y: 0, z: 0, w: 0}\n      outline: []\n      physicsShape: []\n      tessellationDetail: 0\n      bones: []\n      spriteID: ${crypto.randomUUID().replaceAll('-','')}\n      internalID: ${ids[i].id}\n      vertices: []\n      indices: \n      edges: []\n      weights: []`).join('\n');
meta=meta.replace('    sprites: []','    sprites:\n'+sprites);
meta=meta.replace('    nameFileIdTable: {}','    nameFileIdTable:\n'+ids.map(f=>`      ${f.name}: ${f.id}`).join('\n'));
fs.writeFileSync(outputFile+'.meta',meta);
const report={source,width:2048,height:1536,frames:48,preservedPixels:preserved,discardedDetachedPixels:discarded,transparentPixels:transparent,
 minSafetyMargin:Math.min(...frames.map(f=>Math.min(f.bounds.x0,f.bounds.y0,255-f.bounds.x1,255-f.bounds.y1))),
 rgbaPreservedWithoutScaling:true,unityTextureGuid:guid,outputFile};
fs.writeFileSync(path.join(__dirname,'packing-validation.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
