# KStyleMath porting guide

This repository is a C# port of KaTeX 0.19.1 (source in `.katex-ref/src`).
The public goal is a faithful `string -> string` mapping: for a LaTeX
expression, `KStyleMath.Katex.RenderToString` must produce exactly the same
markup as `katex.renderToString` with the same settings.

## Rules

- Translate the TypeScript source **line by line**. Keep control flow, order of
  operations, class/method names, CSS class names, attribute order, and string
  contents identical. Do not "improve" algorithms.
- Do not load a JavaScript engine. Everything must be pure C#. Browser APIs are
  replaced by the virtual DOM classes in `Dom/DomTree.cs` and
  `MathML/MathMLTree.cs`; markup is produced by `ToMarkup()`.
- All numbers are `double`. Use `JsNumber.ToString(double)` when interpolating a
  number into a string (it implements ECMAScript Number::toString).
- Use `Units.MakeEm(double)` instead of JS `makeEm`.
- Strings are UTF-16 like JS.
- `null` and missing properties are the same thing for parse-node properties.

## Core types (already implemented)

`ParseNode` is a dynamic object (like the JS parse nodes):

```csharp
var node = new ParseNode("ordgroup", mode);
node["body"] = new List<ParseNode>();     // property write
node.Str("text"); node.Num("size"); node.Bool("infix");
node.Node("body"); node.List("body"); node.ListList("body");
node["body"];                             // object
```

`Token` (`Text`, `Loc`), `SourceLocation`, `ParseError`, `Namespace<T>`,
`Style`, `Options`, `Settings`, `Measurement`, `Units`, `Utils`, `StrictHelper`.

Virtual DOM (namespace `KStyleMath`):

- `HtmlDomNode` (abstract): `Classes`, `Height`, `Depth`, `MaxFontSize`, `Style`, `HasClass`.
- `Span : HtmlDomNode`: `Children` (`List<VirtualNode>`), `Width`, `Italic`, `Attributes`, `SetAttribute`.
- `Anchor : HtmlDomNode`: same.
- `SymbolNode : HtmlDomNode`: `Text`, `Italic`, `Skew`, `Width`.
- `SvgNode`, `PathNode`, `LineNode`, `Img`, `DocumentFragment` (HTML+MathML).
- `CssStyle`: `style.Color = ...`, `style["marginRight"] = ...`, `style.MarginRight`, `style.ToCssString()`.
- `MathNode : VirtualNode, IMathDomNode` (`Type`, `Attributes`, `Children`, `Classes`, `SetAttribute`, `GetAttribute`, `ToText`), `MathTextNode`, `SpaceNode`.
- `DocumentFragment` holds `List<VirtualNode>` children.

Important: `Span.Children` and `SvgNode.Children` are `List<VirtualNode>`.
Cast with `(HtmlDomNode)child` / `(SymbolNode)child` as needed.

## Build helpers (`Build/BuildCommon.cs`)

`BuildCommon.MakeSymbol`, `Mathsym`, `MakeOrd`, `TryCombineChars`, `MakeSpan`,
`MakeSvgSpan`, `MakeLineSpan`, `MakeAnchor`, `MakeFragment`, `WrapFragment`,
`MakeVList`, `MakeGlue`, `FontMap`, `StaticSvg`, `RetrieveTextFontName`.
`VListParam`, `VListElem`, `VListKern` model `makeVList` parameters.

## Registration

Original `defineFunction({type, names, numArgs, argTypes, ...})` becomes:

```csharp
Registries.DefineFunction(new FunctionSpec {
    Type = "accent", Names = new List<string>{"\\acute"},
    NumArgs = 1, ArgTypes = new List<string>{"math"},
    Handler = (context, args, optArgs) => { ... return node; },
    HtmlBuilder = (group, options) => { ... return htmlNode; },
    MathmlBuilder = (group, options) => { ... return mathNode; },
});
```

`Registries.DefineFunctionBuilders(type, htmlBuilder, mathmlBuilder)` for nodes
built directly by the parser. Environments:

```csharp
Registries.DefineEnvironment(new EnvSpec {
    Type = "array", Names = ..., NumArgs = 1, ArgTypes = ...,
    Handler = ..., HtmlBuilder = ..., MathmlBuilder = ...
});
```

Builder delegates return `VirtualNode` (`Func<ParseNode, Options, VirtualNode>`).
Every module exposes a static method `Define()` (or a static constructor) that
registers its functions; the central `FunctionRegistry.DefineAll()` calls them.
If you port a whole group of files, add a `Define()` method to the group class
instead of relying on static constructors (predictable ordering).

## Other conventions

- `Parser` methods and fields are PascalCase versions of the TS names
  (`parseExpression` -> `ParseExpression`, `gullet` -> `Gullet`, ...).
- `MacroExpander` implements the same surface as `MacroContextInterface`
  (`Future`, `PopToken`, `ConsumeSpaces`, `ExpandOnce`, `ExpandAfterFuture`,
  `ExpandNextToken`, `ExpandMacro`, `ExpandMacroAsText`, `ExpandTokens`,
  `ConsumeArg`, `ConsumeArgs`, `IsDefined`, `IsExpandable`).
- `MacroDefinition` values are `object`: `string`, `MacroExpansion`, or
  `MacroFunction` (`object MacroFunction(MacroContextInterface ctx)`).
- Keep `List<ParseNode>` for node arrays and `List<List<ParseNode>>` for 2D arrays.
- Use `System.Linq` freely, but keep the order of enumeration.
