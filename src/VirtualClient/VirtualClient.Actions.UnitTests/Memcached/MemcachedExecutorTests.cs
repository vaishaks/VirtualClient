﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace VirtualClient.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;
    using NUnit.Framework;
    using VirtualClient.Common.Telemetry;
    using VirtualClient.Contracts;

    [TestFixture]
    [Category("Unit")]
    public class MemcachedExecutorTests
    {
        private MockFixture fixture;

        [SetUp]
        public void SetupDefaults()
        {
            this.fixture = new MockFixture();
            this.fixture.Setup(PlatformID.Unix);

            this.fixture.Parameters = new Dictionary<string, IConvertible>
            {
                ["Scenario"] = "Memtier_Scenario",
                ["PackageName"] = "memtier",
                ["Bind"] = 1,
                ["ClientsPerThread"] = 1,
                ["ThreadCount"] = 1,
                ["PipelineDepth"] = 32,
                ["RunCount"] = 1,
                ["Duration"] = "00:03:00",
                ["Port"] = 6379,
                ["Protocol"] = "memcache_text",
                ["Username"] = "any-username"
            };

            this.fixture.Layout = new EnvironmentLayout(new List<ClientInstance>
            {
                new ClientInstance($"{Environment.MachineName}-Server", "1.2.3.4", "Server"),
                new ClientInstance($"{Environment.MachineName}-Client", "1.2.3.5", "Client")
            });

            this.fixture.FileSystem.Setup(fe => fe.Directory.Exists(It.IsAny<string>())).Returns(true);
        }

        [Test]
        public void MemcachedMemtierExecutorThrowsOnUnsupportedPlatformAsync()
        {
            this.SetupDefaultMockBehavior(PlatformID.Win32NT);

            using (var memcachedMemtierExecutor = new TestMemcachedMemtierExecutor(this.fixture.Dependencies, this.fixture.Parameters))
            {
                WorkloadException exception = Assert.ThrowsAsync<WorkloadException>(
                    () => memcachedMemtierExecutor.ExecuteAsync(CancellationToken.None));

                Assert.AreEqual(ErrorReason.PlatformNotSupported, exception.Reason);
            }
        }

        [Test]
        public void MemcachedMemtierExecutorThrowsOnUnsupportedDistroAsync()
        {
            this.SetupDefaultMockBehavior(PlatformID.Unix);

            LinuxDistributionInfo mockInfo = new LinuxDistributionInfo()
            {
                OperationSystemFullName = "TestUbuntu",
                LinuxDistribution = LinuxDistribution.SUSE
            };
            this.fixture.SystemManagement.Setup(sm => sm.GetLinuxDistributionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockInfo);

            using (var memcachedMemtierExecutor = new TestMemcachedMemtierExecutor(this.fixture.Dependencies, this.fixture.Parameters))
            {
                WorkloadException exception = Assert.ThrowsAsync<WorkloadException>(
                    () => memcachedMemtierExecutor.ExecuteAsync(CancellationToken.None));

                Assert.AreEqual(ErrorReason.LinuxDistributionNotSupported, exception.Reason);
            }
        }

        private void SetupDefaultMockBehavior(PlatformID platformID)
        {
            this.fixture.Setup(platformID);
            this.fixture.File.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
            this.fixture.ProcessManager.OnCreateProcess = (cmd, args, wd) => this.fixture.Process;
        }

        private class TestMemcachedMemtierExecutor : MemcachedExecutor
        {
            public TestMemcachedMemtierExecutor(IServiceCollection dependencies, IDictionary<string, IConvertible> parameters = null)
                : base(dependencies, parameters)
            {
            }

            public Func<EventContext, CancellationToken, Task> OnInitialize => base.InitializeAsync;
        }
    }
}
