import fs from 'node:fs';
import * as yaml from 'js-yaml';
import katex from 'katex';

const ref = '../.katex-ref';
const data = yaml.load(fs.readFileSync(`${ref}/test/screenshotter/ss_data.yaml`, 'utf8'));
const cases = [];
for (const [name, value] of Object.entries(data)) {
  if (typeof value === 'string') {
    cases.push({name, tex: value, display: false});
  } else if (value && typeof value === 'object') {
    const tex = value.tex ?? value[0];
    if (typeof tex === 'string') {
      cases.push({name, tex, display: !!value.display});
    }
  }
}

const out = [];
for (const c of cases) {
  let html;
  try {
    html = katex.renderToString(c.tex, {displayMode: c.display, throwOnError: false, strict: 'ignore'});
  } catch (e) {
    html = 'ERROR: ' + e.message;
  }
  out.push({...c, settings: {throwOnError: false}, html});
}
fs.mkdirSync('../corpus', {recursive: true});
fs.writeFileSync('../corpus/goldens.json', JSON.stringify(out, null, 1));
console.log('cases:', out.length);


