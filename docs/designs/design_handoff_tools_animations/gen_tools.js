// The Haunt — tool + work-animation renderer.
// Eval AFTER the shared part of art/gen_cast.js (everything before its first saveFile call),
// so P, W, H, buf/set/hline/rect/outline, head*/torso*/legs* and C.jane are in scope.
// Sheets: 64x192 per tool. 4 columns = frames (windup, strike, impact, recover).
// 6 rows = basic-down, basic-side, dad-down, dad-side, pro-down, pro-side. Cells are 16x32.

const TIER = {
  basic: { head:'#7a6a5c', light:'#a89a88', shade:'#3a322c' },   // pitted, half-rusted iron
  dad:   { head:'#b8b5a5', light:'#f0f3ee', shade:'#4e524f' },   // clean steel
  pro:   { head:'#575a58', light:'#8a8f8c', shade:'#22262a' },   // dark forged
};
const WOOD = { base:'#6b4a2f', light:'#a5855c' };
const TIERS = ['basic','dad','pro'];

function lineP(b,x0,y0,x1,y1,c){
  let dx=Math.abs(x1-x0), dy=Math.abs(y1-y0), sx=x0<x1?1:-1, sy=y0<y1?1:-1, err=dx-dy;
  for(;;){ set(b,x0,y0,c); if(x0===x1&&y0===y1) break;
    const e2=2*err; if(e2>-dy){ err-=dy; x0+=sx; } if(e2<dx){ err+=dx; y0+=sy; } }
}
function handle(b,gx,gy,ax,ay){
  const dx=Math.sign(gx-ax), dy=Math.sign(gy-ay);
  // shaft plus a highlight rail one pixel off-axis, so it reads against clothing
  lineP(b,gx,gy,ax,ay,WOOD.base);
  lineP(b,gx-dy,gy-dx,ax-dy,ay-dx,WOOD.light);
  for(let i=1;i<=4;i++) set(b,gx+dx*i,gy+dy*i,WOOD.base);
  set(b,gx+dx*2-dy,gy+dy*2-dx,WOOD.light);
}
// tool heads, anchored at (x,y), oriented by handle direction
const HEAD = {
  hoe:{ // wide flat blade, always perpendicular to the shaft
    up:(b,x,y,t)=>{ hline(b,x-2,x+2,y,t.head); hline(b,x-2,x+2,y+1,t.shade); hline(b,x-2,x,y,t.light); },
    diag:(b,x,y,t)=>{ hline(b,x-1,x+2,y,t.head); hline(b,x,x+3,y+1,t.shade); set(b,x-1,y,t.light); set(b,x,y,t.light); },
    down:(b,x,y,t)=>{ hline(b,x-2,x+2,y,t.head); hline(b,x-2,x+2,y+1,t.shade); hline(b,x-2,x,y,t.light); },
  },
  axe:{ // chunky wedge, bit facing the swing direction
    up:(b,x,y,t)=>{ rect(b,x-1,y,x+1,y+1,t.head); hline(b,x-1,x,y,t.light); hline(b,x,x+1,y+2,t.shade); set(b,x+1,y+1,t.shade); },
    diag:(b,x,y,t)=>{ rect(b,x-1,y,x+1,y+1,t.head); set(b,x-1,y,t.light); set(b,x+2,y+1,t.head); rect(b,x,y+2,x+2,y+2,t.shade); },
    down:(b,x,y,t)=>{ rect(b,x-1,y,x+1,y+2,t.head); hline(b,x-1,x,y,t.light); hline(b,x,x+1,y+2,t.shade); set(b,x+1,y+1,t.shade); },
  },
  pick:{ // long twin-ended bar
    up:(b,x,y,t)=>{ hline(b,x-3,x+3,y,t.head); set(b,x-3,y,t.light); set(b,x+3,y,t.light); hline(b,x-2,x+2,y+1,t.shade); },
    diag:(b,x,y,t)=>{ set(b,x-2,y+2,t.light); set(b,x-1,y+1,t.head); set(b,x,y+1,t.head); set(b,x+1,y,t.head); set(b,x+2,y-1,t.light); hline(b,x-1,x+1,y+2,t.shade); },
    down:(b,x,y,t)=>{ rect(b,x,y-2,x,y+2,t.head); set(b,x,y-2,t.light); rect(b,x+1,y-1,x+1,y+2,t.shade); },
  },
};
// swing geometry: grip point, head anchor, head orientation
const SWING = {
  down:[ {g:[12,13],a:[13,6],o:'up'},  {g:[13,15],a:[15,10],o:'diag'},
         {g:[12,18],a:[14,24],o:'down'}, {g:[13,16],a:[15,12],o:'diag'} ],
  side:[ {g:[10,12],a:[12,5],o:'up'},  {g:[11,14],a:[14,9],o:'diag'},
         {g:[11,18],a:[13,24],o:'down'}, {g:[11,15],a:[14,11],o:'diag'} ],
};
// watering can: body box, spout pixels, stream flag
// watering can: 4-wide body, spout swinging from level to pouring
const CAN = {
  down:[ {box:[11,16,13,19], spout:[[14,16],[15,15],[15,16]], grip:[12,15], stream:0},
         {box:[11,17,13,20], spout:[[14,18],[15,18],[15,19]], grip:[12,16], stream:0},
         {box:[11,17,13,20], spout:[[14,21],[15,22],[15,23]], grip:[12,16], stream:1},
         {box:[11,17,13,20], spout:[[14,19],[15,20],[15,21]], grip:[12,16], stream:0} ],
  side:[ {box:[10,16,12,19], spout:[[13,16],[14,15],[14,16]], grip:[11,15], stream:0},
         {box:[10,17,12,20], spout:[[13,18],[14,18],[14,19]], grip:[11,16], stream:0},
         {box:[10,17,12,20], spout:[[13,21],[14,22],[14,23]], grip:[11,16], stream:1},
         {box:[10,17,12,20], spout:[[13,19],[14,20],[14,21]], grip:[11,16], stream:0} ],
};
function clearIdleArms(b,s,dir,yT){ // torso* always draw hanging arms; work poses replace them
  if(dir==='down'){ // arms hang outside the torso box — erase them outright.
    // NB: set() ignores a null color, so clear the buffer directly.
    for(let y=yT+1;y<=yT+8;y++) for(const x of [2,3,12,13]) if(y<H) b[y][x]=null;
  } else { // side arm is drawn ON the torso, so paint it back out in body color
    const body = s.over || s.shirt;
    for(let y=yT+2;y<=yT+8;y++){ set(b,6,y,body.base); set(b,7,y,body.base); }
  }
}
// Both hands always on the tool: the lead hand takes the grip point, the off hand
// sits two pixels up the shaft toward the head, so the stagger reads as a real grip.
function arms(b,s,dir,gx,gy,yT,ax,ay){
  const sleeve = s.over ? s.over.base : s.shirt.base;
  const shade = (s.over?s.over.shade:s.shirt.shade);
  const dx=Math.sign(ax-gx), dy=Math.sign(ay-gy);
  const ux=gx+dx*2, uy=gy+dy*2; // off-hand, up the shaft
  const reach=(sx,sy,hx,hy)=>{
    lineP(b,sx,sy,hx,hy,sleeve); lineP(b,sx,sy+1,hx,hy+1,shade);
    const mx=Math.round((sx+hx*2)/3), my=Math.round((sy+hy*2)/3);
    lineP(b,mx,my,hx,hy,SK(s)); lineP(b,mx,my+1,hx,hy+1,SS(s));
    set(b,hx,hy,SK(s)); set(b,hx,hy+1,SS(s));
  };
  if(dir==='down'){ reach(11,yT+2,gx,gy); reach(4,yT+2,ux,uy); }
  else { reach(7,yT+2,gx,gy); reach(6,yT+3,ux,uy); }
}
function workFrame(tool,tier,dir,f){
  const s=C.jane, b=buf(), lean=f===1||f===2?1:0;
  const yH=2+lean, yT=11+lean, yL=20, stance=(f===1||f===2)?1:0;
  const legs = dir==='side'?legsSide:legsDown;
  if(dir==='down'){ headDown(b,s,yH); torsoDown(b,s,yT,0,false); legs(b,s,yL,stance); }
  else { headSide(b,s,yH); torsoSide(b,s,yT,0); legs(b,s,yL,stance); }
  clearIdleArms(b,s,dir,yT);
  const t=TIER[tier];
  if(tool==='can'){
    const k=CAN[dir][f], [x0,y0,x1,y1]=k.box;
    const [bx0,by0,bx1,by1]=k.box;
    arms(b,s,dir,k.grip[0],k.grip[1]-1,yT,k.grip[0],k.grip[1]-4);
    { // off hand braced under the far side of the can
      const sleeve=s.over?s.over.base:s.shirt.base, shade=(s.over?s.over.shade:s.shirt.shade);
      const hx=bx0-1, hy=by1-1, sx=dir==='down'?4:6, sy=yT+3;
      lineP(b,sx,sy,hx,hy,sleeve); lineP(b,sx,sy+1,hx,hy+1,shade);
      const mx=Math.round((sx+hx*2)/3), my=Math.round((sy+hy*2)/3);
      lineP(b,mx,my,hx,hy,SK(s)); lineP(b,mx,my+1,hx,hy+1,SS(s));
      set(b,hx,hy,SK(s)); set(b,hx,hy+1,SS(s));
    }
    rect(b,x0,y0,x1,y1,t.head); hline(b,x0,x1,y0,t.light); rect(b,x1,y0+1,x1,y1,t.shade);
    hline(b,x0+1,x1-1,y0-1,t.shade); // carry handle across the top
    set(b,x0,y0-1,t.head); set(b,x1,y0-1,t.head);
    k.spout.forEach(([x,y],i)=>{ set(b,x,y,i===1?t.light:t.head); });
    if(k.stream){ const [sx,sy]=k.spout[k.spout.length-1];
      for(let i=1;i<=4;i++){ set(b,sx,sy+i,i>2?P.waterDeep:P.waterMid); set(b,sx-1,sy+i,P.waterMid); } }
  } else {
    const k=SWING[dir][f];
    handle(b,k.g[0],k.g[1],k.a[0],k.a[1]);
    HEAD[tool][k.o](b,k.a[0],k.a[1],t);
    arms(b,s,dir,k.g[0],k.g[1],yT,k.a[0],k.a[1]);
    handle(b,k.g[0],k.g[1],k.a[0],k.a[1]);
    HEAD[tool][k.o](b,k.a[0],k.a[1],t);
  }
  return outline(b,P.ink7);
}
function toolSheet(tool){
  const c=createCanvas(64,192), g=c.getContext('2d');
  let row=0;
  for(const tier of TIERS) for(const dir of ['down','side']){
    for(let f=0;f<4;f++){ const fb=workFrame(tool,tier,dir,f);
      for(let y=0;y<H;y++) for(let x=0;x<W;x++){ const px=fb[y][x]; if(!px) continue;
        g.fillStyle=px; g.fillRect(f*16+x,row*32+y,1,1); } }
    row++;
  }
  return c;
}
const TOOLNAMES={hoe:'Hoe',can:'Watering can',axe:'Axe',pick:'Pickaxe'};
for(const tool of Object.keys(TOOLNAMES)) await saveFile('art/tool_'+tool+'.png', toolSheet(tool));
// zoomed review: rows = tool x tier, cols = 4 frames (down facing) then 4 (side)
const Z=5, order=Object.keys(TOOLNAMES);
const rc=createCanvas(8*18*Z, order.length*3*36*Z), rg=rc.getContext('2d');
rg.imageSmoothingEnabled=false; rg.fillStyle='#4a7c3a'; rg.fillRect(0,0,rc.width,rc.height);
order.forEach((tool,ti)=>{ const sh=toolSheet(tool);
  TIERS.forEach((tier,tr)=>{ const rowY=(ti*3+tr)*36*Z;
    ['down','side'].forEach((dir,di)=>{ const srcRow=tr*2+di;
      for(let f=0;f<4;f++) rg.drawImage(sh, f*16, srcRow*32, 16, 32,
        ((di*4+f)*18+1)*Z, rowY+2*Z, 16*Z, 32*Z); }); }); });
await saveFile('art/tools_review.png', rc);
log('tool sheets done');
