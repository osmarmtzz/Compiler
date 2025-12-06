using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using IDE_COMPILADOR.AnalizadorSemantico;
using IDE_COMPILADOR.AnalizadorSintactico.AST;
using Microsoft.VisualBasic; // Para InputBox en las instrucciones de Input

namespace IDE_COMPILADOR.CodigoIntermedio
{
    public enum OpCode
    {
        Nop,
        PushConst,
        Load,
        Store,
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Pow,
        Less,
        LessEq,
        Greater,
        GreaterEq,
        Equal,
        NotEqual,
        And,
        Or,
        Not,
        Concat,
        Jump,
        JumpIfFalse,
        JumpIfTrue,
        Input,
        Output
    }

    public class Instruction
    {
        public OpCode Op { get; }
        public object? Operand { get; set; }

        public Instruction(OpCode op, object? operand = null)
        {
            Op = op;
            Operand = operand;
        }

        public override string ToString()
        {
            return Operand == null
                ? $"{Op}"
                : $"{Op} {Operand}";
        }
    }

    /// <summary>
    /// Genera código intermedio de pila a partir del AST y la tabla de símbolos.
    /// </summary>
    public class IntermediateCodeGenerator
    {
        private readonly List<Instruction> _code = new List<Instruction>();
        private readonly Dictionary<string, int> _varIndex;

        public IntermediateCodeGenerator(SymbolTable symbols)
        {
            // Mapeamos nombre -> Loc (índice de celda)
            _varIndex = symbols.AllEntries().ToDictionary(e => e.Name, e => e.Loc);
        }

        private int Emit(OpCode op, object? operand = null)
        {
            _code.Add(new Instruction(op, operand));
            return _code.Count - 1;
        }

        private int VarIndex(string name)
        {
            if (_varIndex.TryGetValue(name, out var idx))
                return idx;

            // Si por alguna razón no está (no debería pasar si semántica está limpia),
            // lo agregamos al final.
            idx = _varIndex.Count;
            _varIndex[name] = idx;
            return idx;
        }

        public IList<Instruction> Generate(ProgramNode program)
        {
            // Solo generamos código para los bloques de sentencias dentro de main
            foreach (var decl in program.Declarations)
            {
                if (decl is StatementListNode sl)
                {
                    foreach (var st in sl.Statements)
                        GenStmt(st);
                }
            }

            return _code;
        }

        private void GenStmt(StatementNode st)
        {
            switch (st)
            {
                case AssignmentNode a:
                    GenExpr(a.Expression);
                    Emit(OpCode.Store, VarIndex(a.Identifier));
                    break;

                case UnaryPostfixNode up:
                    {
                        int idx = VarIndex(up.Identifier);
                        Emit(OpCode.Load, idx);
                        Emit(OpCode.PushConst, 1);
                        if (up.Operator == "++")
                            Emit(OpCode.Add);
                        else
                            Emit(OpCode.Sub);
                        Emit(OpCode.Store, idx);
                        break;
                    }

                case InputNode inp:
                    Emit(OpCode.Input, VarIndex(inp.Identifier));
                    break;

                case OutputNode outp:
                    if (outp.Value is ExpressionNode ex)
                    {
                        GenExpr(ex);
                        Emit(OpCode.Output);
                    }
                    break;

                case IfNode iff:
                    {
                        // if cond then ... [else ...] end
                        GenExpr(iff.Condition);
                        int jFalse = Emit(OpCode.JumpIfFalse, null); // destino luego

                        foreach (var s in iff.ThenBranch)
                            GenStmt(s);

                        int jEnd = Emit(OpCode.Jump, null);

                        int elseStart = _code.Count;
                        _code[jFalse].Operand = elseStart;

                        if (iff.ElseBranch != null)
                        {
                            foreach (var s in iff.ElseBranch)
                                GenStmt(s);
                        }

                        int end = _code.Count;
                        _code[jEnd].Operand = end;
                        break;
                    }

                case WhileNode w:
                    {
                        // while cond body end
                        int start = _code.Count;
                        GenExpr(w.Condition);
                        int jFalse = Emit(OpCode.JumpIfFalse, null);

                        foreach (var s in w.Body)
                            GenStmt(s);

                        Emit(OpCode.Jump, start);
                        int end = _code.Count;
                        _code[jFalse].Operand = end;
                        break;
                    }

                case DoWhileNode dw:
                    {
                        // do { body } while (cond)
                        int bodyStart = _code.Count;
                        foreach (var s in dw.Body)
                            GenStmt(s);

                        GenExpr(dw.Condition);
                        Emit(OpCode.JumpIfTrue, bodyStart);
                        break;
                    }

                case DoUntilNode du:
                    {
                        // Interpretación:
                        // do {
                        //   BodyDo;
                        //   while(condWhile) { BodyWhile; }
                        // } until (condUntil);

                        int doStart = _code.Count;

                        // BodyDo
                        foreach (var s in du.BodyDo)
                            GenStmt(s);

                        // while interno
                        int whileStart = _code.Count;
                        GenExpr(du.ConditionWhile);
                        int jWhileEnd = Emit(OpCode.JumpIfFalse, null);

                        foreach (var s in du.BodyWhile)
                            GenStmt(s);

                        Emit(OpCode.Jump, whileStart);
                        int whileEnd = _code.Count;
                        _code[jWhileEnd].Operand = whileEnd;

                        // until condUntil ? repetir si condUntil == false
                        GenExpr(du.ConditionUntil);
                        Emit(OpCode.JumpIfFalse, doStart);

                        break;
                    }

                default:
                    // otros tipos de sentencia se pueden ignorar o extender aquí
                    break;
            }
        }

        private void GenExpr(ExpressionNode expr)
        {
            switch (expr)
            {
                case LiteralNode lit:
                    {
                        object? val;
                        if (lit.Value == "true") val = true;
                        else if (lit.Value == "false") val = false;
                        else if (int.TryParse(lit.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                            val = iv;
                        else if (double.TryParse(lit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                            val = dv;
                        else
                            val = lit.Value; // cadena u otra cosa

                        Emit(OpCode.PushConst, val);
                        break;
                    }

                case IdentifierNode id:
                    Emit(OpCode.Load, VarIndex(id.Name));
                    break;

                case BinaryOpNode bin:
                    {
                        string op = bin.Operator;

                        // '!' unario está modelado como BinaryOpNode especial
                        if (op == "!")
                        {
                            if (bin.Right != null)
                                GenExpr(bin.Right);
                            else
                                GenExpr(bin.Left);

                            Emit(OpCode.Not);
                            break;
                        }

                        // Generar izquierda y derecha en pila
                        GenExpr(bin.Left);
                        GenExpr(bin.Right);

                        switch (op)
                        {
                            case "+": Emit(OpCode.Add); break;
                            case "-": Emit(OpCode.Sub); break;
                            case "*": Emit(OpCode.Mul); break;
                            case "/": Emit(OpCode.Div); break;
                            case "%": Emit(OpCode.Mod); break;
                            case "^": Emit(OpCode.Pow); break;

                            case "<": Emit(OpCode.Less); break;
                            case "<=": Emit(OpCode.LessEq); break;
                            case ">": Emit(OpCode.Greater); break;
                            case ">=": Emit(OpCode.GreaterEq); break;
                            case "==": Emit(OpCode.Equal); break;
                            case "!=": Emit(OpCode.NotEqual); break;

                            case "&&": Emit(OpCode.And); break;
                            case "||": Emit(OpCode.Or); break;

                            case "<<": // cout << a << "b"  -> concatenamos
                                Emit(OpCode.Concat);
                                break;

                            case "++":
                                Emit(OpCode.Add);
                                break;
                            case "--":
                                Emit(OpCode.Sub);
                                break;

                            default:
                                // operador desconocido, dejamos la pila como está
                                break;
                        }
                        break;
                    }

                default:
                    // otros tipos de expresión (si hubiera) se pueden ampliar aquí
                    break;
            }
        }
    }

    /// <summary>
    /// Intérprete de la máquina de pila para las instrucciones generadas.
    /// </summary>
    public class StackMachine
    {
        private readonly IList<Instruction> _code;
        private readonly object?[] _memory;
        private readonly Dictionary<int, SymbolEntry> _symbolsByLoc;

        public string OutputLog { get; private set; } = string.Empty;

        public StackMachine(IList<Instruction> code, IEnumerable<SymbolEntry> symbols)
        {
            _code = code;
            var list = symbols.ToList();
            int maxLoc = list.Count == 0 ? -1 : list.Max(s => s.Loc);
            _memory = new object?[Math.Max(maxLoc + 1, 0)];
            _symbolsByLoc = list.ToDictionary(s => s.Loc);

            // valores iniciales desde el analizador semántico
            foreach (var s in list)
                _memory[s.Loc] = s.Value;
        }

        public void Run()
        {
            var stack = new Stack<object?>();
            int ip = 0;

            while (ip < _code.Count)
            {
                var inst = _code[ip];

                switch (inst.Op)
                {
                    case OpCode.Nop:
                        break;

                    case OpCode.PushConst:
                        stack.Push(inst.Operand);
                        break;

                    case OpCode.Load:
                        {
                            int idx = (int)inst.Operand!;
                            stack.Push(_memory[idx]);
                            break;
                        }

                    case OpCode.Store:
                        {
                            int idx = (int)inst.Operand!;
                            var val = stack.Pop();
                            _memory[idx] = val;

                            if (_symbolsByLoc.TryGetValue(idx, out var se))
                                se.Value = val;

                            break;
                        }


                    case OpCode.Add:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a + b);
                            break;
                        }
                    case OpCode.Sub:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a - b);
                            break;
                        }
                    case OpCode.Mul:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a * b);
                            break;
                        }
                    case OpCode.Div:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(b == 0 ? 0.0 : a / b);
                            break;
                        }
                    case OpCode.Mod:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(b == 0 ? 0.0 : a % b);
                            break;
                        }
                    case OpCode.Pow:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(Math.Pow(a, b));
                            break;
                        }

                    case OpCode.Less:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a < b);
                            break;
                        }
                    case OpCode.LessEq:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a <= b);
                            break;
                        }
                    case OpCode.Greater:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a > b);
                            break;
                        }
                    case OpCode.GreaterEq:
                        {
                            var b = ToDouble(stack.Pop());
                            var a = ToDouble(stack.Pop());
                            stack.Push(a >= b);
                            break;
                        }
                    case OpCode.Equal:
                        {
                            var b = stack.Pop();
                            var a = stack.Pop();
                            stack.Push(Equals(a, b));
                            break;
                        }
                    case OpCode.NotEqual:
                        {
                            var b = stack.Pop();
                            var a = stack.Pop();
                            stack.Push(!Equals(a, b));
                            break;
                        }

                    case OpCode.And:
                        {
                            var b = ToBool(stack.Pop());
                            var a = ToBool(stack.Pop());
                            stack.Push(a && b);
                            break;
                        }
                    case OpCode.Or:
                        {
                            var b = ToBool(stack.Pop());
                            var a = ToBool(stack.Pop());
                            stack.Push(a || b);
                            break;
                        }
                    case OpCode.Not:
                        {
                            var a = ToBool(stack.Pop());
                            stack.Push(!a);
                            break;
                        }

                    case OpCode.Concat:
                        {
                            var b = ToStringVal(stack.Pop());
                            var a = ToStringVal(stack.Pop());
                            stack.Push(a + b);
                            break;
                        }

                    case OpCode.Jump:
                        ip = (int)inst.Operand!;
                        continue;

                    case OpCode.JumpIfFalse:
                        {
                            var cond = ToBool(stack.Pop());
                            if (!cond)
                            {
                                ip = (int)inst.Operand!;
                                continue;
                            }
                            break;
                        }

                    case OpCode.JumpIfTrue:
                        {
                            var cond = ToBool(stack.Pop());
                            if (cond)
                            {
                                ip = (int)inst.Operand!;
                                continue;
                            }
                            break;
                        }

                    case OpCode.Input:
                        {
                            int idx = (int)inst.Operand!;
                            string varName = _symbolsByLoc.TryGetValue(idx, out var se)
                                ? se.Name
                                : $"var{idx}";

                            string prompt = $"Ingrese valor para {varName}:";
                            string s = Interaction.InputBox(prompt, "Entrada", "0");

                            object? parsed = s;

                            if (_symbolsByLoc.TryGetValue(idx, out var se2))
                            {
                                switch (se2.Type)
                                {
                                    case DataType.Int:
                                        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                                            iv = 0;
                                        parsed = iv;
                                        break;

                                    case DataType.Float:
                                        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                                            dv = 0.0;
                                        parsed = dv;
                                        break;

                                    case DataType.Bool:
                                        if (!bool.TryParse(s, out var bv))
                                            bv = false;
                                        parsed = bv;
                                        break;

                                    default:
                                        parsed = s;
                                        break;
                                }
                            }

                            _memory[idx] = parsed;
                            break;
                        }

                    case OpCode.Output:
                        {
                            var val = stack.Pop();
                            string text = ToStringVal(val);
                            OutputLog += text + Environment.NewLine;
                            break;
                        }
                }

                ip++;
            }
        }

        private static double ToDouble(object? v)
        {
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is float f) return f;
            if (v is bool b) return b ? 1.0 : 0.0;
            if (v == null) return 0.0;

            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
                return r;

            return 0.0;
        }

        private static bool ToBool(object? v)
        {
            if (v is bool b) return b;
            if (v is int i) return i != 0;
            if (v is double d) return Math.Abs(d) > 1e-9;
            if (v == null) return false;

            if (bool.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var r))
                return r;

            return false;
        }

        private static string ToStringVal(object? v)
        {
            if (v == null) return "null";
            if (v is bool b) return b.ToString().ToLower();
            if (v is double d) return d.ToString("0.##", CultureInfo.InvariantCulture);
            if (v is float f) return ((double)f).ToString("0.##", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        }
    }
}
