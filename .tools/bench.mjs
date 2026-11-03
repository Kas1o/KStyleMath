import fs from 'node:fs';
import katex from 'katex';

const cases = JSON.parse(fs.readFileSync('../corpus/perf.json', 'utf8'));
const warmup = 40;
const passes = 250;

function settingsFor(c, output) {
  return {displayMode: !!c.display, output, strict: 'ignore', throwOnError: false};
}

function run(mode, output, displayOverride, parseOnly) {
  const prepared = cases.map(c => ({c, s: {...settingsFor(c, output), ...(displayOverride === null ? {} : {displayMode: displayOverride})}}));
  for (let p = 0; p < warmup; p++) {
    for (const {c, s} of prepared) {
      if (parseOnly) katex.__parse(c.tex, s); else katex.renderToString(c.tex, s);
    }
  }
  const total = cases.length * passes;
  const samples = new Float64Array(total);
  let idx = 0;
  if (global.gc) global.gc();
  const heap0 = process.memoryUsage().heapUsed;
  const t0 = process.hrtime.bigint();
  for (let p = 0; p < passes; p++) {
    for (const {c, s} of prepared) {
      const a = process.hrtime.bigint();
      if (parseOnly) katex.__parse(c.tex, s); else katex.renderToString(c.tex, s);
      const b = process.hrtime.bigint();
      samples[idx++] = Number(b - a) / 1000;
    }
  }
  const elapsed = Number(process.hrtime.bigint() - t0) / 1e9;
  const heap1 = process.memoryUsage().heapUsed;
  samples.sort();
  const pick = q => samples[Math.min(total - 1, Math.max(0, Math.ceil(q * total) - 1))];
  const mean = samples.reduce((a, b) => a + b, 0) / total;
  return {
    mode, total, elapsed, ops: total / elapsed, mean,
    median: pick(0.5), p95: pick(0.95), p99: pick(0.99),
    bytes: (heap1 - heap0) / total,
  };
}

console.log('KaTeX (Node.js) performance benchmark');
console.log('=====================================');
console.log(`Runtime      : Node ${process.version} / V8 ${process.versions.v8}`);
console.log(`Corpus       : ${cases.length} formulas`);
console.log(`Passes       : ${passes} after ${warmup} warmup passes`);
console.log('');

const t = process.hrtime.bigint();
katex.renderToString(cases[0].tex, settingsFor(cases[0], 'htmlAndMathml'));
console.log(`Cold first render (incl. module init): ${(Number(process.hrtime.bigint() - t) / 1e6).toFixed(2)} ms`);
console.log('');

const rows = [
  run('full', 'htmlAndMathml', null, false),
  run('html', 'html', null, false),
  run('mathml', 'mathml', null, false),
  run('display', 'htmlAndMathml', true, false),
  run('parse-only', 'htmlAndMathml', null, true),
];

const pad = (s, n) => String(s).padEnd(n);
const padl = (s, n) => String(s).padStart(n);
console.log(`${pad('mode',10)} ${padl('renders',10)} ${padl('total ms',10)} ${padl('ops/s',10)} ${padl('mean µs',9)} ${padl('median µs',10)} ${padl('p95 µs',9)} ${padl('p99 µs',9)} ${padl('bytes/op',10)}`);
console.log('-'.repeat(103));
for (const r of rows) {
  console.log(`${pad(r.mode,10)} ${padl(r.total.toLocaleString(),10)} ${padl((r.elapsed * 1000).toFixed(1),10)} ${padl(Math.round(r.ops).toLocaleString(),10)} ${padl(r.mean.toFixed(2),9)} ${padl(r.median.toFixed(2),10)} ${padl(r.p95.toFixed(2),9)} ${padl(r.p99.toFixed(2),9)} ${padl(Math.round(r.bytes).toLocaleString(),10)}`);
}
