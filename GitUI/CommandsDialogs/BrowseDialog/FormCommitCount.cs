﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GitCommands;
using GitCommands.Statistics;
using Microsoft.VisualStudio.Threading;

namespace GitUI.CommandsDialogs.BrowseDialog
{
    public partial class FormCommitCount : GitModuleForm
    {
        private CancellationTokenSource _backgroundLoaderTokenSource = new CancellationTokenSource();

        public FormCommitCount(GitUICommands commands)
            : base(commands)
        {
            InitializeComponent();
            Loading.Image = Properties.Resources.loadingpanel;
            Translate();
        }

        private void FormCommitCountLoad(object sender, EventArgs e)
        {
            FetchData();
        }

        private void cbIncludeSubmodules_CheckedChanged(object sender, EventArgs e)
        {
            FetchData();
        }

        private void FetchData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _backgroundLoaderTokenSource.Cancel();
            _backgroundLoaderTokenSource = new CancellationTokenSource();
            var token = _backgroundLoaderTokenSource.Token;

            ThreadHelper.JoinableTaskFactory.RunAsync(
                async () =>
                {
                    Loading.Visible = true;

                    CommitCount.Text = "";

                    var includeSubmodules = cbIncludeSubmodules.Checked;

                    await TaskScheduler.Default;

                    var text = GenerateText(Module, includeSubmodules, token);

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                    if (!token.IsCancellationRequested)
                    {
                        CommitCount.Text = text;
                        Loading.Visible = false;
                    }
                })
                .FileAndForget();
        }

        private static string GenerateText(GitModule module, bool includeSubmodules, CancellationToken token)
        {
            var text = new StringBuilder();
            var submodulesByName = new Dictionary<string, HashSet<string>>();
            var (countByName, _) = CommitCounter.GroupAllCommitsByContributor(module);

            if (includeSubmodules)
            {
                token.ThrowIfCancellationRequested();

                var submodules = module.GetSubmodulesLocalPaths();

                foreach (var submoduleName in submodules)
                {
                    token.ThrowIfCancellationRequested();

                    var submodule = module.GetSubmodule(submoduleName);

                    if (submodule.IsValidGitWorkingDir())
                    {
                        token.ThrowIfCancellationRequested();

                        var (submoduleItems, _) = CommitCounter.GroupAllCommitsByContributor(submodule);

                        foreach (var (name, count) in submoduleItems)
                        {
                            if (!submodulesByName.ContainsKey(name))
                            {
                                submodulesByName.Add(name, new HashSet<string>());
                            }

                            submodulesByName[name].Add(submodule.SubmoduleName);

                            if (countByName.ContainsKey(name))
                            {
                                countByName[name] += count;
                            }
                            else
                            {
                                countByName.Add(name, count);
                            }
                        }
                    }
                }
            }

            foreach (var (name, count) in countByName.OrderByDescending(pair => pair.Value))
            {
                text.AppendFormat("{0,6} - {1}", count, name);

                if (submodulesByName.TryGetValue(name, out var sub))
                {
                    text.Append(" [");

                    if (sub.Count == 1)
                    {
                        text.Append(sub.Single());
                    }
                    else
                    {
                        text.Append(sub.Count).Append(" submodules");
                    }

                    text.Append("]");
                }

                text.AppendLine();
            }

            return text.ToString();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _backgroundLoaderTokenSource.Cancel();
            _backgroundLoaderTokenSource.Dispose();
            base.OnFormClosed(e);
        }
    }
}