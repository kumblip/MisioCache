'use strict';
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const path=require('node:path');
const source=fs.readFileSync(path.resolve(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/configPage.html'),'utf8');
const script=source.match(/<script>([\s\S]*?)<\/script>/)[1];
const GUID='5cf36828-5b7b-4d18-a28e-1e87922e505b';
const raw='https://raw.githubusercontent.com/kumblip/MisioCache/main/manifest.json';
const pin='https://raw.githubusercontent.com/kumblip/MisioCache/'+'a'.repeat(40)+'/manifest.json';
function element(id){return {id, textContent:'', value:'', checked:false, disabled:false, isConnected:true,
  listeners:{},addEventListener(type,fn){this.listeners[type]=fn;},querySelector(selector){return nodes[selector.slice(1)];}};}
let nodes;
async function caseRun({withRepo,installed,remoteVersion}){
  nodes={};
  for(const id of [...Array.from(source.matchAll(/id="(mc-[\w-]+)"/g),m=>m[1]),'mc-updates-panel'])nodes[id]=element(id);
  nodes['mc-update-install'].disabled=true;
  nodes['mc-updates-panel'].querySelector=sel=>nodes[sel.slice(1)];
  const view={querySelector:sel=>nodes[sel.slice(1)],addEventListener(){}};
  let repo=withRepo?raw:null;
  let packageVersion=installed;
  let saved=null;
  const calls=[];
  const cfg={AutoCheckUpdates:true,DownloadUntilEnd:true,PreserveWatchedPartsUntilCompletion:true,
    DownloadWhilePaused:true,DeleteAfterCompleted:true,KeepAfterStopMinutes:60,PlannedMaxDeviceCacheGiB:3};
  const result=(value,status=200)=>({ok:status<400,status,json:async()=>value});
  const fetch=async (url,opts={})=>{
    const p=String(url).replace(/^\/+/,'');calls.push([p,opts.method||'GET']);
    if(p.startsWith('MisioCache/api/updates/native-catalog/refresh')&&opts.method==='POST'){
      if(!repo)return result({error:'Jellyfin musi mieć dokładnie jedno repozytorium MisioCache.'},400);
      repo=pin;packageVersion=remoteVersion;
      return result({repositoryVersion:remoteVersion,repositoryTargetAbi:'12.4.0.0',verifiedByJellyfin:true,updated:true,repositoryUrl:pin});
    }
    if(p.startsWith('MisioCache/api/updates/status'))return result({installedVersion:installed,gitHubVersion:remoteVersion,
      repositoryVersion:remoteVersion,repositoryTargetAbi:'12.4.0.0',checkedAt:new Date().toISOString()});
    if(p==='Packages')return result([{Guid:GUID,Name:'MisioCache',Versions:[{Version:packageVersion,TargetAbi:'12.4.0.0'}]}]);
    if(p==='Repositories')return result(repo?[{Name:'MisioCache',Url:repo,Enabled:true}]:[]);
    if(p==='Plugins')return result([{Id:GUID,Name:'MisioCache',Version:installed}]);
    if(p.startsWith('Packages/Installed/MisioCache')&&opts.method==='POST')return result({},204);
    throw Error('Unexpected request '+p);
  };
  const context={document:{getElementById:id=>id==='misioCacheConfigPage'?view:null},
    window:{isSecureContext:false,setInterval:()=>1,clearInterval:()=>{},confirm:()=>true,ApiClient:null},
    ApiClient:{accessToken:()=> 'session-token',getUrl:p=>'/'+p,getPluginConfiguration:async()=>cfg,
      updatePluginConfiguration:async(_id,obj)=>{saved=obj;}},
    navigator:{},fetch,URLSearchParams,Date,console,setTimeout,clearTimeout};
  context.window.ApiClient=context.ApiClient;
  vm.createContext(context);
  vm.runInContext(script,context);
  async function tick(){for(let i=0;i<5;i++)await new Promise(ok=>setTimeout(ok,0));}
  await tick();
  assert.equal(nodes['mc-update-installed'].textContent,installed);
  assert.equal(nodes['mc-update-github'].textContent,remoteVersion);
  assert.equal(nodes['mc-update-manifest'].textContent.includes(remoteVersion),true);
  assert.equal(nodes['mc-update-install'].disabled,true,'Initially catalog is not newer');
  if(!repo){
    assert.match(nodes['mc-update-status'].textContent,/Dodaj jedno aktywne repozytorium MisioCache/);
    await nodes['mc-update-check'].listeners.click();
    await tick();
    assert.match(nodes['mc-update-status'].textContent,/nie powiodło się/);
    assert.equal(nodes['mc-update-install'].disabled,true);
    assert.equal(calls.filter(x=>x[1]==='POST'&&x[0].startsWith('Packages/Installed')).length,0);
    return;
  }
  await nodes['mc-update-check'].listeners.click();
  await tick();
  assert.equal(nodes['mc-update-install'].disabled,false,'Newer pinned catalog should permit native install');
  assert.equal(nodes['mc-update-catalog'].textContent,remoteVersion);
  assert(calls.some(x=>x[0].includes('native-catalog/refresh')&&x[1]==='POST'));
  await nodes['mc-update-install'].listeners.click();
  assert(calls.some(x=>x[0].includes('Packages/Installed/MisioCache?')&&x[0].includes('assemblyGuid='+GUID)&&x[1]==='POST'));
  assert.match(nodes['mc-update-status'].textContent,/przyjął zlecenie instalacji/);
  nodes['mc-update-auto'].checked=false;
  await nodes['mc-update-auto'].listeners.change();
  assert.equal(saved.AutoCheckUpdates,false);
}
(async()=>{
  await caseRun({withRepo:true,installed:'0.1.0.0',remoteVersion:'0.1.1.0'});
  await caseRun({withRepo:false,installed:'0.1.0.0',remoteVersion:'0.1.1.0'});
  console.log('UI updates: force, catalog, native install, missing repo, auto check — OK');
})().catch(e=>{console.error(e);process.exitCode=1;});
