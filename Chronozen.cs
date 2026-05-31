using System;
using System.Collections.Generic;
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
    public static class EntryPoint
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }

    static class C
    {
        public static readonly Color BG      = Hex("#0e0e12");
        public static readonly Color CARD    = Hex("#1c1c26");
        public static readonly Color SURFACE = Hex("#16161e");
        public static readonly Color ACCENT  = Hex("#c8a96e");
        public static readonly Color TEAL    = Hex("#6ec8b4");
        public static readonly Color TEXT    = Hex("#e8e4da");
        public static readonly Color MUTED   = Hex("#5a5a72");
        public static readonly Color DANGER  = Hex("#e06c75");
        public static readonly Color SUCCESS = Hex("#98c379");
        public static SolidColorBrush B(Color c) => new(c);
        static Color Hex(string h) => (Color)ColorConverter.ConvertFromString(h);
        public static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }
    }

    public class MainWindow : Window
    {
        enum Mode { Idle, Running, PauseRunning }
        Mode   _mode        = Mode.Idle;
        double _timerTotal  = 0;
        double _timerRemain = 0;
        double _pauseTotal  = 0;
        double _pauseRemain = 0;
        int    _dailyCount  = 0;
        Canvas    _clock         = null!;
        TextBox   _minBox        = null!;
        TextBox   _secBox        = null!;
        TextBox   _pMinBox       = null!;
        TextBox   _pSecBox       = null!;
        Border    _progFill      = null!;
        Border    _progContainer = null!;
        TextBlock _lblStatus     = null!;
        TextBlock _lblCount      = null!;
        Button    _btnStart      = null!;
        readonly DispatcherTimer _clockTimer     = new() { Interval = TimeSpan.FromMilliseconds(50) };
        readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        System.Windows.Forms.NotifyIcon? _tray;
        static readonly string DataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChronoZen", "data.json");

        public MainWindow()
        {
            Title = "ChronoZen"; Width = 520; Height = 760;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = C.B(C.BG);
            FontFamily = new FontFamily("Segoe UI");
            _dailyCount = LoadCounter();
            BuildUI();
            SetupTray();
            _clockTimer.Tick += (_, _) => DrawClock();
            _countdownTimer.Tick += CountdownTick;
            _clockTimer.Start();
            Closing += (_, _) => _tray?.Dispose();
        }

        void SetupTray()
        {
            try { _tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Time, Visible = true, Text = "ChronoZen" }; }
            catch { }
        }

        void Notify(string title, string msg)
        {
            try { _tray?.ShowBalloonTip(6000, title, msg, System.Windows.Forms.ToolTipIcon.Info); }
            catch { }
        }

        void BuildUI()
        {
            var root = new StackPanel { Background = C.B(C.BG) };
            root.Children.Add(new TextBlock { Text = "C H R O N O Z E N", Foreground = C.B(C.ACCENT), FontFamily = new FontFamily("Segoe UI Light"), FontSize = 15, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20, 0, 8) });
            _clock = new Canvas { Width = 360, Height = 360 };
            root.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10), Child = _clock });
            var cardMain = new Border { Background = C.B(C.CARD), CornerRadius = new CornerRadius(6), Margin = new Thickness(28, 0, 28, 0), Padding = new Thickness(20, 14, 20, 14) };
            var stackMain = new StackPanel();
            stackMain.Children.Add(new TextBlock { Text = "TIMER PRINCIPAL", Foreground = C.B(C.MUTED), FontSize = 9, Margin = new Thickness(0, 0, 0, 10) });
            var rowMain = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _minBox = MakeTimeBox(C.CARD); _minBox.Text = "05"; rowMain.Children.Add(_minBox);
            rowMain.Children.Add(new TextBlock { Text = ":", Foreground = C.B(C.ACCENT), FontFamily = new FontFamily("Segoe UI Light"), FontSize = 30, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 8) });
            _secBox = MakeTimeBox(C.CARD); _secBox.Text = "00"; rowMain.Children.Add(_secBox);
            _btnStart = MakeButton("DÉBUT", C.ACCENT, C.BG); _btnStart.Margin = new Thickness(16, 0, 0, 0); _btnStart.Click += BtnStart_Click; rowMain.Children.Add(_btnStart);
            var btnStop = MakeButton("STOP", C.SURFACE, C.MUTED); btnStop.Margin = new Thickness(6, 0, 0, 0); btnStop.Click += BtnStop_Click; rowMain.Children.Add(btnStop);
            stackMain.Children.Add(rowMain);
            _progFill = new Border { Background = C.B(C.ACCENT), Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            _progContainer = new Border { Background = C.B(C.SURFACE), Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 12, 0, 0), Child = _progFill };
            stackMain.Children.Add(_progContainer); cardMain.Child = stackMain; root.Children.Add(cardMain);
            var cardPause = new Border { Background = C.B(C.SURFACE), CornerRadius = new CornerRadius(6), Margin = new Thickness(28, 10, 28, 0), Padding = new Thickness(20, 14, 20, 14) };
            var stackPause = new StackPanel();
            stackPause.Children.Add(new TextBlock { Text = "PAUSE", Foreground = C.B(C.TEAL), FontSize = 9, Margin = new Thickness(0, 0, 0, 10) });
            var rowPause = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _pMinBox = MakeTimeBox(C.SURFACE); _pMinBox.Text = "05"; rowPause.Children.Add(_pMinBox);
            rowPause.Children.Add(new TextBlock { Text = ":", Foreground = C.B(C.TEAL), FontFamily = new FontFamily("Segoe UI Light"), FontSize = 30, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 8) });
            _pSecBox = MakeTimeBox(C.SURFACE); _pSecBox.Text = "00"; rowPause.Children.Add(_pSecBox);
            stackPause.Children.Add(rowPause); cardPause.Child = stackPause; root.Children.Add(cardPause);
            _lblStatus = new TextBlock { Foreground = C.B(C.ACCENT), FontSize = 11, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
            _lblCount  = new TextBlock { Foreground = C.B(C.MUTED),  FontSize = 9,  TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 14) };
            root.Children.Add(_lblStatus); root.Children.Add(_lblCount);
            UpdateCountLabel();
            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        TextBox MakeTimeBox(Color bg)
        {
            var tb = new TextBox { Background = C.B(bg), Foreground = C.B(C.TEXT), CaretBrush = C.B(C.ACCENT), BorderBrush = C.B(C.MUTED), BorderThickness = new Thickness(1), FontFamily = new FontFamily("Segoe UI Light"), FontSize = 30, TextAlignment = TextAlignment.Center, Width = 70, Height = 50, VerticalContentAlignment = VerticalAlignment.Center, MaxLength = 2 };
            tb.PreviewTextInput += (_, e) => e.Handled = !char.IsDigit(e.Text, 0);
            tb.GotFocus += (_, _) => tb.SelectAll();
            return tb;
        }

        Button MakeButton(string text, Color bg, Color fg) => new() { Content = text, Background = C.B(bg), Foreground = C.B(fg), BorderThickness = new Thickness(0), Padding = new Thickness(18, 0, 18, 0), Height = 50, FontFamily = new FontFamily("Segoe UI Semibold"), FontSize = 10, Cursor = Cursors.Hand };

        void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            int total = ParseTime(_minBox.Text, _secBox.Text);
            if (total <= 0) return;
            _timerTotal = total; _timerRemain = total; _mode = Mode.Running;
            _lblStatus.Text = "▶  Timer en cours…"; _lblStatus.Foreground = C.B(C.ACCENT);
            _countdownTimer.Start();
        }

        void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _countdownTimer.Stop(); _mode = Mode.Idle; _timerRemain = 0;
            _lblStatus.Text = ""; _progFill.Width = 0;
        }

        void CountdownTick(object? sender, EventArgs e)
        {
            if (_mode == Mode.Running) { if (_timerRemain > 0) _timerRemain--; else { _countdownTimer.Stop(); OnTimerDone(); } }
            else if (_mode == Mode.PauseRunning) { if (_pauseRemain > 0) _pauseRemain--; else { _countdownTimer.Stop(); OnPauseDone(); } }
        }

        void OnTimerDone()
        {
            _dailyCount++; SaveCounter(_dailyCount); UpdateCountLabel();
            Notify("ChronoZen ⏰", "Votre timer est terminé !");
            var dlg = new DoneWindow(_dailyCount) { Owner = this };
            dlg.ShowDialog();
            if (dlg.Result == DoneResult.Snooze) { _timerRemain = 300; _mode = Mode.Running; _lblStatus.Text = "💤  Snooze +5 min…"; _lblStatus.Foreground = C.B(C.TEAL); _countdownTimer.Start(); }
            else if (dlg.Result == DoneResult.Pause) { int pt = ParseTime(_pMinBox.Text, _pSecBox.Text); if (pt > 0) { _pauseTotal = pt; _pauseRemain = pt; _mode = Mode.PauseRunning; _lblStatus.Text = "☕  Pause en cours…"; _lblStatus.Foreground = C.B(C.TEAL); _countdownTimer.Start(); } else { _mode = Mode.Idle; _lblStatus.Text = ""; } }
            else { _mode = Mode.Idle; _lblStatus.Text = ""; }
        }

        void OnPauseDone()
        {
            _mode = Mode.Idle; Notify("ChronoZen ☕", "La pause est terminée !");
            _lblStatus.Text = "✓  Pause terminée"; _lblStatus.Foreground = C.B(C.SUCCESS);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) => { _lblStatus.Text = ""; t.Stop(); }; t.Start();
        }

        void UpdateCountLabel()
        {
            int n = _dailyCount;
            _lblCount.Text = n == 0 ? "Aucun timer complété aujourd'hui" : n == 1 ? "1 timer complété aujourd'hui  ✦" : $"{n} timers complétés aujourd'hui  {new string('✦', Math.Min(n, 8))}";
        }

        void DrawClock()
        {
            _clock.Children.Clear();
            double cx = 180, cy = 180, R = 165;
            Add(_clock, new Ellipse { Width = R*2, Height = R*2, Fill = C.B(C.CARD), Stroke = C.B(C.MUTED), StrokeThickness = 1 }, cx-R, cy-R);
            DrawPattern(cx, cy, R);
            for (int i = 0; i < 60; i++) { double a = Rad(i*6.0-90); bool h = i%5==0; AddLine(_clock, cx+(R-6)*Cos(a), cy+(R-6)*Sin(a), cx+(h?R-18:R-11)*Cos(a), cy+(h?R-18:R-11)*Sin(a), h?C.TEXT:C.MUTED, h?2:1); }
            for (int i = 1; i <= 12; i++) { double a = Rad(i*30.0-90); Add(_clock, new TextBlock { Text=i.ToString(), Foreground=C.B(C.TEXT), FontFamily=new FontFamily("Segoe UI Light"), FontSize=9 }, cx+(R-30)*Cos(a)-6, cy+(R-30)*Sin(a)-7); }
            var now = DateTime.Now;
            double angHr, angMin, angSec; Color cMin, cSec;
            if (_mode == Mode.Running && _timerTotal > 0) { double frac=1.0-_timerRemain/_timerTotal, el=_timerTotal-_timerRemain; angHr=Rad((now.Hour%12+now.Minute/60.0)*30-90); angMin=Rad(frac*360-90); angSec=Rad(el%60/60.0*360-90); cMin=C.TEXT; cSec=C.DANGER; }
            else if (_mode == Mode.PauseRunning && _pauseTotal > 0) { double frac=1.0-_pauseRemain/_pauseTotal, el=_pauseTotal-_pauseRemain; angHr=Rad((now.Hour%12+now.Minute/60.0)*30-90); angMin=Rad(frac*360-90); angSec=Rad(el%60/60.0*360-90); cMin=C.TEAL; cSec=C.TEAL; }
            else { angHr=Rad((now.Hour%12+now.Minute/60.0)*30-90); angMin=Rad((now.Minute+now.Second/60.0)*6-90); angSec=Rad(now.Second*6.0-90); cMin=C.TEXT; cSec=C.DANGER; }
            DrawHand(cx, cy, angHr, R*0.45, 3, C.ACCENT); DrawHand(cx, cy, angMin, R*0.72, 2, cMin); DrawHand(cx, cy, angSec, R*0.82, 1, cSec);
            Add(_clock, new Ellipse { Width=10, Height=10, Fill=C.B(C.ACCENT) }, cx-5, cy-5);
            if (_mode == Mode.Running) DrawCenterTime(cx, cy, _timerRemain, C.ACCENT);
            else if (_mode == Mode.PauseRunning) DrawCenterTime(cx, cy, _pauseRemain, C.TEAL);
            UpdateProg();
        }

        void DrawHand(double cx, double cy, double a, double len, double w, Color col)
        {
            AddLine(_clock, cx+1, cy+1, cx+len*Cos(a)+1, cy+len*Sin(a)+1, Color.FromArgb(50,0,0,0), w+1);
            AddLine(_clock, cx, cy, cx+len*Cos(a), cy+len*Sin(a), col, w);
        }

        void DrawCenterTime(double cx, double cy, double secs, Color col)
        {
            int m=(int)secs/60, s=(int)secs%60;
            Add(_clock, new Ellipse { Width=80, Height=36, Fill=C.B(C.BG), Stroke=C.B(col), StrokeThickness=1 }, cx-40, cy-18);
            Add(_clock, new TextBlock { Text=$"{m:D2}:{s:D2}", Foreground=C.B(col), FontFamily=new FontFamily("Segoe UI Light"), FontSize=16, Width=80, TextAlignment=TextAlignment.Center }, cx-40, cy-12);
        }

        void DrawPattern(double cx, double cy, double R)
        {
            int n = _dailyCount; if (n == 0) return;
            for (int i = 1; i <= Math.Min(n,3); i++) { double r=R*(0.3+i*0.12); Add(_clock, new Ellipse { Width=r*2, Height=r*2, Stroke=new SolidColorBrush(Color.FromArgb(50,0x5a,0x5a,0x72)), StrokeThickness=1, StrokeDashArray=new DoubleCollection{2,4} }, cx-r, cy-r); }
            if (n >= 3) { int sides=Math.Min(3+n,12); var poly=new Polygon{Stroke=C.B(C.Lerp(C.MUTED,C.ACCENT,Math.Min(n/10.0,1))),StrokeThickness=1,Fill=Brushes.Transparent}; for(int i=0;i<sides;i++){double a=Rad(i*360.0/sides-90);poly.Points.Add(new Point(cx+R*0.75*Cos(a),cy+R*0.75*Sin(a)));} _clock.Children.Add(poly); }
            if (n >= 5) { int rays=Math.Min(n*2,24); for(int i=0;i<rays;i++){double a=Rad(i*360.0/rays);AddLine(_clock,cx,cy,cx+R*0.55*Cos(a),cy+R*0.55*Sin(a),C.Lerp(C.ACCENT,C.TEAL,(double)i/rays),1);} }
            if (n >= 8) { int turns=Math.Min(n-7,5),steps=turns*60; var pf=new PathFigure(); var pg=new PathGeometry(); bool first=true; for(int i=0;i<steps;i++){double f=(double)i/steps,a=Rad(f*turns*360),r=R*(0.1+0.6*f);var pt=new Point(cx+r*Cos(a),cy+r*Sin(a));if(first){pf.StartPoint=pt;first=false;}else pf.Segments.Add(new LineSegment(pt,true));} pg.Figures.Add(pf); _clock.Children.Add(new System.Windows.Shapes.Path{Data=pg,Stroke=C.B(C.TEAL),StrokeThickness=1}); }
            if (n >= 12) { int petals=Math.Min(6+n-12,18); for(int i=0;i<petals;i++){double a=Rad(i*360.0/petals),a2=a+Rad(20);var pf=new PathFigure{StartPoint=new Point(cx,cy)};pf.Segments.Add(new LineSegment(new Point(cx+R*0.25*Cos(a2),cy+R*0.25*Sin(a2)),true));pf.Segments.Add(new LineSegment(new Point(cx+R*0.60*Cos(a),cy+R*0.60*Sin(a)),true));var pg=new PathGeometry();pg.Figures.Add(pf);_clock.Children.Add(new System.Windows.Shapes.Path{Data=pg,Stroke=C.B(C.Lerp(C.ACCENT,C.TEAL,(double)i/petals)),StrokeThickness=1});} }
        }

        void UpdateProg() { double w=_progContainer.ActualWidth; if(w<=0)w=420; _progFill.Width=(_mode==Mode.Running&&_timerTotal>0)?Math.Max(0,w*(1.0-_timerRemain/_timerTotal)):0; }
        static void Add(Canvas c, UIElement el, double x, double y) { Canvas.SetLeft(el,x); Canvas.SetTop(el,y); c.Children.Add(el); }
        static void AddLine(Canvas c, double x1, double y1, double x2, double y2, Color col, double w) => c.Children.Add(new Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=C.B(col),StrokeThickness=w,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        static double Rad(double d) => d*Math.PI/180;
        static double Cos(double r) => Math.Cos(r);
        static double Sin(double r) => Math.Sin(r);
        static int ParseTime(string m, string s) { int.TryParse(m,out int mm); int.TryParse(s,out int ss); return mm*60+ss; }
        static int LoadCounter() { try{var d=JsonDocument.Parse(File.ReadAllText(DataPath));if(d.RootElement.GetProperty("date").GetString()==DateTime.Today.ToString("yyyy-MM-dd"))return d.RootElement.GetProperty("count").GetInt32();}catch{} return 0; }
        static void SaveCounter(int n) { try{Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);File.WriteAllText(DataPath,$"{{\"date\":\"{DateTime.Today:yyyy-MM-dd}\",\"count\":{n}}}}");}catch{} }
    }

    public enum DoneResult { None, Snooze, Pause }

    public class DoneWindow : Window
    {
        public DoneResult Result { get; private set; } = DoneResult.None;
        public DoneWindow(int count)
        {
            Title="Timer terminé"; Width=360; Height=190; ResizeMode=ResizeMode.NoResize;
            WindowStartupLocation=WindowStartupLocation.CenterOwner; Background=C.B(C.CARD);
            var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(20)};
            stack.Children.Add(new TextBlock{Text=count>1?$"⏰  Timer terminé !  ({count} aujourd'hui)":"⏰  Timer terminé !",Foreground=C.B(C.ACCENT),FontFamily=new FontFamily("Segoe UI Light"),FontSize=15,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,0,0,24)});
            var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            var bSnooze=Btn("💤 Snooze +5 min",C.ACCENT,C.BG); var bPause=Btn("☕ Pause",C.TEAL,C.BG); var bClose=Btn("✕ Fermer",C.SURFACE,C.TEXT);
            bSnooze.Click+=(_,_)=>{Result=DoneResult.Snooze;DialogResult=true;}; bPause.Click+=(_,_)=>{Result=DoneResult.Pause;DialogResult=true;}; bClose.Click+=(_,_)=>{Result=DoneResult.None;DialogResult=true;};
            row.Children.Add(bSnooze); row.Children.Add(bPause); row.Children.Add(bClose);
            stack.Children.Add(row); Content=stack;
        }
        static Button Btn(string txt,Color bg,Color fg)=>new(){Content=txt,Background=C.B(bg),Foreground=C.B(fg),BorderThickness=new Thickness(0),Padding=new Thickness(14,8,14,8),Margin=new Thickness(5,0,5,0),FontFamily=new FontFamily("Segoe UI Semibold"),FontSize=9,Cursor=Cursors.Hand,Height=38};
    }
}
