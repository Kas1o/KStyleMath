import fs from 'node:fs';
import katex from 'katex';

const exprs = [
  String.raw`\def\foo#1{#1^2}\foo{x}`,
  String.raw`\def\foo#1#2{#1+#2}\foo{a}{b}`,
  String.raw`\def\foo#1[#2]{#1_{#2}}\foo{x}[i]`,
  String.raw`\gdef\foo{x}\foo`,
  String.raw`\edef\foo{\noexpand\alpha}\foo`,
  String.raw`\xdef\foo{x}\foo`,
  String.raw`\let\foo\alpha\foo`,
  String.raw`\let\foo=\alpha\foo`,
  String.raw`\futurelet\foo\alpha x`,
  String.raw`\def\foo#1{#1}\expandafter\foo\csname alpha\endcsname`,
  String.raw`\noexpand\alpha`,
  String.raw`\newcommand{\foo}[1]{#1^2}\foo{x}`,
  String.raw`\newcommand{\foo}{x}\renewcommand{\foo}{y}\foo`,
  String.raw`\providecommand{\foo}{x}\foo`,
  String.raw`\providecommand{\alpha}{x}\alpha`,
  String.raw`\global\def\foo{x}\foo`,
  String.raw`\long\def\foo#1{#1}\foo{x}`,
  String.raw`\char` + "`A",
  String.raw`\char"41`,
  String.raw`\char'101`,
  String.raw`\char65`,
  String.raw`\@ifstar{a}{b}*`,
  String.raw`\@ifstar{a}{b}x`,
  String.raw`\TextOrMath{text}{math}`,
  String.raw`\TextOrMath{text}{math}`,
  String.raw`\bra{a}`,
  String.raw`\ket{b}`,
  String.raw`\braket{a|b}`,
  String.raw`\set{x|y}`,
  String.raw`\dots`,
  String.raw`\dotsc`,
  String.raw`\dotsb`,
  String.raw`\dotsi`,
  String.raw`\dotsx`,
  String.raw`\notin`,
  String.raw`\not= \not< \not\in`,
  String.raw`\mathbb{R}\mathcal{L}\mathfrak{g}\mathscr{H}\mathsf{T}\mathtt{T}`,
  String.raw`\boldsymbol{\alpha+\beta}`,
  String.raw`\text{a \textbf{b} \textit{c} \textsf{d} \texttt{e}}`,
  String.raw`\operatorname{lim\,sup}`,
  String.raw`\operatorname*{arg\,max}_{x} f(x)`,
  String.raw`\sqrt[3]{x}`,
  String.raw`\frac{a}{b} \dfrac{a}{b} \tfrac{a}{b} \cfrac{a}{b}`,
  String.raw`\binom{n}{k} \dbinom{n}{k} \tbinom{n}{k}`,
  String.raw`{a \over b} {a \choose b} {a \atop b} {a \brace b} {a \brack b} {a \above 1pt b}`,
  String.raw`\overbrace{a+b}^{n} \underbrace{a+b}_{n}`,
  String.raw`\xrightarrow[a]{b} \xleftarrow[a]{b} \xlongequal{a}`,
  String.raw`\overset{a}{b} \underset{a}{b} \stackrel{a}{b}`,
  String.raw`\mathllap{a}\mathrlap{b}\mathclap{c}`,
  String.raw`\cancel{a}\bcancel{b}\xcancel{c}\sout{d}\phase{e}`,
  String.raw`\colorbox{red}{x}\fcolorbox{red}{blue}{x}\fbox{x}`,
  String.raw`\raisebox{1em}{x}\reflectbox{x}\smash{x}`,
  String.raw`\rule{1em}{2em}`,
  String.raw`\kern1em\mkern3mu\hskip2pt\mskip4mu`,
  String.raw`\vcenter{\frac12}`,
  String.raw`\verb|hello| \verb*+world+`,
  String.raw`\textcircled{a}`,
  String.raw`\begin{CD} A @>a>> B \\ @VbVV @AAcA \\ C @= D \end{CD}`,
  String.raw`\begin{array}{c|c} a & b \\ \hline c & d \\ \hdashline e & f \end{array}`,
  String.raw`\begin{matrix} a & b \\ c & d \end{matrix}`,
  String.raw`\begin{pmatrix} a & b \\ c & d \end{pmatrix}`,
  String.raw`\begin{cases} a & x>0 \\ b & x\le 0 \end{cases}`,
  String.raw`\begin{alignedat}{2} a &= 1 & b &= 2 \\ c &= 3 & d &= 4 \end{alignedat}`,
  String.raw`\begin{align} a &= 1 \\ b &= 2 \end{align}`,
  String.raw`\begin{gather} a = 1 \\ b = 2 \end{gather}`,
  String.raw`x^{y^{z}} + x_{y_{z}}`,
  String.raw`\left(\frac{a}{b}\right) \left[\frac{a}{b}\right] \left\{\frac{a}{b}\right\}`,
  String.raw`\left\langle x \middle| y \right\rangle`,
  String.raw`\bigl( \Bigl[ \biggl\{ \Biggl\langle`,
  String.raw`\sqrt{\frac{a}{b}} \sqrt[3]{\frac{a}{b}}`,
  String.raw`\overline{x} \underline{x} \widehat{xyz} \widetilde{xyz}`,
  String.raw`\overleftarrow{x} \underrightarrow{x}`,
  String.raw`\phantom{x} \hphantom{x} \vphantom{x}`,
  String.raw`\mathchoice{a}{b}{c}{d}`,
  String.raw`\displaystyle\sum_{i=1}^{n}i`,
  String.raw`\tag{1} x`,
];

const out = [];
const settingsList = [
  {throwOnError: false, strict: 'ignore'},
  {throwOnError: false, strict: 'ignore', displayMode: true},
];
for (const tex of exprs) {
  for (const settings of settingsList) {
    let html;
    try {
      html = katex.renderToString(tex, settings);
    } catch (e) {
      html = 'THROW:' + e.message;
    }
    out.push({name: 'm' + out.length, tex, settings, html});
  }
}
fs.writeFileSync('../corpus/macros.json', JSON.stringify(out, null, 1));
console.log('macro cases:', out.length);
