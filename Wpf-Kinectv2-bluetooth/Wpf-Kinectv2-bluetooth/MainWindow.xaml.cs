using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Kinect;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.IO;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Threading.Tasks;
using System.ComponentModel;

namespace Wpf_Kinectv2_bluetooth
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// kinectのインスタンス
        /// </summary>
        public KinectSensor kinect;
        public ColorFrameReader colorFrameReader;
        public FrameDescription colorFrameDesc;
        public byte[] colorBuffer;
        public BodyFrameReader bodyFrameReader;
        public Body[] bodies;

        private RfcommServiceProvider rfcommProvider;
        private StreamSocketListener socketListener;
        private StreamSocket socket;
        private DataWriter writer;
        private DataReader reader;
        public float StartPosition_X;
        public float StartPosition_Y;
        public float StartPosition_Z;
        public float CurrentPosition_X;
        public float CurrentPosition_Y;
        public float CurrentPosition_Z;
        public int startflag = 1;
        public int connectflag = 1;
        public int recordflag = 0;

        /// <summary>
        /// 日付と時刻
        /// </summary>
        static public DateTime dt = DateTime.Now;
        /// <summary>
        /// マイドキュメント下のKinectフォルダへのパス
        /// </summary>
        static public string pathKinect = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/Kinect";
        /// <summary>
        /// 「kinectフォルダのパス+保存先フォルダの名前」フォルダのrenameに使用
        /// </summary>
        /// 
        static public string dateFolderName = pathKinect + "/TrainingData/"
            + dt.Year + digits(dt.Month) + digits(dt.Day) + digits(dt.Hour) + digits(dt.Minute);
        /// <summary>
        /// 保存先のフォルダパス
        /// </summary>
        /// 
        static public string pathSaveFolder = dateFolderName + "/";

        static public string pathImageSaveFolder = pathSaveFolder + "image/";
        /// <summary>
        /// 座標書き込み用ストリーム
        /// </summary>
        private StreamWriter sw = null;
        /// <summary>
        /// 時間計測用ストップウォッチ
        /// </summary>
        public static System.Diagnostics.Stopwatch StopWatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// 画像保存用bitmap source
        /// </summary>
        public static BitmapSource bitmapSource = null;
        /// <summary>
        /// frame数のカウント
        /// </summary>
        static int frameCount = 0;

        public TimeSpan DelayTime1;
        public TimeSpan DelayTime2;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Windowがロードされた時の処理（初期化処理）bluetoothの初期化はボタンで行う
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeKinect();
            InitializeFile();
        }

        /// <summary>
        /// Windowが閉じるときの処理（終了処理）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopWatch.Stop();

            if (sw != null) sw.Close();

            if (Directory.Exists(@pathSaveFolder))
            {
                //Directoryの名前変更
                //Directory.Move(@pathSaveFolder, dateFolderName+"fin/");
            }

            if (colorFrameReader != null)
            {
                colorFrameReader.Dispose();
                colorFrameReader = null;
            }

            if (bodyFrameReader != null)
            {
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            if (kinect != null)
            {
                kinect.Close();
                kinect = null;
            }
        }

        /// <summary>
        /// kinectの初期化
        /// </summary>
        void InitializeKinect()
        {
            try
            {
                //kinectを開く
                kinect = KinectSensor.GetDefault();
                kinect.Open();

                // 抜き差し検出イベントを設定
                kinect.IsAvailableChanged += kinect_IsAvailableChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }
        }

        /// <summary>
        /// ファイル関連の初期化
        /// </summary>
        void InitializeFile()
        {
            //SourceChangeHandlerの登録
            var prop = DependencyPropertyDescriptor.FromProperty
                (System.Windows.Controls.Image.SourceProperty, typeof(System.Windows.Controls.Image));
            prop.AddValueChanged(this.ImageColor, SourceChangedHandler);
        }

        /// <summary>
        /// bitmapSourceが変わった時のイベント
        /// 非同期でファイル書き込みをさせたい
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SourceChangedHandler(object sender, EventArgs e)
        {
            /*
            Func<bool> LongTimeProc = () =>
            {
                //時間のかかる処理
                //System.Threading.Thread.Sleep(100);
                if (RecordPoints.IsChecked == true && RecordPoints.IsChecked == false)
                {
                    using (Stream stream =
                    new FileStream(pathSaveFolder + "image/" + StopWatch.ElapsedMilliseconds + ".jpg", FileMode.Create))
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(stream);
                        stream.Close();
                    }
                }
                return true;
            };

            // 非同期で処理を実行（完了を待機する）
            // 制御がメインスレッドに戻るため、画面全体が固まることはない
            await Task.Run(LongTimeProc);
            */
        }

        //kinect関連
        //----------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Kinectの抜き差し検知イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinect_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // Kinectが接続された
            if (e.IsAvailable)
            {
                Check.Text = "Connect";
                // カラーを設定する
                if (colorFrameReader == null)
                {
                    //カラー画像の情報を作成する（BGRA）
                    colorFrameDesc = kinect.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

                    //データを読み込むカラーリーダーを開くとイベントハンドラの登録
                    colorFrameReader = kinect.ColorFrameSource.OpenReader();
                    colorFrameReader.FrameArrived += colorFrameReader_FrameArrived;
                }

                if (bodyFrameReader == null)
                {
                    // Bodyを入れる配列を作る
                    bodies = new Body[kinect.BodyFrameSource.BodyCount];

                    // ボディーリーダーを開く
                    bodyFrameReader = kinect.BodyFrameSource.OpenReader();
                    bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;
                }
            }
            // Kinectが外された
            else
            {
                Check.Text="UnConnect";
            }
        }

        /// <summary>
        /// カラーフレームを取得した時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void colorFrameReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var colorFrame = e.FrameReference.AcquireFrame())
            {
                if(colorFrame == null)
                {
                    return;
                }
                //BGRAデータを登録
                colorBuffer = new byte[colorFrameDesc.Width * colorFrameDesc.Height * colorFrameDesc.BytesPerPixel];
                colorFrame.CopyConvertedFrameDataToArray(colorBuffer, ColorImageFormat.Bgra);
                
                bitmapSource = BitmapSource.Create(colorFrameDesc.Width, colorFrameDesc.Height, 96, 96,
                PixelFormats.Bgra32, null, colorBuffer, colorFrameDesc.Width * (int)colorFrameDesc.BytesPerPixel);
                //ImageColor.Source = bitmapSource;
                ImageColor.SetCurrentValue(System.Windows.Controls.Image.SourceProperty, bitmapSource);
                
                if (RecordPoints.IsChecked == true && frameCount % 3 ==0)
                {
                    using (Stream stream =
                    new FileStream(pathSaveFolder + "image/" + StopWatch.ElapsedMilliseconds + ".jpg", FileMode.Create))
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(stream);
                        stream.Close();
                    }
                }
                frameCount++;
            }
        }

        /// <summary>
        /// ボディフレームを取得した時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame(e);
            DrawBodyFrame();
        }

        /// <summary>
        /// ボディの更新
        /// </summary>
        /// <param name="e"></param>
        private void UpdateBodyFrame(BodyFrameArrivedEventArgs e)
        {
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame == null)
                {
                    return;
                }

                // ボディデータを取得する
                bodyFrame.GetAndRefreshBodyData(bodies);
            }
        }

        /// <summary>
        /// ボディの表示
        /// </summary>
        private void DrawBodyFrame()
        {
            CanvasBody.Children.Clear();
            foreach (var body in bodies.Where(b => b.IsTracked))
            {
                foreach (var joint in body.Joints)
                {
                    if (StopWatch.ElapsedMilliseconds >= 66000)
                    {
                        RecordPoints.IsChecked = false;
                        StopWatch.Reset();
                        sw.WriteLine();
                        sw.Close();
                        RecordPoints.Content = "Start Record";

                    }

                    if(recordflag == 1)
                    {
                        recordflag = 0;
                        StartPosition_X = joint.Value.Position.X;
                        StartPosition_Y = joint.Value.Position.Y;
                        StartPosition_Z = joint.Value.Position.Z;
                    }
                    //左手の座標を表示
                    if (joint.Value.JointType == JointType.HandRight)
                    {
                        DrawEllipse(joint.Value, 10, System.Windows.Media.Brushes.Red);

                        //記録ボタンが押してあるなら座標を書き込み
                        if (RecordPoints.IsChecked == true)
                        {
                            CurrentPosition_X = joint.Value.Position.X - StartPosition_X;
                            CurrentPosition_Y = joint.Value.Position.Y - StartPosition_Y;
                            CurrentPosition_Z = joint.Value.Position.Z - StartPosition_Z;
                            sw.WriteLine(StopWatch.ElapsedMilliseconds + ","
                            + CurrentPosition_X + ","
                            + CurrentPosition_Y + ","
                            + CurrentPosition_Z);
                        }
                    }
                    else
                    {
                        //他の関節の描画
                        //DrawEllipse(joint.Value, 10, System.Windows.Media.Brushes.Blue);
                    }
                }
            }
        }

        /// <summary>
        /// 関節の描画
        /// </summary>
        /// <param name="joint"></param>
        /// <param name="R"></param>
        /// <param name="brush"></param>
        private void DrawEllipse(Joint joint, int R, System.Windows.Media.Brush brush)
        {
            var ellipse = new Ellipse()
            {
                Width = R,
                Height = R,
                Fill = brush,
            };

            // カメラ座標系をDepth座標系に変換する
            var point = kinect.CoordinateMapper.MapCameraPointToDepthSpace(joint.Position);
            if ((point.X < 0) || (point.Y < 0))
            {
                return;
            }

            // Depth座標系で円を配置する
            Canvas.SetLeft(ellipse, point.X - (R / 2));
            Canvas.SetTop(ellipse, point.Y - (R / 2));

            CanvasBody.Children.Add(ellipse);
        }

        //bluetooth
        //----------------------------------------------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// ConnectButtonを押すことでListeningの開始・終了を指定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ConnectButton.IsChecked == true)
            {
                InitializeRfcommServer();
                ConnectButton.Content = "Stop Listening";
                //初めて押したとき
                if (sw == null)
                {
                    //MY Document直下にKinectフォルダを作成
                    Directory.CreateDirectory(pathKinect);
                    //ファイル書き込み用のdirectoryを用意
                    Directory.CreateDirectory(pathSaveFolder);
                    //画像書き込み用のdirectoryを用意
                    Directory.CreateDirectory(pathImageSaveFolder);

                    //座標書き込み用csvファイルを用意
                    sw = new StreamWriter(pathSaveFolder + "Points.csv", true);

                }
            }
            else
            {
                Disconnect();
                ConnectButton.Content = "Start Listening";
            }
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
            sdpWriter.UnicodeEncoding = UnicodeEncoding.Utf8;
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
            Console.WriteLine("ddd");

            writer = new DataWriter(socket.OutputStream);
            writer.UnicodeEncoding = UnicodeEncoding.Utf8;

            reader = new DataReader(socket.InputStream);
            reader.InputStreamOptions = InputStreamOptions.Partial;
            reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            //reader.ByteOrder = ByteOrder.LittleEndian;

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
                uint bytesToRead = reader.ReadUInt32();
                //Console.WriteLine(reader.ReadString(bytesToRead)+"aaaaaaaaaaa");
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
                    recordflag = 1;
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
                    ConnectButton.IsEnabled = true;
                })
            );
        }

        //ファイル書き込み
        //----------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// 座標記録ボタンのイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordPoints_Check(object sender, RoutedEventArgs e)
        {
            StopWatch.Start();
            ConnectButton.Content = "Start Listening";
        }

        /// <summary>
        /// 1桁の場合の桁の補正：1時1分→0101
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        static public String digits(int date)
        {
            if (date / 10 == 0) return "0" + date;
            else return date.ToString();
        }
    }
}
