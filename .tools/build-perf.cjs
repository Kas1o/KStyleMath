const fs=require('fs');
const g=JSON.parse(fs.readFileSync('corpus/goldens.json','utf8'));
const e=JSON.parse(fs.readFileSync('corpus/extra.json','utf8')).slice(0,220);
const cases=[];
for(const c of g) cases.push({name:c.name,tex:c.tex,display:!!c.display});
for(const c of e){ if(/\\\\(message|show|errmessage)/.test(c.tex)) continue; cases.push({name:c.name,tex:c.tex,display:false}); }
fs.writeFileSync('corpus/perf.json',JSON.stringify(cases,null,1));
console.log('perf cases',cases.length);
