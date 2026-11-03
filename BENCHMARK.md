# Performance benchmark

Measured with `src/KStyleMath.Benchmark` (C#) and `.tools/bench.mjs` (upstream
KaTeX on Node.js). Both run the same 348 formulas from `corpus/perf.json`
(KaTeX's screenshot suite plus formulas extracted from its Jest specs).

- 250 measured passes after 40 warm-up passes (87,000 renders per mode).
- Settings: `strict: "ignore"`, `throwOnError: false`, per-case display mode.
- Timings are per `renderToString` call, including parse + build + markup.
- `parse-only` calls the parse phase only (`Katex.GenerateParseTree` / `katex.__parse`).

## Environment

| | |
|---|---|
| CPU | 16 logical cores (Windows x64) |
| OS | Windows 11 `10.0.26100` |
| .NET | 8.0.31 (Release, tiered JIT) |
| Node.js | 24.11.1, V8 13.6 |

## KStyleMath

| mode | renders | total | ops/s | mean µs | median µs | p95 µs | p99 µs | bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| full (HTML + MathML) | 87,000 | 4.72 s | **18,430** | 54.23 | 20.60 | 221.5 | 405.1 | 156,574 |
| HTML only | 87,000 | 4.29 s | **20,286** | 49.27 | 18.60 | 207.4 | 373.4 | 145,606 |
| MathML only | 87,000 | 2.30 s | **37,794** | 26.44 | 8.30 | 111.9 | 213.9 | 79,192 |
| full, display | 87,000 | 4.71 s | **18,481** | 54.08 | 20.60 | 221.0 | 404.0 | 157,042 |
| parse only | 87,000 | 1.83 s | **47,635** | 20.97 | 6.00 | 91.8 | 180.3 | 67,254 |

Cold first render (static initialization + embedded data loading + JIT):
**~75 ms**.

## Upstream KaTeX on Node.js

| mode | ops/s | mean µs | median µs | p95 µs | p99 µs |
| --- | ---: | ---: | ---: | ---: | ---: |
| full (HTML + MathML) | 14,297 | 69.84 | 28.60 | 277.1 | 495.5 |
| HTML only | 16,674 | 59.87 | 24.20 | 234.2 | 441.2 |
| MathML only | 43,990 | 22.66 | 8.70 | 87.7 | 157.8 |
| full, display | 14,240 | 70.10 | 29.20 | 276.2 | 492.9 |
| parse only | 66,055 | 15.08 | 5.10 | 61.4 | 109.4 |

Cold module initialization + first render: **5.1 ms** (V8 startup excluded).

## Summary

- Full rendering is **~1.29× faster** than upstream KaTeX on Node (18.4k vs
  14.3k renders/s); HTML-only is ~1.22× faster.
- MathML-only is ~14% slower and parsing is ~28% slower than V8; the
  HTML/MathML string building is where the C# implementation gains.
- Median render time is ~20.6 µs for the full pipeline; 95% of formulas render
  in under ~225 µs.
- Compared with the first working port, the markup path was changed to append
  directly into a `StringBuilder` and to skip escaping when unnecessary:
  throughput improved from 11.5k to 18.4k full renders/s (+58%) and allocations
  dropped from ~400 KB to ~157 KB per render.

The absolute numbers depend on CPU, JIT warm-up and the formula mix; the
`ops/s` ratios are the useful signal. Run `dotnet run --project
src/KStyleMath.Benchmark -c Release` to reproduce.
