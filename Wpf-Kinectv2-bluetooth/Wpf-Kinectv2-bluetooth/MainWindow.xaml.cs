using System;
using System.Windows;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Wpf_Kinectv2_bluetooth
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private RfcommServiceProvider rfcommProvider;
        private StreamSocketListener socketListener;
        private StreamSocket socket;
        private DataWriter writer;
        private DataReader reader;
        public float StartPosition_X;
        public float StartPosition_Y;
        public float StartPosition_Z;
        public int startflag = 1;
        public int connectflag = 1;

        /// <summary>
        /// 時間計測用ストップウォッチ
        /// </summary>
        System.Diagnostics.Stopwatch StopWatch = new System.Diagnostics.Stopwatch();

        public TimeSpan DelayTime1;
        public TimeSpan DelayTime2;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// RfcommServerの初期化
        /// </summary>
        private async void InitializeRfcommServer()
        {
            //UUIDを定義
            Guid RfcommChatServiceUuid = Guid.Parse("17fcf242-f86d-4e35-805e-546ee3040b84");
            //UUIDからRfcommProviderをイニシャライズ
            rfcommProvider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.FromUuid(RfcommChatServiceUuid));

            // SocketListenerを生成，OnConnectionReceivedをコールバックとして登録
            socketListener = new StreamSocketListener();
            socketListener.ConnectionReceived += OnConnectionReceived;

            // Listeningをスタート
            //  
            //awaitはasyncキーワードによって変更された非同期メソッドでのみ使用でる.
            //中断ポイントを挿入することで,メソッドの実行を,待機中のタスクが完了するまで中断する
            //async修飾子を使用して定義され,通常1つ以上のawait式を含むメソッドが,"非同期メソッド"と呼る
            await socketListener.BindServiceNameAsync(rfcommProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

            //SDP属性を設定し，そのSDPレコードを他のデバイスが検索できるようにアドバタイズする
            //
            //SDP(Session Description Protcol):
            //セッションの告知・招待などを必要とするマルチメディアセッションを開始するため
            //必要な情報を記述するためのプレゼンテーション層に属するプロトコル
            InitializeServiceSdpAttributes(rfcommProvider);
            rfcommProvider.StartAdvertising(socketListener, true);
        }

        /// <summary>
        /// SDP属性を設定
        /// </summary>
        /// <param name="rfcommProvider"></param>
        private void InitializeServiceSdpAttributes(RfcommServiceProvider rfcommProvider)
        {
            const UInt16 SdpServiceNameAttributeId = 0x100;
            const byte SdpServiceNameAttributeType = (4 << 3) | 5;
            const string SdpServiceName = "Bluetooth Rfcomm Chat Service";

            var sdpWriter = new DataWriter();

            // 初めに属性タイプを記述
            sdpWriter.WriteByte(SdpServiceNameAttributeType);

            // UTF-8でエンコードされたサービス名SDP属性の長さ。
            sdpWriter.WriteByte((byte)SdpServiceName.Length);

            // UTF-8でエンコードされたサービス名の値。
            sdpWriter.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            sdpWriter.WriteString(SdpServiceName);

            // RFCOMMサービスプロバイダでSDP属性を設定します。
            rfcommProvider.SdpRawAttributes.Add(SdpServiceNameAttributeId, sdpWriter.DetachBuffer());
        }

        /// <summary>
        /// StreamSocketを受け取ったら呼ばれるコールバック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnConnectionReceived(
            StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            // もうリスナーは必要ない
            socketListener.Dispose();
            socketListener = null;
            try
            {
                socket = args.Socket;
            }
            catch (Exception e)
            {
                Disconnect();
                Console.WriteLine(e.Message);
                return;
            }

            // 指定されたソケットからBluetoothデバイスを取得するためのサポートされている方法
            var remoteDevice = await BluetoothDevice.FromHostNameAsync(socket.Information.RemoteHostName);
            Console.WriteLine(socket.Information.RemoteHostName.DisplayName);

            writer = new DataWriter(socket.OutputStream);
            writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            reader = new DataReader(socket.InputStream);
            reader.InputStreamOptions = InputStreamOptions.Partial;
            reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            bool remoteDisconnection = false;
            await Dispatcher.BeginInvoke(
                new Action(() =>
                {
                })
            );

            try
            {
                // Based on the protocol we've defined, the first uint is the size of the message
                uint readLength = await reader.LoadAsync(sizeof(uint));


                //シグナルを送る操作
                if (readLength == 4 && connectflag == 1)
                {
                    await Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                RecordPoints.IsChecked = true;
                            })
                    );
                    readLength = 0;
                    //break;
                    StopWatch.Start();
                    byte[] data = System.Text.Encoding.UTF8.GetBytes("シグナル");
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                    Disconnect();
                }
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x800703E3)
            {
                await Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                    })
                );
            }

            reader.DetachStream();
            if (remoteDisconnection)
            {
                Disconnect();
                await Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                    })
                );
            }
        }

        /// <summary>
        /// 切断
        /// </summary>
        private async void Disconnect()
        {
            if (rfcommProvider != null)
            {
                rfcommProvider.StopAdvertising();
                rfcommProvider = null;
            }

            if (socketListener != null)
            {
                socketListener.Dispose();
                socketListener = null;
            }

            if (writer != null)
            {
                writer.DetachStream();
                writer = null;
            }

            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
            await Dispatcher.BeginInvoke(
                new Action(() => {
                    ListenButton.IsEnabled = true;
                    /*
                    SendButton.IsEnabled = false;
                    */

                })
            );
        }

        /// <summary>
        /// メッセージの受信
        /// </summary>
        private async void ReadMessage()
        {

            try
            {
                MessageBox.Show("実験");
                if (socket != null)
                {
                    //byte[] bytes = new byte[10];
                    MessageBox.Show("1");
                    uint readLength = await reader.LoadAsync(sizeof(uint));
                    MessageBox.Show("2");
                    // Check if the size of the data is expected (otherwise the remote has already terminated the connection)
                    uint currentLength = reader.ReadUInt32();

                    // Load the rest of the message since you already know the length of the data expected.  
                    readLength = await reader.LoadAsync(currentLength);

                    // Check if the size of the data is expected (otherwise the remote has already terminated the connection)
                    string message = reader.ReadString(currentLength);
                    MessageBox.Show(message);
                }
            }
            catch (Exception ex)
            {
                lock (this)
                {
                    if (socket == null)
                    {
                        // Do not print anything here -  the user closed the sock
                    }
                    else
                    {
                        Disconnect();
                    }
                }
            }
        }

        /// <summary>
        /// メッセージの送信
        /// </summary>
        private async void SendMessage()
        {
            // There's no need to send a zero length message
            // Make sure that the connection is still up and there is a message to send
            if (socket != null)
            {
                string message = "1234";
                byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
                /*
                writer.WriteUInt32((uint)message.Length);
                */
                writer.WriteBytes(data);
                //writer.WriteInt16(1);
                // Clear the messageTextBox for a new message

                await writer.StoreAsync();

                var count = await reader.LoadAsync(256);

                while (true)
                {
                    string text = reader.ReadString(count);
                    MessageBox.Show(text);
                    count = await reader.LoadAsync(256);
                }

            }
        }

        /// <summary>
        /// ListeningButtonを押すことでListeningの開始・終了を指定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ListenButton.IsChecked == true)
            {
                InitializeRfcommServer();
                ListenButton.Content = "Stop Listening";
            }
            else
            {
                Disconnect();
                ListenButton.Content = "Start Listening";
            }
        }

        /// <summary>
        /// ReadButtonを押すことでメッセージを受信
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReadButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ReadMessage();
        }

        /// <summary>
        /// SendButtonを押すことでメッセージを送信
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SendMessage();
        }

        private void RecordPoints_Checked(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// DisconnectButtonを押すと切断
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisconnectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Disconnect();
        }
    }
}
