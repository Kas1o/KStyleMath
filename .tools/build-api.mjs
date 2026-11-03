import fs from 'node:fs';
import katex from 'katex';

const cases = [];
function add(name, tex, settings) {
  let html;
  const opts = {throwOnError: true, strict: 'ignore', ...settings};
  try {
    html = katex.renderToString(tex, opts);
  } catch (e) {
    html = 'THROW:' + e.message;
  }
  cases.push({name, tex, settings: settings ?? {}, html});
}

add('macro-user', '\\RR + \\foo', {macros: {'\\RR': '\\mathbb{R}', '\\foo': 'x^2'}});
add('macro-recursive', '\\a', {macros: {'\\a': '\\b', '\\b': '\\a'}, maxExpand: 10});
add('macro-expand', '\\a', {macros: {'\\a': 'x'}});
add('trust-href', '\\href{https://example.com/a?b=1}{x}', {trust: true});
add('untrust-href', '\\href{javascript:alert(1)}{x}', {trust: true});
add('notrust-href', '\\href{https://example.com}{x}', {trust: false});
add('trust-url', '\\url{https://example.com/a_b}', {trust: true});
add('trust-htmlclass', '\\htmlClass{abc}{x}', {trust: true});
add('trust-htmlid', '\\htmlId{abc}{x}', {trust: true});
add('trust-htmlstyle', '\\htmlStyle{color:red}{x}', {trust: true});
add('trust-htmldata', '\\htmlData{a=1,b=2}{x}', {trust: true});
add('notrust-htmlclass', '\\htmlClass{abc}{x}', {trust: false});
add('includegraphics', '\\includegraphics[height=2em]{a.png}', {trust: true});
add('color-text', '\\color{red} x', {colorIsTextColor: false});
add('color-text-2', '\\color{red} x', {colorIsTextColor: true});
add('leqno', '\\tag{1} x', {displayMode: true, leqno: true});
add('fleqn', 'x', {displayMode: true, fleqn: true});
add('tag-inline', 'x\\tag{1}', {});
add('max-size', '\\rule{100em}{100em}', {maxSize: 5});
add('min-rule', '\\frac{1}{2}', {minRuleThickness: 0.1});
add('error-undefined', '\\notacommand', {});
add('error-sup', 'x^2^3', {});
add('error-color', '\\color{not a color}{x}', {});
add('error-sqrt', '\\sqrt', {});
add('error-env', '\\begin{nope}x\\end{nope}', {});
add('error-math', 'x^', {});
add('render-error-false', '\\notacommand', {throwOnError: false});
add('error-color-custom', '\\notacommand', {throwOnError: false, errorColor: '#123456'});
add('text-mode', '\\text{hello \\textbf{world}}', {});
add('unicode', 'α + β = γ', {});
add('unicode-sup', 'x² + y₁', {});
add('accent-unicode', 'é + ü', {});
add('mathchoice', '\\mathchoice{a}{b}{c}{d}', {displayMode: true});
add('displaystyle', '\\displaystyle\\sum_{i=1}^n i', {});
add('styling-in-text', '\\text{a $x^2$ b}', {});
add('operatorname', '\\operatorname*{arg\\,max}_{x} f(x)', {displayMode: true});
add('array', '\\begin{array}{c|c}a&b\\\\c&d\\end{array}', {displayMode: true});

fs.writeFileSync('../corpus/api.json', JSON.stringify(cases, null, 1));
console.log('api cases:', cases.length);
