﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using NullVoidCreations.Janitor.Shared;
using NullVoidCreations.Janitor.Shared.Base;
using NullVoidCreations.Janitor.Shared.Helpers;
using NullVoidCreations.Janitor.Shared.Models;

namespace NullVoidCreations.Janitor.Shell.Core
{
    [Serializable]
    sealed class PluginManager
    {
        //AppDomain _container;
        readonly Dictionary<string, ScanTargetBase> _targets;
        static PluginManager _instance;

        #region constructor/destructor

        private PluginManager()
        {
            _targets = new Dictionary<string, ScanTargetBase>();

            // create plugins directory if missing
            if (!Directory.Exists(Constants.PluginsDirectory))
                Directory.CreateDirectory(Constants.PluginsDirectory);

            // install any pending plugins update
            /*
            if (File.Exists(UpdateCommand.PluginsUpdateFile))
                UpdatePlugins(UpdateCommand.PluginsUpdateFile);
             */
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
            get { return SettingsManager.Instance.PluginsVersion; }
            internal set
            {
                if (value == SettingsManager.Instance.PluginsVersion)
                    return;

                SettingsManager.Instance.PluginsVersion = value;
            }
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

        bool UpdatePlugins(string archiveFile)
        {
            if (string.IsNullOrEmpty(archiveFile))
                return false;
            if (!File.Exists(archiveFile))
                return false;

            var isUpdated = true;
            UnloadPlugins();
            var zip = ZipStorer.Open(archiveFile, FileAccess.Read);
            var directory = zip.ReadCentralDir();
            foreach (var entry in directory)
            {
                var fileName = Path.GetFileName(entry.FilenameInZip);
                var filePath = Path.Combine(Constants.PluginsDirectory, fileName);
                zip.ExtractFile(entry, filePath);
            }

            FileSystemHelper.Instance.DeleteFile(archiveFile);

            return isUpdated;
        }

        public void LoadPlugins()
        {
            UnloadPlugins();

            var pluginFiles = Directory.GetFiles(Constants.PluginsDirectory, Constants.PluginsSearchFilter);
            foreach (var pluginFile in pluginFiles)
            {
                var document = new XmlDocument();
                document.Load(pluginFile);

                var target = new ScanTargetModel(document);
                if (_targets.ContainsKey(target.Name))
                    continue;

                _targets.Add(target.Name, target);

                /*
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
                 */
            }

            SignalHost.Instance.RaiseSignal(Signal.PluginsLoaded, Targets);
        }

        public void UnloadPlugins()
        {
            _targets.Clear();

            /*
            DestroyContainer();
            CreateContainer();
             */

            SignalHost.Instance.RaiseSignal(Signal.PluginsUnloaded);
        }

        Assembly FindPlugin(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);

            var pluginAssembly = Path.Combine(Constants.PluginsDirectory, string.Format("{0}.dll", assemblyName.Name));
            return Assembly.LoadFile(pluginAssembly);
        }

        /*

        void CreateContainer()
        {
            CopyDependencies();

            var evidence = AppDomain.CurrentDomain.Evidence;

            var setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = Constants.PluginsDirectory;

            _container = AppDomain.CreateDomain("ScanTargets", evidence, setupInfo);
            _container.AssemblyResolve += new ResolveEventHandler(FindPlugin);
        }

        void DestroyContainer()
        {
            if (_container != null)
            {
                _container.AssemblyResolve -= new ResolveEventHandler(FindPlugin);
                AppDomain.Unload(_container);
            }

            _container = null;
        }

         */

        void CopyDependencies()
        {
            const string SharedAssemblyName = "shared.dll";

            // copy shared DLL if missing
            var sharedAssemblyPath = Path.Combine(Constants.PluginsDirectory, SharedAssemblyName);
            var sharedAssemblySourcePath = Path.Combine(KnownPaths.Instance.ApplicationDirectory, SharedAssemblyName);
            if (!File.Exists(sharedAssemblyPath))
                File.Copy(sharedAssemblySourcePath, sharedAssemblyPath, true);
        }
    }
}
