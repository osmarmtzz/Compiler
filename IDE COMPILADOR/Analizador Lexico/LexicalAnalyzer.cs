using System;
using System.Collections.Generic;
using System.Text;

namespace IDE_COMPILADOR.AnalizadorLexico
{
    public class LexicalAnalyzer
    {
        private readonly DFA _dfa = new();
        private readonly List<Token> _tokens = new();
        private readonly List<string> _errores = new();
        private readonly HashSet<string> _palabrasReservadas = new()
        {
            "if", "else", "end", "do", "while", "switch", "case",
            "int", "float", "main", "cin", "cout","then","true",
            "false","until",
        };

        public (List<Token> tokens, List<string> errores) Analizar(string entrada)
        {
            _tokens.Clear();
            _errores.Clear();
            int linea = 1, columna = 1;
            int pos = 0;

            while (pos < entrada.Length)
            {
                char c = entrada[pos];
                if (char.IsWhiteSpace(c))
                {
                    if (c == '\n')
                    {
                        linea++;
                        columna = 1;
                    }
                    else
                    {
                        columna++;
                    }
                    pos++;
                    continue;
                }

                var sb = new StringBuilder();
                int startCol = columna;
                var state = DFA.State.START;
                var lastAccept = DFA.State.START;
                int lastAcceptPos = -1;
                int iter = pos;

                while (iter < entrada.Length)
                {
                    char cc = entrada[iter];
                    var next = _dfa.GetNext(state, cc);
                    if (next == DFA.State.ERROR) break;

                    state = next;
                    sb.Append(cc);
                    if (_dfa.IsAccepting(state))
                    {
                        lastAccept = state;
                        lastAcceptPos = iter;
                    }
                    iter++;
                }

                // Detectar punto decimal sin dígito
                if (state == DFA.State.DECIMAL_POINT)
                {
                    string malFormado = sb.ToString();
                    _errores.Add(
                        $"Error léxico: número mal formado '{malFormado}' en línea {linea}, columna {startCol}"
                    );
                    pos += malFormado.Length;
                    columna += malFormado.Length;
                    continue;
                }

                if (lastAcceptPos >= pos)
                {
                    string lexema = sb.ToString(0, lastAcceptPos - pos + 1);
                    string tipo = ClasificarToken(lastAccept, lexema);

                    // IGNORAR comentarios
                    if (tipo == "ComentarioInline" || tipo == "ComentarioExtenso")
                    {
                        // avanzar posición y NO generar token
                        for (int k = pos; k <= lastAcceptPos; k++)
                        {
                            if (entrada[k] == '\n')
                            {
                                linea++;
                                columna = 1;
                            }
                            else
                            {
                                columna++;
                            }
                        }
                        pos = lastAcceptPos + 1;
                        continue;
                    }

                    _tokens.Add(new Token(tipo, lexema, linea, startCol));

                    for (int k = pos; k <= lastAcceptPos; k++)
                    {
                        if (entrada[k] == '\n')
                        {
                            linea++;
                            columna = 1;
                        }
                        else
                        {
                            columna++;
                        }
                    }
                    pos = lastAcceptPos + 1;
                }
                else
                {
                    _errores.Add($"Error léxico: símbolo '{entrada[pos]}' en línea {linea}, columna {columna}");
                    pos++;
                    columna++;
                }
            }

            return (_tokens, _errores);
        }

        private string ClasificarToken(DFA.State estado, string lexema)
        {
            return estado switch
            {
                DFA.State.IDENTIFIER =>
                    _palabrasReservadas.Contains(lexema) ? "PalabraReservada" : "Identificador",
                DFA.State.NUMBER => "Numero",
                DFA.State.FLOAT => "PuntoFlotante",
                DFA.State.PLUSPLUS => "OperadorAritmetico",
                DFA.State.MINUSMINUS => "OperadorAritmetico",
                DFA.State.PLUS => "OperadorAritmetico",
                DFA.State.MINUS => "OperadorAritmetico",
                DFA.State.MULTIPLY => "OperadorAritmetico",
                DFA.State.MODULUS => "OperadorAritmetico",
                DFA.State.POWER => "OperadorAritmetico",
                DFA.State.SLASH => "OperadorAritmetico",
                DFA.State.RELATIONAL
                or DFA.State.LOGICAL
                or DFA.State.ASSIGN => "OperadorLogico",
                DFA.State.SYMBOL => "Simbolo",
                DFA.State.COMMENT_LINE => "ComentarioInline",
                DFA.State.COMMENT_BLOCK_END => "ComentarioExtenso",
                _ => "Desconocido"
            };
        }
    }
}