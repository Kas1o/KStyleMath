import * as esbuild from 'esbuild';
import {createRequire} from 'node:module';
import fs from 'node:fs';
import path from 'node:path';

const require = createRequire(import.meta.url);
const root = path.resolve('..');
const ref = path.join(root, '.katex-ref');
const out = path.join(root, 'src', 'KStyleMath', 'Data');
fs.mkdirSync(out, {recursive: true});
fs.mkdirSync(path.join(root, '.tools', 'tmp'), {recursive: true});

function load(tsPath) {
  const bundle = path.join(root, '.tools', 'tmp', path.basename(tsPath).replace(/\.[^.]+$/, '.cjs'));
  esbuild.buildSync({
    entryPoints: [tsPath], bundle: true, platform: 'node', format: 'cjs',
    outfile: bundle, logLevel: 'silent',
  });
  delete require.cache[require.resolve(bundle)];
  return require(bundle);
}

const symbolsMod = load(path.join(ref, 'src', 'symbols.ts'));
fs.writeFileSync(path.join(out, 'symbols.json'), JSON.stringify({
  math: symbolsMod.default.math,
  text: symbolsMod.default.text,
  ligatures: symbolsMod.ligatures,
  extraLatin: symbolsMod.extraLatin,
}));

const fm = load(path.join(ref, 'src', 'fontMetricsData.js'));
fs.writeFileSync(path.join(out, 'fontMetricsData.json'), JSON.stringify(fm.default));

const svg = load(path.join(ref, 'src', 'svgGeometry.ts'));
fs.writeFileSync(path.join(out, 'svgGeometry.json'), JSON.stringify(svg.path));

const accents = load(path.join(ref, 'src', 'unicodeAccents.js'));
const unicodeSymbols = load(path.join(ref, 'src', 'unicodeSymbols.js'));
fs.writeFileSync(path.join(out, 'unicode.json'), JSON.stringify({
  accents: accents.default ?? accents,
  symbols: unicodeSymbols,
}));

console.log('wrote data files');
