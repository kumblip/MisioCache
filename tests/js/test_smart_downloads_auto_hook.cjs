'use strict';
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const script=fs.readFileSync(path.join(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/smart-downloads.js'),'utf8');
const page=fs.readFileSync(path.join(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/overviewPage.html'),'utf8');
const root=path.join(__dirname,'../../src/Jellyfin.Plugin.MisioCache');
const guid=n=>`11111111-1111-4111-8111-${String(n).padStart(12,'0')}`;
const USER1=guid(999), USER2=guid(998), SERIES=guid(990);
const episodes=[1,2,3,4,5].map(n=>({Id:guid(n),Type:'Episode',SeriesId:SERIES,ParentIndexNumber:1,IndexNumber:n,MediaSources:[{Size:100000000}]}));
async function flush(){for(let n=0;n<5;n++)await new Promise(resolve=>setImmediate(resolve));}
function setup({user=USER1,native=true,api=true,storage=new Map(),missingSize=false,bigSize=false,server='server1',device='phone1',singleDownload=true}={}){
 const nativeCalls=[],events=[];
 const userHolder={id:user};
 const data=episodes.map(x=>({...x,MediaSources:missingSize?[]:[{Size:bigSize?3e9:100000000}]}));
 const nativeShell={
  AppHost:{supports:k=>k==='filedownload',deviceId:()=>device},
  downloadFile(info){nativeCalls.push({type:'single',items:[info]});return 'native-single';},
  downloadFiles(items){nativeCalls.push({type:'batch',items});return 'native-batch';},
  openDownloadManager(){}
 };
 const service={
  getCurrentUser:async()=>({Id:userHolder.id}),serverId:()=>server,
  getItems:async(_,query)=>{
   if(query.Ids)return {Items:data.filter(x=>x.Id.replaceAll('-','')===String(query.Ids).replaceAll('-',''))};
   if(query.IncludeItemTypes==='Episode')return {Items:data.slice().reverse()};
   return {Items:[]};
  }
 };
 const window={
  NativeShell:native?nativeShell:undefined,
  ApiClient:api?service:undefined,
  localStorage:{getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,v)},
  location:{origin:'http://server'},
  addEventListener(){},dispatchEvent:e=>events.push(e),
  setInterval:()=>1,clearInterval:()=>{},
 };
 const context={window,console,CustomEvent:class{constructor(type,options){this.type=type;this.detail=options.detail;}}};
 vm.runInNewContext(script,context,{filename:'smart-downloads.js'});
 return {window,nativeShell,nativeCalls,events,storage,userHolder,service,
  prefKey:(id=userHolder.id)=>`misio.cache.smart.v1:${server}:${id}:${device}`,
  enable:(count=2,budgetGB=5)=>storage.set(`misio.cache.smart.v1:${server}:${userHolder.id}:${device}`,JSON.stringify({enabled:true,count,budgetGB})),
  async download(n){const result=nativeShell.downloadFile({itemId:guid(n)});await flush();return result;},
  async downloadFiles(items){const result=nativeShell.downloadFiles(items);await flush();return result;}
 };
}
(async()=>{
 const disabled=setup();
 assert.equal(await disabled.download(1),'native-single');
 assert.equal(disabled.nativeCalls.length,1,'native action remains untouched when opt-out');
 const active=setup();active.enable();
 assert.equal(await active.download(1),'native-single');
 assert.equal(active.nativeCalls.length,2,'user action + one extra batch');
 assert.equal(active.nativeCalls[1].type,'batch');
 assert.equal(JSON.stringify(active.nativeCalls[1].items),JSON.stringify([{itemId:guid(2)},{itemId:guid(3)}]));
 const key='misio.cache.smart.last.v1:'+active.prefKey();
 assert.match(JSON.parse(active.storage.get(key)).message,/2 kolejnych/,'show status on MisioCache page');
 assert.equal(active.nativeCalls[0].items[0].itemId,guid(1),'original downloaded item unchanged');
 // Repeating action during the same session does not enqueue the same following episodes again.
 await active.download(1);assert.equal(active.nativeCalls.length,3);
 // Downloading a different episode advances the queue by one, not a runaway chain.
 await active.download(2);assert.equal(active.nativeCalls.length,5);
 assert.equal(JSON.stringify(active.nativeCalls[4].items),JSON.stringify([{itemId:guid(4)}]));
 // Custom MisioCache page should not recurse even with one manually queued episode.
 const before=active.nativeCalls.length;
 active.window.MisioCacheSmartDownloads.downloadManual([{itemId:guid(5)}]);await flush();
 assert.equal(active.nativeCalls.length,before+1,'manual single-item call does not cause automatic queue');
 assert(page.includes('window.MisioCacheSmartDownloads?.downloadManual'),'manual panel uses bypass');
 // Multi-select downloads are left alone, not treated as a new queue head.
 const multi=setup();multi.enable();await multi.downloadFiles([{itemId:guid(1)},{itemId:guid(2)}]);
 assert.equal(multi.nativeCalls.length,1);
 // Unknown file sizes are rejected conservatively, while original download remains.
 const missing=setup({missingSize:true});missing.enable();await missing.download(1);
 assert.equal(missing.nativeCalls.length,1);
 assert.match(JSON.parse(missing.storage.get('misio.cache.smart.last.v1:'+missing.prefKey())).message,/Rozmiar/);
 // GB budget covers selected episode + extra episodes.
 const budget=setup();budget.enable(2,0.25);await budget.download(1);
 assert.equal(budget.nativeCalls.length,1,'3 x 100MB exceeds 0.25GB');
 // Another Jellyfin account on the same device cannot inherit consent.
 const switching=setup();switching.enable();switching.userHolder.id=USER2;
 await switching.download(1);assert.equal(switching.nativeCalls.length,1);
 // The second user can opt in separately.
 switching.enable(1,5);await switching.download(2);assert.equal(switching.nativeCalls.length,3);
 // Disabled on another phone even with same account.
 const otherDevice=setup({storage:active.storage,device:'phone2'});await otherDevice.download(1);
 assert.equal(otherDevice.nativeCalls.length,1);
 // Arbitrary configured counts and comma budget are read.
 const arbitrary=setup();arbitrary.enable(4,'7,5');await arbitrary.download(1);
 assert.equal(arbitrary.nativeCalls[1].items.length,4);
 // Invalid preference or missing API => original native action only.
 const invalid=setup();invalid.enable(0,5);await invalid.download(1);
 assert.equal(invalid.nativeCalls.length,1);
 const noapi=setup({api:false});noapi.enable();await noapi.download(1);
 assert.equal(noapi.nativeCalls.length,1);
 const nonnative=setup({native:false});
 assert.equal(nonnative.window.MisioCacheSmartDownloads.refreshHook(),false);
 // Native method thrown errors are preserved, not swallowed by the hook.
 const exception=setup();exception.nativeShell.downloadFile=()=>{throw Error('Native error')};
 exception.window.MisioCacheSmartDownloads.refreshHook();
 assert.throws(()=>exception.nativeShell.downloadFile({itemId:guid(1)}),/Native error/);
 // This is a separate server plugin, with own script and own middleware.
 const injection=fs.readFileSync(path.join(root,'Services/MisioCacheSmartDownloadsStartupFilter.cs'),'utf8');
 const serviceFile=fs.readFileSync(path.join(root,'Services/MisioCacheServiceRegistrator.cs'),'utf8');
 const controller=fs.readFileSync(path.join(root,'Controllers/MisioCacheClientAssetsController.cs'),'utf8');
 assert(injection.includes('IsIndex(context.Request.Path.Value)'));
 assert(injection.includes('data-misiocache'));
 assert(!injection.includes('Video/'));
 assert(serviceFile.includes('AddTransient<IStartupFilter, MisioCacheSmartDownloadsStartupFilter>'));
 assert(controller.includes('MisioCache/assets'));
 assert(!script.includes('window.MisioFlix') && !script.includes('/MisioFlix'));
 console.log('Smart Downloads: standard native download, 2 next, opt-in, budget, multi-account/device, no recursion, manual bypass, unknown sizes, error preserving, own injection — OK');
})().catch(e=>{console.error(e);process.exitCode=1;});
