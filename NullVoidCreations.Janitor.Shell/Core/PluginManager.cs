﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NullVoidCreations.Janitor.Shared.Base;

namespace NullVoidCreations.Janitor.Shell.Core
{
    sealed class PluginManager: IObserver
    {
        AppDomain _container;
        readonly Dictionary<string, ScanTargetBase> _targets;
        static PluginManager _instance;
        Version _version;
        readonly string _versionFile;

        #region constructor/destructor

        private PluginManager()
        {
            _version = new Version();
            _targets = new Dictionary<string, ScanTargetBase>();

            Subject.Instance.AddObserver(this);

            // create plugins directory if missing
            if (!Directory.Exists(SettingsManager.Instance.PluginsDirectory))
                Directory.CreateDirectory(SettingsManager.Instance.PluginsDirectory);

            LoadPlugins();
        }

        ~PluginManager()
        {
            Subject.Instance.RemoveObserver(this);
        }

        #endregion

        #region properties

        public static PluginManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PluginManager();

                return _instance;
            }
        }

        public Version Version
        {
            get { return _version; }
        }

        public ScanTargetBase this[string name]
        {
            get
            {
                if (_targets.ContainsKey(name))
                    return _targets[name];

                return null;
            }
        }

        public IEnumerable<ScanTargetBase> Targets
        {
            get { return _targets.Values; }
        }

        #endregion

        public void LoadPlugins()
        {
            UnloadPlugins();

            

            var scanTargetType = typeof(ScanTargetBase);
            var proxyType = typeof(Proxy);
            var proxy = (Proxy)_container.CreateInstanceAndUnwrap(proxyType.Assembly.FullName, proxyType.FullName);

            var pluginFiles = Directory.GetFiles(SettingsManager.Instance.PluginsDirectory, SettingsManager.Instance.PluginsSearchFilter);
            foreach (var pluginFile in pluginFiles)
            {
                var assembly = proxy.GetAssembly(pluginFile);
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(scanTargetType))
                    {
                        var target = (ScanTargetBase) assembly.CreateInstance(type.FullName, false);
                        if (_targets.ContainsKey(target.Name))
                            continue;

                        _targets.Add(target.Name, target);
                    }
                }
            }

            Subject.Instance.NotifyAllObservers(this, MessageCode.PluginsLoaded, Targets);
        }

        public void UnloadPlugins()
        {
            _targets.Clear();
            DestroyContainer();
            CreateContainer();

            Subject.Instance.NotifyAllObservers(this, MessageCode.PluginsUnloaded);
        }

        void CreateContainer()
        {
            var evidence = AppDomain.CurrentDomain.Evidence;

            var setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = SettingsManager.Instance.PluginsDirectory;

            _container = AppDomain.CreateDomain("ScanTargets", evidence, setupInfo);
        }

        Assembly Container_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var fileNamme = string.Format(new AssemblyName(args.Name).Name, ".dll");
            var path = Path.Combine(SettingsManager.Instance.PluginsDirectory, fileNamme);
            if (File.Exists(path))
                return Assembly.LoadFrom(fileNamme);

            return null;
        }

        void DestroyContainer()
        {
            if (_container != null)
                AppDomain.Unload(_container);

            _container = null;
        }


        public void Update(IObserver sender, MessageCode code, params object[] data)
        {
            
        }
    }
}
