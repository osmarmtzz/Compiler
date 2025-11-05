
using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic; // List<T>, LINQ
using System.Runtime.InteropServices;             // <- Win32 helpers
using IDE_COMPILADOR.AnalizadorLexico;
using IDE_COMPILADOR.AnalizadorSintactico;
using IDE_COMPILADOR.AnalizadorSintactico.AST;
using IDE_COMPILADOR.AnalizadorSemantico; // SymbolEntry, ToSource()

namespace IDE_COMPILADOR
{
    public partial class MainForm : Form
    {
        private string currentFilePath = string.Empty;
        private RichTextBox rtbLexico = null!;     // <- se crea en InitializeComponent()
        private ProgramNode _lastAst = null;

        public MainForm()
        {
            InitializeComponent();

            // Texto inicial en el editor:
            txtEditor.Text = "Escriba aquí...";

            // Eventos del editor
            txtEditor.SelectionChanged += TxtEditor_SelectionChanged;
            txtEditor.TextChanged += TxtEditor_TextChanged;
            txtEditor.VScroll += TxtEditor_VScroll;
            txtEditor.KeyDown += TxtEditor_KeyDown;
            txtEditor.Resize += TxtEditor_Resize;

            // Evento de panel de números de línea
            lineNumberPanel.Paint += LineNumberPanel_Paint;

            // Eventos de explorador de archivos
            btnAgregarArchivo.Click += BtnAgregarArchivo_Click;
            btnEliminarArchivo.Click += BtnEliminarArchivo_Click;
            fileExplorer.NodeMouseDoubleClick += FileExplorer_NodeMouseDoubleClick;

            // Menú (opcional)
            InicializarMenuPersonalizado();

            // ToolStrip
            InicializarToolStrip();

            // Moderniza botones del explorador
            ModernizarBotonesFileExplorer();

            // Cargar íconos para nodos del explorador de archivos
            Image iconArchivo = Image.FromFile("Resources/Icons/archivo.png");
            Bitmap smallIcon = new Bitmap(iconArchivo, new Size(16, 16));
            fileExplorer.ImageList = new ImageList();
            fileExplorer.ImageList.Images.Add("archivo", smallIcon);
            fileExplorer.ImageList.Images.Add("php", new Bitmap(Image.FromFile("Resources/Icons/php.png"), new Size(16, 16)));

            // TabControl inferior para errores/resultados
            InicializarTabOutput();
        }

        #region Menú (Opcional)

        private void InicializarMenuPersonalizado()
        {
            menuStrip.Items.Clear();

            Image openIcon = SystemIcons.Application.ToBitmap();
            Image saveIcon = SystemIcons.Information.ToBitmap();
            Image saveAsIcon = SystemIcons.Warning.ToBitmap();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open", openIcon, (s, e) => OpenFile()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save", saveIcon, (s, e) => SaveFile()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save As", saveAsIcon, (s, e) => SaveFileAs()));

            Image lexicalIcon = SystemIcons.Information.ToBitmap();
            Image syntaxIcon = SystemIcons.Question.ToBitmap();
            Image semanticIcon = SystemIcons.Shield.ToBitmap();
            Image intermediateIcon = SystemIcons.WinLogo.ToBitmap();
            Image executionIcon = SystemIcons.Exclamation.ToBitmap();

            ToolStripMenuItem compileMenu = new ToolStripMenuItem("Compile");
            compileMenu.DropDownItems.Add(new ToolStripMenuItem("Lexical Analysis", lexicalIcon, (s, e) => EjecutarFase("Lexical Analysis")));
            compileMenu.DropDownItems.Add(new ToolStripMenuItem("Syntax Analysis", syntaxIcon, (s, e) => EjecutarFase("Syntax Analysis")));
            compileMenu.DropDownItems.Add(new ToolStripMenuItem("Semantic Analysis", semanticIcon, (s, e) => EjecutarFase("Semantic Analysis")));
            compileMenu.DropDownItems.Add(new ToolStripMenuItem("Intermediate Code", intermediateIcon, (s, e) => EjecutarFase("Intermediate Code")));
            compileMenu.DropDownItems.Add(new ToolStripMenuItem("Execution", executionIcon, (s, e) => EjecutarFase("Execution")));

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(compileMenu);
        }

        #endregion

        #region ToolStrip con botones

        private void InicializarToolStrip()
        {
            Image iconNuevoProyecto = Image.FromFile("Resources/Icons/guardar.png");
            Image iconBuildDebug = Image.FromFile("Resources/Icons/debug.png");
            Image iconCerrarVentana = Image.FromFile("Resources/Icons/cerrar.png");
            Image iconCorrerAnalizador = Image.FromFile("Resources/Icons/lexico.png");
            Image iconCorrerSintactico = Image.FromFile("Resources/Icons/sintactico.png");
            Image iconCorrerSemantico = Image.FromFile("Resources/Icons/semantico.png");
            Image iconGenerarCodigo = Image.FromFile("Resources/Icons/codigo-intermedio.png");

            toolStrip1.Items.Clear();

            // New Project
            ToolStripButton newProjectButton = new ToolStripButton
            {
                Image = iconNuevoProyecto,
                ToolTipText = "Nuevo Proyecto"
            };
            newProjectButton.Click += (s, e) =>
            {
                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Selecciona la ubicación para el nuevo proyecto";

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string nombreProyecto = Microsoft.VisualBasic.Interaction.InputBox(
                            "Escribe el nombre del nuevo proyecto:", "Nuevo Proyecto", "MiProyecto");

                        if (!string.IsNullOrWhiteSpace(nombreProyecto))
                        {
                            string rutaCompleta = Path.Combine(folderDialog.SelectedPath, nombreProyecto);

                            try
                            {
                                Directory.CreateDirectory(rutaCompleta);
                                MessageBox.Show($"Proyecto creado en:\n{rutaCompleta}", "Éxito",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                                MainForm nuevoForm = new MainForm { Text = nombreProyecto };
                                nuevoForm.currentFilePath = rutaCompleta;
                                nuevoForm.Show();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error al crear la carpeta:\n" + ex.Message,
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            };
            toolStrip1.Items.Add(newProjectButton);

            // Build & Debug
            ToolStripButton buildDebugButton = new ToolStripButton
            {
                Image = iconBuildDebug,
                ToolTipText = "Build and Debug"
            };
            buildDebugButton.Click += (s, e) => { /* lógica */ };
            toolStrip1.Items.Add(buildDebugButton);

            // Cerrar ventana
            ToolStripButton closeButton = new ToolStripButton
            {
                Image = iconCerrarVentana,
                ToolTipText = "Cerrar ventana"
            };
            closeButton.Click += (s, e) => this.Close();
            toolStrip1.Items.Add(closeButton);

            // Léxico
            ToolStripButton lexicoButton = new ToolStripButton
            {
                Image = iconCorrerAnalizador,
                ToolTipText = "Análisis Léxico"
            };
            lexicoButton.Click += (s, e) => EjecutarFase("Lexical Analysis");
            toolStrip1.Items.Add(lexicoButton);

            // Sintáctico
            ToolStripButton sintacticoButton = new ToolStripButton
            {
                Image = iconCorrerSintactico,
                ToolTipText = "Análisis Sintáctico"
            };
            sintacticoButton.Click += (s, e) => EjecutarFase("Syntax Analysis");
            toolStrip1.Items.Add(sintacticoButton);

            // Semántico
            ToolStripButton semanticoButton = new ToolStripButton
            {
                Image = iconCorrerSemantico,
                ToolTipText = "Análisis Semántico"
            };
            semanticoButton.Click += (s, e) => EjecutarFase("Semantic Analysis");
            toolStrip1.Items.Add(semanticoButton);

            // Código intermedio
            ToolStripButton compilarButton = new ToolStripButton
            {
                Image = iconGenerarCodigo,
                ToolTipText = "Generar Código Intermedio"
            };
            compilarButton.Click += (s, e) => EjecutarFase("Intermediate Code");
            toolStrip1.Items.Add(compilarButton);
        }

        #endregion

        #region Botones del Explorador de Archivos

        private void ModernizarBotonesFileExplorer()
        {
            Image iconOriginal2 = Image.FromFile("Resources/Icons/agregar-archivo.png");
            Image iconAgregar = new Bitmap(iconOriginal2, new Size(24, 24));
            Image iconOriginal = Image.FromFile("Resources/Icons/basura.png");
            Image iconBasura = new Bitmap(iconOriginal, new Size(24, 24));

            // Agregar Archivo
            btnAgregarArchivo.FlatStyle = FlatStyle.Flat;
            btnAgregarArchivo.FlatAppearance.BorderSize = 0;
            btnAgregarArchivo.BackColor = Color.FromArgb(45, 45, 48);
            btnAgregarArchivo.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 63, 70);
            btnAgregarArchivo.Size = new Size(32, 32);
            btnAgregarArchivo.Image = iconAgregar;
            btnEliminarArchivo.Text = "";

            // Eliminar Archivo
            btnEliminarArchivo.FlatStyle = FlatStyle.Flat;
            btnEliminarArchivo.FlatAppearance.BorderSize = 0;
            btnEliminarArchivo.BackColor = Color.FromArgb(45, 45, 48);
            btnEliminarArchivo.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 63, 70);
            btnEliminarArchivo.Size = new Size(32, 32);
            btnEliminarArchivo.Image = iconBasura;
            btnEliminarArchivo.Text = "";
        }

        #endregion

        #region TabControl inferior para errores/resultados

        private void InicializarTabOutput()
        {
            tabOutput.TabPages.Clear();
            // Incluimos "Hash Table" abajo
            string[] nombresPestañas = { "Errores Lexicos", "Errores Sintacticos", "Errores Semanticos", "Resultados", "Hash Table" };

            foreach (string nombre in nombresPestañas)
            {
                TabPage pagina = new TabPage(nombre);
                RichTextBox rtb = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 10),
                    BackColor = Color.Black,
                    ForeColor = Color.White,
                    ReadOnly = true,
                    Text = $"Contenido para {nombre}..."
                };
                pagina.Controls.Add(rtb);
                tabOutput.TabPages.Add(pagina);
            }
        }

        #endregion

        #region Win32 helpers + números de línea

        const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        const int EM_LINEINDEX = 0x00BB;
        const int EM_GETLINECOUNT = 0x00BA;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private int FirstVisibleLine()
            => SendMessage(txtEditor.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);

        private int LineIndex(int line)
            => SendMessage(txtEditor.Handle, EM_LINEINDEX, (IntPtr)line, IntPtr.Zero);

        private int LineCount()
            => SendMessage(txtEditor.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);

        #endregion

        #region Eventos del Editor y Números de Línea

        private void TxtEditor_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateLineColumn();
            lineNumberPanel.Invalidate();
        }

        // Flags
        private bool pasteDetected = false;
        private bool insideBlockComment = false; // (warning si no se usa)

        private void TxtEditor_TextChanged(object? sender, EventArgs e)
        {
            UpdateLineColumn();
            lineNumberPanel.Invalidate();

            var analizador = new LexicalAnalyzer();

            if (pasteDetected)
            {
                pasteDetected = false;
                var (tokens, _) = analizador.Analizar(txtEditor.Text);
                AplicarColoreadoCompleto(tokens);
            }
            else
            {
                if (txtEditor.Text.Contains("/*") && txtEditor.Text.Contains("*/"))
                {
                    var (tokens, _) = analizador.Analizar(txtEditor.Text);
                    AplicarColoreadoCompleto(tokens);
                }
                else
                {
                    int lineIndex = txtEditor.GetLineFromCharIndex(txtEditor.SelectionStart);
                    int start = txtEditor.GetFirstCharIndexFromLine(lineIndex);
                    int end = (lineIndex == txtEditor.Lines.Length - 1)
                        ? txtEditor.Text.Length
                        : txtEditor.GetFirstCharIndexFromLine(lineIndex + 1);

                    int length = end - start;
                    string lineaTexto = txtEditor.Text.Substring(start, length);
                    var (tokens, _) = analizador.Analizar(lineaTexto);
                    AplicarColoreadoLinea(tokens, start);
                }
            }
        }

        private void AplicarColoreadoCompleto(List<Token> tokens)
        {
            int selStart = txtEditor.SelectionStart;
            int selLen = txtEditor.SelectionLength;

            txtEditor.TextChanged -= TxtEditor_TextChanged;
            txtEditor.SuspendLayout();

            txtEditor.SelectAll();
            txtEditor.SelectionColor = Color.White;

            foreach (var t in tokens)
            {
                int lineIdx = t.Linea - 1;
                int pos = txtEditor.GetFirstCharIndexFromLine(lineIdx) + (t.Columna - 1);
                if (pos < 0 || pos + t.Valor.Length > txtEditor.Text.Length) continue;

                txtEditor.Select(pos, t.Valor.Length);
                txtEditor.SelectionColor = ColorForToken(t.Tipo, t.Valor);
            }

            txtEditor.Select(selStart, selLen);
            txtEditor.SelectionColor = Color.White;
            txtEditor.ResumeLayout();
            txtEditor.TextChanged += TxtEditor_TextChanged;
        }

        private void AplicarColoreadoLinea(List<Token> tokens, int offset)
        {
            int selStart = txtEditor.SelectionStart;
            int selLen = txtEditor.SelectionLength;

            txtEditor.TextChanged -= TxtEditor_TextChanged;
            txtEditor.SuspendLayout();

            foreach (var t in tokens)
            {
                int pos = offset + (t.Columna - 1);
                if (pos < 0 || pos + t.Valor.Length > txtEditor.Text.Length) continue;

                txtEditor.Select(pos, t.Valor.Length);
                txtEditor.SelectionColor = ColorForToken(t.Tipo, t.Valor);
            }

            txtEditor.Select(selStart, selLen);
            txtEditor.SelectionColor = Color.White;
            txtEditor.ResumeLayout();
            txtEditor.TextChanged += TxtEditor_TextChanged;
        }

        private void TxtEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
                pasteDetected = true;
        }

        private Color ColorForToken(string tipo, string valor)
        {
            return tipo switch
            {
                "Numero" or "PuntoFlotante" => Color.LightGreen,
                "Identificador" => Color.Cyan,
                "ComentarioInline" or "ComentarioExtenso" => Color.Gray,
                "PalabraReservada" => Color.Orange,
                "OperadorAritmetico" => Color.Yellow,
                "OperadorRelacional" or "OperadorLogico" or "Asignacion" or "Simbolo" => Color.Red,
                _ => Color.White,
            };
        }

        private void TxtEditor_VScroll(object? sender, EventArgs e)
        {
            lineNumberPanel.Invalidate();
        }

        private void TxtEditor_Resize(object? sender, EventArgs e)
        {
            lineNumberPanel.Invalidate();
        }

        private void LineNumberPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.FromArgb(50, 50, 70));

            int totalLines = LineCount();
            int firstLine = FirstVisibleLine();
            if (totalLines <= 0) return;

            using var f = new Font("JetBrains Mono", txtEditor.Font.Size, FontStyle.Regular);

            // ancho dinámico según dígitos
            var digits = Math.Max(2, totalLines.ToString().Length);
            var size9s = g.MeasureString(new string('9', digits), f);
            int desiredWidth = (int)Math.Ceiling(size9s.Width) + 10;
            if (lineNumberPanel.Width != desiredWidth)
                lineNumberPanel.Width = desiredWidth;

            // resalta línea actual (opcional)
            int currentLine = txtEditor.GetLineFromCharIndex(txtEditor.SelectionStart);
            int currentIdx = LineIndex(Math.Max(0, Math.Min(currentLine, totalLines - 1)));
            var currPos = txtEditor.GetPositionFromCharIndex(Math.Max(0, currentIdx));
            using var hl = new SolidBrush(Color.FromArgb(40, 120, 120, 160));
            g.FillRectangle(hl, 0, currPos.Y, lineNumberPanel.Width, (int)f.GetHeight(g));

            // números alineados a la derecha usando la Y real de cada línea
            var fmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near };
            int h = lineNumberPanel.ClientSize.Height;

            for (int line = firstLine; line < totalLines; line++)
            {
                int charIndex = LineIndex(line);
                if (charIndex < 0) break;

                var pos = txtEditor.GetPositionFromCharIndex(Math.Max(0, charIndex));
                if (pos.Y > h) break;

                g.DrawString((line + 1).ToString(), f, Brushes.Gainsboro,
                             new RectangleF(0, pos.Y, lineNumberPanel.Width - 4, f.GetHeight(g)), fmt);
            }
        }

        private void UpdateLineColumn()
        {
            if (txtEditor == null || lblStatus == null) return;

            int line = txtEditor.GetLineFromCharIndex(txtEditor.SelectionStart) + 1;
            int column = txtEditor.SelectionStart - txtEditor.GetFirstCharIndexOfCurrentLine() + 1;
            lblStatus.Text = $"Línea: {line}, Columna: {column}";
        }

        #endregion

        #region Helpers de Hash Table (arriba y abajo)

        // Construye el ListView con la tabla de símbolos
        private ListView BuildSymbolList(IEnumerable<SymbolEntry> entries)
        {
            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.Black,
                ForeColor = Color.White
            };
            list.Columns.Add("Nombre", 120);
            list.Columns.Add("Tipo", 70);
            list.Columns.Add("Valor", 100);
            // Ámbito eliminado
            list.Columns.Add("Offset", 70);
            list.Columns.Add("Loc", 60);
            list.Columns.Add("Líneas", 160);

            foreach (var e2 in entries)
            {
                list.Items.Add(new ListViewItem(new[]
                {
                    e2.Name,
                    e2.Type.ToSource(),
                    e2.ValueAsString,
                    e2.Offset.ToString(),
                    e2.Loc.ToString(),
                    string.Join(", ", e2.Lines)
                }));
            }
            return list;
        }

        // Asegura/crea la pestaña "Hash Table" abajo en tabOutput
        private TabPage EnsureBottomHashTab()
        {
            var page = tabOutput.TabPages.Cast<TabPage>()
                .FirstOrDefault(tp => tp.Text.Equals("Hash Table", StringComparison.OrdinalIgnoreCase));
            if (page == null)
            {
                page = new TabPage("Hash Table");
                // lo insertamos antes de "Resultados" para que quede juntito
                int idxResultados = tabOutput.TabPages.Cast<TabPage>()
                    .Select((tp, i) => new { tp.Text, i })
                    .FirstOrDefault(x => x.Text.Equals("Resultados", StringComparison.OrdinalIgnoreCase))?.i ?? tabOutput.TabPages.Count;
                tabOutput.TabPages.Insert(idxResultados, page);
            }
            return page;
        }

        #endregion

        #region Lógica de Menú/Compilación

        private RichTextBox? GetOutputRichTextBox(string tabName)
        {
            foreach (TabPage pagina in tabOutput.TabPages)
                if (pagina.Text.Equals(tabName, StringComparison.OrdinalIgnoreCase))
                    return pagina.Controls
                                 .OfType<RichTextBox>()
                                 .FirstOrDefault();
            return null;
        }

        private void EjecutarFase(string fase)
        {
            string tabName;

            switch (fase)
            {
                case "Lexical Analysis":
                    {
                        tabName = "Errores Lexicos";

                        LexicalAnalyzer analizador = new LexicalAnalyzer();
                        var (tokens, errores) = analizador.Analizar(txtEditor.Text);
                        AplicarColoreado(tokens);

                        foreach (TabPage pagina in tabOutput.TabPages)
                        {
                            if (pagina.Text.Equals(tabName, StringComparison.OrdinalIgnoreCase))
                            {
                                if (pagina.Controls.Count > 0 && pagina.Controls[0] is RichTextBox rtb)
                                {
                                    rtb.Text = errores.Count > 0
                                        ? "Errores Léxicos Encontrados:\n\n" + string.Join(Environment.NewLine, errores)
                                        : "Sin errores léxicos encontrados.";
                                }
                                tabOutput.SelectedTab = pagina;
                                break;
                            }
                        }

                        var tokensSinComentarios = tokens
                            .Where(t => t.Tipo != "ComentarioInline" && t.Tipo != "ComentarioExtenso")
                            .ToList();

                        rtbLexico.Clear();
                        rtbLexico.Text = string.Join(Environment.NewLine, tokensSinComentarios.Select(t => t.ToString()));
                        tabAnalysis.SelectedTab = tabLexico;

                        break;
                    }

                case "Syntax Analysis":
                    {
                        var lexico2 = new LexicalAnalyzer();
                        var (tokens2, lexErrors) = lexico2.Analizar(txtEditor.Text);

                        if (lexErrors.Count > 0)
                        {
                            var rtbLexErr = GetOutputRichTextBox("Errores Sintacticos");
                            if (rtbLexErr != null)
                                rtbLexErr.Text = "Antes de parsear, hay errores léxicos:\r\n" + string.Join("\r\n", lexErrors);
                            tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Sintacticos");
                            break;
                        }

                        var parser = new SyntaxAnalyzer(tokens2);
                        ProgramNode ast = null;
                        try { ast = parser.Parse(); }
                        catch { parser.Errors.Add("Error interno al parsear."); }

                        var rtbSynErr = GetOutputRichTextBox("Errores Sintacticos");
                        if (parser.Errors.Count > 0)
                        {
                            if (rtbSynErr != null)
                                rtbSynErr.Text = "Errores Sintácticos Encontrados:\r\n" + string.Join("\r\n", parser.Errors);
                            tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Sintacticos");
                        }
                        else
                        {
                            if (rtbSynErr != null)
                                rtbSynErr.Text = "Análisis sintáctico completado correctamente.\r\nNo se encontraron errores de sintaxis.";
                            tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Sintacticos");

                            treeViewSintactico.Nodes.Clear();
                            treeViewSintactico.Nodes.Add(BuildTree(ast));
                            treeViewSintactico.ExpandAll();
                            tabAnalysis.SelectedTab = tabSintactico;
                        }
                        break;
                    }

                case "Semantic Analysis":
                    {
                        // 1) Léxico
                        var lexico = new LexicalAnalyzer();
                        var (tokens, lexErrs) = lexico.Analizar(txtEditor.Text);
                        if (lexErrs.Count > 0)
                        {
                            var rtbSem = GetOutputRichTextBox("Errores Semanticos");
                            if (rtbSem != null)
                                rtbSem.Text = "Antes del análisis semántico, hay errores léxicos:\r\n" +
                                              string.Join("\r\n", lexErrs);
                            tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Semanticos");
                            break;
                        }

                        // 2) Sintaxis
                        var parser = new SyntaxAnalyzer(tokens);
                        ProgramNode ast = null;
                        try { ast = parser.Parse(); }
                        catch { parser.Errors.Add("Error interno al parsear."); }

                        var rtbSem2 = GetOutputRichTextBox("Errores Semanticos");
                        if (parser.Errors.Count > 0 || ast == null)
                        {
                            if (rtbSem2 != null)
                                rtbSem2.Text = "Antes del análisis semántico, hay errores sintácticos:\r\n" +
                                               string.Join("\r\n", parser.Errors);
                            tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Semanticos");
                            break;
                        }

                        _lastAst = ast;

                        // 3) Semántico - PASAR LOS TOKENS + fuente
                        var sem = new IDE_COMPILADOR.AnalizadorSemantico.SemanticAnalyzer();
                        sem.Analyze(_lastAst, tokens, txtEditor.Text);

                        // 4) Errores semánticos
                        if (rtbSem2 != null)
                            rtbSem2.Text = sem.Errors.Count > 0
                                ? "Errores semánticos:\r\n" + string.Join("\r\n", sem.Errors)
                                : "Análisis semántico completado sin errores.";

                        tabOutput.SelectedTab = tabOutput.TabPages.Cast<TabPage>().First(tp => tp.Text == "Errores Semanticos");

                        // 5) Árbol anotado (en pestaña Semántico, arriba)
                        var annotatedRoot = sem.BuildAnnotatedTree(_lastAst);
                        var treeSem = new TreeView
                        {
                            Dock = DockStyle.Fill,
                            BackColor = Color.Black,
                            ForeColor = Color.White
                        };
                        treeSem.Nodes.Add(annotatedRoot);

                        tabSemantico.Controls.Clear();
                        tabSemantico.Controls.Add(treeSem);
                        treeSem.ExpandAll();
                        tabAnalysis.SelectedTab = tabSemantico;

                        // 6) Tabla de símbolos (ARRIBA)
                        tabHashTable.Controls.Clear();
                        tabHashTable.Controls.Add(BuildSymbolList(sem.Symbols.AllEntries()));
                        tabAnalysis.SelectedTab = tabHashTable;

                        // 6bis) Misma tabla de símbolos ABAJO (tabOutput)
                        var bottomHash = EnsureBottomHashTab();
                        bottomHash.Controls.Clear();
                        bottomHash.Controls.Add(BuildSymbolList(sem.Symbols.AllEntries()));
                        tabOutput.SelectedTab = bottomHash;

                        // 7) Exportación de entregables
                        try
                        {
                            string outDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SalidaSemantica");
                            System.IO.Directory.CreateDirectory(outDir);

                            // Tabla de símbolos
                            var tablaPath = System.IO.Path.Combine(outDir, "tabla_simbolos.txt");
                            var lineasTabla = new List<string>
                            {
                                "# Nombre\tTipo\tValor\tOffset\tLoc\tLineas"
                            };
                            foreach (var e2 in sem.Symbols.AllEntries())
                            {
                                lineasTabla.Add($"{e2.Name}\t{e2.Type.ToSource()}\t{e2.ValueAsString}\t{e2.Offset}\t{e2.Loc}\t[{string.Join(",", e2.Lines)}]");
                            }
                            System.IO.File.WriteAllLines(tablaPath, lineasTabla);

                            // Errores semánticos
                            var erroresPath = System.IO.Path.Combine(outDir, "errores_semanticos.txt");
                            if (sem.Errors.Count > 0)
                                System.IO.File.WriteAllLines(erroresPath, sem.Errors);
                            else
                                System.IO.File.WriteAllText(erroresPath, "Análisis semántico completado sin errores.");

                            // AST anotado (texto)
                            var anotadoPath = System.IO.Path.Combine(outDir, "ast_anotado.txt");
                            using (var sw = new System.IO.StreamWriter(anotadoPath))
                            {
                                void Dump(TreeNode n, int depth)
                                {
                                    sw.WriteLine(new string(' ', depth * 2) + n.Text.Replace("\r", "").Replace("\n", " | "));
                                    foreach (TreeNode ch in n.Nodes) Dump(ch, depth + 1);
                                }
                                Dump(annotatedRoot, 0);
                            }
                        }
                        catch { /* no romper UI si falla IO */ }

                        break;
                    }

                case "Intermediate Code":
                    {
                        tabName = "Resultados";
                        MostrarMensajeTemporal(tabName, "Generando código intermedio...");
                        break;
                    }

                case "Execution":
                    {
                        tabName = "Resultados";
                        MostrarMensajeTemporal(tabName, "Ejecutando programa...");
                        break;
                    }

                default:
                    {
                        tabName = "Resultados";
                        MostrarMensajeTemporal(tabName, $"Fase '{fase}' en ejecución...");
                        break;
                    }
            }
        }

        #endregion

        #region Árbol sintáctico base (para pestaña Sintáctico)

        private TreeNode BuildTree(ASTNode node)
        {
            switch (node)
            {
                case ProgramNode p:
                    var root = new TreeNode("Program");
                    foreach (var d in p.Declarations)
                        root.Nodes.Add(BuildTree(d));
                    return root;

                case VariableDeclarationNode vd:
                    var vNode = new TreeNode($"VarDecl: {vd.TypeName}");
                    foreach (var id in vd.Identifiers)
                        vNode.Nodes.Add(new TreeNode(id));
                    return vNode;

                case StatementListNode sl:
                    var sList = new TreeNode("StatementList");
                    foreach (var st in sl.Statements)
                        sList.Nodes.Add(BuildTree(st));
                    return sList;

                case AssignmentNode a:
                    var an = new TreeNode($"Assign: {a.Identifier}");
                    an.Nodes.Add(BuildTree(a.Expression));
                    return an;

                case IfNode iff:
                    var ifn = new TreeNode("If");
                    ifn.Nodes.Add(BuildTree(iff.Condition));
                    var thenN = new TreeNode("Then");
                    iff.ThenBranch.ForEach(s => thenN.Nodes.Add(BuildTree(s)));
                    ifn.Nodes.Add(thenN);
                    if (iff.ElseBranch != null)
                    {
                        var elseN = new TreeNode("Else");
                        iff.ElseBranch.ForEach(s => elseN.Nodes.Add(BuildTree(s)));
                        ifn.Nodes.Add(elseN);
                    }
                    return ifn;

                case WhileNode w:
                    var wn = new TreeNode("While");
                    wn.Nodes.Add(BuildTree(w.Condition));
                    var bodyW = new TreeNode("Body");
                    w.Body.ForEach(s => bodyW.Nodes.Add(BuildTree(s)));
                    wn.Nodes.Add(bodyW);
                    return wn;

                case DoWhileNode dw:
                    var dwn = new TreeNode("DoWhile");
                    var bodyD = new TreeNode("Body");
                    dw.Body.ForEach(s => bodyD.Nodes.Add(BuildTree(s)));
                    dwn.Nodes.Add(bodyD);
                    var condD = new TreeNode("Condition");
                    condD.Nodes.Add(BuildTree(dw.Condition));
                    dwn.Nodes.Add(condD);
                    return dwn;

                case DoUntilNode du:
                    var duNode = new TreeNode("DoUntil");
                    var bodyDoNode = new TreeNode("DoBody");
                    foreach (var stmt in du.BodyDo)
                        bodyDoNode.Nodes.Add(BuildTree(stmt));
                    duNode.Nodes.Add(bodyDoNode);

                    var condWhileNode = new TreeNode("WhileCondition");
                    condWhileNode.Nodes.Add(BuildTree(du.ConditionWhile));
                    duNode.Nodes.Add(condWhileNode);

                    var bodyWhileNode = new TreeNode("WhileBody");
                    foreach (var stmt2 in du.BodyWhile)
                        bodyWhileNode.Nodes.Add(BuildTree(stmt2));
                    duNode.Nodes.Add(bodyWhileNode);

                    var condUntilNode = new TreeNode("UntilCondition");
                    condUntilNode.Nodes.Add(BuildTree(du.ConditionUntil));
                    duNode.Nodes.Add(condUntilNode);

                    return duNode;

                case UnaryPostfixNode up:
                    var upNode = new TreeNode($"Postfix: {up.Identifier} {up.Operator}");
                    upNode.Nodes.Add(new TreeNode($"Id: {up.Identifier}"));
                    upNode.Nodes.Add(new TreeNode("Literal: 1"));
                    return upNode;

                case InputNode inp:
                    return new TreeNode($"Input: {inp.Identifier}");

                case OutputNode outp:
                    var on = new TreeNode("Output");
                    on.Nodes.Add(new TreeNode(outp.Value is ExpressionNode
                        ? ((ExpressionNode)outp.Value).ToString()
                        : outp.Value.ToString()));
                    return on;

                case BinaryOpNode b:
                    var bn = new TreeNode($"Op {b.Operator}");
                    bn.Nodes.Add(BuildTree(b.Left));
                    bn.Nodes.Add(BuildTree(b.Right));
                    return bn;

                case LiteralNode lit:
                    return new TreeNode($"Literal: {lit.Value}");

                case IdentifierNode idn:
                    return new TreeNode($"Id: {idn.Name}");
            }

            return new TreeNode(node.GetType().Name);
        }

        #endregion

        #region Utilidades varias

        private void AplicarColoreado(List<Token> tokens)
        {
            int originalSelectionStart = txtEditor.SelectionStart;
            int originalSelectionLength = txtEditor.SelectionLength;

            txtEditor.TextChanged -= TxtEditor_TextChanged;
            txtEditor.SuspendLayout();

            txtEditor.SelectAll();
            txtEditor.SelectionColor = Color.White;

            foreach (var token in tokens)
            {
                try
                {
                    int start = txtEditor.GetFirstCharIndexFromLine(token.Linea - 1) + token.Columna - 1;
                    txtEditor.Select(start, token.Valor.Length);

                    switch (token.Tipo)
                    {
                        case "Numero":
                        case "PuntoFlotante":
                            txtEditor.SelectionColor = Color.LightGreen; break;
                        case "Identificador":
                            txtEditor.SelectionColor = Color.Cyan; break;
                        case "ComentarioInline":
                        case "ComentarioExtenso":
                            txtEditor.SelectionColor = Color.Gray; break;
                        case "PalabraReservada":
                            txtEditor.SelectionColor = Color.Orange; break;
                        case "OperadorAritmetico":
                            txtEditor.SelectionColor = Color.Yellow; break;
                        case "OperadorRelacional":
                        case "OperadorLogico":
                        case "Asignacion":
                            txtEditor.SelectionColor = Color.Red; break;
                    }
                }
                catch { /* fuera de rango: ignorar */ }
            }

            txtEditor.Select(originalSelectionStart, originalSelectionLength);
            txtEditor.SelectionColor = Color.White;
            txtEditor.ResumeLayout();
            txtEditor.TextChanged += TxtEditor_TextChanged;
        }

        private void MostrarMensajeTemporal(string tabName, string mensaje)
        {
            foreach (TabPage pagina in tabOutput.TabPages)
            {
                if (pagina.Text.Equals(tabName, StringComparison.OrdinalIgnoreCase))
                {
                    if (pagina.Controls.Count > 0 && pagina.Controls[0] is RichTextBox rtb)
                        rtb.Text = mensaje;
                    tabOutput.SelectedTab = pagina;
                    break;
                }
            }
        }

        #endregion

        #region Abrir/Guardar Archivos

        private void OpenFile()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Archivos permitidos (*.txt;*.html;*.php;*.java;*.cs)|*.txt;*.html;*.php;*.java;*.cs|Todos los archivos (*.*)|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                currentFilePath = dlg.FileName;
                txtEditor.Text = File.ReadAllText(currentFilePath);

                var analizador = new LexicalAnalyzer();
                var (tokens, _) = analizador.Analizar(txtEditor.Text);
                AplicarColoreadoCompleto(tokens);
            }
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(currentFilePath))
                SaveFileAs();
            else
                File.WriteAllText(currentFilePath, txtEditor.Text);
        }

        private void SaveFileAs()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveFileDialog.FileName;
                    File.WriteAllText(currentFilePath, txtEditor.Text);
                }
            }
        }

        #endregion

        #region Explorador de Archivos

        private void BtnAgregarArchivo_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Archivos permitidos (*.txt;*.html;*.php;*.java;*.cs)|*.txt;*.html;*.php;*.java;*.cs|Todos los archivos (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    if (!FileNodeExists(filePath))
                    {
                        string extension = Path.GetExtension(filePath).ToLower();
                        string iconKey = "default";

                        switch (extension)
                        {
                            case ".php": iconKey = "php"; break;
                            case ".html": iconKey = "html"; break;
                            case ".java": iconKey = "java"; break;
                            case ".cs": iconKey = "cs"; break;
                        }

                        TreeNode node = new TreeNode(Path.GetFileName(filePath))
                        {
                            Tag = filePath,
                            ImageKey = iconKey,
                            SelectedImageKey = iconKey
                        };

                        fileExplorer.Nodes.Add(node);
                    }
                }
            }
        }

        private bool FileNodeExists(string filePath)
        {
            foreach (TreeNode node in fileExplorer.Nodes)
            {
                if (node.Tag != null && node.Tag.ToString() == filePath)
                    return true;
            }
            return false;
        }

        private void BtnEliminarArchivo_Click(object? sender, EventArgs e)
        {
            TreeNode selectedNode = fileExplorer.SelectedNode;
            if (selectedNode != null)
            {
                DialogResult result = MessageBox.Show(
                    "¿Estás seguro de que deseas eliminar este archivo del explorador?",
                    "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    string filePathToDelete = selectedNode.Tag as string;
                    if (!string.IsNullOrEmpty(filePathToDelete) && filePathToDelete == currentFilePath)
                    {
                        txtEditor.Clear();
                        currentFilePath = string.Empty;
                    }
                    fileExplorer.Nodes.Remove(selectedNode);
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecciona un archivo para eliminar.",
                                "Eliminar Archivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void FileExplorer_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null) return;

            string filePath = e.Node.Tag.ToString();
            if (!File.Exists(filePath))
            {
                MessageBox.Show("El archivo no existe.");
                return;
            }

            currentFilePath = filePath;
            txtEditor.Text = File.ReadAllText(filePath);

            var analizador = new LexicalAnalyzer();
            var (tokens, _) = analizador.Analizar(txtEditor.Text);
            AplicarColoreadoCompleto(tokens);
        }

        #endregion
    }
}
