using System;
using System.Collections.Generic;

namespace IDE_COMPILADOR.AnalizadorLexico
{
    public class DFA
    {
        public enum State
        {
            START,
            IDENTIFIER,
            NUMBER,
            DECIMAL_POINT,
            FLOAT,
            PLUS,
            PLUSPLUS,
            MINUS,
            MINUSMINUS,
            MULTIPLY,
            MODULUS,
            POWER,
            SLASH,
            COMMENT_LINE,
            COMMENT_BLOCK,
            COMMENT_BLOCK_END,
            RELATIONAL,
            ASSIGN,
            LOGICAL,
            SYMBOL,
            ERROR
        }

        private struct Transition
        {
            public State From;
            public Func<char, bool> Condition;
            public State To;
        }

        private readonly List<Transition> _transitions = new();
        private readonly HashSet<State> _acceptingStates = new();

        public DFA()
        {
            BuildTransitions();
            BuildAcceptStates();
        }

        private void BuildTransitions()
        {
            // 1) Espacios → vuelve a START
            AddTransition(State.START, ch => char.IsWhiteSpace(ch), State.START);

            // 2) Identificadores
            AddTransition(State.START, ch => char.IsLetter(ch), State.IDENTIFIER);
            AddTransition(State.IDENTIFIER, ch => char.IsLetterOrDigit(ch), State.IDENTIFIER);

            // 3) Números enteros y reales - CORREGIDO
            AddTransition(State.START, ch => char.IsDigit(ch), State.NUMBER);
            AddTransition(State.NUMBER, ch => char.IsDigit(ch), State.NUMBER);
            AddTransition(State.NUMBER, ch => ch == '.', State.DECIMAL_POINT);
            AddTransition(State.DECIMAL_POINT, ch => char.IsDigit(ch), State.FLOAT);
            AddTransition(State.FLOAT, ch => char.IsDigit(ch), State.FLOAT);

            // 4) Operadores ++ y +
            AddTransition(State.START, ch => ch == '+', State.PLUS);
            AddTransition(State.PLUS, ch => ch == '+', State.PLUSPLUS);

            // 5) Operadores -- y -
            AddTransition(State.START, ch => ch == '-', State.MINUS);
            AddTransition(State.MINUS, ch => ch == '-', State.MINUSMINUS);

            // 6) Otros aritméticos
            AddTransition(State.START, ch => ch == '*', State.MULTIPLY);
            AddTransition(State.START, ch => ch == '%', State.MODULUS);
            AddTransition(State.START, ch => ch == '^', State.POWER);

            // 7) Slash → división o comentario
            AddTransition(State.START, ch => ch == '/', State.SLASH);
            AddTransition(State.SLASH, ch => ch == '/', State.COMMENT_LINE);
            AddTransition(State.SLASH, ch => ch == '*', State.COMMENT_BLOCK);

            // 8) Relacionales y asignación
            AddTransition(State.START, ch => ch == '=' || ch == '>' || ch == '<', State.RELATIONAL);
            AddTransition(State.RELATIONAL, ch => ch == '=', State.RELATIONAL);
            AddTransition(State.START, ch => ch == '!', State.ASSIGN);
            AddTransition(State.ASSIGN, ch => ch == '=', State.RELATIONAL);

            // 9) Lógicos complejos &&, ||
            AddTransition(State.START, ch => ch == '&', State.LOGICAL);
            AddTransition(State.LOGICAL, ch => ch == '&', State.LOGICAL);
            AddTransition(State.START, ch => ch == '|', State.LOGICAL);
            AddTransition(State.LOGICAL, ch => ch == '|', State.LOGICAL);

            // 10) Símbolos individuales
            AddTransition(State.START, ch => "(){};:,".Contains(ch), State.SYMBOL);

            // Comentario de línea: consume todo hasta '\n'
            AddTransition(State.COMMENT_LINE, ch => ch != '\n', State.COMMENT_LINE);

            // Comentario de bloque: /* ... */
            AddTransition(State.COMMENT_BLOCK, ch => ch != '*', State.COMMENT_BLOCK);
            AddTransition(State.COMMENT_BLOCK, ch => ch == '*', State.COMMENT_BLOCK_END);
            AddTransition(State.COMMENT_BLOCK_END, ch => ch == '/', State.COMMENT_BLOCK_END);
            AddFallback(State.COMMENT_BLOCK_END, State.COMMENT_BLOCK);
        }

        private void BuildAcceptStates()
        {
            _acceptingStates.UnionWith(new[]
            {
                State.IDENTIFIER,
                State.NUMBER,
                State.FLOAT,
                State.PLUS,
                State.PLUSPLUS,
                State.MINUS,
                State.MINUSMINUS,
                State.MULTIPLY,
                State.MODULUS,
                State.POWER,
                State.SLASH,
                State.RELATIONAL,
                State.ASSIGN,
                State.LOGICAL,
                State.SYMBOL,
                State.COMMENT_LINE,
                State.COMMENT_BLOCK_END
            });
        }

        private void AddTransition(State from, Func<char, bool> condition, State to)
            => _transitions.Add(new Transition { From = from, Condition = condition, To = to });

        private void AddFallback(State from, State to)
            => _transitions.Add(new Transition { From = from, Condition = _ => true, To = to });

        public State GetNext(State current, char input)
        {
            foreach (var t in _transitions)
                if (t.From == current && t.Condition(input))
                    return t.To;
            return State.ERROR;
        }

        public bool IsAccepting(State state) => _acceptingStates.Contains(state);
    }
}