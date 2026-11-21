'use strict';
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const page=fs.readFileSync(path.resolve(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/configPage.html'),'utf8');
const script=page.match(/<script>([\s\S]*?)<\/script>/)[1];
const nodes={};
function elem(id){return {id,textContent:'',value:'',checked:false,disabled:false,isConnected:true,children:[],listeners:{},
  addEventListener(type,fn){this.listeners[type]=fn;},
  querySelector(sel){return nodes[sel.slice(1)];},
  replaceChildren(){this.children=[];this.textContent='';},
  appendChild(el){this.children.push(el);}};}
for(const [,id] of page.matchAll(/id="(mc-[\w-]+)"/g))nodes[id]=elem(id);
nodes['mc-updates-panel'].querySelector=sel=>nodes[sel.slice(1)];
const view={querySelector:sel=>nodes[sel.slice(1)],addEventListener() {}};
const cfg={AutoCheckUpdates:false,EnablePassiveHttpDiagnostics:false,DownloadUntilEnd:true};
let resets=0;
const routes=[];
const result=(val,status=200)=>({ok:status<400,status,json:async()=>val});
const fetch=async(url,{method='GET'}={})=>{
 const name=String(url).replace(/^\//,'');routes.push(name+' '+method);
 if(name==='MisioCache/api/diagnostics/http')return result({enabled:cfg.EnablePassiveHttpDiagnostics,pipelineInstalled:true,
   totalRequests:4,rangeRequests:3,partialResponses206:2,validPartialRanges:2,
   ignoredRange200:1,unsatisfiableRanges416:0,fileRequests:3,playlistRequests:1,segmentRequests:0});
 if(name==='MisioCache/api/diagnostics/http/reset'&&method==='POST'){resets++;return result({totalRequests:0});}
 if(name==='Sessions')return result([{NowPlayingItem:{Name:'<img src=bad>',Id:'0000'},
   Client:'Android Web',PlayState:{PlayMethod:'DirectPlay'}}]);
 if(name.startsWith('MisioCache/api/updates/status'))return result({installedVersion:'0.1.3.0',repositoryVersion:'0.1.3.0',repositoryTargetAbi:'12.4.0.0'});
 if(name==='Packages')return result([]);
 if(name==='Plugins')return result([{Id:'5cf36828-5b7b-4d18-a28e-1e87922e505b',Name:'MisioCache',Version:'0.1.3.0'}]);
 if(name==='Repositories')return result([]);
 throw Error('unexpected '+name);
};
const ApiClient={accessToken:()=> 'token-for-test',getUrl:p=>'/'+p,getPluginConfiguration:async()=>cfg,
 updatePluginConfiguration:async(_id,obj)=>Object.assign(cfg,obj)};
const context={document:{getElementById:id=>id==='misioCacheConfigPage'?view:null,createElement:tag=>elem(tag)},
 window:{ApiClient,isSecureContext:false,setInterval:()=>4,clearInterval(){},confirm:()=>false},
 ApiClient,navigator:{},fetch,URLSearchParams,Date,console,setTimeout,clearTimeout};
vm.createContext(context);vm.runInContext(script,context);
async function tick(){for(let i=0;i<5;i++)await new Promise(resolve=>setTimeout(resolve,0));}
(async()=>{
 await tick();
 assert.equal(nodes['mc-http-range'].textContent,'3');
 assert.equal(nodes['mc-http-206'].textContent,'2');
 assert.equal(nodes['mc-http-valid'].textContent,'2');
 assert.equal(nodes['mc-http-types'].textContent,'3 / 1 / 0');
 assert.match(nodes['mc-http-active'].textContent,/Wyłączona/);
 assert.match(nodes['mc-live-sessions'].children[0].textContent,/DirectPlay/);
 assert.match(nodes['mc-live-sessions'].children[0].textContent,/<img src=bad>/);
 assert(!nodes['mc-live-sessions'].innerHTML,'untrusted title must only be textContent');
 nodes['mc-http-optin'].checked=true;
 await nodes['mc-http-optin'].listeners.change();
 await tick();
 assert.equal(cfg.EnablePassiveHttpDiagnostics,true);
 assert.match(nodes['mc-http-active'].textContent,/Aktywna/);
 await nodes['mc-http-reset'].listeners.click();
 assert.equal(resets,1);
 assert(routes.some(x=>x==='MisioCache/api/diagnostics/http/reset POST'));
 console.log('MisioCache v0.1.3.0: panel HTTP Range/206 + sesje + opt-in + reset — OK');
})().catch(e=>{console.error(e);process.exitCode=1;});
