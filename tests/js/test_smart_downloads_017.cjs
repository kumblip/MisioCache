'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const page=fs.readFileSync(path.join(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/configPage.html'),'utf8');
const script=[...page.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(x=>x[1]).find(x=>x.includes("const view=document.getElementById('misioCacheSmartPage')"));
assert(script,'shared configuration page contains the native download panel');
function element(id=''){
 const events={};
 return {id,value:'',checked:false,disabled:false,hidden:false,options:[],children:[],dataset:{},textContent:'',
   addEventListener(name,fn){(events[name] ||= []).push(fn);},
   async emit(name,data={}){for(const fn of events[name]||[])await fn(data);},
   replaceChildren(){this.children=[];this.value='';},
   appendChild(ch){this.children.push(ch);if(!this.value&&ch.value)this.value=ch.value;},
  };
}
const userA='11111111-1111-4111-8111-111111111111';
const userB='22222222-2222-4222-8222-222222222222';
const seriesId='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const e1='11111111-2222-4333-8444-555555555551';
const e2='11111111-2222-4333-8444-555555555552';
const e3='11111111-2222-4333-8444-555555555553';
const e4='11111111-2222-4333-8444-555555555554';
const responseItems=[
 {Id:e3,Type:'Episode',Name:'3',ParentIndexNumber:1,IndexNumber:3,MediaSources:[{Size:300000000}]},
 {Id:e1,Type:'Episode',Name:'1',ParentIndexNumber:1,IndexNumber:1,MediaSources:[{Size:300000000}]},
 {Id:e4,Type:'Episode',Name:'4',ParentIndexNumber:1,IndexNumber:4,MediaSources:[{Size:300000000}]},
 {Id:e2,Type:'Episode',Name:'2',ParentIndexNumber:1,IndexNumber:2,MediaSources:[{Size:300000000}]},
];
const stored=new Map();
const server=new Map();
function createEnv(userId=userA,hasNative=true,epis=responseItems){
 const els=new Map();const view=element('misioCacheSmartPage');
 view.querySelector=x=>{const id=x.slice(1);if(!els.has(id))els.set(id,element(id));return els.get(id);};
 const get=id=>view.querySelector('#'+id);
 get('mc-smart-count').value='2';get('mc-smart-budget').value='5';
 const calls=[];const identity={id:userId};
 const NativeShell=hasNative?{AppHost:{supports:v=>v==='filedownload',deviceId:()=> 'phoneA'},downloadFiles:arg=>{calls.push(arg);},openDownloadManager:()=>calls.push('open')}:null;
 const api={getCurrentUser:async()=>({Id:identity.id,Name:identity.id===userA?'A':'B'}),serverId:()=> 'server1',getItems:async(_,args)=>{
  if(args.IncludeItemTypes==='Series')return {Items:[{Id:seriesId,Type:'Series',Name:'Series 1'}]};
  if(args.IncludeItemTypes==='Episode')return {Items:epis};
  return {Items:[]};
 }};
 const bridge={
  async getSettings(){return {...(server.get(identity.id)||{enabled:false,count:2,budgetGB:5}),lastAction:''};},
  async saveSettings(values){server.set(identity.id,{...values});return await this.getSettings();},
  downloadManual(entries){NativeShell.downloadFiles(entries);return true;},
  reportStatus(){return Promise.resolve()}
 };
 const document={getElementById:id=>id==='misioCacheSmartPage'?view:null,createElement:()=>element()};
 const localStorage={getItem:k=>stored.has(k)?stored.get(k):null,setItem:(k,v)=>stored.set(k,v)};
 const context={window:{ApiClient:api,NativeShell,MisioCacheSmartDownloads:bridge,addEventListener(){}},document,localStorage,location:{origin:'http://server'},console};
 vm.runInNewContext(script,context,{filename:'overviewPage.html'});
 return {view,get,calls,stored,identity,async tick(){await new Promise(setImmediate);}};
}
async function run(){
 assert(page.includes('mc-smart-devices-panel'),'settings also contain device inventory');
 const env=createEnv();await env.tick();
 assert.equal(env.get('mc-smart-enabled').checked,false,'default opt-out');
 assert.equal(env.get('mc-smart-workflow').hidden,true,'no controls on opt-out');
 env.get('mc-smart-enabled').checked=true;await env.get('mc-smart-enabled').emit('change');await env.tick();
 assert.equal(env.get('mc-smart-workflow').hidden,false);
 assert.equal(server.get(userA).enabled,true,'consent is persisted through MisioCache server API');
 env.get('mc-smart-query').value='Serie';await env.get('mc-smart-search').emit('click');
 await env.tick();
 env.get('mc-smart-series').value=seriesId;await env.get('mc-smart-series').emit('change');await env.tick();
 assert.equal(env.get('mc-smart-episode').children.length,5,'episodes loaded');
 assert.equal(env.get('mc-smart-episode').children[1].value,e1,'episodes sorted');
 env.get('mc-smart-episode').value=e1;
 await env.get('mc-smart-batch').emit('click');
 assert.equal(env.calls.length,1);
 assert.equal(JSON.stringify(env.calls[0]),JSON.stringify([{itemId:e1},{itemId:e2},{itemId:e3}]));
 await env.get('mc-smart-next').emit('click');
 assert.equal(env.calls.length,2);
 assert.equal(JSON.stringify(env.calls[1]),JSON.stringify([{itemId:e2},{itemId:e3}]));
 await env.get('mc-smart-open').emit('click');assert.equal(env.calls[2],'open');
 // Arbitrary numbers: not limited to the former 1/2/3/5 and 2/5/10/20 presets.
 env.get('mc-smart-count').value='4';await env.get('mc-smart-count').emit('change');await env.tick();
 env.get('mc-smart-budget').value='7,5';await env.get('mc-smart-budget').emit('change');await env.tick();
 const prefs4=server.get(userA);
 assert.equal(prefs4.count,4,'any positive integer episode count persists');
 assert.equal(prefs4.budgetGB,7.5,'comma decimal GB limit persists');
 await env.get('mc-smart-batch').emit('click');
 assert.equal(JSON.stringify(env.calls[3]),JSON.stringify([{itemId:e1},{itemId:e2},{itemId:e3},{itemId:e4}]));
 env.get('mc-smart-count').value='12';await env.get('mc-smart-count').emit('change');
 await env.get('mc-smart-batch').emit('click');
 assert.equal(env.calls[4].length,4,'more requested episodes than exist: only available ones');
 for(const invalidCount of ['0','-1','2.5','abc','9007199254740992']){
  env.get('mc-smart-count').value=invalidCount;
  await env.get('mc-smart-batch').emit('click');
  assert.equal(env.calls.length,5,'invalid count must not enqueue: '+invalidCount);
 }
 env.get('mc-smart-count').value='4';
 for(const invalidBudget of ['0','-2','abc','2,3,4']){
  env.get('mc-smart-budget').value=invalidBudget;
  await env.get('mc-smart-batch').emit('click');
  assert.equal(env.calls.length,5,'invalid GB budget must not enqueue: '+invalidBudget);
 }
 env.get('mc-smart-budget').value='7.5';await env.get('mc-smart-budget').emit('change');await env.tick();
 env.get('mc-smart-enabled').checked=false;await env.get('mc-smart-enabled').emit('change');await env.tick();
 await env.get('mc-smart-batch').emit('click');assert.equal(env.calls.length,5,'no downloads if opt-out');
 const b=createEnv(userB);await b.tick();
 assert.equal(b.get('mc-smart-enabled').checked,false,'other user cannot inherit consent');
 const c=createEnv(userA);await c.tick();
 assert.equal(c.get('mc-smart-enabled').checked,false,'opt-out persists');
 // The old localStorage store is no longer the source of truth.
 const localKey='misio.cache.smart.v1:server1:'+userA+':phoneA';
 stored.set(localKey,JSON.stringify({enabled:true,count:5,budgetGB:10}));
 const ignoredLocal=createEnv(userA);await ignoredLocal.tick();
 assert.equal(ignoredLocal.get('mc-smart-enabled').checked,false,'server opt-out overrides stale local consent');
 const d=createEnv(userA,false);await d.tick();
 assert.equal(d.get('mc-smart-enabled').disabled,true,'browser cannot enable');
 const big=createEnv(userB,true,responseItems.map(x=>({...x,MediaSources:[{Size:4*1073741824}]})));await big.tick();
 big.get('mc-smart-enabled').checked=true;await big.get('mc-smart-enabled').emit('change');await big.tick();
 big.get('mc-smart-query').value='Serie';await big.get('mc-smart-search').emit('click');await big.tick();
 big.get('mc-smart-series').value=seriesId;await big.get('mc-smart-series').emit('change');await big.tick();
 big.get('mc-smart-episode').value=e1;await big.get('mc-smart-batch').emit('click');
 assert.equal(big.calls.length,0,'budget reject');
 const missing=createEnv(userB,true,responseItems.map(x=>({...x,MediaSources:[]})));await missing.tick();
 missing.get('mc-smart-enabled').checked=true;await missing.get('mc-smart-enabled').emit('change');await missing.tick();
 missing.get('mc-smart-query').value='Serie';await missing.get('mc-smart-search').emit('click');await missing.tick();
 missing.get('mc-smart-series').value=seriesId;await missing.get('mc-smart-series').emit('change');await missing.tick();
 missing.get('mc-smart-episode').value=e1;await missing.get('mc-smart-batch').emit('click');
 assert.equal(missing.calls.length,0,'unknown size reject');
 for(const key of [...stored.keys()])if(key.includes(userB))stored.delete(key);
 server.delete(userB);
 const switching=createEnv(userA);await switching.tick();
 switching.get('mc-smart-enabled').checked=true;await switching.get('mc-smart-enabled').emit('change');await switching.tick();
 switching.identity.id=userB;await switching.view.emit('pageshow');await switching.tick();
 assert.equal(switching.get('mc-smart-enabled').checked,false,'user switching must reset preference');
 assert(!page.includes('playFromDownloads'),'do not claim to invoke unsupported playback bridge');
 console.log('Smart Downloads: opt-in, arbitrary count and GB, legacy migration, invalid guards, accounts, bridge and sorting OK');
}
run().catch(e=>{console.error(e);process.exitCode=1;});
