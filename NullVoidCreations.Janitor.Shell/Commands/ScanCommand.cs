﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using NullVoidCreations.Janitor.Core.Models;
using NullVoidCreations.Janitor.Shared.Base;
using NullVoidCreations.Janitor.Shell.Core;
using NullVoidCreations.Janitor.Shell.Models;
using NullVoidCreations.Janitor.Shell.ViewModels;
using NullVoidCreations.Janitor.Shell.Views;

namespace NullVoidCreations.Janitor.Shell.Commands
{
    public class ScanCommand : CommandBase, ISignalObserver
    {
        ComputerScanViewModel _viewModel;
        BackgroundWorker _worker;
        LicenseModel _license;
        bool _isFixPending;
        volatile bool _isCancelled;

        #region constructor / destructor

        public ScanCommand(ComputerScanViewModel viewModel)
            : base(viewModel)
        {
            _viewModel = ViewModel as ComputerScanViewModel;
            IsRecallAllowed = true;

            SignalHost.Instance.AddObserver(this);
        }

        ~ScanCommand()
        {
            SignalHost.Instance.RemoveObserver(this);

            if (_worker == null)
                return;

            _worker.DoWork -= new DoWorkEventHandler(Worker_DoWork);
            _worker.ProgressChanged -= new ProgressChangedEventHandler(Worker_ProgressChanged);
            _worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);
            _worker.Dispose();
        }

        #endregion

        public void SignalReceived(Signal signal, params object[] data)
        {
            switch (signal)
            {
                case Signal.Initialized:
                    SignalReceived(Signal.LicenseChanged, data);
                    break;

                case Signal.ScanStatusChanged:
                    if (_worker.IsBusy)
                        _worker.ReportProgress(-1, data[0]);
                    break;

                case Signal.LicenseChanged:
                    _license = Core.LicenseManager.Instance.License;
                    break;

                case Signal.ScanTrigerred:
                    if (IsExecuting)
                        break;

                    switch ((ScanType)data[0])
                    {
                        case ScanType.SmartScan:
                            Execute("Smart");
                            break;
                    }
                    break;
            }
        }

        public override void Execute(object parameter)
        {
            IsEnabled = true;
            switch (parameter as string)
            {
                case "Smart":
                    if (_viewModel.IsExecuting)
                        break;

                    _viewModel.Scan = new ScanModel(ScanType.SmartScan);
                    StartScan(_viewModel.Scan);
                    break;

                case "Custom":
                    if (_viewModel.IsExecuting)
                        break;

                    _viewModel.Scan = new ScanModel(ScanType.CustomScan);

                    var scanParametersView = new CustomScanView(_viewModel.Scan.Targets);
                    scanParametersView.Owner = Application.Current.MainWindow;
                    var result = scanParametersView.ShowDialog();
                    if (result.HasValue && result.Value == true)
                        StartScan(_viewModel.Scan);
                    break;

                case "Repeat":
                    if (_viewModel.IsExecuting)
                        break;

                    _viewModel.Scan = ScanModel.GetSavedScanDetails();
                    StartScan(_viewModel.Scan);
                    break;

                case "Cancel":
                    CancelScan();
                    break;

                case "Fix":
                    if (_viewModel.IsExecuting || _license == null)
                        break;

                    if (_license.IsTrial)
                    {
                        (UiHelper.Instance.Resources["ShowPopupCommand"] as CommandBase).Execute("LicenseActivation");
                        break;
                    }

                    // start scan if it wasen't done earlier or re-scan if issues are already fixed
                    if (_viewModel.Scan == null || _viewModel.Scan.IsFixed)
                    {
                        _isFixPending = true;
                        _viewModel.Scan = new ScanModel(ScanType.SmartScan);
                        StartScan(_viewModel.Scan);
                        break;
                    }

                    StartFix(_viewModel.Scan);
                    break;
            }
        }

        void StartScan(ScanModel scan)
        {
            if (scan.Type == ScanType.Unknown)
                return;

            SignalHost.Instance.RaiseSignal(Signal.AnalysisStarted, false);
            _viewModel.IsExecuting = IsExecuting = true;

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            _worker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);

            _worker.RunWorkerAsync(new object[] { true, _viewModel.Scan });
        }

        void StartFix(ScanModel scan)
        {
            UiHelper.Instance.Alert("Some issues may not be resolved due to resources locked by running programs. This ensures error free execution of running applications.");

            SignalHost.Instance.RaiseSignal(Signal.FixingStarted);
            _viewModel.IsExecuting = IsExecuting = true;

            _worker = new BackgroundWorker();
            _worker.WorkerSupportsCancellation = true;
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            _worker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);

            _worker.RunWorkerAsync(new object[] { false, _viewModel.Scan });
        }

        void RaiseProgessChanged(
            ScanTargetBase target,
            ScanAreaBase area,
            bool isScanning,
            bool isFixing,
            int targetsScanned,
            int areasScanned,
            long issueCount,
            int progressMax,
            int progressMin,
            int progressCurrent,
            bool isExecuting)
        {
            var status = new ScanStatusModel(target, area, isScanning, isFixing, isExecuting);
            status.TargetScanned = targetsScanned;
            status.AreaScanned = areasScanned;
            status.IssueCount = issueCount;
            status.ProgressMax = progressMax;
            status.ProgressMin = progressMin;
            status.ProgressCurrent = progressCurrent;
            SignalHost.Instance.RaiseSignal(Signal.ScanStatusChanged, status);
        }

        /// <summary>
        /// This method scans for issues.
        /// </summary>
        /// <param name="scan"></param>
        /// <returns></returns>
        ScanModel Analyse(ScanModel scan)
        {
            var issues = new List<IssueBase>();
            var targets = 0;
            var areas = 0;

            var progressCurrent = 0;
            var progressMax = 0;
            foreach (var target in scan.Targets)
                foreach (var area in target.Areas)
                    if (area.IsSelected)
                        progressMax++;

            if (_worker.CancellationPending)
                goto EXIT_SCAN;

            RaiseProgessChanged(null, null, true, false, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
            foreach (var target in scan.Targets)
            {
                if (_worker.CancellationPending)
                    goto EXIT_SCAN;

                targets++;
                RaiseProgessChanged(target, null, true, false, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                foreach (var area in target.Areas)
                {
                    if (area.IsSelected)
                    {
                        if (_worker.CancellationPending)
                            goto EXIT_SCAN;

                        progressCurrent++;
                        areas++;
                        RaiseProgessChanged(target, area, true, false, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                        foreach (var issue in area.Analyse())
                        {
                            if (_worker.CancellationPending)
                                goto EXIT_SCAN;

                            RaiseProgessChanged(target, area, true, false, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                            issues.Add(issue);
                            Thread.Sleep(20);
                        }
                    }
                }
            }

        EXIT_SCAN:
            RaiseProgessChanged(null, null, true, false, targets, areas, issues.Count, progressMax, 0, progressCurrent, false);
            scan.Issues = issues;
            scan.IsCancelled = _isCancelled;

            ScanModel.SaveScanDetails(scan);

            return scan;
        }

        /// <summary>
        /// This method fixes issues found during scan.
        /// </summary>
        /// <param name="scan"></param>
        /// <returns></returns>
        ScanModel Fix(ScanModel scan)
        {
            var issues = new List<IssueBase>();
            var targets = 0;
            var areas = 0;

            var progressCurrent = 0;
            var progressMax = 0;
            foreach (var target in scan.Targets)
                foreach (var area in target.Areas)
                    if (area.IsSelected)
                        progressMax++;

            if (_worker.CancellationPending)
                goto EXIT_SCAN;

            RaiseProgessChanged(null, null, false, true, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
            foreach (var target in scan.Targets)
            {
                if (_worker.CancellationPending)
                    goto EXIT_SCAN;

                targets++;
                RaiseProgessChanged(target, null, false, true, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                foreach (var area in target.Areas)
                {
                    if (area.IsSelected)
                    {
                        if (_worker.CancellationPending)
                            goto EXIT_SCAN;

                        progressCurrent++;
                        areas++;
                        RaiseProgessChanged(target, area, false, true, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                        foreach (var issue in area.Fix())
                        {
                            if (_worker.CancellationPending)
                                goto EXIT_SCAN;

                            RaiseProgessChanged(target, area, false, true, targets, areas, issues.Count, progressMax, 0, progressCurrent, true);
                            issues.Add(issue);
                            Thread.Sleep(20);
                        }
                    }
                }
            }

        EXIT_SCAN:
            RaiseProgessChanged(null, null, false, true, targets, areas, issues.Count, progressMax, 0, progressCurrent, false);
            scan.IsCancelled = _isCancelled;
            scan.Issues = issues;

            ScanModel.SaveScanDetails(scan);

            scan.IsFixed = !scan.IsCancelled;
            return scan;
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _viewModel.ScanStatus = e.UserState as ScanStatusModel;
        }

        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            var parameters = e.Argument as object[];
            var isScanOperation = (bool)parameters[0];
            var activeScan = parameters[1] as ScanModel;

            e.Result = isScanOperation ? Analyse(activeScan) : Fix(activeScan);
        }

        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _worker.DoWork -= new DoWorkEventHandler(Worker_DoWork);
            _worker.ProgressChanged -= new ProgressChangedEventHandler(Worker_ProgressChanged);
            _worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);
            _worker.Dispose();
            _worker = null;

            var scan = e.Result as ScanModel;
            SignalHost.Instance.RaiseSignal(scan.IsFixed ? Signal.FixingStopped : Signal.AnalysisStopped, scan.Issues.Count, scan.IsCancelled);

            _viewModel.Scan = scan;
            _viewModel.IsExecuting = IsExecuting = false;
            _isCancelled = false;

            if (!_viewModel.Scan.IsCancelled && _isFixPending)
            {
                _isFixPending = false;
                Execute("Fix");
            }
            else
                SignalHost.Instance.RaiseSignal(Signal.StopWork);

            if (_viewModel.Scan.IsFixed)
            {
                if (SettingsManager.Instance.CloseAfterFixing)
                    SignalHost.Instance.RaiseSignal(Signal.Close);
                if (SettingsManager.Instance.ShutdownAfterFixing)
                    SignalHost.Instance.RaiseSignal(Signal.Shutdown);
            }
        }

        void CancelScan()
        {
            _isCancelled = true;
            if (_worker != null && _worker.IsBusy)
                _worker.CancelAsync();
        }
    }
}
