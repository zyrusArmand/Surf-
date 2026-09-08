// Screenshot the game's UI without starting the game.
//
// A headless render of the real thing costs eight to ten minutes, because the ride draws at about
// a third of a frame a second under swiftshader. Almost every UI question -- does this button look
// like that one, does the ready cue still read, is the word inside the panel -- is a question about
// CSS and markup only, and those can be answered in under two seconds by lifting the <style> blocks
// and the markup out of index.html into a page of their own.
//
// The CSS is taken WHOLE. An earlier version of this trick sliced it by line number and cut a rule
// in half, which collapsed a grid silently -- the page still rendered, just wrongly, which is the
// worst way for a test harness to fail. Every <style> block, start to end, or nothing.
//
//   node tools/uishot.mjs <selector-of-markup> ... --out shot.png [--bg run.jpg] [--css extra.css]
//
// Markup is named by the id of a top-level element to copy, e.g. trickBar, jumpBtn, overRow.
import {chromium} from 'playwright';
import http from 'http'; import fs from 'fs'; import path from 'path';

const args=process.argv.slice(2);
const out=(()=>{const i=args.indexOf('--out'); return i<0?'uishot.png':args[i+1];})();
const bg =(()=>{const i=args.indexOf('--bg');  return i<0?null:args[i+1];})();
const xtra=(()=>{const i=args.indexOf('--css'); return i<0?'':fs.readFileSync(args[i+1],'utf8');})();
const ids=args.filter((a,i)=>!a.startsWith('--')&&!(args[i-1]||'').startsWith('--'));
if(!ids.length){ console.error('name at least one element id to render'); process.exit(1); }

const src=fs.readFileSync('index.html','utf8');
const css=[...src.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)].map(m=>m[1]).join('\n');
// The element plus its whole subtree, found by walking the tag depth from its opening tag -- the
// markup here nests, so "up to the next </div>" would truncate every one of these.
function grab(id){
  const open=new RegExp('<(\\w+)[^>]*\\bid="'+id+'"');
  const m=open.exec(src); if(!m) throw new Error('no element with id '+id);
  const tag=m[1]; let i=m.index, depth=0;
  const re=new RegExp('</?'+tag+'\\b','g'); re.lastIndex=i;
  for(let g; (g=re.exec(src)); ){
    depth += g[0][1]==='/' ? -1 : 1;
    if(depth===0) return src.slice(i, src.indexOf('>', g.index)+1);
  }
  throw new Error('unbalanced markup for '+id);
}
const body=ids.map(grab).join('\n').replace(/\bclass="([^"]*)\bhidden\b([^"]*)"/g,'class="$1$2"');
const page=`<!doctype html><meta charset="utf-8"><style>${css}</style>
<style>html,body{margin:0;height:100%}
${bg?`body{background:url('${path.basename(bg)}') center/cover no-repeat}`:'body{background:#123}'}
${xtra}</style>${body}`;
fs.writeFileSync('.uishot.html', page);

const MIME={'.html':'text/html','.jpg':'image/jpeg','.png':'image/png','.webp':'image/webp'};
const server=http.createServer((q,s)=>{const f=path.join(process.cwd(),decodeURIComponent(q.url.split(/[?#]/)[0]));
  fs.readFile(f,(e,d)=>{ if(e){s.writeHead(404);s.end();return;}
    s.writeHead(200,{'Content-Type':MIME[path.extname(f)]||'application/octet-stream'}); s.end(d); });});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const b=await chromium.launch({executablePath:'/opt/pw-browsers/chromium-1194/chrome-linux/chrome',
  args:['--no-sandbox']});
const p=await b.newPage({viewport:{width:402,height:874},deviceScaleFactor:2});
await p.goto(`http://127.0.0.1:${server.address().port}/.uishot.html`);
await p.waitForTimeout(600);
await p.screenshot({path:out});
await b.close(); server.close(); fs.unlinkSync('.uishot.html');
console.log('wrote',out);
