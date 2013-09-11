﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;

namespace MadsKristensen.EditorExtensions.BrowserLink.UnusedCss
{
    public class CompositeUsageData : IUsageDataSource
    {
        private readonly UnusedCssExtension _extension;
        private readonly HashSet<RuleUsage> _ruleUsages = new HashSet<RuleUsage>();
        private readonly HashSet<IUsageDataSource> _sources = new HashSet<IUsageDataSource>();
        private readonly object _sync = new object();

        public CompositeUsageData(UnusedCssExtension extension)
        {
            _extension = extension;
        }

        public void AddUsageSource(IUsageDataSource source)
        {
            lock (_sync)
            {
                _sources.Add(source);
                _ruleUsages.UnionWith(source.GetRuleUsages());
            }
        }

        public IEnumerable<IStylingRule> GetAllRules()
        {
            lock (_sync)
            {
                return RuleRegistry.GetAllRules(_extension);
            }
        }

        public IEnumerable<RuleUsage> GetRuleUsages()
        {
            lock (_sync)
            {
                return _ruleUsages;
            }
        }

        public IEnumerable<IStylingRule> GetUnusedRules()
        {
            lock (_sync)
            {
                var unusedRules = new HashSet<IStylingRule>(GetAllRules());
                unusedRules.ExceptWith(_ruleUsages.Select(x => x.Rule).Distinct());
                return unusedRules.Where(x => !UsageRegistry.IsAProtectedClass(x)).ToList();
            }
        }

        public IEnumerable<Task> GetWarnings()
        {
            lock(_sync)
            {
                return GetUnusedRules().Select(x => x.ProduceErrorListTask(TaskErrorCategory.Warning, _extension.Connection.Project, "Unused CSS rule \"{1}\""));
            }
        }
        
        public IEnumerable<Task> GetWarnings(Uri uri)
        {
            lock(_sync)
            {
                return GetUnusedRules().Select(x => x.ProduceErrorListTask(TaskErrorCategory.Warning, _extension.Connection.Project, "Unused CSS rule \"{1}\" on page " + uri));
            }
        }

        public async System.Threading.Tasks.Task ResyncAsync()
        {
            IEnumerable<IUsageDataSource> srcs;

            lock (_sync)
            {
                srcs = _sources.ToList();
            }

            foreach (var source in srcs)
            {
                await source.ResyncAsync();
            }

            lock (_sync)
            {
                _ruleUsages.Clear();

                foreach (var source in _sources)
                {
                    _ruleUsages.UnionWith(source.GetRuleUsages());
                }
            }
        }

        public void Resync()
        {
            IEnumerable<IUsageDataSource> srcs;

            lock (_sync)
            {
                srcs = _sources.ToList();
            }

            foreach (var source in srcs)
            {
                source.Resync();
            }

            lock (_sync)
            {
                _ruleUsages.Clear();

                foreach (var source in _sources)
                {
                    _ruleUsages.UnionWith(source.GetRuleUsages());
                }
            }
        }
    }
}