# Validation corpus

All files are generated from upstream KaTeX 0.19.1 and contain a `tex` input plus
the exact `html` produced by `katex.renderToString`.  `KStyleMath.Cli` compares
its own `Katex.RenderToString` output byte-for-byte.

| File | Cases | Contents |
| --- | ---: | --- |
| `goldens.json` | 128 | `test/screenshotter/ss_data.yaml` |
| `extra.json` | 492 | formulas extracted from KaTeX's Jest specs |
| `variants.json` | 744 | the above rendered with `output: html`, `output: mathml`, display mode |
| `commands.json` | 1,076 | every command/symbol in KaTeX's tables, including error markup |
| `macros.json` | 152 | `\def`, `\let`, `\newcommand`, `\expandafter`, environments, … |
| `api.json` | 37 | settings/API surface: trust, custom macros, maxExpand, tagging, colors, errors |

Run everything:

```powershell
pwsh ./validate.ps1
```

Regenerate after changing the KaTeX reference:

```powershell
cd .tools
node extract.mjs
node build-goldens.mjs
node build-extra.mjs
node build-variants.mjs
node build-commands.mjs
node build-macros.mjs
node build-api.mjs
```

`build-*` scripts require the `katex`, `esbuild`, `js-yaml` npm packages in
`.tools` (already listed in `package-lock.json`).
