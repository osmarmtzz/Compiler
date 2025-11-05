using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using IDE_COMPILADOR.AnalizadorLexico;
using IDE_COMPILADOR.AnalizadorSintactico.AST;

namespace IDE_COMPILADOR.AnalizadorSemantico
{
    public class SemanticInfo
    {
        public DataType Type { get; set; } = DataType.Unknown;
        public object? ConstValue { get; set; } = null;
        public bool IsConst => ConstValue != null;
    }

    public class SemanticAnalyzer
    {
        public readonly SymbolTable Symbols = new SymbolTable();
        public readonly List<string> Errors = new List<string>();
        public readonly Dictionary<ASTNode, SemanticInfo> Annotations = new Dictionary<ASTNode, SemanticInfo>();

        // Mapa opcional (no es la fuente de verdad principal)
        private Dictionary<string, List<int>> _identifierLines = new Dictionary<string, List<int>>();

        // Fuente de verdad #1: todos los tokens
        private List<Token> _allTokens = new List<Token>();

        // Fuente de verdad #2 (fallback): texto fuente
        private string _sourceText = string.Empty;

        /// <summary>
        /// Analiza el programa con info de líneas desde tokens y/o texto fuente.
        /// </summary>
        public void Analyze(ProgramNode program, List<Token> tokens = null, string sourceText = null)
        {
            Errors.Clear();
            Annotations.Clear();
            Symbols.Reset();
            _identifierLines.Clear();

            _allTokens = tokens ?? new List<Token>();
            _sourceText = sourceText ?? string.Empty;

            if (tokens != null)
                BuildIdentifierLineMap(tokens);

            Symbols.EnterScope("global");

            // 1) Declaraciones globales
            foreach (var decl in program.Declarations)
            {
                if (decl is VariableDeclarationNode vd)
                {
                    var t = TypeUtils.FromString(vd.TypeName);
                    foreach (var id in vd.Identifiers)
                    {
                        int line = GetFirstLine(id);
                        if (!Symbols.TryDeclare(id, t, line, out _, out string err) && err != null)
                            Errors.Add(err);
                    }
                    Annotate(vd, t, null);
                }
            }

            // 2) Cuerpos
            int blockIndex = 1;
            foreach (var decl in program.Declarations)
            {
                if (decl is StatementListNode sl)
                {
                    WithScope($"main#{blockIndex++}", () =>
                    {
                        foreach (var st in sl.Statements) AnalyzeStmt(st);
                        Annotate(sl, DataType.Unknown, null);
                    });
                }
            }

            Symbols.ExitScope();

            // Desduplicar conservando orden
            var dedup = Errors.Distinct().ToList();
            Errors.Clear();
            Errors.AddRange(dedup);
        }

        private void BuildIdentifierLineMap(List<Token> tokens)
        {
            foreach (var token in tokens)
            {
                var tipo = token.Tipo?.Trim();
                bool esIdent =
                    !string.IsNullOrEmpty(tipo) &&
                    (tipo.Equals("Identificador", StringComparison.OrdinalIgnoreCase)
                     || tipo.Equals("ID", StringComparison.OrdinalIgnoreCase)
                     || tipo.Equals("Ident", StringComparison.OrdinalIgnoreCase)
                     || tipo.IndexOf("ident", StringComparison.OrdinalIgnoreCase) >= 0);

                if (esIdent)
                {
                    string nombre = token.Valor;
                    if (!_identifierLines.ContainsKey(nombre))
                        _identifierLines[nombre] = new List<int>();

                    if (token.Linea > 0 && !_identifierLines[nombre].Contains(token.Linea))
                        _identifierLines[nombre].Add(token.Linea);
                }
            }
        }

        // ===== Líneas: tokens + fallback a texto =====

        private List<int> GetAllLines(string identifier)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(identifier))
                return result;

            // 1) Buscar en TODOS los tokens por Valor (case-insensitive)
            if (_allTokens != null && _allTokens.Count > 0)
            {
                result = _allTokens
                    .Where(t => t != null && !string.IsNullOrEmpty(t.Valor)
                             && string.Equals(t.Valor, identifier, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.Linea)
                    .Where(l => l > 0)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToList();
            }

            if (result.Count > 0)
                return result;

            // 2) Fallback: buscar en el texto fuente por palabra completa
            if (!string.IsNullOrEmpty(_sourceText))
            {
                var pattern = $@"\b{Regex.Escape(identifier)}\b";
                var rgx = new Regex(pattern, RegexOptions.IgnoreCase);

                var lines = _sourceText.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (rgx.IsMatch(lines[i]))
                        result.Add(i + 1); // 1-based
                }

                result = result.Distinct().OrderBy(l => l).ToList();
            }

            return result;
        }

        private int GetFirstLine(string identifier)
        {
            var lines = GetAllLines(identifier);
            return (lines.Count > 0) ? lines[0] : -1;
        }

        private int GetNextLine(string identifier)
        {
            var sym = Symbols.Lookup(identifier);
            var lines = GetAllLines(identifier);

            if (lines.Count == 0)
                return -1;

            if (sym == null || sym.Lines.Count == 0)
                return lines[0];

            foreach (var l in lines)
                if (!sym.Lines.Contains(l))
                    return l;

            return lines[0];
        }

        private void WithScope(string name, Action action)
        {
            Symbols.EnterScope(name);
            try { action(); }
            finally { Symbols.ExitScope(); }
        }

        private void AnalyzeStmt(StatementNode st)
        {
            if (st is AssignmentNode a)
            {
                var rhs = AnalyzeExpr(a.Expression);
                int line = GetNextLine(a.Identifier);

                // Asegurar placeholder para que salga en Hash Table
                if (Symbols.Lookup(a.Identifier) == null)
                    Symbols.EnsurePlaceholderUnknown(a.Identifier, line);

                if (rhs.Type == DataType.Unknown)
                {
                    Errors.Add(line > 0
                        ? $"Error línea {line}: Tipo desconocido en asignación a '{a.Identifier}'."
                        : $"Tipo desconocido en asignación a '{a.Identifier}'.");
                }
                else
                {
                    if (!Symbols.TryAssign(a.Identifier, rhs.ConstValue, rhs.Type, line, out string err) && err != null)
                        Errors.Add(err);
                }
                Annotate(a, rhs.Type, rhs.ConstValue);
                return;
            }

            if (st is IfNode iff)
            {
                var c = AnalyzeExpr(iff.Condition);
                if (c.Type != DataType.Bool)
                    Errors.Add($"La condición de 'if' debe ser bool (es {c.Type.ToSource()}).");

                WithScope($"if-then", () =>
                {
                    foreach (var s in iff.ThenBranch) AnalyzeStmt(s);
                });

                if (iff.ElseBranch != null)
                {
                    WithScope($"if-else", () =>
                    {
                        foreach (var s in iff.ElseBranch) AnalyzeStmt(s);
                    });
                }

                Annotate(iff, DataType.Unknown, null);
                return;
            }

            if (st is WhileNode w)
            {
                var c = AnalyzeExpr(w.Condition);
                if (c.Type != DataType.Bool)
                    Errors.Add($"La condición de 'while' debe ser bool (es {c.Type.ToSource()}).");

                WithScope($"while", () =>
                {
                    foreach (var s in w.Body) AnalyzeStmt(s);
                });

                Annotate(w, DataType.Unknown, null);
                return;
            }

            if (st is DoWhileNode dw)
            {
                WithScope($"do-body", () =>
                {
                    foreach (var s in dw.Body) AnalyzeStmt(s);
                });

                var cond = AnalyzeExpr(dw.Condition);
                if (cond.Type != DataType.Bool)
                    Errors.Add($"La condición de 'do-while' debe ser bool (es {cond.Type.ToSource()}).");

                Annotate(dw, DataType.Unknown, null);
                return;
            }

            if (st is DoUntilNode du)
            {
                WithScope($"do-body", () =>
                {
                    foreach (var s in du.BodyDo) AnalyzeStmt(s);
                });

                var cWhile = AnalyzeExpr(du.ConditionWhile);
                if (cWhile.Type != DataType.Bool)
                    Errors.Add($"La condición del 'while' interno de 'do..until' debe ser bool (es {cWhile.Type.ToSource()}).");

                WithScope($"do-whileBody", () =>
                {
                    foreach (var s in du.BodyWhile) AnalyzeStmt(s);
                });

                var cUntil = AnalyzeExpr(du.ConditionUntil);
                if (cUntil.Type != DataType.Bool)
                    Errors.Add($"La condición de 'until' debe ser bool (es {cUntil.Type.ToSource()}).");

                Annotate(du, DataType.Unknown, null);
                return;
            }

            if (st is UnaryPostfixNode up)
            {
                int line = GetNextLine(up.Identifier);
                var sym = Symbols.Lookup(up.Identifier);
                if (sym == null)
                {
                    Symbols.EnsurePlaceholderUnknown(up.Identifier, line);
                    Errors.Add(line > 0
                        ? $"Error línea {line}: Variable '{up.Identifier}' no declarada (uso en {up.Operator})."
                        : $"Variable '{up.Identifier}' no declarada (uso en {up.Operator}).");
                    Annotate(up, DataType.Unknown, null);
                    return;
                }
                if (!sym.Type.IsNumeric())
                {
                    Errors.Add(line > 0
                        ? $"Error línea {line}: Operador '{up.Operator}' solo aplica a tipos numéricos (variable '{up.Identifier}' es {sym.Type.ToSource()})."
                        : $"Operador '{up.Operator}' solo aplica a tipos numéricos (variable '{up.Identifier}' es {sym.Type.ToSource()}).");
                }

                if (sym.Value is int ivi)
                    sym.Value = up.Operator == "++" ? ivi + 1 : ivi - 1;
                else if (sym.Value is double dvd)
                    sym.Value = up.Operator == "++" ? dvd + 1.0 : dvd - 1.0;

                if (line > 0 && !sym.Lines.Contains(line))
                    sym.Lines.Add(line);
                Annotate(up, sym.Type, sym.Value);
                return;
            }

            if (st is InputNode inp)
            {
                int line = GetNextLine(inp.Identifier);
                if (!Symbols.TryUse(inp.Identifier, line, out string err))
                {
                    Symbols.EnsurePlaceholderUnknown(inp.Identifier, line);
                    Errors.Add(err);
                    Annotate(inp, DataType.Unknown, null);
                }
                else
                {
                    var sym = Symbols.Lookup(inp.Identifier);
                    if (sym != null) sym.Value = null; // lectura → valor desconocido
                    Annotate(inp, sym?.Type ?? DataType.Unknown, null);
                }
                return;
            }

            if (st is OutputNode outp)
            {
                if (outp.Value is ExpressionNode ex)
                {
                    var info = AnalyzeExpr(ex);
                    Annotate(outp, info.Type, info.ConstValue);
                }
                else
                {
                    Annotate(outp, DataType.Unknown, null);
                }
                return;
            }

            Annotate(st, DataType.Unknown, null);
        }

        private SemanticInfo AnalyzeExpr(ExpressionNode e)
        {
            if (e is LiteralNode lit)
            {
                if (lit.Value == "true" || lit.Value == "false")
                    return Annotate(e, DataType.Bool, lit.Value == "true");

                if (int.TryParse(lit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                    return Annotate(e, DataType.Int, iv);

                if (double.TryParse(lit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                    return Annotate(e, DataType.Float, dv);

                return Annotate(e, DataType.Unknown, null);
            }

            if (e is IdentifierNode idn)
            {
                int line = GetNextLine(idn.Name);
                var sym = Symbols.Lookup(idn.Name);
                if (sym == null)
                {
                    Symbols.EnsurePlaceholderUnknown(idn.Name, line);
                    Errors.Add(line > 0
                        ? $"Error línea {line}: Variable '{idn.Name}' no declarada."
                        : $"Variable '{idn.Name}' no declarada.");
                    return Annotate(e, DataType.Unknown, null);
                }
                if (line > 0 && !sym.Lines.Contains(line))
                    sym.Lines.Add(line);
                return Annotate(e, sym.Type, sym.Value);
            }

            if (e is BinaryOpNode bin)
            {
                if (bin.Operator == "!")
                {
                    var operand = bin.Right ?? bin.Left;
                    var R = AnalyzeExpr(operand);
                    if (R.Type != DataType.Bool)
                        Errors.Add($"'!' requiere bool (recibe {R.Type.ToSource()}).");
                    bool? v = null;
                    if (R.IsConst && R.ConstValue is bool rb) v = !rb;
                    return Annotate(bin, DataType.Bool, v);
                }

                var L = AnalyzeExpr(bin.Left);
                var R2 = AnalyzeExpr(bin.Right);
                var op = bin.Operator;

                if (op == "&&" || op == "||")
                {
                    if (L.Type != DataType.Bool || R2.Type != DataType.Bool)
                        Errors.Add($"Operadores lógicos '{op}' requieren bool.");
                    bool? v = null;
                    if (L.IsConst && R2.IsConst && L.ConstValue is bool lb && R2.ConstValue is bool rb)
                        v = op == "&&" ? (lb && rb) : (lb || rb);
                    return Annotate(bin, DataType.Bool, v);
                }

                if (op == "<" || op == "<=" || op == ">" || op == ">=" || op == "==" || op == "!=")
                {
                    if (op == "==" || op == "!=")
                    {
                        object? v = null;
                        if (L.IsConst && R2.IsConst)
                            v = CompareEquality(L.ConstValue, R2.ConstValue, op == "==");
                        return Annotate(bin, DataType.Bool, v);
                    }
                    else
                    {
                        if (!L.Type.IsNumeric() || !R2.Type.IsNumeric())
                            Errors.Add($"Operador relacional '{op}' requiere operandos numéricos.");

                        bool? v = null;
                        if (L.IsConst && R2.IsConst)
                        {
                            var pair = ToDoubles(L.ConstValue, R2.ConstValue);
                            double ld = pair.ld; double rd = pair.rd;
                            switch (op)
                            {
                                case "<": v = ld < rd; break;
                                case "<=": v = ld <= rd; break;
                                case ">": v = ld > rd; break;
                                case ">=": v = ld >= rd; break;
                            }
                        }
                        return Annotate(bin, DataType.Bool, v);
                    }
                }

                if (op == "+" || op == "-" || op == "*" || op == "/" || op == "%" || op == "^" || op == "++" || op == "--")
                {
                    if (!L.Type.IsNumeric() || !R2.Type.IsNumeric())
                        Errors.Add($"Operador '{op}' requiere operandos numéricos.");

                    var (T, v) = EvalNumericConstant(L, R2, op);
                    return Annotate(bin, T, v);
                }

                if (op == "<<")
                    return Annotate(bin, DataType.Unknown, null);

                return Annotate(bin, DataType.Unknown, null);
            }

            return Annotate(e, DataType.Unknown, null);
        }

        private (DataType T, object? v) EvalNumericConstant(SemanticInfo L, SemanticInfo R, string op)
        {
            var T = TypeUtils.PromoteNumeric(L.Type, R.Type);
            object? v = null;

            if (L.IsConst && R.IsConst)
            {
                if (T == DataType.Int && L.ConstValue is int li && R.ConstValue is int ri)
                {
                    try
                    {
                        int res = op switch
                        {
                            "+" => li + ri,
                            "-" => li - ri,
                            "*" => li * ri,
                            "/" => (ri == 0) ? 0 : li / ri,
                            "%" => (ri == 0) ? 0 : li % ri,
                            "^" => (int)Math.Pow(li, ri),
                            "++" => li + ri,
                            "--" => li - ri,
                            _ => 0
                        };
                        v = res;
                    }
                    catch { v = null; }
                }
                else
                {
                    try
                    {
                        double ld = Convert.ToDouble(L.ConstValue, CultureInfo.InvariantCulture);
                        double rd = Convert.ToDouble(R.ConstValue, CultureInfo.InvariantCulture);
                        double res = op switch
                        {
                            "+" => ld + rd,
                            "-" => ld - rd,
                            "*" => ld * rd,
                            "/" => (rd == 0) ? double.NaN : ld / rd,
                            "%" => (rd == 0) ? double.NaN : ld % rd,
                            "^" => Math.Pow(ld, rd),
                            "++" => ld + rd,
                            "--" => ld - rd,
                            _ => double.NaN
                        };
                        if (!double.IsNaN(res) && !double.IsInfinity(res))
                            v = (T == DataType.Int) ? (object)(int)res : (object)res;
                    }
                    catch { v = null; }
                }
            }

            return (T, v);
        }

        private static (double ld, double rd) ToDoubles(object? l, object? r)
        {
            double ld = (l is int) ? (int)l : (l is double) ? (double)l : double.NaN;
            double rd = (r is int) ? (int)r : (r is double) ? (double)r : double.NaN;
            return (ld, rd);
        }

        private static bool? CompareEquality(object? a, object? b, bool equal)
        {
            if (a is bool ab && b is bool bb) return equal ? ab == bb : ab != bb;
            if (a is int ai && b is int bi) return equal ? ai == bi : ai != bi;
            if (a is double ad && b is double bd) return equal ? ad == bd : ad != bd;
            if (a is int ai2 && b is double bd2) return equal ? ai2 == bd2 : ai2 != bd2;
            if (a is double ad2 && b is int bi2) return equal ? ad2 == bi2 : ad2 != bi2;
            return null;
        }

        private SemanticInfo Annotate(ASTNode node, DataType t, object? value)
        {
            var info = new SemanticInfo { Type = t, ConstValue = value };
            Annotations[node] = info;
            return info;
        }

        public TreeNode BuildAnnotatedTree(ASTNode node)
        {
            Func<SemanticInfo?, string> S = si =>
                si == null ? "[type=?, val=?]" :
                "[type=" + si.Type.ToSource() + ", val=" + (si.ConstValue == null ? "∅" : si.ConstValue.ToString()) + "]";

            Func<string, ASTNode, TreeNode> Make = (label, n) =>
            {
                Annotations.TryGetValue(n, out var si);
                var top = label + " " + S(si);
                return new TreeNode(top);
            };

            if (node is ProgramNode p)
            {
                var root = Make("Program", node);
                foreach (var d in p.Declarations)
                    root.Nodes.Add(BuildAnnotatedTree(d));
                return root;
            }
            if (node is VariableDeclarationNode vd)
            {
                var vdNode = Make("VarDecl: " + vd.TypeName, node);
                foreach (var id in vd.Identifiers)
                    vdNode.Nodes.Add(new TreeNode(id));
                return vdNode;
            }
            if (node is StatementListNode sl)
            {
                var slNode = Make("StatementList", node);
                foreach (var st in sl.Statements)
                    slNode.Nodes.Add(BuildAnnotatedTree(st));
                return slNode;
            }
            if (node is AssignmentNode a)
            {
                var asg = Make("Assign: " + a.Identifier, node);
                asg.Nodes.Add(BuildAnnotatedTree(a.Expression));
                return asg;
            }
            if (node is IfNode iff)
            {
                var ifn = Make("If", node);
                ifn.Nodes.Add(BuildAnnotatedTree(iff.Condition));
                var thenN = new TreeNode("Then");
                foreach (var s in iff.ThenBranch) thenN.Nodes.Add(BuildAnnotatedTree(s));
                ifn.Nodes.Add(thenN);
                if (iff.ElseBranch != null)
                {
                    var elseN = new TreeNode("Else");
                    foreach (var s in iff.ElseBranch) elseN.Nodes.Add(BuildAnnotatedTree(s));
                    ifn.Nodes.Add(elseN);
                }
                return ifn;
            }
            if (node is WhileNode w)
            {
                var wn = Make("While", node);
                wn.Nodes.Add(BuildAnnotatedTree(w.Condition));
                var bodyW = new TreeNode("Body");
                foreach (var s in w.Body) bodyW.Nodes.Add(BuildAnnotatedTree(s));
                wn.Nodes.Add(bodyW);
                return wn;
            }
            if (node is DoWhileNode dw)
            {
                var dwn = Make("DoWhile", node);
                var bodyD = new TreeNode("Body");
                foreach (var s in dw.Body) bodyD.Nodes.Add(BuildAnnotatedTree(s));
                dwn.Nodes.Add(bodyD);
                var condD = new TreeNode("Condition");
                condD.Nodes.Add(BuildAnnotatedTree(dw.Condition));
                dwn.Nodes.Add(condD);
                return dwn;
            }
            if (node is DoUntilNode du)
            {
                var duNode = Make("DoUntil", node);
                var doBody = new TreeNode("DoBody");
                foreach (var st in du.BodyDo) doBody.Nodes.Add(BuildAnnotatedTree(st));
                duNode.Nodes.Add(doBody);
                var whileCond = new TreeNode("WhileCondition");
                whileCond.Nodes.Add(BuildAnnotatedTree(du.ConditionWhile));
                duNode.Nodes.Add(whileCond);
                var whileBody = new TreeNode("WhileBody");
                foreach (var st2 in du.BodyWhile) whileBody.Nodes.Add(BuildAnnotatedTree(st2));
                duNode.Nodes.Add(whileBody);
                var untilCond = new TreeNode("UntilCondition");
                untilCond.Nodes.Add(BuildAnnotatedTree(du.ConditionUntil));
                duNode.Nodes.Add(untilCond);
                return duNode;
            }
            if (node is UnaryPostfixNode up)
                return Make("Postfix: " + up.Identifier + " " + up.Operator, node);
            if (node is InputNode inp)
                return Make("Input: " + inp.Identifier, node);
            if (node is OutputNode)
                return Make("Output", node);
            if (node is BinaryOpNode b)
            {
                var bn = Make("Op " + b.Operator, node);
                bn.Nodes.Add(BuildAnnotatedTree(b.Left));
                bn.Nodes.Add(BuildAnnotatedTree(b.Right));
                return bn;
            }
            if (node is LiteralNode lit)
                return Make("Literal: " + lit.Value, node);
            if (node is IdentifierNode idn)
                return Make("Id: " + idn.Name, node);

            return new TreeNode(node.GetType().Name);
        }
    }
}
