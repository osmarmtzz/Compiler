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
        public object? Value { get; set; } = null;   // int, double, bool o null
        public int Loc { get; set; }                 // número de registro
        public int Offset { get; set; } = 0;         // dirección o desplazamiento
        public List<int> Lines { get; } = new List<int>();

        public string ValueAsString
        {
            get
            {
                if (Value == null) return "sin valor";
                if (Value is bool b) return b ? "true" : "false";
                if (Value is int i) return i.ToString();
                if (Value is double d) return d.ToString(CultureInfo.InvariantCulture);
                return Value?.ToString() ?? "sin valor";
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

        /// <summary>Declara una nueva variable en el ámbito actual.</summary>
        public bool TryDeclare(string name, DataType type, int line, out SymbolEntry entry, out string? error)
        {
            error = null;
            entry = null!;

            if (_stack.Count == 0)
                EnterScope("global");

            var current = _stack.Peek();

            if (current.Symbols.ContainsKey(name))
            {
                entry = current.Symbols[name];
                if (line > 0 && !entry.Lines.Contains(line))
                    entry.Lines.Add(line);
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

        /// <summary>Busca una variable declarada (del scope actual hacia atrás).</summary>
        public SymbolEntry? Lookup(string name)
        {
            foreach (var frame in _stack)
                if (frame.Symbols.TryGetValue(name, out var e))
                    return e;
            return null;
        }

        /// <summary>Marca el uso de una variable (existe o error).</summary>
        public bool TryUse(string name, int line, out string error)
        {
            error = string.Empty;
            var sym = Lookup(name);
            if (sym == null)
            {
                error = $"Error línea {line}: Variable '{name}' no declarada.";
                return false;
            }
            if (line > 0 && !sym.Lines.Contains(line))
                sym.Lines.Add(line);
            return true;
        }

        /// <summary>Asigna valor y tipo a una variable declarada, validando compatibilidad.</summary>
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
                if (isNarrowing)
                    error = $"Error línea {line}: Conversión no válida (float → int) en '{name}'.";
                else
                    error = $"Error línea {line}: Tipos incompatibles al asignar a '{name}'.";
                return false;
            }

            if (value == null)
            {
                sym.Value = null;
            }
            else if (sym.Type == DataType.Float)
            {
                sym.Value = value is int iv ? (double)iv : value;
            }
            else if (sym.Type == DataType.Int)
            {
                sym.Value = value is double dv ? (int)dv : value;
            }
            else
            {
                sym.Value = value;
            }

            if (line > 0 && !sym.Lines.Contains(line))
                sym.Lines.Add(line);

            return true;
        }

        /// <summary>
        /// Crea (si no existe) un símbolo con tipo desconocido para que aparezca en la tabla
        /// y se acumulen sus líneas de uso.
        /// </summary>
        public SymbolEntry EnsurePlaceholderUnknown(string name, int line)
        {
            var sym = Lookup(name);
            if (sym != null)
            {
                if (line > 0 && !sym.Lines.Contains(line))
                    sym.Lines.Add(line);
                return sym;
            }

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

            if (line > 0) sym.Lines.Add(line);

            current.Symbols[name] = sym;
            _allEntries.Add(sym);
            return sym;
        }

        /// <summary>Devuelve únicamente variables declaradas (Type != Unknown), ordenadas por Loc.</summary>
        public IEnumerable<SymbolEntry> AllEntries()
        {
            return _allEntries
                .Where(e => e.Type != DataType.Unknown)
                .OrderBy(e => e.Loc);
        }
    }
}
