import fs from 'node:fs';
import * as yaml from 'js-yaml';
import katex from 'katex';

const data = yaml.load(fs.readFileSync('../.katex-ref/test/screenshotter/ss_data.yaml', 'utf8'));
const base = [];
for (const [name, value] of Object.entries(data)) {
  if (typeof value === 'string') base.push({name, tex: value, display: false});
  else if (value && typeof value === 'object' && typeof value.tex === 'string') {
    base.push({name, tex: value.tex, display: !!value.display});
  }
}
const extra = JSON.parse(fs.readFileSync('../corpus/extra.json', 'utf8')).slice(0, 120);
for (const e of extra) base.push({name: 'x' + e.name, tex: e.tex, display: false});

const variants = [
  {suffix: 'html', settings: {output: 'html'}},
  {suffix: 'mathml', settings: {output: 'mathml'}},
  {suffix: 'display', settings: {displayMode: true}},
];
const out = [];
for (const c of base) {
  for (const v of variants) {
    const settings = {...v.settings};
    if (v.suffix === 'display') settings.displayMode = true;
    else settings.displayMode = c.display;
    let html;
    try {
      html = katex.renderToString(c.tex, {...settings, throwOnError: false, strict: 'ignore'});
    } catch (e) { continue; }
    out.push({name: c.name + ':' + v.suffix, tex: c.tex, settings: {...settings, throwOnError: false}, html});
  }
}
fs.writeFileSync('../corpus/variants.json', JSON.stringify(out, null, 1));
console.log('variant cases:', out.length);


