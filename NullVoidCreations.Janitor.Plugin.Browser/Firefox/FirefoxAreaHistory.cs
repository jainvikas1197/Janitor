﻿using System.Collections.Generic;
using System.IO;
using NullVoidCreations.Janitor.Shared.Helpers;
using NullVoidCreations.Janitor.Shared.Models;

namespace NullVoidCreations.Janitor.Plugin.Browser.Firefox
{
    public class FirefoxAreaHistory : ScanAreaBase
    {
        public FirefoxAreaHistory(ScanTargetBase target)
            : base("Internet History", target)
        {

        }

        public override List<Issue> Analyse()
        {
            var profiles = (Target as FirefoxTarget).Profiles;

            var paths = new List<string>();
            foreach (var profile in profiles)
            {
                paths.Add(Path.Combine(KnownPaths.Instance.AppDataLocal, string.Format(@"Mozilla\Firefox\Profiles\{0}\thumbnails", profile)));
            }

            Issues.Clear();
            foreach (var directory in paths)
                foreach (var file in new DirectoryWalker(directory))
                    Issues.Add(new Issue(Target, this, file));

            return Issues;
        }

        public override List<Issue> Fix()
        {
            IssuesFixed.Clear();
            for (var index = Issues.Count - 1; index >= 0; index--)
            {
                if (FileSystemHelper.Instance.DeleteFile(Issues[index].Details))
                {
                    var issue = Issues[index];
                    Issues.RemoveAt(index);
                    IssuesFixed.Add(issue);
                }
            }

            return IssuesFixed;
        }
    }
}
