using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CityCapture.Models;
using CityCapture.Services;

namespace CityCapture.Views
{
    public partial class MainForm : Form
    {
        private Graph graph;
        private int k;
        private int[] capitals;
        private List<CaptureStep> history;
        private int currentStep = 0;
        private PointF[] positions;
        private bool layoutReady = false;

        private Color[] stateColors = {
            Color.LightBlue, Color.LightGreen, Color.Orange, Color.Plum, Color.LightSalmon,
            Color.Aquamarine, Color.Gold, Color.LightCoral, Color.LightSeaGreen, Color.Violet
        };

        private Panel graphPanel;
        private Button btnPrev, btnNext, btnLoad, btnShowMatrix;
        private Label lblStep;
        private TrackBar stepTrackBar;
        private TextBox txtConsole;

        // ==== Параметры трансформации (зум/панорамирование) ====
        private float zoomFactor = 1.0f;
        private PointF panOffset = PointF.Empty;
        private Point lastMousePos;
        private bool isPanning = false;

        public MainForm()
        {
            InitializeComponent();
            LoadSampleData();
        }

        private void InitializeComponent()
        {
            this.Text = "Города и дороги — захват государств";
            this.Size = new Size(1000, 700);

            graphPanel = new Panel()
            {
                Location = new Point(10, 10),
                Size = new Size(600, 500),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            graphPanel.Paint += GraphPanel_Paint;
            graphPanel.MouseWheel += GraphPanel_MouseWheel;
            graphPanel.MouseDown += GraphPanel_MouseDown;
            graphPanel.MouseMove += GraphPanel_MouseMove;
            graphPanel.MouseUp += GraphPanel_MouseUp;

            btnPrev = new Button() { Text = "◀ Назад", Location = new Point(10, 520), Size = new Size(80, 30) };
            btnPrev.Click += (s, e) => { if (currentStep > 0) { currentStep--; DrawGraph(); UpdateControls(); } };

            btnNext = new Button() { Text = "Вперёд ▶", Location = new Point(100, 520), Size = new Size(80, 30) };
            btnNext.Click += (s, e) => { if (currentStep < history.Count - 1) { currentStep++; DrawGraph(); UpdateControls(); } };

            lblStep = new Label() { Location = new Point(200, 525), Size = new Size(150, 25), Text = "Шаг 0 / 0" };

            stepTrackBar = new TrackBar()
            {
                Location = new Point(10, 560),
                Size = new Size(580, 45),
                Minimum = 0,
                Maximum = 0,
                TickStyle = TickStyle.None
            };
            stepTrackBar.Scroll += (s, e) => { currentStep = stepTrackBar.Value; DrawGraph(); UpdateControls(); };

            btnLoad = new Button() { Text = "Загрузить из файла", Location = new Point(400, 520), Size = new Size(130, 30) };
            btnLoad.Click += BtnLoad_Click;

            btnShowMatrix = new Button() { Text = "Матрица расстояний", Location = new Point(540, 520), Size = new Size(140, 30) };
            btnShowMatrix.Click += BtnShowMatrix_Click;

            txtConsole = new TextBox()
            {
                Location = new Point(620, 10),
                Size = new Size(350, 500),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9)
            };

            this.Controls.Add(graphPanel);
            this.Controls.Add(btnPrev);
            this.Controls.Add(btnNext);
            this.Controls.Add(lblStep);
            this.Controls.Add(stepTrackBar);
            this.Controls.Add(btnLoad);
            this.Controls.Add(btnShowMatrix);
            this.Controls.Add(txtConsole);
        }

        private void LoadSampleData()
        {
            // Пример без петель; при загрузке файла петли будут отрисованы корректно
            graph = new Graph(8, new List<(int, int, int)>
            {
                (0,1,4), (0,2,2), (1,2,1), (1,3,5),
                (2,3,8), (2,4,10), (3,4,2), (3,5,6),
                (4,5,3), (4,6,4), (5,7,3), (6,7,1)
            });
            k = 2;
            capitals = new int[] { 0, 3 };
            Run();
        }

        private void Run()
        {
            if (graph == null) return;
            graph.ComputeAllPairsShortestPaths();
            var (hist, stateCities) = CaptureEngine.Run(graph, k, capitals);
            history = hist;

            // Раскладка выполняется даже при наличии петель (они игнорируются в модели притяжения)
            positions = GraphLayouter.ComputeLayout(graph, graphPanel.Size);
            layoutReady = true;

            string output = "";
            for (int s = 0; s < k; s++)
            {
                var sorted = stateCities[s].OrderBy(c => c).Select(c => c + 1);
                output += $"Государство {s + 1} (столица {capitals[s] + 1}): {string.Join(", ", sorted)}\r\n";
            }
            output += $"\r\nВсего шагов захвата: {history.Count - 1}";
            txtConsole.Text = output;

            stepTrackBar.Maximum = history.Count - 1;
            stepTrackBar.Value = 0;
            currentStep = 0;
            zoomFactor = 1.0f;
            panOffset = PointF.Empty;
            UpdateControls();
            DrawGraph();
        }

        private void DrawGraph()
        {
            graphPanel.Invalidate();
        }

        private void GraphPanel_Paint(object sender, PaintEventArgs e)
        {
            if (!layoutReady || graph == null || history == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.TranslateTransform(panOffset.X, panOffset.Y);
            g.ScaleTransform(zoomFactor, zoomFactor);

            float baseFontSize = 10f;
            float scaledFontSize = baseFontSize / zoomFactor;
            scaledFontSize = Math.Max(6, Math.Min(scaledFontSize, 24));

            using (Font edgeFont = new Font("Arial", scaledFontSize, FontStyle.Regular))
            using (Pen edgePen = new Pen(Color.Gray, 1.5f / zoomFactor))
            {
                float R = 18f / zoomFactor;   // радиус вершины в мировых единицах (на экране всегда ~18px)

                foreach (var (u, v, len) in graph.Edges)
                {
                    if (u == v)
                    {
                        // Петля: кривая Безье, закреплённая на границе вершины
                        PointF pos = positions[u];

                        // Точки на окружности: вверх-влево и вверх-вправо под 45° от вертикали
                        float offset = R * 0.707f;   // cos(45°) = sin(45°)
                        PointF startPt = new PointF(pos.X - offset, pos.Y - offset);
                        PointF endPt = new PointF(pos.X + offset, pos.Y - offset);

                        // Контрольные точки – вытянуты вверх от краёв вершины
                        float ctrlUp = R * 3f;     // насколько петля поднимается над вершиной
                        float ctrlSide = R * 0.8f;   // боковой размах петли
                        PointF ctrl1 = new PointF(startPt.X - ctrlSide, pos.Y - ctrlUp);
                        PointF ctrl2 = new PointF(endPt.X + ctrlSide, pos.Y - ctrlUp);

                        g.DrawBezier(edgePen, startPt, ctrl1, ctrl2, endPt);

                        // Подпись длины – над вершиной, чуть выше самой высокой точки петли
                        string label = len.ToString();
                        SizeF textSize = g.MeasureString(label, edgeFont);
                        float labelY = pos.Y - ctrlUp - textSize.Height - 2f / zoomFactor;
                        g.DrawString(label, edgeFont, Brushes.Black,
                                     pos.X - textSize.Width / 2, labelY);
                    }
                    else
                    {
                        // Обычное ребро
                        PointF p1 = positions[u];
                        PointF p2 = positions[v];
                        g.DrawLine(edgePen, p1, p2);

                        PointF mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                        string label = len.ToString();
                        SizeF textSize = g.MeasureString(label, edgeFont);
                        g.DrawString(label, edgeFont, Brushes.Black,
                                     mid.X - textSize.Width / 2, mid.Y - textSize.Height / 2);
                    }
                }
            }

            int nodeRadiusInt = (int)(18 / zoomFactor);
            int[] owner = history[currentStep].Owners;

            // Рисуем вершины
            for (int i = 0; i < graph.VertexCount; i++)
            {
                Color fillColor = (owner[i] == 0) ? Color.LightGray : stateColors[(owner[i] - 1) % stateColors.Length];
                PointF pos = positions[i];
                RectangleF rect = new RectangleF(pos.X - nodeRadiusInt, pos.Y - nodeRadiusInt,
                                                 nodeRadiusInt * 2, nodeRadiusInt * 2);
                using (Brush brush = new SolidBrush(fillColor))
                    g.FillEllipse(brush, rect);
                using (Pen pen = new Pen(Color.Black, 1.5f / zoomFactor))
                    g.DrawEllipse(pen, rect);

                string cityLabel = (i + 1).ToString();
                using (Font cityFont = new Font("Arial", scaledFontSize, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(cityLabel, cityFont);
                    g.DrawString(cityLabel, cityFont, Brushes.Black,
                                 pos.X - textSize.Width / 2, pos.Y - textSize.Height / 2);
                }
            }

            g.ResetTransform();
        }

        private void UpdateControls()
        {
            if (history != null)
                lblStep.Text = $"Шаг {currentStep} / {history.Count - 1}";
            stepTrackBar.Value = currentStep;
        }

        // ===== Обработчики мыши для зумирования и панорамирования =====
        private void GraphPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!layoutReady) return;

            float zoomDelta = (e.Delta > 0) ? 0.1f : -0.1f;
            float oldZoom = zoomFactor;
            zoomFactor = Math.Max(0.2f, Math.Min(zoomFactor + zoomDelta, 5.0f));

            Point mousePos = e.Location;
            float ratio = zoomFactor / oldZoom;
            panOffset.X = mousePos.X - ratio * (mousePos.X - panOffset.X);
            panOffset.Y = mousePos.Y - ratio * (mousePos.Y - panOffset.Y);

            graphPanel.Invalidate();
        }

        private void GraphPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = true;
                lastMousePos = e.Location;
                graphPanel.Cursor = Cursors.Hand;
            }
        }

        private void GraphPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                int dx = e.X - lastMousePos.X;
                int dy = e.Y - lastMousePos.Y;
                panOffset.X += dx;
                panOffset.Y += dy;
                lastMousePos = e.Location;
                graphPanel.Invalidate();
            }
        }

        private void GraphPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = false;
                graphPanel.Cursor = Cursors.Default;
            }
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var (g, kk, caps) = InputParser.Parse(ofd.FileName);
                        graph = g;
                        k = kk;
                        capitals = caps;
                        Run();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при чтении файла:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnShowMatrix_Click(object sender, EventArgs e)
        {
            if (graph?.ShortestDistances == null || history == null)
            {
                MessageBox.Show("Сначала загрузите данные или дождитесь обработки.");
                return;
            }

            var matrixForm = new Form()
            {
                Text = "Матрица кратчайших расстояний",
                Size = new Size(700, 500),
                StartPosition = FormStartPosition.CenterParent
            };

            var dgv = new DataGridView()
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = true,
                ColumnHeadersVisible = true
            };

            int n = graph.VertexCount;
            int[] finalOwner = history.Last().Owners;

            for (int i = 0; i < n; i++)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = (i + 1).ToString(),
                    Width = 50,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                dgv.Columns.Add(col);
            }

            for (int i = 0; i < n; i++)
            {
                var row = new DataGridViewRow();
                row.HeaderCell.Value = (i + 1).ToString();
                for (int j = 0; j < n; j++)
                {
                    int d = graph.ShortestDistances[i, j];
                    var cell = new DataGridViewTextBoxCell
                    {
                        Value = (d < int.MaxValue / 3) ? d.ToString() : "∞"
                    };
                    row.Cells.Add(cell);
                }
                dgv.Rows.Add(row);
            }

            for (int i = 0; i < n; i++)
            {
                int ownerIdx = finalOwner[i];
                if (ownerIdx > 0)
                {
                    Color color = stateColors[(ownerIdx - 1) % stateColors.Length];
                    dgv.Rows[i].DefaultCellStyle.BackColor = color;
                    dgv.Columns[i].DefaultCellStyle.BackColor = color;
                }
            }

            matrixForm.Controls.Add(dgv);
            matrixForm.ShowDialog();
        }
    }
}