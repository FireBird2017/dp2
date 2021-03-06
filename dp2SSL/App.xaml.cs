﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.Remoting;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using DigitalPlatform;
using DigitalPlatform.Core;
using DigitalPlatform.IO;
using DigitalPlatform.LibraryClient;
using DigitalPlatform.Text;

namespace dp2SSL
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application, INotifyPropertyChanged
    {
        // 主要的通道池，用于当前服务器
        public LibraryChannelPool _channelPool = new LibraryChannelPool();

        CancellationTokenSource _cancelRefresh = new CancellationTokenSource();

        Mutex myMutex;

        ErrorTable _errorTable = null;

        #region 属性

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        private string _error = null;   // "test error line asdljasdkf; ;jasldfjasdjkf aasdfasdf";

        public string Error
        {
            get => _error;
            set
            {
                if (_error != value)
                {
                    _error = value;
                    OnPropertyChanged("Error");
                }
            }
        }

        #endregion

        protected override void OnStartup(StartupEventArgs e)
        {
            bool aIsNewInstance = false;
            myMutex = new Mutex(true, "{75BAF3F0-FF7F-46BB-9ACD-8FE7429BF291}", out aIsNewInstance);
            if (!aIsNewInstance)
            {
                MessageBox.Show("dp2SSL 不允许重复启动");
                App.Current.Shutdown();
                return;
            }

            if (DetectVirus.Detect360() || DetectVirus.DetectGuanjia())
            {
                MessageBox.Show("dp2SSL 被木马软件干扰，无法启动");
                System.Windows.Application.Current.Shutdown();
                return;
            }

            _errorTable = new ErrorTable((s) =>
            {
                this.Error = s;
            });

            WpfClientInfo.TypeOfProgram = typeof(App);
            if (StringUtil.IsDevelopMode() == false)
                WpfClientInfo.PrepareCatchException();

            WpfClientInfo.Initial("dp2SSL");
            base.OnStartup(e);

            this._channelPool.BeforeLogin += new DigitalPlatform.LibraryClient.BeforeLoginEventHandle(Channel_BeforeLogin);
            this._channelPool.AfterLogin += new AfterLoginEventHandle(Channel_AfterLogin);

            // InitialFingerPrint();

            // 后台自动检查更新
            Task.Run(() =>
            {
                NormalResult result = WpfClientInfo.InstallUpdateSync();
                if (result.Value == -1)
                    OutputHistory("自动更新出错: " + result.ErrorInfo, 2);
                else if (result.Value == 1)
                    OutputHistory(result.ErrorInfo, 1);
                else if (string.IsNullOrEmpty(result.ErrorInfo) == false)
                    OutputHistory(result.ErrorInfo, 0);
            });

#if REMOVED
            // 用于重试初始化指纹环境的 Timer
            // https://stackoverflow.com/questions/13396582/wpf-user-control-throws-design-time-exception
            _timer = new System.Threading.Timer(
    new System.Threading.TimerCallback(timerCallback),
    null,
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(60));
#endif
            FingerprintManager.Base.Name = "指纹中心";
            FingerprintManager.Url = App.FingerprintUrl;
            FingerprintManager.SetError += FingerprintManager_SetError;
            FingerprintManager.Start(_cancelRefresh.Token);

            RfidManager.Base.Name = "RFID 中心";
            RfidManager.Url = App.RfidUrl;
            RfidManager.SetError += RfidManager_SetError;
            RfidManager.Start(_cancelRefresh.Token);
        }

        private void RfidManager_SetError(object sender, SetErrorEventArgs e)
        {
            SetError("rfid", e.Error);
        }

        private void FingerprintManager_SetError(object sender, SetErrorEventArgs e)
        {
            SetError("fingerprint", e.Error);
        }

#if REMOVED
        // 重新初始化指纹环境
        public List<string> InitialFingerPrint()
        {
            EndFingerprint();
            ClearErrors("fingerprint");
            List<string> errors = TryInitialFingerprint();
            if (errors.Count > 0)
                AddErrors("fingerprint", errors);

            EnableSendkey(false);

            return errors;
        }
#endif

        // TODO: 如何显示后台任务执行信息? 可以考虑只让管理者看到
        public void OutputHistory(string strText, int nWarningLevel = 0)
        {
            // OutputText(DateTime.Now.ToShortTimeString() + " " + strText, nWarningLevel);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LibraryChannelManager.Log.Debug("Begin WpfClientInfo.Finish()");
            WpfClientInfo.Finish();
            LibraryChannelManager.Log.Debug("End WpfClientInfo.Finish()");

            _cancelRefresh.Cancel();

            // EndFingerprint();

            this._channelPool.BeforeLogin -= new DigitalPlatform.LibraryClient.BeforeLoginEventHandle(Channel_BeforeLogin);
            this._channelPool.AfterLogin -= new AfterLoginEventHandle(Channel_AfterLogin);
            this._channelPool.Close();

            base.OnExit(e);
        }

        public static App CurrentApp
        {
            get
            {
                return ((App)Application.Current);
            }
        }

        public void ClearChannelPool()
        {
            this._channelPool.Clear();
        }

        public static string dp2ServerUrl
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "dp2ServerUrl", "");
            }
        }

        public static string dp2UserName
        {
            get
            {
                return WpfClientInfo.Config.Get("global", "dp2UserName", "");
            }
        }

        public static string RfidUrl
        {
            get
            {
                return WpfClientInfo.Config?.Get("global", "rfidUrl", "");
            }
        }

        public static string FingerprintUrl
        {
            get
            {
                return WpfClientInfo.Config?.Get("global", "fingerprintUrl", "");
            }
        }

        public static string dp2Password
        {
            get
            {
                return DecryptPasssword(WpfClientInfo.Config.Get("global", "dp2Password", ""));
            }
        }

#if NO
        // 用于锁屏的密码
        public string LockingPassword
        {
            get
            {
                return DecryptPasssword(WpfClientInfo.Config.Get("global", "lockingPassword", ""));
            }
            set
            {
                WpfClientInfo.Config.Set("global", "lockingPassword", EncryptPassword(value));
            }
        }
#endif

        public static void SetLockingPassword(string password)
        {
            string strSha1 = Cryptography.GetSHA1(password + "_ok");
            WpfClientInfo.Config.Set("global", "lockingPassword", strSha1);
        }

        public static bool MatchLockingPassword(string password)
        {
            string sha1 = WpfClientInfo.Config.Get("global", "lockingPassword", "");
            string current_sha1 = Cryptography.GetSHA1(password + "_ok");
            if (sha1 == current_sha1)
                return true;
            return false;
        }

        public static bool IsLockingPasswordEmpty()
        {
            string sha1 = WpfClientInfo.Config.Get("global", "lockingPassword", "");
            return (string.IsNullOrEmpty(sha1));
        }

        static string EncryptKey = "dp2ssl_client_password_key";

        public static string DecryptPasssword(string strEncryptedText)
        {
            if (String.IsNullOrEmpty(strEncryptedText) == false)
            {
                try
                {
                    string strPassword = Cryptography.Decrypt(
        strEncryptedText,
        EncryptKey);
                    return strPassword;
                }
                catch
                {
                    return "errorpassword";
                }
            }

            return "";
        }

        public static string EncryptPassword(string strPlainText)
        {
            return Cryptography.Encrypt(strPlainText, EncryptKey);
        }

#region LibraryChannel

        internal void Channel_BeforeLogin(object sender,
DigitalPlatform.LibraryClient.BeforeLoginEventArgs e)
        {
            if (e.FirstTry == true)
            {
                {
                    e.UserName = dp2UserName;

                    // e.Password = this.DecryptPasssword(e.Password);
                    e.Password = dp2Password;

#if NO
                    strPhoneNumber = AppInfo.GetString(
        "default_account",
        "phoneNumber",
        "");
#endif

                    bool bIsReader = false;

                    string strLocation = "";

                    e.Parameters = "location=" + strLocation;
                    if (bIsReader == true)
                        e.Parameters += ",type=reader";
                }

                e.Parameters += ",client=dp2ssl|" + WpfClientInfo.ClientVersion;

                if (String.IsNullOrEmpty(e.UserName) == false)
                    return; // 立即返回, 以便作第一次 不出现 对话框的自动登录
                else
                {
                    e.ErrorInfo = "尚未配置 dp2library 服务器用户名";
                    e.Cancel = true;
                }
            }

            // e.ErrorInfo = "尚未配置 dp2library 服务器用户名";
            e.Cancel = true;
        }

        string _currentUserName = "";

        public string ServerUID = "";

        internal void Channel_AfterLogin(object sender, AfterLoginEventArgs e)
        {
            LibraryChannel channel = sender as LibraryChannel;
            _currentUserName = channel.UserName;
            //_currentUserRights = channel.Rights;
            //_currentLibraryCodeList = channel.LibraryCodeList;
        }

        List<LibraryChannel> _channelList = new List<LibraryChannel>();

        public void AbortAllChannel()
        {
            foreach (LibraryChannel channel in _channelList)
            {
                if (channel != null)
                    channel.Abort();
            }
        }

        // parameters:
        //      style    风格。如果为 GUI，表示会自动添加 Idle 事件，并在其中执行 Application.DoEvents
        public LibraryChannel GetChannel()
        {
            string strServerUrl = dp2ServerUrl;

            string strUserName = dp2UserName;

            LibraryChannel channel = this._channelPool.GetChannel(strServerUrl, strUserName);
            _channelList.Add(channel);
            // TODO: 检查数组是否溢出
            return channel;
        }

        public void ReturnChannel(LibraryChannel channel)
        {
            this._channelPool.ReturnChannel(channel);
            _channelList.Remove(channel);
        }

        SpeechSynthesizer m_speech = new SpeechSynthesizer();
        string m_strSpeakContent = "";

        public void Speak(string strText, bool bError = false)
        {
            if (this.m_speech == null)
                return;

            //if (strText == this.m_strSpeakContent)
            //    return; // 正在说同样的句子，不必打断

            this.m_strSpeakContent = strText;

            try
            {
                this.m_speech.SpeakAsyncCancelAll();
                this.m_speech.SpeakAsync(strText);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // TODO: 如何报错?
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            FingerprintManager.EnableSendkey(false);
            RfidManager.EnableSendkey(false);
            base.OnActivated(e);
        }

        protected override void OnDeactivated(EventArgs e)
        {
            // Speak("DeActivated");
            base.OnDeactivated(e);
        }

#endregion

#if REMOVED
        List<string> _errors = new List<string>();

        public List<string> Errors
        {
            get
            {
                return _errors;
            }
        }
#endif

#if REMOVED
        public void AddErrors(List<string> errors)
        {
            DateTime now = DateTime.Now;
            // _errors.AddRange(errors);
            foreach (string error in errors)
            {
                _errors.Add($"{now.ToShortTimeString()} {error}");
            }

            while (_errors.Count > 1000)
            {
                errors.RemoveAt(0);
            }
        }
#endif

        public void AddErrors(string type, List<string> errors)
        {
            DateTime now = DateTime.Now;
            List<string> results = new List<string>();
            foreach (string error in errors)
            {
                results.Add($"{now.ToShortTimeString()} {error}");
            }

            _errorTable.SetError(type, StringUtil.MakePathList(results, "; "));
        }

        public void SetError(string type, string error)
        {
            _errorTable.SetError(type, error);
        }

        public void ClearErrors(string type)
        {
            // _errors.Clear();
            _errorTable.SetError(type, "");
        }

#if NO
        public void EnableSendkey(bool enable)
        {
            try
            {
                if (_fingerprintChannel != null && _fingerprintChannel.Started)
                    _fingerprintChannel?.Object?.EnableSendKey(enable);
            }
            catch
            {
                // TODO: 如何显示出错信息？
            }
        }
#endif

#if NO
        public void ClearFingerprintMessage()
        {
            try
            {
                if (_fingerprintChannel != null && _fingerprintChannel.Started)
                    _fingerprintChannel?.Object?.GetMessage("clear");
            }
            catch
            {
                // TODO: 如何显示出错信息？
            }
        }
#endif

#region 指纹

#if NO
        FingerprintChannel _fingerprintChannel = null;

        public FingerprintChannel FingerprintChannel
        {
            get
            {
                return _fingerprintChannel;
            }
        }
#endif

#if REMOVED
        private static readonly Object _syncRoot_fingerprint = new Object(); // 2017/5/18

        // TODO: 如果没有初始化成功，要提供重试初始化的办法
        public List<string> TryInitialFingerprint()
        {
            lock (_syncRoot_fingerprint)
            {
                // 准备指纹通道
                List<string> errors = new List<string>();
                if (string.IsNullOrEmpty(App.FingerprintUrl) == false
                    && (_fingerprintChannel == null || _fingerprintChannel.Started == false))
                {
#if NO
                eventProxy = new EventProxy();
                eventProxy.MessageArrived +=
                  new MessageArrivedEvent(eventProxy_MessageArrived);
#endif
                    _fingerprintChannel = FingerPrint.StartFingerprintChannel(
                        App.FingerprintUrl,
                        out string strError);
                    if (_fingerprintChannel == null)
                        errors.Add($"启动指纹通道时出错: {strError}");
                    // https://stackoverflow.com/questions/7608826/how-to-remote-events-in-net-remoting
#if NO
                _fingerprintChannel.Object.MessageArrived +=
  new MessageArrivedEvent(eventProxy.LocallyHandleMessageArrived);
#endif
                    try
                    {
                        _fingerprintChannel.Object.GetMessage("clear");
                        _fingerprintChannel.Started = true;
                    }
                    catch (Exception ex)
                    {
                        if (ex is RemotingException && (uint)ex.HResult == 0x8013150b)
                            errors.Add($"启动指纹通道时出错: “指纹中心”({App.FingerprintUrl})没有响应");
                        else
                            errors.Add($"启动指纹通道时出错(2): {ex.Message}");
                    }
                }

                return errors;
            }
        }

        void EndFingerprint()
        {
            if (_fingerprintChannel != null)
            {
                FingerPrint.EndFingerprintChannel(_fingerprintChannel);
                _fingerprintChannel = null;
            }
        }


        Timer _timer = null;


        void timerCallback(object o)
        {
            // 避免重叠启动
            if (_cancelRefresh != null)
                return;

            Refresh();
        }

        int _inRefresh = 0;

        void Refresh()
        {
            if (this._fingerprintChannel?.Started == true)
                return;

            // 防止重入
            int v = Interlocked.Increment(ref this._inRefresh);
            if (v > 1)
            {
                Interlocked.Decrement(ref this._inRefresh);
                return;
            }

            _cancelRefresh = new CancellationTokenSource();
            try
            {
                ClearErrors("fingerprint");  // 2019/5/20
                List<string> errors = TryInitialFingerprint();
                if (errors.Count > 0)
                    AddErrors("fingerprint", errors);
            }
            catch (Exception ex)
            {
                AddErrors("fingerprint", new List<string> { $"重试初始化指纹环境时出现异常: {ex.Message}" });
                return;
            }
            finally
            {
                _cancelRefresh = null;
                Interlocked.Decrement(ref this._inRefresh);
            }
        }
#endif

#endregion

    }
}
