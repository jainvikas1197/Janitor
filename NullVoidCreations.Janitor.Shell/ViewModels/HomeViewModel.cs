﻿using System;
using NullVoidCreations.Janitor.Shared.Base;
using NullVoidCreations.Janitor.Shared.Helpers;
using NullVoidCreations.Janitor.Shell.Core;
using NullVoidCreations.Janitor.Shell.Models;
using NullVoidCreations.Janitor.Shell.Views;

namespace NullVoidCreations.Janitor.Shell.ViewModels
{
    public class HomeViewModel: ViewModelBase, IObserver
    {
        readonly CommandBase _load, _activate;
        string _computerName, _operatingSyetem, _processor, _model;
        decimal _memory;
        int _issueCount;
        bool _isLicensed, _isHavingIssues, _isHavingPluginUpdatesAvailable, _isHavingUpdatesAvailable;
        LicenseModel _license;

        #region constructor / destructor

        public HomeViewModel()
        {
            Subject.Instance.AddObserver(this);

            _license = new LicenseModel();
            _computerName = _operatingSyetem = _processor = _model = "Analysing...";

            _load = new AsyncDelegateCommand(this, null, ExecuteGetSystemInformation, null);
            _activate = new DelegateCommand(this, ExecuteActivate);
            _activate.IsEnabled = true;
        }

        ~HomeViewModel()
        {
            Subject.Instance.RemoveObserver(this);
        }

        #endregion

        #region properties

        public LicenseModel License
        {
            get { return _license; }
            private set
            {
                if (value == _license)
                    return;

                _license = value;
                RaisePropertyChanged("License");
            }
        }

        public bool IsLicensed
        {
            get { return _isLicensed; }
            private set
            {
                if (value == _isLicensed)
                    return;

                _isLicensed = value;
                RaisePropertyChanged("IsLicensed");
            }
        }

        public bool IsHavingIssues
        {
            get { return _isHavingIssues; }
            private set
            {
                if (value == _isHavingIssues)
                    return;

                _isHavingIssues = value;
                RaisePropertyChanged("IsHavingIssues");
            }
        }

        public int IssueCount
        {
            get { return _issueCount; }
            private set
            {
                if (value == _issueCount)
                    return;

                _issueCount = value;
                RaisePropertyChanged("IssueCount");
            }
        }

        public bool IsHavingPluginUpdatesAvailable
        {
            get { return _isHavingPluginUpdatesAvailable; }
            private set
            {
                if (value == _isHavingPluginUpdatesAvailable)
                    return;

                _isHavingPluginUpdatesAvailable = value;
                RaisePropertyChanged("IsHavingPluginUpdatesAvailable");
            }
        }

        public bool IsHavingUpdatesAvailable
        {
            get { return _isHavingUpdatesAvailable; }
            private set
            {
                if (value == _isHavingUpdatesAvailable)
                    return;

                _isHavingUpdatesAvailable = value;
                RaisePropertyChanged("IsHavingUpdatesAvailable");
            }
        }

        public string ComputerName
        {
            get { return _computerName; }
            private set
            {
                if (value == _computerName)
                    return;

                _computerName = value;
                RaisePropertyChanged("ComputerName");
            }
        }

        public string Model
        {
            get { return _model; }
            private set
            {
                if (value == _model)
                    return;

                _model = value;
                RaisePropertyChanged("Model");
            }
        }

        public string OperatingSystem
        {
            get { return _operatingSyetem; }
            private set
            {
                if (value == _operatingSyetem)
                    return;

                _operatingSyetem = value;
                RaisePropertyChanged("OperatingSystem");
            }
        }

        public decimal Memory
        {
            get { return _memory; }
            private set
            {
                if (value == _memory)
                    return;

                _memory = value;
                RaisePropertyChanged("Memory");
            }
        }

        public string Processor
        {
            get { return _processor; }
            private set
            {
                if (value == _processor)
                    return;

                _processor = value;
                RaisePropertyChanged("Processor");
            }
        }

        #endregion

        #region commands

        public CommandBase LoadData
        {
            get { return _load; }
        }

        public CommandBase Activate
        {
            get { return _activate; }
        }

        #endregion

        void ExecuteActivate(object parameter)
        {
            var activation = new LicenseActivationView();
            activation.ShowDialog();
        }

        void WeHaveProblems()
        {
            byte problems = 0;
            if (!IsLicensed)
                problems++;
            if (IsHavingIssues)
                problems++;
            if (IsHavingPluginUpdatesAvailable)
                problems++;
            if (IsHavingUpdatesAvailable)
                problems++;

            Subject.Instance.NotifyAllObservers(this, MessageCode.ProblemsAppeared, problems);
        }

        object ExecuteGetSystemInformation(object parameter)
        {
            // load plugins
            PluginManager.Instance.LoadPlugins();

            // load license
            LicenseManager.Instance.LoadLicense();

            // load system information
            SysInformation.Instance.Fill(SysInformation.ManagementClassNames.ComputerSystem);
            SysInformation.Instance.Fill(SysInformation.ManagementClassNames.OperatingSystem);
            SysInformation.Instance.Fill(SysInformation.ManagementClassNames.Processor);
            Subject.Instance.NotifyAllObservers(this, MessageCode.SystemInformationLoaded);

            return null;
        }

        public void Update(IObserver sender, MessageCode code, params object[] data)
        {
            switch(code)
            {
                case MessageCode.SystemInformationLoaded:
                    ComputerName = SysInformation.Instance[SysInformation.ManagementClassNames.ComputerSystem, "Name"] as string;
                    Model = SysInformation.Instance[SysInformation.ManagementClassNames.ComputerSystem, "Model"] as string;
                    Memory = Convert.ToDecimal(SysInformation.Instance[SysInformation.ManagementClassNames.ComputerSystem, "TotalPhysicalMemory"]) / 1024 / 1024 / 1024;
                    OperatingSystem = string.Format("{0} ({1})", SysInformation.Instance[SysInformation.ManagementClassNames.OperatingSystem, "Caption"], SysInformation.Instance[SysInformation.ManagementClassNames.OperatingSystem, "OSArchitecture"]);
                    Processor = SysInformation.Instance[SysInformation.ManagementClassNames.Processor, "Name"] as string;
                    break;

                case MessageCode.LicenseChanged:
                    License = LicenseManager.Instance.License;
                    IsLicensed = License != null && !License.IsTrial;
                    WeHaveProblems();
                    break;

                case MessageCode.ScanStarted:
                    IssueCount = 0;
                    IsHavingIssues = false;
                    WeHaveProblems();
                    break;

                case MessageCode.ScanStopped:
                    IssueCount = (int)data[0];
                    IsHavingIssues = IssueCount > 0;
                    WeHaveProblems();
                    break;
            }
        }
    }
}
