using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Reflection;

namespace IDE_COMPILADOR
{
    partial class MainForm : Form
    {
        private System.ComponentModel.IContainer components = null;

        private MenuStrip menuStrip;
        private ToolStrip toolStrip1;
        private SplitContainer splitContainerMain; // ARRIBA/ABAJO
        private SplitContainer splitContainer;     // IZQUIERDA/DERECHA (editor / análisis)
        private Panel panelEditor;
        private Panel lineNumberPanel;
        private RichTextBox txtEditor;
        private Panel panelAnalysis;
        private TabControl tabAnalysis;
        private TabPage tabLexico;
        private TabPage tabSintactico;
        private TreeView treeViewSintactico;
        private TabPage tabSemantico;
        private TabPage tabHashTable;
        private TabPage tabCodigoIntermedio;
        private TabPage tabColores;                  // (no usada, puedes quitarla si no la necesitas)
        private TableLayoutPanel tlpColores;
        private Panel panelFileExplorer;
        private FlowLayoutPanel panelFileExplorerButtons;
        private Button btnAgregarArchivo;
        private Button btnEliminarArchivo;
        private TreeView fileExplorer;
        private TabControl tabOutput;               // ABAJO (errores, resultados, hash table extra)
        private Label lblStatus;
        private ToolTip toolTip1;
        private Button btnToggleExplorer;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStrip = new MenuStrip();
            this.toolStrip1 = new ToolStrip();
            this.splitContainerMain = new SplitContainer(); // INICIALIZAR CONTENEDOR HORIZONTAL
            this.splitContainer = new SplitContainer();
            this.panelEditor = new Panel();
            this.lineNumberPanel = new Panel();
            this.txtEditor = new RichTextBox();
            this.panelAnalysis = new Panel();
            this.tabAnalysis = new TabControl();
            this.tabLexico = new TabPage();
            this.rtbLexico = new RichTextBox();
            this.tabSintactico = new TabPage();
            this.treeViewSintactico = new TreeView();
            this.tabSemantico = new TabPage();
            this.tabHashTable = new TabPage();
            this.tabCodigoIntermedio = new TabPage();
            this.panelFileExplorer = new Panel();
            this.panelFileExplorerButtons = new FlowLayoutPanel();
            this.btnAgregarArchivo = new Button();
            this.btnEliminarArchivo = new Button();
            this.fileExplorer = new TreeView();
            this.tabOutput = new TabControl();
            this.lblStatus = new Label();
            this.toolTip1 = new ToolTip(this.components);
            this.btnToggleExplorer = new Button();

            // menuStrip
            this.menuStrip.Dock = DockStyle.Top;
            this.menuStrip.BackColor = Color.FromArgb(18, 18, 30);
            this.menuStrip.ForeColor = Color.White;
            this.menuStrip.Font = new Font("Segoe UI", 10F);

            // toolStrip1
            this.toolStrip1.Dock = DockStyle.Top;
            this.toolStrip1.BackColor = Color.FromArgb(28, 28, 48);
            this.toolStrip1.ForeColor = Color.White;
            this.toolStrip1.ImageScalingSize = new Size(32, 32);
            this.toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            this.toolStrip1.Padding = new Padding(5, 5, 5, 5);
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.Height = 50;

            // splitContainerMain (EDITOR+ANÁLISIS arriba / OUTPUT abajo)
            this.splitContainerMain.Dock = DockStyle.Fill;
            this.splitContainerMain.Orientation = Orientation.Horizontal;
            this.splitContainerMain.SplitterDistance = 400;
            this.splitContainerMain.SplitterWidth = 6;
            this.splitContainerMain.IsSplitterFixed = false;
            this.splitContainerMain.BorderStyle = BorderStyle.FixedSingle;

            // splitContainer (editor y análisis)
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Orientation = Orientation.Vertical;
            this.splitContainer.SplitterDistance = 600;
            this.splitContainer.BackColor = Color.Transparent;
            this.splitContainer.IsSplitterFixed = false;
            this.splitContainer.BorderStyle = BorderStyle.FixedSingle;
            this.splitContainer.SplitterWidth = 6;
            this.splitContainer.Cursor = Cursors.VSplit;

            // panelEditor
            this.panelEditor.Dock = DockStyle.Fill;
            this.panelEditor.BackColor = Color.Transparent;
            this.panelEditor.Padding = new Padding(5);
            this.panelEditor.Paint += PanelEditor_Paint;

            // lineNumberPanel
            this.lineNumberPanel.Dock = DockStyle.Left;
            this.lineNumberPanel.Width = 40;
            this.lineNumberPanel.BackColor = Color.FromArgb(50, 50, 70);

            // txtEditor
            this.txtEditor.Dock = DockStyle.Fill;
            this.txtEditor.Font = new Font("JetBrains Mono", 10F);
            this.txtEditor.AcceptsTab = true;
            this.txtEditor.WordWrap = false;
            this.txtEditor.BackColor = Color.FromArgb(20, 20, 20);
            this.txtEditor.ForeColor = Color.FromArgb(220, 220, 220);
            this.txtEditor.BorderStyle = BorderStyle.None;

            this.panelEditor.Controls.Add(this.txtEditor);
            this.panelEditor.Controls.Add(this.lineNumberPanel);

            // panelAnalysis
            this.panelAnalysis.Dock = DockStyle.Fill;
            this.panelAnalysis.BackColor = Color.FromArgb(30, 30, 50);

            // tabAnalysis
            this.tabAnalysis.Dock = DockStyle.Fill;
            this.tabAnalysis.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.tabAnalysis.Appearance = TabAppearance.FlatButtons;
            this.tabAnalysis.ItemSize = new Size(120, 30);
            this.tabAnalysis.SizeMode = TabSizeMode.Normal;
            this.tabAnalysis.Multiline = true;

            this.tabLexico.Text = "🧩 Léxico";
            this.tabSintactico.Text = "📐 Sintáctico";
            this.tabSemantico.Text = "🧠 Semántico";
            this.tabHashTable.Text = "🔑 Hash Table";
            this.tabCodigoIntermedio.Text = "💻 Código Intermedio";

            // rtbLexico dentro de tabLexico
            this.rtbLexico.Dock = DockStyle.Fill;
            this.rtbLexico.Font = new Font("Consolas", 10);
            this.rtbLexico.BackColor = Color.Black;
            this.rtbLexico.ForeColor = Color.White;
            this.rtbLexico.ReadOnly = true;
            this.tabLexico.Controls.Add(this.rtbLexico);

            // TreeView Sintáctico
            this.treeViewSintactico.Dock = DockStyle.Fill;
            this.treeViewSintactico.BackColor = Color.Black;
            this.treeViewSintactico.ForeColor = Color.White;
            this.tabSintactico.Controls.Add(this.treeViewSintactico);

            this.tabAnalysis.TabPages.AddRange(new TabPage[] {
                this.tabLexico, this.tabSintactico, this.tabSemantico, this.tabHashTable, this.tabCodigoIntermedio });

            this.panelAnalysis.Controls.Add(this.tabAnalysis);

            // splitContainer panels
            this.splitContainer.Panel1.Controls.Add(this.panelEditor);
            this.splitContainer.Panel2.Controls.Add(this.panelAnalysis);

            // panelFileExplorer (derecha)
            this.panelFileExplorer.Dock = DockStyle.Right;
            this.panelFileExplorer.Width = 200;
            this.panelFileExplorer.BackColor = Color.FromArgb(40, 40, 60);

            // panelFileExplorerButtons
            this.panelFileExplorerButtons.Dock = DockStyle.Top;
            this.panelFileExplorerButtons.Height = 40;
            this.panelFileExplorerButtons.FlowDirection = FlowDirection.LeftToRight;
            this.panelFileExplorerButtons.Padding = new Padding(5);

            this.btnAgregarArchivo.Text = "+";
            this.btnEliminarArchivo.Text = "–";

            this.panelFileExplorerButtons.Controls.Add(this.btnAgregarArchivo);
            this.panelFileExplorerButtons.Controls.Add(this.btnEliminarArchivo);

            // fileExplorer
            this.fileExplorer.Dock = DockStyle.Fill;
            this.fileExplorer.BackColor = Color.FromArgb(30, 30, 45);
            this.fileExplorer.ForeColor = Color.White;

            this.panelFileExplorer.Controls.Add(this.fileExplorer);
            this.panelFileExplorer.Controls.Add(this.panelFileExplorerButtons);

            // tabOutput (abajo)
            this.tabOutput.Dock = DockStyle.Fill;
            this.tabOutput.BackColor = Color.FromArgb(15, 15, 25);
            this.tabOutput.ForeColor = Color.LightGreen;

            // lblStatus
            this.lblStatus.Dock = DockStyle.Bottom;
            this.lblStatus.Height = 20;
            this.lblStatus.ForeColor = Color.White;
            this.lblStatus.BackColor = Color.FromArgb(25, 25, 35);

            // btnToggleExplorer
            this.btnToggleExplorer.Dock = DockStyle.Right;
            this.btnToggleExplorer.Width = 25;
            this.btnToggleExplorer.Text = "⇆";
            this.btnToggleExplorer.FlatStyle = FlatStyle.Flat;
            this.btnToggleExplorer.ForeColor = Color.White;
            this.btnToggleExplorer.BackColor = Color.FromArgb(28, 28, 48);
            this.btnToggleExplorer.FlatAppearance.BorderSize = 0;
            this.btnToggleExplorer.Click += BtnToggleExplorer_Click;

            // Armado final de contenedores:
            this.splitContainerMain.Panel1.Controls.Add(this.splitContainer);
            this.splitContainerMain.Panel2.Controls.Add(this.tabOutput);

            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.panelFileExplorer);
            this.Controls.Add(this.btnToggleExplorer);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip);

            this.ClientSize = new Size(1200, 700);
            this.Name = "MainForm";
            this.Text = "Compilador";
            this.WindowState = FormWindowState.Maximized;
        }

        private void BtnToggleExplorer_Click(object sender, EventArgs e)
        {
            panelFileExplorer.Visible = !panelFileExplorer.Visible;
        }

        private void ConfigureModernButton(Button button, string text)
        {
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = Color.White;
            button.BackColor = Color.FromArgb(60, 60, 90);
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            button.AutoSize = true;
            button.Margin = new Padding(5);
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) => button.BackColor = Color.FromArgb(80, 80, 120);
            button.MouseLeave += (s, e) => button.BackColor = Color.FromArgb(60, 60, 90);
        }

        private ToolStripButton CreateToolStripButton(string icon, string tooltip)
        {
            return new ToolStripButton
            {
                Text = icon,
                ToolTipText = tooltip,
                Font = new Font("Segoe UI", 14F),
                ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Width = 40,
                Height = 40
            };
        }

        private void PanelEditor_Paint(object sender, PaintEventArgs e)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRectangle(panelEditor.ClientRectangle);
                using (Pen pen = new Pen(Color.FromArgb(100, 100, 150), 2))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}
