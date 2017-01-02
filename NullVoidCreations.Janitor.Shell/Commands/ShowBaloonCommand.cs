﻿using System;
using System.Net;
using System.Windows.Controls.Primitives;
using Hardcodet.Wpf.TaskbarNotification;
using NullVoidCreations.Janitor.Shared.Base;
using NullVoidCreations.Janitor.Shell.ViewModels;
using NullVoidCreations.Janitor.Shell.Views;

namespace NullVoidCreations.Janitor.Shell.Commands
{
    public class ShowBaloonCommand: DelegateCommand
    {
        TaskbarIcon _notificationIcon;
        WebClient _client;
        BaloonView _content;

        public ShowBaloonCommand(ViewModelBase viewModel)
            : base(viewModel)
        {
            IsEnabled = true;

            _client = new WebClient();
            _client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(Client_DownloadStringCompleted);
        }

        ~ShowBaloonCommand()
        {
            _client.DownloadStringCompleted -= new DownloadStringCompletedEventHandler(Client_DownloadStringCompleted);
            _client.Dispose();
        }

        void Client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error != null)
                return;

            if (_notificationIcon == null)
                _notificationIcon = (TaskbarIcon)App.Current.Resources["NotificationIcon"];
            if (_content == null)
                _content = new BaloonView();

            (_content.DataContext as BaloonViewModel).Html = e.Result;
            _notificationIcon.ShowCustomBalloon(_content, PopupAnimation.Slide, 60000);
        }

        protected override void ExecuteOverride(object parameter)
        {
            if (parameter == null)
                return;
            
            var uri = new Uri(parameter as string);
            _client.DownloadStringAsync(uri);
        }
    }
}
