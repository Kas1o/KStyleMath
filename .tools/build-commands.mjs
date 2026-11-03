import fs from 'node:fs';
import katex from 'katex';

const symbols = JSON.parse(fs.readFileSync('../src/KStyleMath/Data/symbols.json', 'utf8'));
const names = new Set();
for (const k of Object.keys(symbols.math)) if (k.startsWith('\\')) names.add(k);
for (const k of Object.keys(symbols.text)) if (k.startsWith('\\')) names.add(k);

function scrape(dir) {
  for (const f of fs.readdirSync(dir)) {
    const p = dir + '/' + f;
    if (fs.statSync(p).isDirectory()) { scrape(p); continue; }
    if (!/\.(ts|js)$/.test(f)) continue;
    const src = fs.readFileSync(p, 'utf8');
    for (const m of src.matchAll(/"((?:\\\\.|[^"\\\\\n])+)"/g)) {
      let s;
      try { s = JSON.parse('"' + m[1] + '"'); } catch { continue; }
      if (/^\\+[A-Za-z@]+$/.test(s) || /^\\.$/.test(s)) names.add(s);
    }
    for (const m of src.matchAll(/'((?:\\\\.|[^'\\\\\n])+)'/g)) {
      const s = m[1].replace(/\\\\/g, '\\');
      if (/^\\+[A-Za-z@]+$/.test(s) || /^\\.$/.test(s)) names.add(s);
    }
  }
}
scrape('../.katex-ref/src/functions');
scrape('../.katex-ref/src/environments');

const macrosSrc = fs.readFileSync('../.katex-ref/src/macros.ts', 'utf8');
for (const m of macrosSrc.matchAll(/defineMacro\(\s*"((?:\\\\.|[^"\\\\\n])+)"/g)) {
  let s;
  try { s = JSON.parse('"' + m[1] + '"'); } catch { continue; }
  if (s.startsWith('\\')) names.add(s);
}

const out = [];
for (const tex of names) {
  let html;
  try {
    html = katex.renderToString(tex, {throwOnError: false, strict: 'ignore'});
  } catch (e) {
    html = 'THROW:' + e.message;
  }
  out.push({name: 'c' + out.length, tex, settings: {throwOnError: false}, html});
}
fs.writeFileSync('../corpus/commands.json', JSON.stringify(out, null, 1));
console.log('command cases:', out.length);
