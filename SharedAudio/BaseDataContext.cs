using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using NAudio.Utils;
using NAudio.Wave;

namespace SharedAudio
{
    public class BaseDataContext : ViewModel
    {

        #region Instance
        private static readonly BaseDataContext thisInstance = new BaseDataContext();
        private static TcpListener listener;
        public CancellationTokenSource cancellationTokenSource;
        private List<TcpClient> connectedClients = new List<TcpClient>();
        private CircularBuffer _circularBuffer = new CircularBuffer(44100 * 2 * 10);
        public EventHandler<bool> StateChanged;
        #endregion

        public static BaseDataContext GetInstance()
        {
            return thisInstance;
        }

        #region Varriable
        private string ip = GetCurrentIPv4Address();
        private int port = 5555;

        public string IP
        {
            get => ip;
            set
            {
                ip = value;
                OnPropertyChanged(nameof(IP));
            }
        }

        public int Port
        {
            get => port;
            set
            {
                port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        private string ipClient = string.Empty;
        private int portClient = 5555;

        public string IPClient
        {
            get => ipClient;
            set
            {
                ipClient = value;
                OnPropertyChanged(nameof(IPClient));
            }
        }

        public int PortClient
        {
            get => portClient;
            set
            {
                portClient = value;
                OnPropertyChanged(nameof(PortClient));
            }
        }

        private string status;
        public string Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        private bool isServerStart = false;
        public bool IsServerStart
        {
            get => isServerStart;
            set
            {
                isServerStart = value;
                OnPropertyChanged(nameof(IsServerStart));
                StateChanged?.Invoke(this, isServerStart);
            }
        }

        private bool isServer = true;
        public bool IsServer
        {
            get => isServer;
            set
            {
                isServer = value;
                OnPropertyChanged(nameof(IsServer));
            }
        }

        private bool isClientStart = false;
        public bool IsClientStart
        {
            get => isClientStart;
            set
            {
                isClientStart = value;
                OnPropertyChanged(nameof(IsClientStart));
                StateChanged?.Invoke(this, isClientStart);
            }
        }
        #endregion

        public static string GetCurrentIPv4Address()
        {
            string ipv4Address = string.Empty;

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipv4Address = address.Address.ToString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(ipv4Address))
                {
                    break;
                }
            }

            return ipv4Address;
        }

        #region Server
        public async void StartServer()
        {
            IsServerStart = true;
            cancellationTokenSource = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Parse(IP), Port);
            listener.Start();

            Status = "Server is running...";

            try
            {
                while (IsServerStart)
                {
                    var client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
                    connectedClients.Add(client);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Status = "Client connected.";
                    });

                    _ = Task.Run(() => TransmitAudio(client, cancellationTokenSource.Token));
                }
            }
            catch (OperationCanceledException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Server stopped.";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Server error: " + ex.Message;
                });
            }
            finally
            {
                foreach (var client in connectedClients)
                {
                    client.Close();
                }
                connectedClients.Clear();

                listener.Stop();
                cancellationTokenSource?.Dispose();
            }
        }

        private void TransmitAudio(TcpClient client, CancellationToken cancellationToken)
        {
            using var waveOut = new WaveOutEvent();
            using var networkStream = client.GetStream();
            using var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new WaveFormat(44100, 16, 2);

            capture.DataAvailable += (sender, e) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    networkStream.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (IOException ex)
                {
                    Console.WriteLine("Error writing to stream: " + ex.Message);
                }
            };

            try
            {
                capture.StartRecording();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Recording and transmitting system audio...";
                });

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Status = "Error starting recording: " + ex.Message;
                });
            }
            finally
            {
                capture.StopRecording();
                client.Close();
            }
        }

        public void StopServer()
        {
            if (IsServerStart)
            {
                IsServerStart = false;
                cancellationTokenSource?.Cancel();
            }
        }
        #endregion

        #region Client
        public async void StartClient()
        {
            IsClientStart = true;
            cancellationTokenSource = new CancellationTokenSource();
            var client = new TcpClient();
            await client.ConnectAsync(IPClient, PortClient);
            var networkStream = client.GetStream();

            var waveOut = new WaveOutEvent();
            var waveProvider = new BufferedWaveProvider(new WaveFormat(44100, 16, 2));

            try
            {

                waveOut.Init(waveProvider);
                waveOut.Play();

                byte[] buffer = new byte[1024];
                int bytesRead;

                Status = "Receiving and playing audio...";

                while (IsClientStart && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                        if (bytesRead > 0)
                        {
                            _circularBuffer.Write(buffer, 0, bytesRead);
                            byte[] playbackBuffer = new byte[1024];
                            int bytesToRead = _circularBuffer.Read(playbackBuffer, 0, playbackBuffer.Length);
                            waveProvider.AddSamples(playbackBuffer, 0, bytesToRead);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException ex)
                    {
                        Status = "Error reading from stream: " + ex.Message;
                        break;
                    }
                    catch (SocketException ex)
                    {
                        Status = "Socket error: " + ex.Message;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Status = "Unexpected error: " + ex.Message;
                        break;
                    }
                }
            }
            finally
            {
                waveOut.Stop();
                waveOut.Dispose();
                _circularBuffer?.Reset();
                client?.Close();
                IsClientStart = false;
                Status = "Audio playback ended.";
            }
        }

        public void StopClient()
        {
            if (IsClientStart)
            {
                IsClientStart = false;
                cancellationTokenSource?.Cancel();
            }
        }
        #endregion
    }
}
