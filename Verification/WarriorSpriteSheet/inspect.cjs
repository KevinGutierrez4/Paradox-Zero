const fs=require('fs');
const {PNG}=require('C:/Users/gehbd/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const im=PNG.sync.read(fs.readFileSync(process.argv[2]));
const {width:w,height:h,data:d}=im;
const seen=new Uint8Array(w*h),components=[];
for(let p=0;p<w*h;p++){
 if(seen[p]||d[p*4+3]<32)continue;
 const q=[p];seen[p]=1;let k=0,x0=w,y0=h,x1=0,y1=0;
 while(k<q.length){const v=q[k++],x=v%w,y=Math.floor(v/w);x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);
  for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
   const xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;
   const n=yy*w+xx;if(!seen[n]&&d[n*4+3]>=32){seen[n]=1;q.push(n);}
  }
 }
 if(q.length>1000)components.push({n:q.length,x0,y0,x1,y1,cx:(x0+x1)/2,cy:(y0+y1)/2});
}
console.log(JSON.stringify({w,h,components},null,2));
