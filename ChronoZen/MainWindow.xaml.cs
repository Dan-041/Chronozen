using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChronoZen
{
    public partial class MainWindow : Window
    {
        // ── Couleurs ────────────────────────────────────────────────────────
        static readonly Color C_BG      = (Color)ColorConverter.ConvertFromString("#0e0e12");
        static readonly Color C_CARD    = (Color)ColorConverter.ConvertFromString("#1c1c26");
        static readonly Color C_SURFACE = (Color)ColorConverter.ConvertFromString("#16161e");
        static readonly Color C_ACCENT  = (Color)ColorConverter.ConvertFromString("#c8a96e");
        static readonly Color C_TEAL    = (Color)ColorConverter.ConvertFromString("#6ec8b4");
        static readonly Color C_TEXT    = (Color)ColorConverter.ConvertFromString("#e8e4da");
        static readonly Color C_MUTED   = (Color)ColorConverter.ConvertFromString("#5a5a72");
        static readonly Color C_DANGER  = (Color)ColorConverter.ConvertFromString("#e06c75");
        static readonly Color C_SUCCESS = (Color)ColorConverter.ConvertFromString("#98c379");

        // ── État ────────────────────────────────────────────────────────────
        enum Mode { Idle, Running, PauseRunning, Finished }
        Mode   _mode         = Mode.Idle;
        double _timerTotal   = 0;
        double _timerRemain  = 0;
        double _pauseTotal   = 0;
        double _pauseRemain  = 0;
        int    _dailyCount   = 0;

        // ── Ticks ───────────────────────────────────────────────────────────
        readonly DispatcherTimer _clockTick  = new() { Interval = TimeSpan.FromMilliseconds(50) };
        readonly DispatcherTimer _countdownTick = new() { Interval = TimeSpan.FromSeconds(1) };

        // ── Persistance ─────────────────────────────────────────────────────
        static readonly string DataPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChronoZen", "data.json");

        // ── Notification (Windows Toast via System.Windows.Forms fallback) ──
        System.Windows.Forms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _dailyCount = LoadCounter();
            UpdateCountLabel();
            SetupTray();

            _clockTick.Tick += (_, _) => DrawClock();
            _clockTick.Start();

            _countdownTick.Tick += CountdownTick;

            Closing += (_, _) => { _trayIcon?.Dispose(); };
        }

        // ── Tray / Notification ──────────────────────────────────────────────
        void SetupTray()
        {
            try
            {
                _trayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.SystemIcons.Application,
                    Visible = true,
                    Text = "ChronoZen"
                };
            }
            catch { }
        }

        void Notify(string title, string msg)
        {
            try
            {
                _trayIcon?.ShowBalloonTip(5000, title, msg,
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            catch { }
            // Fallback visuel même si tray échoue
            Dispatcher.Invoke(() => LblStatus.Text = $"🔔 {msg}");
        }

        // ── Dessin horloge ───────────────────────────────────────────────────
        void DrawClock()
        {
            var canvas = ClockCanvas;
            canvas.Children.Clear();

            double cx = canvas.Width  / 2;
            double cy = canvas.Height / 2;
            double R  = Math.Min(cx, cy) - 10;

            // Fond
            var bg = new Ellipse
            {
                Width  = R * 2, Height = R * 2,
                Fill   = new SolidColorBrush(C_CARD),
                Stroke = new SolidColorBrush(C_MUTED),
                StrokeThickness = 1
            };
            Canvas.SetLeft(bg, cx - R);
            Canvas.SetTop(bg,  cy - R);
            canvas.Children.Add(bg);

            // Motif décoratif
            DrawPattern(canvas, cx, cy, R);

            // Graduations
            for (int i = 0; i < 60; i++)
            {
                double a  = i * 6.0 - 90;
                double ar = ToRad(a);
                bool isHour = i % 5 == 0;
                double r1 = isHour ? R - 6  : R - 6;
                double r2 = isHour ? R - 18 : R - 11;
                double w  = isHour ? 2 : 1;
                var col   = isHour ? C_TEXT : C_MUTED;
                AddLine(canvas, cx + r1*Cos(ar), cy + r1*Sin(ar),
                                cx + r2*Cos(ar), cy + r2*Sin(ar), col, w);
            }

            // Chiffres
            for (int i = 1; i <= 12; i++)
            {
                double ar = ToRad(i * 30.0 - 90);
                double tx = cx + (R - 30) * Cos(ar);
                double ty = cy + (R - 30) * Sin(ar);
                var tb = new TextBlock
                {
                    Text       = i.ToString(),
                    Foreground = new SolidColorBrush(C_TEXT),
                    FontFamily = new FontFamily("Segoe UI Light"),
                    FontSize   = 9
                };
                tb.Measure(new Size(20, 20));
                Canvas.SetLeft(tb, tx - 6);
                Canvas.SetTop(tb,  ty - 7);
                canvas.Children.Add(tb);
            }

            // Aiguilles
            var now = DateTime.Now;
            double angHr, angMin, angSec;
            Color  colMin, colSec;

            if (_mode == Mode.Running && _timerTotal > 0)
            {
                double frac = 1.0 - (_timerRemain / _timerTotal);
                double elapsed = _timerTotal - _timerRemain;
                angHr  = ToRad((now.Hour % 12 + now.Minute / 60.0) * 30.0 - 90);
                angMin = ToRad(frac * 360.0 - 90);
                angSec = ToRad((elapsed % 60) / 60.0 * 360.0 - 90);
                colMin = C_TEXT; colSec = C_DANGER;
            }
            else if (_mode == Mode.PauseRunning && _pauseTotal > 0)
            {
                double frac = 1.0 - (_pauseRemain / _pauseTotal);
                double elapsed = _pauseTotal - _pauseRemain;
                angHr  = ToRad((now.Hour % 12 + now.Minute / 60.0) * 30.0 - 90);
                angMin = ToRad(frac * 360.0 - 90);
                angSec = ToRad((elapsed % 60) / 60.0 * 360.0 - 90);
                colMin = C_TEAL; colSec = C_TEAL;
            }
            else
            {
                angHr  = ToRad((now.Hour % 12 + now.Minute / 60.0) * 30.0 - 90);
                angMin = ToRad((now.Minute + now.Second / 60.0) * 6.0 - 90);
                angSec = ToRad(now.Second * 6.0 - 90);
                colMin = C_TEXT; colSec = C_DANGER;
            }

            DrawHand(canvas, cx, cy, angHr,  R * 0.45, 3, C_ACCENT);
            DrawHand(canvas, cx, cy, angMin, R * 0.72, 2, colMin);
            DrawHand(canvas, cx, cy, angSec, R * 0.82, 1, colSec);

            // Centre
            var dot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(C_ACCENT) };
            Canvas.SetLeft(dot, cx - 5); Canvas.SetTop(dot, cy - 5);
            canvas.Children.Add(dot);

            // Temps restant au centre
            if (_mode == Mode.Running && _timerRemain >= 0)
                DrawCenterTime(canvas, cx, cy, _timerRemain, C_ACCENT);
            else if (_mode == Mode.PauseRunning)
                DrawCenterTime(canvas, cx, cy, _pauseRemain, C_TEAL);

            // Mise à jour barre de progression
            UpdateProgressBar();
        }

        void DrawHand(Canvas c, double cx, double cy, double angle, double len, double w, Color col)
        {
            // Ombre
            AddLine(c, cx+1, cy+1, cx + len*Cos(angle)+1, cy + len*Sin(angle)+1,
                    Color.FromArgb(60,0,0,0), w + 1);
            AddLine(c, cx, cy, cx + len*Cos(angle), cy + len*Sin(angle), col, w);
        }

        void DrawCenterTime(Canvas c, double cx, double cy, double secs, Color col)
        {
            int m = (int)secs / 60, s = (int)secs % 60;
            string txt = $"{m:D2}:{s:D2}";

            var bg = new Ellipse
            {
                Width = 78, Height = 36,
                Fill   = new SolidColorBrush(C_BG),
                Stroke = new SolidColorBrush(col),
                StrokeThickness = 1
            };
            Canvas.SetLeft(bg, cx - 39); Canvas.SetTop(bg, cy - 18);
            c.Children.Add(bg);

            var tb = new TextBlock
            {
                Text       = txt,
                Foreground = new SolidColorBrush(col),
                FontFamily = new FontFamily("Segoe UI Light"),
                FontSize   = 16,
                Width      = 78,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, cx - 39); Canvas.SetTop(tb, cy - 12);
            c.Children.Add(tb);
        }

        // ── Motifs décoratifs ────────────────────────────────────────────────
        void DrawPattern(Canvas c, double cx, double cy, double R)
        {
            int n = _dailyCount;
            if (n == 0) return;

            // Niveau 1 : cercles pointillés
            if (n >= 1)
            {
                for (int i = 1; i <= Math.Min(n, 3); i++)
                {
                    double r = R * (0.3 + i * 0.12);
                    var el = new Ellipse
                    {
                        Width  = r * 2, Height = r * 2,
                        Stroke = new SolidColorBrush(Color.FromArgb(50, 0x5a, 0x5a, 0x72)),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 2, 4 }
                    };
                    Canvas.SetLeft(el, cx - r); Canvas.SetTop(el, cy - r);
                    c.Children.Add(el);
                }
            }

            // Niveau 2 : polygone inscrit
            if (n >= 3)
            {
                int sides = Math.Min(3 + n, 12);
                var poly  = new Polygon
                {
                    Stroke = new SolidColorBrush(LerpColor(C_MUTED, C_ACCENT, Math.Min(n / 10.0, 1))),
                    StrokeThickness = 1, Fill = Brushes.Transparent
                };
                for (int i = 0; i < sides; i++)
                {
                    double a = ToRad(i * 360.0 / sides - 90);
                    poly.Points.Add(new Point(cx + R * 0.75 * Cos(a), cy + R * 0.75 * Sin(a)));
                }
                c.Children.Add(poly);
            }

            // Niveau 3 : rayons colorés
            if (n >= 5)
            {
                int rays = Math.Min(n * 2, 24);
                for (int i = 0; i < rays; i++)
                {
                    double a   = ToRad(i * 360.0 / rays);
                    double r   = R * 0.55;
                    Color  col = LerpColor(C_ACCENT, C_TEAL, (double)i / rays);
                    AddLine(c, cx, cy, cx + r * Cos(a), cy + r * Sin(a), col, 1);
                }
            }

            // Niveau 4 : spirale
            if (n >= 8)
            {
                int turns = Math.Min(n - 7, 5);
                int steps = turns * 60;
                var pg    = new PathGeometry();
                var pf    = new PathFigure();
                bool first = true;
                for (int i = 0; i < steps; i++)
                {
                    double frac = (double)i / steps;
                    double a    = ToRad(frac * turns * 360);
                    double r    = R * 0.1 + R * 0.6 * frac;
                    var pt      = new Point(cx + r * Cos(a), cy + r * Sin(a));
                    if (first) { pf.StartPoint = pt; first = false; }
                    else pf.Segments.Add(new LineSegment(pt, true));
                }
                pg.Figures.Add(pf);
                c.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = pg,
                    Stroke = new SolidColorBrush(C_TEAL),
                    StrokeThickness = 1
                });
            }

            // Niveau 5 : mandala pétales
            if (n >= 12)
            {
                int petals = Math.Min(6 + n - 12, 18);
                for (int i = 0; i < petals; i++)
                {
                    double a   = ToRad(i * 360.0 / petals);
                    double r1  = R * 0.25, r2 = R * 0.60;
                    Color  col = LerpColor(C_ACCENT, C_TEAL, (double)i / petals);
                    double a2  = a + ToRad(20);
                    var pg = new PathGeometry();
                    var pf = new PathFigure { StartPoint = new Point(cx, cy) };
                    pf.Segments.Add(new LineSegment(new Point(cx + r1*Cos(a2), cy + r1*Sin(a2)), true));
                    pf.Segments.Add(new LineSegment(new Point(cx + r2*Cos(a),  cy + r2*Sin(a)),  true));
                    pg.Figures.Add(pf);
                    c.Children.Add(new System.Windows.Shapes.Path
                    {
                        Data = pg,
                        Stroke = new SolidColorBrush(col),
                        StrokeThickness = 1
                    });
                }
            }
        }

        // ── Barre progression ────────────────────────────────────────────────
        void UpdateProgressBar()
        {
            double containerW = ProgressBar.Parent is Border b ? b.ActualWidth : 460;
            if (_timerTotal > 0 && _mode == Mode.Running)
            {
                double frac = 1.0 - (_timerRemain / _timerTotal);
                ProgressBar.Width = Math.Max(0, containerW * frac);
            }
            else
            {
                ProgressBar.Width = 0;
            }
        }

        // ── Boutons ──────────────────────────────────────────────────────────
        void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            int total = ParseTime(TxtMainMin.Text, TxtMainSec.Text);
            if (total <= 0) return;
            _timerTotal  = total;
            _timerRemain = total;
            _mode = Mode.Running;
            LblStatus.Text = "▶  Timer en cours…";
            LblStatus.Foreground = new SolidColorBrush(C_ACCENT);
            _countdownTick.Start();
        }

        void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _countdownTick.Stop();
            _mode = Mode.Idle;
            _timerRemain = 0;
            LblStatus.Text = "";
        }

        // ── Countdown ────────────────────────────────────────────────────────
        void CountdownTick(object? sender, EventArgs e)
        {
            if (_mode == Mode.Running)
            {
                if (_timerRemain > 0) { _timerRemain--; }
                else { _countdownTick.Stop(); OnTimerDone(); }
            }
            else if (_mode == Mode.PauseRunning)
            {
                if (_pauseRemain > 0) { _pauseRemain--; }
                else { _countdownTick.Stop(); OnPauseDone(); }
            }
        }

        void OnTimerDone()
        {
            _mode = Mode.Finished;
            _dailyCount++;
            SaveCounter(_dailyCount);
            UpdateCountLabel();
            Notify("ChronoZen ⏰", "Votre timer est terminé !");
            ShowDoneDialog();
        }

        void ShowDoneDialog()
        {
            var dlg = new DoneDialog(_dailyCount) { Owner = this };
            dlg.ShowDialog();

            if (dlg.Result == DoneResult.Snooze)
            {
                _timerRemain = 300;
                _mode = Mode.Running;
                LblStatus.Text = "💤  Snooze +5 min…";
                LblStatus.Foreground = new SolidColorBrush(C_TEAL);
                _countdownTick.Start();
            }
            else if (dlg.Result == DoneResult.Pause)
            {
                int pt = ParseTime(TxtPauseMin.Text, TxtPauseSec.Text);
                if (pt > 0)
                {
                    _pauseTotal  = pt;
                    _pauseRemain = pt;
                    _mode = Mode.PauseRunning;
                    LblStatus.Text = "☕  Pause en cours…";
                    LblStatus.Foreground = new SolidColorBrush(C_TEAL);
                    _countdownTick.Start();
                }
                else { _mode = Mode.Idle; LblStatus.Text = ""; }
            }
            else
            {
                _mode = Mode.Idle;
                LblStatus.Text = "";
            }
        }

        void OnPauseDone()
        {
            _mode = Mode.Idle;
            Notify("ChronoZen ☕", "La pause est terminée !");
            LblStatus.Text = "✓  Pause terminée";
            LblStatus.Foreground = new SolidColorBrush(C_SUCCESS);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) => { LblStatus.Text = ""; t.Stop(); };
            t.Start();
        }

        // ── Compteur ─────────────────────────────────────────────────────────
        void UpdateCountLabel()
        {
            int n = _dailyCount;
            string stars = n > 0 ? new string('✦', Math.Min(n, 8)) : "";
            LblCount.Text = n == 0
                ? "Aucun timer complété aujourd'hui"
                : n == 1 ? "1 timer complété aujourd'hui  ✦"
                : $"{n} timers complétés aujourd'hui  {stars}";
        }

        // ── Persistance ──────────────────────────────────────────────────────
        static int LoadCounter()
        {
            try
            {
                var txt  = File.ReadAllText(DataPath);
                var doc  = JsonDocument.Parse(txt);
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                if (doc.RootElement.GetProperty("date").GetString() == today)
                    return doc.RootElement.GetProperty("count").GetInt32();
            }
            catch { }
            return 0;
        }

        static void SaveCounter(int n)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DataPath)!);
                File.WriteAllText(DataPath,
                    $"{{\"date\":\"{DateTime.Today:yyyy-MM-dd}\",\"count\":{n}}}");
            }
            catch { }
        }

        // ── Helpers UI ───────────────────────────────────────────────────────
        void NumOnly(object sender, TextCompositionEventArgs e)
            => e.Handled = !char.IsDigit(e.Text, 0);

        void SelectAll(object sender, RoutedEventArgs e)
            => (sender as TextBox)?.SelectAll();

        void TimeInputChanged(object sender, TextChangedEventArgs e) { }

        static int ParseTime(string min, string sec)
        {
            int.TryParse(min, out int m);
            int.TryParse(sec, out int s);
            return m * 60 + s;
        }

        // ── Helpers géométrie ────────────────────────────────────────────────
        static double ToRad(double deg) => deg * Math.PI / 180;
        static double Cos(double r)     => Math.Cos(r);
        static double Sin(double r)     => Math.Sin(r);

        static void AddLine(Canvas c, double x1, double y1, double x2, double y2,
                            Color col, double w)
        {
            c.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(col),
                StrokeThickness = w,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round
            });
        }

        static Color LerpColor(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
