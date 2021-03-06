﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.VsTestConsole.TranslationLayer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.TestPlatform.VsTestConsole.TranslationLayer.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Tracing.Interfaces;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.Interfaces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Moq;

    [TestClass]
    public class VsTestConsoleWrapperAsyncTests
    {
        private IVsTestConsoleWrapperAsync consoleWrapper;

        private Mock<IProcessManager> mockProcessManager;

        private Mock<ITranslationLayerRequestSenderAsync> mockRequestSender;

        private readonly List<string> testSources = new List<string> { "Hello", "World" };

        private readonly List<TestCase> testCases = new List<TestCase>
                                                      {
                                                          new TestCase("a.b.c", new Uri("d://uri"), "a.dll"),
                                                          new TestCase("d.e.f", new Uri("g://uri"), "d.dll")
                                                      };

        private ConsoleParameters consoleParameters;

        [TestInitialize]
        public void TestInitialize()
        {
            this.consoleParameters = new ConsoleParameters();

            this.mockRequestSender = new Mock<ITranslationLayerRequestSenderAsync>();
            this.mockProcessManager = new Mock<IProcessManager>();
            this.consoleWrapper = new VsTestConsoleWrapperAsync(
                this.mockRequestSender.Object,
                this.mockProcessManager.Object,
                this.consoleParameters,
                new Mock<ITestPlatformEventSource>().Object);

            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(100));
        }

        [TestMethod]
        public async Task StartSessionAsyncShouldStartVsTestConsoleWithCorrectArguments()
        {
            var inputPort = 123;
            int expectedParentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(inputPort));

            await this.consoleWrapper.StartSessionAsync();

            Assert.AreEqual(expectedParentProcessId, this.consoleParameters.ParentProcessId, "Parent process Id must be set");
            Assert.AreEqual(inputPort, this.consoleParameters.PortNumber, "Port number must be set");

            this.mockProcessManager.Verify(pm => pm.StartProcess(this.consoleParameters), Times.Once);
        }

        [TestMethod]
        public void StartSessionAsyncShouldThrowExceptionOnBadPort()
        {
            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(-1));

            Assert.ThrowsExceptionAsync<TransationLayerException>(async () => await this.consoleWrapper.StartSessionAsync());
        }

        [TestMethod]
        public async Task StartSessionShouldCallWhenProcessNotInitializedAsync()
        {
            this.mockProcessManager.Setup(pm => pm.IsProcessInitialized()).Returns(false);

            // To call private method EnsureInitialize call InitializeExtensions
            await this.consoleWrapper.InitializeExtensionsAsync(new[] { "path/to/adapter" });

            this.mockProcessManager.Verify(pm => pm.StartProcess(It.IsAny<ConsoleParameters>()));
        }

        [TestMethod]
        public async Task InitializeExtensionsAsyncShouldCachePathToExtensions()
        {
            var pathToExtensions = new[] { "path/to/adapter" };
            this.mockProcessManager.Setup(pm => pm.IsProcessInitialized()).Returns(true);

            await this.consoleWrapper.InitializeExtensionsAsync(pathToExtensions);

            this.mockProcessManager.Setup(pm => pm.IsProcessInitialized()).Returns(false);
            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(100));

            await this.consoleWrapper.InitializeExtensionsAsync(pathToExtensions);

            this.mockRequestSender.Verify(rs => rs.InitializeExtensions(pathToExtensions), Times.Exactly(3));
        }

        [TestMethod]
        public void ProcessExitedEventShouldSetOnProcessExit()
        {
            this.mockProcessManager.Raise(pm => pm.ProcessExited += null, EventArgs.Empty);

            this.mockRequestSender.Verify(rs => rs.OnProcessExited(), Times.Once);
        }

        [TestMethod]
        public async Task InitializeExtensionsAsyncShouldSucceed()
        {
            var pathToAdditionalExtensions = new List<string> { "Hello", "World" };

            await this.consoleWrapper.InitializeExtensionsAsync(pathToAdditionalExtensions);

            this.mockRequestSender.Verify(rs => rs.InitializeExtensions(pathToAdditionalExtensions), Times.Once);
        }

        [TestMethod]
        public void InitializeExtensionsAsyncShouldThrowExceptionOnBadConnection()
        {
            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(-1));

            Assert.ThrowsExceptionAsync<TransationLayerException>(async () => await this.consoleWrapper.InitializeExtensionsAsync(new List<string> { "Hello", "World" }));
            this.mockRequestSender.Verify(rs => rs.InitializeExtensions(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [TestMethod]
        public async Task DiscoverTestsAsyncShouldSucceed()
        {
            await this.consoleWrapper.DiscoverTestsAsync(this.testSources, null, new Mock<ITestDiscoveryEventsHandler>().Object);

            this.mockRequestSender.Verify(rs => rs.DiscoverTestsAsync(this.testSources, null, It.IsAny<ITestDiscoveryEventsHandler>()), Times.Once);
        }

        [TestMethod]
        public void DiscoverTestsAsyncShouldThrowExceptionOnBadConnection()
        {
            this.mockRequestSender.Setup(rs => rs.InitializeCommunicationAsync(It.IsAny<int>())).Returns(Task.FromResult(-1));

            Assert.ThrowsExceptionAsync<TransationLayerException>(async () => await this.consoleWrapper.DiscoverTestsAsync(new List<string> { "Hello", "World" }, null, new Mock<ITestDiscoveryEventsHandler>().Object));
            this.mockRequestSender.Verify(rs => rs.DiscoverTestsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<ITestDiscoveryEventsHandler>()), Times.Never);
        }

        [TestMethod]
        public async Task RunTestsAsyncWithSourcesShouldSucceed()
        {
            await this.consoleWrapper.RunTestsAsync(this.testSources, "RunSettings", new Mock<ITestRunEventsHandler>().Object);

            this.mockRequestSender.Verify(rs => rs.StartTestRunAsync(this.testSources, "RunSettings", null, It.IsAny<ITestRunEventsHandler>()), Times.Once);
        }

        [TestMethod]
        public async Task RunTestsAsyncWithSourcesAndCustomHostShouldSucceed()
        {
            await this.consoleWrapper.RunTestsWithCustomTestHostAsync(
                this.testSources,
                "RunSettings",
                new Mock<ITestRunEventsHandler>().Object,
                new Mock<ITestHostLauncher>().Object);

            this.mockRequestSender.Verify(rs => rs.StartTestRunWithCustomHostAsync(this.testSources, "RunSettings", null, It.IsAny<ITestRunEventsHandler>(), It.IsAny<ITestHostLauncher>()), Times.Once);
        }

        [TestMethod]
        public async Task RunTestsAsyncWithSelectedTestsShouldSucceed()
        {
            await this.consoleWrapper.RunTestsAsync(this.testCases, "RunSettings", new Mock<ITestRunEventsHandler>().Object);

            this.mockRequestSender.Verify(rs => rs.StartTestRunAsync(this.testCases, "RunSettings", It.IsAny<ITestRunEventsHandler>()), Times.Once);
        }

        [TestMethod]
        public async Task RunTestsAsyncWithSelectedTestsAndCustomLauncherShouldSucceed()
        {
            await this.consoleWrapper.RunTestsWithCustomTestHostAsync(
                this.testCases,
                "RunSettings",
                new Mock<ITestRunEventsHandler>().Object,
                new Mock<ITestHostLauncher>().Object);

            this.mockRequestSender.Verify(rs => rs.StartTestRunWithCustomHostAsync(this.testCases, "RunSettings", It.IsAny<ITestRunEventsHandler>(), It.IsAny<ITestHostLauncher>()), Times.Once);
        }

        [TestMethod]
        public void EndSessionShouldSucceed()
        {
            this.consoleWrapper.EndSession();

            this.mockRequestSender.Verify(rs => rs.EndSession(), Times.Once);
            this.mockRequestSender.Verify(rs => rs.Close(), Times.Once);
        }
    }
}
