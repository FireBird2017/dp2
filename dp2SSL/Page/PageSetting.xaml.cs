﻿using DigitalPlatform.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace dp2SSL
{
    /// <summary>
    /// PageSetting.xaml 的交互逻辑
    /// </summary>
    public partial class PageSetting : Page, INotifyPropertyChanged
    {
        public PageSetting()
        {
            InitializeComponent();

            this.DataContext = this;

            this.Loaded += PageSetting_Loaded;
        }

        private void PageSetting_Loaded(object sender, RoutedEventArgs e)
        {
            // 首次设置密码，或者登录
            InitialPage();
            if (this.password.Visibility == Visibility.Visible)
                this.password.Focus();
        }

        void InitialPage()
        {
            if (App.IsLockingPasswordEmpty())
            {
                this.passwordArea.Visibility = Visibility.Visible;
                this.buttonArea.Visibility = Visibility.Collapsed;

                this.setPassword.Visibility = Visibility.Visible;
                this.login.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (HasLoggedin == false)
                {
                    this.passwordArea.Visibility = Visibility.Visible;
                    this.buttonArea.Visibility = Visibility.Collapsed;

                    this.setPassword.Visibility = Visibility.Collapsed;
                    this.login.Visibility = Visibility.Visible;
                }
                else
                {
                    this.passwordArea.Visibility = Visibility.Collapsed;
                    this.buttonArea.Visibility = Visibility.Visible;
                }
            }
        }

#if NO
        static App App
        {
            get
            {
                return ((App)Application.Current);
            }
        }
#endif

#region 属性

#if NO
        private void Entities_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Borrowable")
                OnPropertyChanged(e.PropertyName);
        }
#endif

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

#if NO
        public string Borrowable
        {
            get
            {
                return booksControl.Borrowable;
            }
            set
            {
                booksControl.Borrowable = value;
            }
        }
#endif
        private bool _hasLoggedin = false;

        public bool HasLoggedin
        {
            get
            {
                return _hasLoggedin;
            }
            set
            {
                if (_hasLoggedin != value)
                {
                    _hasLoggedin = value;
                    OnPropertyChanged("HasLoggedin");
                }
            }
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


        private void Config_Click(object sender, RoutedEventArgs e)
        {
            Window cfg_window = new ConfigWindow();
            cfg_window.ShowDialog();


            // 迫使 URL 生效
            RfidManager.Url = App.RfidUrl;
            RfidManager.Clear();
            FingerprintManager.Url = App.FingerprintUrl;
            FingerprintManager.Clear();
#if REMOVED
            // 重试初始化指纹环境
            // TODO: 有时候会遇到报错。可能略微延时一下重试就又可以了
            List<string> errors = App.CurrentApp.InitialFingerPrint();
            if (errors.Count > 0)
            {
                MessageBox.Show(StringUtil.MakePathList(errors, "\r\n"));
            }
#endif
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Close();
        }

        private void GoHome_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new PageMenu());
        }

        // 首次设置密码
        private void SetPassword_Click(object sender, RoutedEventArgs e)
        {
            this.Error = null;

            if (string.IsNullOrEmpty(this.password.Password))
            {
                this.Error = "密码不允许为空";
                return;
            }

            App.SetLockingPassword(this.password.Password);
            InitialPage();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            this.Error = null;

            if (App.MatchLockingPassword(this.password.Password) == false)
            {
                this.Error = "密码不正确，登录失败";
                return;
            }

            this.HasLoggedin = true;
            InitialPage();
        }
    }
}
