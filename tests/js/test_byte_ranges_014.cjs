// Legacy filename retained for compatibility. Verify diagnostics were removed.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const html=fs.readFileSync(path.join(__dirname,'../../src/Jellyfin.Plugin.MisioCache/Web/configPage.html'),'utf8');
assert(html.includes('mc-update-install'));
for(const token of ['mc-http-optin','mc-meter-optin','mc-policy-form','mc-byte-recent'])
 assert(!html.includes(token),token);
console.log('MisioCache: legacy diagnostics absent');
