﻿using NLog;
using NLog.Config;
using NLog.Targets;
using Elmah.Io.NLog;
using StoryBuilder.Models;
using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.DependencyInjection;
using StoryBuilder.Services.Keys;

namespace StoryBuilder.Services.Logging;

/// <summary>
/// Manage the Task Log file.
/// </summary>
public class LogService : ILogService
{
    private static readonly Logger Logger;
    private static readonly string logFilePath;
    private static string stackTraceHelper; //Elmah for some reason doesn't show the stack trace of an exception so this one does.
    static LogService()
    {
        try
        {
            LoggingConfiguration config = new();

            // Create the file logging target
            FileTarget fileTarget = new();
            logFilePath = Path.Combine(GlobalData.RootDirectory, "logs");
            string logfilename = Path.Combine(logFilePath, "updater.${date:format=yyyy-MM-dd}.log");
            fileTarget.FileName = logfilename;
            fileTarget.CreateDirs = true;
            fileTarget.MaxArchiveFiles = 7;
            fileTarget.ArchiveEvery = FileArchivePeriod.Day;
            fileTarget.ConcurrentWrites = true;
            fileTarget.Layout = "${longdate} | ${level} | ${message} | ${exception:format=Message,StackTrace,Data:MaxInnerExceptionLevel=5}";
            LoggingRule fileRule = new("*", NLog.LogLevel.Info, fileTarget);
            config.AddTarget("logfile", fileTarget);
            config.LoggingRules.Add(fileRule);

            // create elmah.io target if keys are defined
            var keys = Ioc.Default.GetService<KeyService>();
            var tokens = keys.ElmahTokens();
            string apiKey = tokens.Item1;
            string logID = tokens.Item2;
            if (apiKey != string.Empty && logID != string.Empty)
            {
                // create elmah.io target
                var elmahIoTarget = new ElmahIoTarget();

                elmahIoTarget.OnMessage += msg =>
                {
                    msg.Version = Windows.ApplicationModel.Package.Current.Id.Version.Major + "."
                    + Windows.ApplicationModel.Package.Current.Id.Version.Minor + "."
                    + Windows.ApplicationModel.Package.Current.Id.Version.Build + " Build " + File.ReadAllText(GlobalData.RootDirectory + "\\RevisionID");

                    msg.User = GlobalData.Preferences.Name + $"({GlobalData.Preferences.Email})";
                    msg.Source = stackTraceHelper;
                };

                elmahIoTarget.Name = "elmahio";
                elmahIoTarget.ApiKey = apiKey;
                elmahIoTarget.LogId = logID;
                config.AddTarget(elmahIoTarget);
                config.AddRule(NLog.LogLevel.Error, NLog.LogLevel.Fatal, elmahIoTarget, "*");
            }

            // create console target
            if (!Debugger.IsAttached)
            {
                ColoredConsoleTarget consoleTarget = new();
                consoleTarget.Layout = @"${date:format=HH\\:MM\\:ss} ${logger} ${message}";
                config.AddTarget("console", consoleTarget);
                LoggingRule consoleRule = new("*", NLog.LogLevel.Info, consoleTarget);
                config.LoggingRules.Add(consoleRule);
            }

            LogManager.Configuration = config;

            Logger = LogManager.GetCurrentClassLogger();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            Debug.WriteLine(e.StackTrace);
        }
    }
    public LogService()
    {
        Log(LogLevel.Info, "Starting Log service");
        Log(LogLevel.Info, "Detailed log at " + logFilePath);
    }

    public void Log(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Trace:
                Logger.Trace(message);
                break;
            case LogLevel.Debug:
                Logger.Debug(message);
                break;
            case LogLevel.Info:
                Logger.Info(message);
                break;
            case LogLevel.Warn:
                Logger.Warn(message);
                break;
            case LogLevel.Error:
                Logger.Error(message);
                break;
            case LogLevel.Fatal:
                Logger.Fatal(message);
                break;
        }
    }

    public void LogException(LogLevel level, Exception exception, string message)
    {
        switch (level)
        {
            case LogLevel.Error:
                stackTraceHelper = exception.StackTrace;
                Logger.Error(exception, message);
                break;
            case LogLevel.Fatal:
                stackTraceHelper = exception.StackTrace;
                Logger.Fatal(exception, message);
                break;
        }
    }

    public void Flush()
    {
        LogManager.Flush();
    }
}