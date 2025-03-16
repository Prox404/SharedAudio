using System.Windows;

namespace SharedAudio;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{

    private BaseDataContext dataContext = BaseDataContext.GetInstance();
    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = dataContext;
        dataContext.StateChanged += (s, a) => ChangeButtonContent(s, a);
    }

    private void ChangeButtonContent(object sender, bool isStarted)
    {
        Start_btn.Content = isStarted ? "Stop" : "Start";
    }

    private void Start_btn_Click(object sender, RoutedEventArgs e)
    {
        if (dataContext.IsServerStart)
        {
            dataContext.StopServer();
            return;
        }
        if (dataContext.IsClientStart)
        {
            dataContext.IsClientStart = false;
            return;
        }


        if (dataContext.IsServer)
        {
            dataContext.StartServer();
        }
        else
        {
            dataContext.StartClient();
        }
    }

    private void Donate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://proxisme.my.canva.site/donation",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            dataContext.Status = "An error occured when navigate to donation url. Pls check on https://proxisme.my.canva.site/donation";
        }
    }
}