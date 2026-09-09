import {chromium} from 'playwright';
const EXE='/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const OUT='/tmp/claude-0/-home-user-Surf-/7480d8db-fb33-5a1b-a73b-0e83e5c3db08/scratchpad/';
const b=await chromium.launch({executablePath:EXE,args:['--use-gl=swiftshader','--enable-unsafe-swiftshader']});
const p=await b.newPage({viewport:{width:430,height:932}});
const errs=[]; p.on('pageerror',e=>errs.push('PE '+String(e).slice(0,160)));
p.on('console',m=>{ if(m.type()==='error'&&!/404|Failed to load resource/.test(m.text()))errs.push('CE '+m.text().slice(0,200)); });
await p.goto('http://127.0.0.1:8712/index.html#debug',{waitUntil:'load'});
await p.waitForFunction(()=>window.__surf&&window.__surf.restart,null,{timeout:60000});
await p.waitForTimeout(75000);
await p.evaluate(()=>window.__surf.restart());
await p.waitForTimeout(4000);
await p.evaluate(()=>window.__surf.forkNow(-7,0));
let seas=[];
for(let i=0;i<20;i++){ await p.waitForTimeout(3500);
  const f=await p.evaluate(()=>window.__surf.fork());
  seas.push(f.sea);
  if(i%3===0)console.log(i,'ph',f.ph,'py',f.py,'gnd',f.gnd,'sea',f.sea,'pugY',f.pugY,'cam',JSON.stringify(f.cam));
  if(f.ph===3){ await p.screenshot({path:OUT+'cm-stop.png'}); break; } }
console.log('sea range',Math.min(...seas).toFixed(2),Math.max(...seas).toFixed(2));
await p.evaluate(()=>window.__surf.walk(0,-1));
for(let i=0;i<8;i++){ await p.waitForTimeout(3500);
  const f=await p.evaluate(()=>window.__surf.fork());
  console.log('walk',i,'pz',f.toGo,'py',f.py,'gnd',f.gnd,'cam',JSON.stringify(f.cam)); }
await p.screenshot({path:OUT+'cm-walk.png'});
console.log('errs',errs.length,errs.slice(0,3));
await b.close();
