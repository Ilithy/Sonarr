﻿using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Test.Common
{
    public abstract class TestBase<TSubject> : TestBase where TSubject : class
    {
        private TSubject _subject;

        [SetUp]
        public void CoreTestSetup()
        {
            _subject = null;
        }

        protected TSubject Subject
        {
            get
            {
                if (_subject == null)
                {
                    _subject = Mocker.Resolve<TSubject>();
                }

                return _subject;
            }
        }
    }

    public abstract class TestBase : LoggingTest
    {
        private static int safeInstanceCount = 0;
        private static readonly Random _random = new Random();

        private AutoMoqer _mocker;
        protected AutoMoqer Mocker
        {
            get
            {
                if (_mocker == null)
                {
                    _mocker = new AutoMoqer();
                    _mocker.SetConstant<ICacheManager>(new CacheManager());
                    _mocker.SetConstant<IStartupContext>(new StartupContext(new string[0]));
                    _mocker.SetConstant(TestLogger);
                }

                return _mocker;
            }
        }

        protected int RandomNumber
        {
            get
            {
                Thread.Sleep(1);
                return _random.Next(0, int.MaxValue);
            }
        }

        protected int UniqueNumber
        {
            get
            {
                Interlocked.Increment(ref safeInstanceCount);

                return safeInstanceCount;
            }
        }

        private string VirtualPath
        {
            get
            {
                var virtualPath = Path.Combine(TempFolder, "VirtualNzbDrone");
                Directory.CreateDirectory(virtualPath);

                return virtualPath;
            }
        }

        protected string TempFolder { get; private set; }

        [SetUp]
        public void TestBaseSetup()
        {
            GetType().IsPublic.Should().BeTrue("All Test fixtures should be public to work in mono.");

            LogManager.ReconfigExistingLoggers();

            TempFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "_temp_" + UniqueNumber);

            TestLogger.Trace("Creating Temp Folder: {0}", TempFolder);
            Directory.CreateDirectory(TempFolder);
            TestLogger.Trace("Created Temp Folder: {0}", TempFolder);
        }

        [TearDown]
        public void TestBaseTearDown()
        {
            _mocker = null;

            try
            {
                var tempFolder = new DirectoryInfo(TempFolder);
                if (tempFolder.Exists)
                {
                    foreach (var file in tempFolder.GetFiles("*", SearchOption.AllDirectories))
                    {
                        file.IsReadOnly = false;
                    }

                    tempFolder.Delete(true);
                }
            }
            catch (Exception ex)
            {
                TestLogger.Trace("Failed to delete temp folder: {0}", TempFolder);
            }
        }

        protected IAppFolderInfo TestFolderInfo { get; private set; }

        protected void WindowsOnly()
        {
            if (OsInfo.IsNotWindows)
            {
                throw new IgnoreException("windows specific test");
            }
        }

        protected void MonoOnly()
        {
            if (!PlatformInfo.IsMono)
            {
                throw new IgnoreException("mono specific test");
            }
        }

        protected void WithTempAsAppPath()
        {
            Mocker.GetMock<IAppFolderInfo>()
                  .SetupGet(c => c.AppDataFolder)
                  .Returns(VirtualPath);

            TestFolderInfo = Mocker.GetMock<IAppFolderInfo>().Object;
        }

        protected string GetTestPath(string path)
        {
            return Path.Combine(TestContext.CurrentContext.TestDirectory, Path.Combine(path.Split('/')));
        }

        protected string ReadAllText(string path)
        {
            return File.ReadAllText(GetTestPath(path));
        }

        protected string GetTempFilePath()
        {
            return Path.Combine(TempFolder, Path.GetRandomFileName());
        }

        protected void VerifyEventPublished<TEvent>() where TEvent : class, IEvent
        {
            VerifyEventPublished<TEvent>(Times.Once());
        }

        protected void VerifyEventPublished<TEvent>(Times times) where TEvent : class, IEvent
        {
            Mocker.GetMock<IEventAggregator>().Verify(c => c.PublishEvent(It.IsAny<TEvent>()), times);
        }

        protected void VerifyEventNotPublished<TEvent>() where TEvent : class, IEvent
        {
            Mocker.GetMock<IEventAggregator>().Verify(c => c.PublishEvent(It.IsAny<TEvent>()), Times.Never());
        }
    }
}
