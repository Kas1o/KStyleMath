import fs from 'node:fs';
import katex from 'katex';

const files = [
  '../.katex-ref/test/katex-spec.ts',
  '../.katex-ref/test/mathml-spec.ts',
  '../.katex-ref/test/unicode-spec.ts',
];

// Cook a JS template literal (only the escapes that matter here).
function cook(s) {
  let out = '';
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (c !== '\\') { out += c; continue; }
    const n = s[++i];
    switch (n) {
      case 'n': out += '\n'; break;
      case 't': out += '\t'; break;
      case 'r': out += '\r'; break;
      case 'b': out += '\b'; break;
      case 'f': out += '\f'; break;
      case 'v': out += '\v'; break;
      case '0': out += '\0'; break;
      case '\\': out += '\\'; break;
      case '`': out += '`'; break;
      case '$': out += '$'; break;
      case 'u':
        if (s[i + 1] === '{') {
          const end = s.indexOf('}', i);
          out += String.fromCodePoint(parseInt(s.slice(i + 2, end), 16));
          i = end;
        } else {
          out += String.fromCharCode(parseInt(s.slice(i + 1, i + 5), 16));
          i += 4;
        }
        break;
      case 'x':
        out += String.fromCharCode(parseInt(s.slice(i + 1, i + 3), 16));
        i += 2;
        break;
      default:
        out += '\\' + (n ?? '');
    }
  }
  return out;
}

const cases = new Map();
for (const file of files) {
  const src = fs.readFileSync(file, 'utf8');
  const re = /(?:expect|r)`((?:[^`\\]|\\.)*)`/gs;
  let m;
  while ((m = re.exec(src))) {
    let tex = m[0].startsWith('r`') ? m[1] : cook(m[1]);
    if (!tex || tex.length > 500) continue;
    if (tex.includes('${')) continue;
    // Skip obvious prose: must contain at least one TeX-ish character.
    if (!/[\\^_{}$&]/.test(tex)) continue;
    cases.set(tex, tex);
  }
}

const out = [];
for (const tex of cases.values()) {
  let html;
  try {
    html = katex.renderToString(tex, {throwOnError: false, strict: 'ignore'});
  } catch (e) {
    continue;
  }
  if (html.includes('katex-error')) continue;
  out.push({name: 'x' + out.length, tex, display: false, settings: {throwOnError: false}, html});
}
fs.writeFileSync('../corpus/extra.json', JSON.stringify(out, null, 1));
console.log('extra cases:', out.length);

