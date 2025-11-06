using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IDE_COMPILADOR.AnalizadorSemantico
{
    public class SymbolEntry
    {
        public string Name { get; set; } = string.Empty;
        public DataType Type { get; set; } = DataType.Unknown;
        public object? Value { get; set; } = null;
        public int Loc { get; set; }
        public int Offset { get; set; } = 0;
        public List<int> Lines { get; } = new List<int>();

        // Aún tenemos una representación por defecto,
        // pero la UI usará su propio formateador.
        public string ValueAsString
        {
            get
            {
                if (Value == null) return "sin valor";

                if (Type == DataType.Float)
                {
                    if (Value is double dd) return dd.ToString("0.00", CultureInfo.InvariantCulture);
                    if (Value is float ff) return ((double)ff).ToString("0.00", CultureInfo.InvariantCulture);
                    if (Value is int ii) return ((double)ii).ToString("0.00", CultureInfo.InvariantCulture);
                    try { return Convert.ToDouble(Value, CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture); }
                    catch { return Value.ToString() ?? "sin valor"; }
                }

                if (Type == DataType.Int)
                {
                    if (Value is int i) return i.ToString(CultureInfo.InvariantCulture);
                    if (Value is double d) return ((int)d).ToString(CultureInfo.InvariantCulture);
                    if (Value is float f) return ((int)f).ToString(CultureInfo.InvariantCulture);
                    try { return Convert.ToInt32(Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture); }
                    catch { return Value.ToString() ?? "sin valor"; }
                }

                if (Type == DataType.Bool)
                {
                    if (Value is bool b) return b.ToString().ToLower();
                    try { return Convert.ToBoolean(Value, CultureInfo.InvariantCulture).ToString().ToLower(); }
                    catch { return Value.ToString() ?? "sin valor"; }
                }

                if (Value is double d2) return d2.ToString("0.00", CultureInfo.InvariantCulture);
                return Value.ToString() ?? "sin valor";
            }
        }
    }

    internal class ScopeFrame
    {
        public string Name { get; }
        public int Level { get; }
        public int NextOffset { get; set; } = 0;
        public Dictionary<string, SymbolEntry> Symbols { get; } = new Dictionary<string, SymbolEntry>();

        public ScopeFrame(string name, int level)
        {
            Name = name;
            Level = level;
        }
    }

    public class SymbolTable
    {
        private readonly Stack<ScopeFrame> _stack = new Stack<ScopeFrame>();
        private readonly List<SymbolEntry> _allEntries = new List<SymbolEntry>();
        private int _locCounter = 0;

        public void Reset()
        {
            _stack.Clear();
            _allEntries.Clear();
            _locCounter = 0;
        }

        public void EnterScope(string name)
        {
            var level = _stack.Count;
            _stack.Push(new ScopeFrame(name, level));
        }

        public void ExitScope()
        {
            if (_stack.Count > 0) _stack.Pop();
        }

        public bool TryDeclare(string name, DataType type, int line, out SymbolEntry entry, out string? error)
        {
            error = null!;
            entry = null!;

            if (_stack.Count == 0)
                EnterScope("global");

            var current = _stack.Peek();

            if (current.Symbols.ContainsKey(name))
            {
                entry = current.Symbols[name];
                error = $"Error línea {line}: Variable '{name}' redeclarada.";
                return false;
            }

            entry = new SymbolEntry
            {
                Name = name,
                Type = type,
                Loc = _locCounter++,
                Offset = current.NextOffset
            };

            current.NextOffset += type.SizeOf();

            if (line > 0)
                entry.Lines.Add(line);

            current.Symbols[name] = entry;
            _allEntries.Add(entry);
            return true;
        }

        public SymbolEntry? Lookup(string name)
        {
            foreach (var frame in _stack)
                if (frame.Symbols.TryGetValue(name, out var e))
                    return e;
            return null;
        }

        public bool TryUse(string name, int line, out string error)
        {
            error = string.Empty;
            var sym = Lookup(name);
            if (sym == null)
            {
                error = $"Error línea {line}: Variable '{name}' no declarada.";
                return false;
            }
            return true;
        }

        public bool TryAssign(string name, object? value, DataType valueType, int line, out string? error)
        {
            error = null;
            var sym = Lookup(name);
            if (sym == null)
            {
                error = $"Error línea {line}: Variable '{name}' no declarada.";
                return false;
            }

            if (!TypeUtils.CanAssign(sym.Type, valueType, out var isNarrowing))
            {
                error = isNarrowing
                    ? $"Error línea {line}: Conversión no válida (float → int) en '{name}'."
                    : $"Error línea {line}: Tipos incompatibles al asignar a '{name}'.";
                return false;
            }

            // Normaliza el valor al tipo declarado del símbolo
            if (value == null)
            {
                sym.Value = null;
                return true;
            }

            try
            {
                switch (sym.Type)
                {
                    case DataType.Float:
                        if (value is double d) sym.Value = d;
                        else if (value is int i) sym.Value = (double)i;
                        else if (value is float f) sym.Value = (double)f;
                        else sym.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        break;

                    case DataType.Int:
                        if (value is int ii) sym.Value = ii;
                        else if (value is double dd) sym.Value = (int)dd;
                        else if (value is float ff) sym.Value = (int)ff;
                        else sym.Value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        break;

                    case DataType.Bool:
                        if (value is bool bb) sym.Value = bb;
                        else sym.Value = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        break;

                    default:
                        sym.Value = value;
                        break;
                }
            }
            catch
            {
                sym.Value = value; // si algo falla, deja el crudo
            }

            return true;
        }

        public SymbolEntry EnsurePlaceholderUnknown(string name, int line)
        {
            var sym = Lookup(name);
            if (sym != null)
                return sym;

            if (_stack.Count == 0)
                EnterScope("global");

            var current = _stack.Peek();
            sym = new SymbolEntry
            {
                Name = name,
                Type = DataType.Unknown,
                Loc = _locCounter++,
                Offset = current.NextOffset
            };

            current.Symbols[name] = sym;
            _allEntries.Add(sym);
            return sym;
        }

        public IEnumerable<SymbolEntry> AllEntries()
        {
            return _allEntries
                .Where(e => e.Type != DataType.Unknown)
                .OrderBy(e => e.Loc);
        }
    }
}
