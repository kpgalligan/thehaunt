// The Haunt — cast sprite generator. Palette-locked, 16x32 cells, 96x96 sheets.
// Run via run_script: read this file and eval it with (createCanvas, saveFile, log) in scope.
const P={ink9:'#171310',ink7:'#2b241d',ink5:'#453a2e',cream:'#ede3cb',stonePale:'#b8b5a5',
 greenDark:'#2f5228',greenMid:'#457539',greenBase:'#4a7c3a',greenLight:'#5f9445',greenPale:'#86ad5c',
 earthDark:'#4a3526',woodWarm:'#6b4a2f',earthMid:'#7a5b3c',earthBase:'#8a6a45',earthLight:'#a5855c',
 stoneDark:'#3e4241',stoneShade:'#575a58',stoneBase:'#7a7a7a',stoneLight:'#9a9a8a',barnRed:'#a4432f',
 waterMid:'#47788c',waterDeep:'#2e5566',skin:'#e8c8a0',skinShade:'#c49a72',
 lantern:'#f2b95c',hairStock:'#5a4a3a',plum:'#6b4560',bone:'#cfd6d1'};
const W=16,H=32;
const buf=()=>Array.from({length:H},()=>Array(W).fill(null));
const set=(b,x,y,c)=>{ if(x<0||y<0||x>=W||y>=H||!c) return; b[y][x]=c; };
const hline=(b,x0,x1,y,c)=>{ for(let x=x0;x<=x1;x++) set(b,x,y,c); };
const rect=(b,x0,y0,x1,y1,c)=>{ for(let y=y0;y<=y1;y++) hline(b,x0,x1,y,c); };
function outline(b,c){ const o=b.map(r=>r.slice());
  for(let y=0;y<H;y++) for(let x=0;x<W;x++){ if(b[y][x]) continue;
    if([[1,0],[-1,0],[0,1],[0,-1]].some(([dx,dy])=>{const yy=y+dy,xx=x+dx;return yy>=0&&yy<H&&xx>=0&&xx<W&&b[yy][xx];})) o[y][x]=c; }
  return o; }
const SK=s=>s.skin||P.skin, SS=s=>s.skinShade||P.skinShade;

function headDown(b,s,y){
  const hr=s.hair, sk=SK(s), ss=SS(s), full=!(hr.style==='short'||hr.style==='thin'||hr.style==='crop'||hr.style==='bald');
  if(hr.style!=='bald'){ hline(b,5,10,y,hr.base); hline(b,4,11,y+1,hr.base); hline(b,4,11,y+2,hr.base);
    hline(b,5,7,y+1,hr.light); set(b,4,y+2,hr.light); }
  else { hline(b,5,10,y,sk); hline(b,4,11,y+1,sk); hline(b,4,11,y+2,sk); hline(b,5,7,y+1,sk);
    set(b,4,y+2,hr.base); set(b,11,y+2,hr.base); }
  if(hr.style==='mop'){ hline(b,3,12,y+1,hr.base); hline(b,3,12,y+2,hr.base); hline(b,3,12,y+3,hr.base); set(b,3,y+2,hr.light); }
  for(let i=3;i<=6;i++){ hline(b,5,10,y+i,sk); if(hr.style!=='bald'){ set(b,4,y+i,hr.base); set(b,11,y+i,hr.base);} }
  if(!full){ set(b,4,y+5,null);set(b,11,y+5,null);set(b,4,y+6,null);set(b,11,y+6,null); }
  if(hr.style==='long'||hr.style==='braid'){ rect(b,3,y+3,3,y+8,hr.base); rect(b,12,y+3,12,y+8,hr.base); set(b,3,y+3,hr.light); }
  hline(b,s.slim?6:5,s.slim?9:10,y+7,sk); hline(b,6,9,y+8,ss);
  set(b,6,y+4,P.ink7); set(b,9,y+4,P.ink7);
  set(b,7,y+6,ss); set(b,8,y+6,ss); set(b,10,y+3,ss); set(b,10,y+6,ss);
  if(s.glasses){ hline(b,5,10,y+4,P.stoneBase); set(b,6,y+4,P.ink9); set(b,9,y+4,P.ink9); }
  if(s.patch){ rect(b,9,y+3,10,y+4,P.ink9); set(b,11,y+3,P.ink7); }
  if(s.beard){ hline(b,5,10,y+6,s.beard); hline(b,5,10,y+7,s.beard); hline(b,6,9,y+8,s.beard); set(b,7,y+6,ss); set(b,8,y+6,ss); }
  if(s.cap){ hline(b,4,11,y,s.cap.base); hline(b,3,12,y+1,s.cap.base); hline(b,3,12,y+2,s.cap.base);
    hline(b,5,8,y,s.cap.light); set(b,3,y+2,s.cap.light);
    if(s.cap.brim){ hline(b,3,12,y+3,s.cap.base);
      for(let i=4;i<=6;i++){ hline(b,5,10,y+i,sk); set(b,4,y+i,null); set(b,11,y+i,null); }
      set(b,6,y+5,P.ink7); set(b,9,y+5,P.ink7); hline(b,5,10,y+7,sk); hline(b,6,9,y+8,ss);
      if(s.beard){ hline(b,5,10,y+7,s.beard); hline(b,6,9,y+8,s.beard); } } }
  if(s.hood){ hline(b,3,12,y,s.hood); rect(b,3,y+1,3,y+8,s.hood); rect(b,12,y+1,12,y+8,s.hood);
    hline(b,4,11,y,s.hood); hline(b,5,8,y,P.stoneShade); }
}
function headUp(b,s,y){
  const hr=s.hair, ss=SS(s);
  if(hr.style==='bald'){ hline(b,5,10,y,SK(s)); rect(b,4,y+1,11,y+5,SK(s)); hline(b,4,y+6,11,y+7,hr.base); }
  else { hline(b,5,10,y,hr.base); rect(b,4,y+1,11,y+7,hr.base);
    hline(b,5,7,y+1,hr.light); set(b,4,y+2,hr.light); set(b,4,y+3,hr.light); set(b,11,y+4,P.ink5); set(b,11,y+5,P.ink5); }
  hline(b,6,9,y+8,ss);
  if(hr.style==='short'||hr.style==='thin'||hr.style==='crop'){ rect(b,4,y+6,11,y+7,null); hline(b,5,10,y+6,hr.base);
    hline(b,6,9,y+7,ss); hline(b,6,9,y+8,ss); }
  if(hr.style==='mop') rect(b,3,y+1,12,y+4,hr.base);
  if(hr.style==='long'){ rect(b,3,y+1,12,y+12,hr.base); hline(b,5,7,y+1,hr.light); set(b,12,y+8,P.ink5); }
  if(hr.style==='bun'){ rect(b,6,y-1,9,y+1,hr.base); set(b,7,y-1,hr.light); }
  if(s.cap){ hline(b,4,11,y,s.cap.base); rect(b,3,y+1,12,y+3,s.cap.base); hline(b,5,8,y,s.cap.light); set(b,3,y+2,s.cap.light); }
  if(s.hood){ hline(b,3,12,y,s.hood); rect(b,3,y+1,12,y+8,s.hood); hline(b,5,8,y,P.stoneShade); }
}
function tailUp(b,s,y){ const hr=s.hair;
  if(hr.style==='pony'){ rect(b,7,y+8,9,y+13,hr.base); set(b,7,y+9,hr.light); set(b,9,y+12,P.ink5); }
  if(hr.style==='braid'){ rect(b,7,y+8,9,y+15,hr.base);
    for(let i=9;i<=15;i+=2) hline(b,7,9,y+i,hr.light); set(b,8,y+15,P.ink5); }
}
function headSide(b,s,y){
  const hr=s.hair, sk=SK(s), ss=SS(s);
  if(hr.style==='bald'){ hline(b,5,10,y,sk); hline(b,4,11,y+1,sk); hline(b,4,11,y+2,sk); rect(b,10,y+3,11,y+6,hr.base); }
  else { hline(b,5,10,y,hr.base); hline(b,4,11,y+1,hr.base); hline(b,4,11,y+2,hr.base); hline(b,5,7,y+1,hr.light);
    rect(b,9,y+3,11,y+7,hr.base); set(b,9,y+3,hr.light); }
  for(let i=3;i<=7;i++) hline(b,4,10,y+i,sk);
  if(hr.style==='bald') for(let i=3;i<=7;i++) hline(b,4,9,y+i,sk);
  if(hr.style==='mop'){ hline(b,3,12,y+1,hr.base); hline(b,3,12,y+2,hr.base); rect(b,10,y+3,12,y+5,hr.base); }
  set(b,5,y+4,P.ink7); set(b,3,y+5,sk); set(b,3,y+6,ss); hline(b,5,9,y+8,ss);
  if(hr.style==='short'||hr.style==='thin'||hr.style==='crop') rect(b,9,y+6,11,y+7,null);
  if(hr.style==='pony'){ rect(b,11,y+4,13,y+9,hr.base); set(b,11,y+5,hr.light); set(b,13,y+8,P.ink5); }
  if(hr.style==='braid'){ rect(b,11,y+4,13,y+11,hr.base); for(let i=5;i<=11;i+=2) hline(b,11,13,y+i,hr.light); }
  if(hr.style==='bun') rect(b,10,y-1,12,y+1,hr.base);
  if(hr.style==='long'){ rect(b,10,y+3,12,y+11,hr.base); set(b,10,y+4,hr.light); }
  if(s.glasses){ hline(b,4,6,y+4,P.stoneBase); set(b,5,y+4,P.ink9); set(b,7,y+4,P.stoneBase); }
  if(s.patch){ rect(b,4,y+3,5,y+4,P.ink9); hline(b,6,9,y+3,P.ink7); }
  if(s.beard){ hline(b,3,9,y+6,s.beard); hline(b,4,9,y+7,s.beard); hline(b,5,9,y+8,s.beard); set(b,3,y+5,sk); }
  if(s.cap){ hline(b,4,11,y,s.cap.base); hline(b,3,12,y+1,s.cap.base); hline(b,3,12,y+2,s.cap.base); hline(b,5,8,y,s.cap.light);
    if(s.cap.brim){ hline(b,1,10,y+3,s.cap.base); for(let i=4;i<=7;i++) hline(b,4,10,y+i,sk);
      set(b,5,y+5,P.ink7); set(b,3,y+6,sk); hline(b,5,9,y+8,ss);
      if(s.beard){ hline(b,3,9,y+7,s.beard); hline(b,5,9,y+8,s.beard); } } }
  if(s.hood){ hline(b,3,12,y,s.hood); rect(b,11,y+1,12,y+8,s.hood); rect(b,3,y+1,3,y+2,s.hood); }
}
function torsoDown(b,s,y,ap,back){
  const sh=s.shirt, ov=s.over, sl=s.slim;
  const x0=sl?4:3, x1=sl?11:12, s0=sl?5:4, s1=sl?10:11, ax0=sl?3:2, ax1=sl?12:13;
  hline(b,s0,s1,y,sh.base); rect(b,x0,y+1,x1,y+8,sh.base);
  rect(b,x0+1,y+1,x0+1,y+6,sh.light); rect(b,x1,y+2,x1,y+8,sh.shade);
  if(s.tie&&!back){ rect(b,7,y+1,8,y+7,s.tie); set(b,7,y+2,P.cream); }
  if(ov){ rect(b,x0,y+1,x0+1,y+8,ov.base); rect(b,x1-1,y+1,x1,y+8,ov.base);
    set(b,s0,y,ov.base); set(b,s1,y,ov.base); set(b,x0,y+1,ov.light); set(b,x0,y+2,ov.light); set(b,x1,y+4,ov.shade);
    if(back){ rect(b,x0,y+1,x1,y+8,ov.base); hline(b,s0,s1,y,ov.base); rect(b,x0+1,y+1,x0+1,y+5,ov.light); rect(b,x1,y+3,x1,y+8,ov.shade); }
    else { set(b,x0+2,y+2,P.ink5); set(b,x1-2,y+2,P.ink5); } }
  if(s.vest){ rect(b,s0,y+1,s1,y+7,s.vest.base); hline(b,s0+1,s1-1,y+3,s.vest.light);
    set(b,7,y+5,P.stonePale); set(b,8,y+5,P.stonePale); }
  if(s.smock){ rect(b,x0,y+1,x1,y+9,s.smock.base); hline(b,s0,s1,y,s.smock.base);
    rect(b,x0+1,y+1,x0+1,y+6,s.smock.light); if(!back) rect(b,7,y+1,8,y+9,s.smock.shade); }
  const dl=ap===1?1:ap===2?-1:0, sleeve=s.smock?s.smock.base:(ov?ov.base:sh.base);
  for(let i=1;i<=5;i++){ set(b,ax0,y+i+dl,sleeve); set(b,ax1,y+i-dl,sleeve); }
  set(b,ax0,y+6+dl,SK(s)); set(b,ax1,y+6-dl,SK(s));
  set(b,ax0,y+7+dl,SS(s)); set(b,ax1,y+7-dl,SS(s));
  if(s.apron&&!back){ rect(b,s0,y+4,s1,y+9,s.apron.base); hline(b,s0+1,s1-1,y+4,s.apron.light); }
  if(sl&&!back&&!s.smock&&!s.apron) hline(b,7,8,y+7,P.ink5);
}
function torsoSide(b,s,y,ap){
  const sh=s.shirt, ov=s.over, sl=s.slim, body=ov||sh;
  const x0=sl?4:3, x1=sl?10:11;
  hline(b,x0+1,x1-1,y,body.base); rect(b,x0,y+1,x1,y+8,body.base);
  rect(b,x0+1,y+1,x0+1,y+6,body.light||sh.light); rect(b,x1,y+2,x1,y+8,body.shade||sh.shade);
  if(ov) rect(b,x0,y+2,x0,y+7,sh.base);
  if(s.vest){ rect(b,x0,y+1,x1-2,y+7,s.vest.base); hline(b,x0+1,x1-3,y+3,s.vest.light); }
  if(s.smock){ rect(b,x0,y+1,x1,y+9,s.smock.base); rect(b,x0+1,y+1,x0+1,y+6,s.smock.light); }
  if(s.apron) rect(b,x0,y+4,x1-2,y+9,s.apron.base);
  const dl=ap===1?2:ap===2?-2:0, ax=6+dl;
  const sleeve=s.smock?s.smock.base:body.base, shade=(s.smock?s.smock.shade:body.shade)||sh.shade;
  for(let i=2;i<=6;i++){ set(b,ax,y+i,sleeve); set(b,ax+1,y+i,shade); }
  set(b,ax,y+7,SK(s)); set(b,ax+1,y+7,SS(s));
}
function legsDown(b,s,y,ph){ const p=s.pants, sh=s.shoes, sl=s.slim;
  const la=sl?5:4, lb=sl?7:6, ra=9, rb=11;
  if(ph===0||ph===3){ rect(b,la,y,lb,y+6,p.base); rect(b,ra,y,rb,y+6,p.base);
    set(b,la,y,p.light); set(b,ra,y,p.light); set(b,rb,y+4,p.shade);
    rect(b,la-1,y+7,lb,y+8,sh); rect(b,ra,y+7,rb,y+8,sh); set(b,8,y,p.base); set(b,8,y+1,p.shade); }
  else if(ph===1){ rect(b,la-1,y,lb,y+5,p.base); rect(b,ra,y,rb,y+6,p.base);
    set(b,la-1,y,p.light); rect(b,la-2,y+6,lb,y+7,sh); rect(b,ra,y+7,rb+1,y+8,sh); set(b,8,y,p.base); }
  else { rect(b,la,y,lb,y+6,p.base); rect(b,ra,y,rb+1,y+5,p.base);
    set(b,la,y,p.light); rect(b,la-1,y+7,lb,y+8,sh); rect(b,ra,y+6,rb+1,y+7,sh); set(b,8,y,p.base); }
}
function legsSide(b,s,y,ph){ const p=s.pants, sh=s.shoes;
  if(ph===0||ph===3){ rect(b,5,y,10,y+1,p.base); rect(b,5,y+2,7,y+6,p.base); rect(b,8,y+2,10,y+6,p.shade);
    set(b,5,y,p.light); rect(b,4,y+7,7,y+8,sh); rect(b,8,y+7,11,y+8,sh); }
  else if(ph===1){ rect(b,5,y,10,y+1,p.base); rect(b,3,y+2,6,y+5,p.base); rect(b,8,y+2,11,y+5,p.shade);
    rect(b,2,y+6,5,y+7,sh); rect(b,9,y+6,12,y+7,sh); }
  else { rect(b,5,y,10,y+1,p.base); rect(b,4,y+2,7,y+6,p.base); rect(b,8,y+2,10,y+6,p.shade);
    rect(b,3,y+7,6,y+8,sh); rect(b,9,y+7,12,y+8,sh); }
}
function skirtDown(b,s,y,ph){ const k=s.skirt, sh=s.shoes;
  rect(b,4,y,11,y+5,k.base); rect(b,5,y,5,y+4,k.light); rect(b,11,y+1,11,y+5,k.shade);
  const off=ph===1?1:ph===2?-1:0;
  rect(b,5-off,y+6,7-off,y+8,s.pants.base); rect(b,9+off,y+6,11+off,y+8,s.pants.base);
  rect(b,4-off,y+8,7-off,y+8,sh); rect(b,9+off,y+8,11+off,y+8,sh); }
function skirtSide(b,s,y,ph){ const k=s.skirt, sh=s.shoes;
  rect(b,4,y,11,y+5,k.base); rect(b,5,y,5,y+4,k.light);
  const off=ph===1?2:ph===2?-2:0;
  rect(b,4-off,y+6,6-off,y+8,s.pants.base); rect(b,8+off,y+6,10+off,y+8,s.pants.base);
  rect(b,3-off,y+8,6-off,y+8,sh); rect(b,8+off,y+8,11+off,y+8,sh); }

function frame(s,dir,lp,ap,bob,breath){
  const b=buf(), st=s.stoop||0, yH=2+bob+st, yT=11+bob+st, yL=20+bob;
  const legs = s.skirt ? (dir==='side'?skirtSide:skirtDown) : (dir==='side'?legsSide:legsDown);
  if(dir==='down'){ headDown(b,s,yH+breath); torsoDown(b,s,yT,ap,false); legs(b,s,yL,lp); }
  else if(dir==='up'){ headUp(b,s,yH+breath); torsoDown(b,s,yT,ap,true); legs(b,s,yL,lp); tailUp(b,s,yH+breath); }
  else { headSide(b,s,yH+breath); torsoSide(b,s,yT,ap); legs(b,s,yL,lp); }
  return outline(b,P.ink7);
}
function sheet(s){ const c=createCanvas(96,96), g=c.getContext('2d');
  ['down','side','up'].forEach((d,row)=>{
    [frame(s,d,0,0,0,0),frame(s,d,0,0,0,1),frame(s,d,1,1,0,0),frame(s,d,3,0,-1,0),frame(s,d,2,2,0,0),frame(s,d,3,0,-1,0)]
    .forEach((fb,col)=>{ for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const px=fb[y][x]; if(!px) continue;
      g.fillStyle=px; g.fillRect(col*16+x,row*32+y,1,1); } }); });
  return c; }

const C={
jane:{slim:1, hair:{base:P.hairStock,light:'#7a4a34',style:'pony'},
  shirt:{base:P.cream,light:'#ffffff',shade:P.stonePale},
  over:{base:P.greenMid,light:P.greenLight,shade:P.greenDark},
  pants:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep}, shoes:P.earthDark},

walt:{stoop:1, hair:{base:P.stoneLight,light:P.bone,style:'thin'},
  shirt:{base:P.stonePale,light:P.cream,shade:P.stoneShade},
  over:{base:P.earthDark,light:P.woodWarm,shade:P.ink7},
  pants:{base:P.ink5,light:P.stoneDark,shade:P.ink7}, shoes:P.ink7,
  skin:P.skinShade, skinShade:'#a87b56'},
dennis:{slim:1, hair:{base:P.ink5,light:P.hairStock,style:'mop'},
  shirt:{base:P.stoneDark,light:P.stoneShade,shade:P.ink9},
  over:{base:P.stoneShade,light:P.stoneBase,shade:P.stoneDark},
  pants:{base:P.waterDeep,light:P.waterMid,shade:P.ink5}, shoes:P.ink9},
gloria:{slim:1, hair:{base:P.stoneLight,light:P.bone,style:'braid'},
  shirt:{base:P.cream,light:'#ffffff',shade:P.stonePale},
  over:{base:P.barnRed,light:'#c25c44',shade:'#732c1f'},
  skirt:{base:P.waterDeep,light:P.waterMid,shade:P.ink5},
  pants:{base:P.stonePale,light:P.cream,shade:P.stoneShade}, shoes:P.earthDark},
// Mike, the garage clerk (Kevin's 2026-08-30 garage-operation commission; name is
// Kevin's). Friendly and NOT a mechanic: soft cap, warm plain shirt, no smock, no
// apron, no coveralls — the counter's clothes, not the pit's. Appended as
// cast_west block 4 (append-only; existing block order is fixed by the README).
mike:{hair:{base:P.woodWarm,light:P.earthMid,style:'short'},
  cap:{base:P.waterMid,light:'#5f8fa3'},
  shirt:{base:P.earthBase,light:P.earthLight,shade:P.earthMid},
  pants:{base:P.ink5,light:P.stoneDark,shade:P.ink7}, shoes:P.earthDark},
pell:{hair:{base:P.stoneShade,light:P.stoneLight,style:'thin'},
  shirt:{base:P.cream,light:'#ffffff',shade:P.stonePale}, tie:P.stoneDark,
  over:{base:P.stoneDark,light:P.stoneShade,shade:P.ink9},
  pants:{base:P.stoneDark,light:P.stoneShade,shade:P.ink9}, shoes:P.ink9},

billie:{stoop:1, patch:1, hair:{base:P.ink5,light:P.hairStock,style:'short'},
  shirt:{base:P.stoneDark,light:P.stoneShade,shade:P.ink9},
  apron:{base:P.stoneShade,light:P.stoneBase},
  pants:{base:P.ink7,light:P.ink5,shade:P.ink9}, shoes:P.ink9},
bud:{stoop:1, hair:{base:P.stoneLight,light:P.bone,style:'thin'}, beard:P.stoneLight,
  cap:{base:P.greenDark,light:P.greenMid,brim:true},
  shirt:{base:P.stonePale,light:P.cream,shade:P.stoneShade},
  over:{base:P.barnRed,light:'#c25c44',shade:'#732c1f'},
  pants:{base:P.waterDeep,light:P.waterMid,shade:P.ink5}, shoes:P.earthDark},
pete:{slim:1, stoop:1, glasses:1, hair:{base:P.stoneLight,light:P.bone,style:'bald'},
  shirt:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep},
  over:{base:P.stonePale,light:P.cream,shade:P.stoneShade},
  pants:{base:P.earthLight,light:'#bfa07a',shade:P.earthMid}, shoes:P.earthDark},
moody:{hair:{base:P.earthMid,light:P.earthLight,style:'mop'},
  shirt:{base:P.greenBase,light:P.greenLight,shade:P.greenDark},
  pants:{base:P.earthMid,light:P.earthBase,shade:P.earthDark}, shoes:P.earthDark},
lyle:{cap:{base:P.stonePale,light:P.cream,brim:true}, hair:{base:P.hairStock,light:P.earthMid,style:'short'},
  shirt:{base:P.earthBase,light:P.earthLight,shade:P.earthMid},
  pants:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep}, shoes:P.earthDark},
harriet:{slim:1, glasses:1, hair:{base:P.stoneShade,light:P.stoneLight,style:'bun'},
  shirt:{base:P.waterDeep,light:P.waterMid,shade:P.ink5},
  over:{base:P.stoneShade,light:P.stoneBase,shade:P.stoneDark},
  skirt:{base:P.ink5,light:P.stoneDark,shade:P.ink7},
  pants:{base:P.stonePale,light:P.cream,shade:P.stoneShade}, shoes:P.ink7},
ray:{hair:{base:P.ink5,light:P.hairStock,style:'crop'},
  shirt:{base:P.stoneLight,light:P.bone,shade:P.stoneShade},
  pants:{base:P.earthLight,light:'#bfa07a',shade:P.earthMid}, shoes:P.earthDark,
  skin:P.skinShade, skinShade:'#a87b56'},
nora:{slim:1, hair:{base:P.earthMid,light:P.earthLight,style:'long'},
  shirt:{base:P.greenPale,light:'#a2c477',shade:P.greenMid},
  pants:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep}, shoes:P.cream},

sam:{slim:1, hair:{base:P.ink7,light:P.ink5,style:'crop'},
  shirt:{base:P.cream,light:'#ffffff',shade:P.stonePale},
  smock:{base:P.stoneShade,light:P.stoneBase,shade:P.stoneDark},
  pants:{base:P.ink5,light:P.stoneDark,shade:P.ink7}, shoes:P.ink9},
abe:{slim:1, hair:{base:P.stoneShade,light:P.stoneLight,style:'short'}, beard:P.stoneLight,
  cap:{base:P.earthDark,light:P.woodWarm},
  shirt:{base:P.earthLight,light:'#bfa07a',shade:P.earthMid},
  over:{base:P.greenDark,light:P.greenMid,shade:P.ink7},
  pants:{base:P.earthDark,light:P.woodWarm,shade:P.ink7}, shoes:P.ink7},

mayor:{hair:{base:P.stoneLight,light:P.bone,style:'thin'},
  shirt:{base:P.waterDeep,light:P.waterMid,shade:P.ink5},
  pants:{base:P.earthLight,light:'#bfa07a',shade:P.earthMid}, shoes:P.ink7},
foreman:{hair:{base:P.hairStock,light:P.earthMid,style:'short'}, cap:{base:P.cream,light:'#ffffff'},
  shirt:{base:P.stoneLight,light:P.bone,shade:P.stoneShade}, vest:{base:P.lantern,light:'#ffd98a'},
  pants:{base:P.waterDeep,light:P.waterMid,shade:P.ink5}, shoes:P.earthDark},
crew_worker_a:{hair:{base:P.ink5,light:P.hairStock,style:'short'}, cap:{base:P.waterDeep,light:P.waterMid,brim:true},
  shirt:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep},
  pants:{base:P.stoneShade,light:P.stoneBase,shade:P.stoneDark}, shoes:P.ink7},
crew_worker_b:{slim:1, hair:{base:P.earthMid,light:P.earthLight,style:'bun'},
  shirt:{base:P.greenBase,light:P.greenLight,shade:P.greenDark}, vest:{base:P.lantern,light:'#ffd98a'},
  pants:{base:P.waterMid,light:'#5f8fa3',shade:P.waterDeep}, shoes:P.earthDark,
  skin:P.skinShade, skinShade:'#a87b56'},
shopkeeper:{hair:{base:P.stoneShade,light:P.stoneLight,style:'thin'},
  shirt:{base:P.stonePale,light:P.cream,shade:P.stoneShade}, apron:{base:P.earthMid,light:P.earthBase},
  pants:{base:P.ink5,light:P.stoneDark,shade:P.ink7}, shoes:P.stoneDark},
};
const groups={
  cast_west:['walt','dennis','gloria','pell','mike'],
  cast_billies:['billie','bud','pete','moody','lyle','harriet','ray','nora'],
  cast_east:['sam','abe'],
  cast_town:['mayor','foreman','crew_worker_a','crew_worker_b','shopkeeper'],
};
await saveFile('art/character.png', sheet(C.jane));
for(const [file,ids] of Object.entries(groups)){
  const c=createCanvas(96*ids.length,96), g=c.getContext('2d');
  g.imageSmoothingEnabled=false;
  ids.forEach((id,i)=>g.drawImage(sheet(C[id]), i*96, 0));
  await saveFile(`art/${file}.png`, c);
  for(const id of ids) await saveFile(`art/cast_${id}.png`, sheet(C[id]));
}
const all=[['jane',C.jane],...Object.values(groups).flat().map(id=>[id,C[id]])];
const Z=6, zc=createCanvas(all.length*20*Z,36*Z), zg=zc.getContext('2d');
zg.imageSmoothingEnabled=false; zg.fillStyle='#4a7c3a'; zg.fillRect(0,0,zc.width,zc.height);
all.forEach(([id,s],i)=>zg.drawImage(sheet(s),0,0,16,32,(i*20+2)*Z,2*Z,16*Z,32*Z));
await saveFile('art/cast_review.png', zc);
log(all.length+' sheets');
