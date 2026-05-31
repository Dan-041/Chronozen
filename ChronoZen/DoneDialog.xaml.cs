using System.Windows;

namespace ChronoZen
{
    public enum DoneResult { None, Snooze, Pause }

    public partial class DoneDialog : Window
    {
        public DoneResult Result { get; private set; } = DoneResult.None;

        public DoneDialog(int dailyCount)
        {
            InitializeComponent();
            if (dailyCount > 1)
                TitleBlock.Text = $"⏰  Timer terminé !  ({dailyCount} aujourd'hui)";
        }

        void BtnSnooze_Click(object sender, RoutedEventArgs e)
        {
            Result = DoneResult.Snooze;
            DialogResult = true;
        }

        void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            Result = DoneResult.Pause;
            DialogResult = true;
        }

        void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = DoneResult.None;
            DialogResult = true;
        }
    }
}
