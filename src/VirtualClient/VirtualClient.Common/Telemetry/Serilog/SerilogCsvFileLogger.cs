﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace VirtualClient.Common.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using global::Serilog;
    using global::Serilog.Core;
    using global::Serilog.Events;
    using global::Serilog.Parsing;
    using Microsoft.Extensions.Logging;
    using VirtualClient.Common.Extensions;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    /// <summary>
    /// Provides an <see cref="ILogger"/> implementation for writing events to local csv file
    /// </summary>
    public class SerilogCsvFileLogger : ILogger
    {
        private static readonly IDictionary<LogLevel, LogEventLevel> LevelMappings = new Dictionary<LogLevel, LogEventLevel>
        {
            { LogLevel.Trace, LogEventLevel.Verbose },
            { LogLevel.Debug, LogEventLevel.Verbose },
            { LogLevel.Information, LogEventLevel.Information },
            { LogLevel.Warning, LogEventLevel.Warning },
            { LogLevel.Error, LogEventLevel.Error },
            { LogLevel.Critical, LogEventLevel.Fatal }
        };

        private Logger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerilogCsvFileLogger"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Configuration settings that will be supplied to the Serilog logger
        /// used by the <see cref="ILogger"/> instance.
        /// </param>
        /// <param name="csvHeaders"> Headers to be added in the Csv Log File </param>
        /// <param name="rollingLogFilePath"> Rolling File Path for CSV Log File </param>
        public SerilogCsvFileLogger(LoggerConfiguration configuration, IEnumerable<string> csvHeaders, string rollingLogFilePath)
        {
            configuration.ThrowIfNull(nameof(configuration));
            this.logger = configuration.CreateLogger();

            this.AddHeadersToRollingCsvLogFile(csvHeaders, rollingLogFilePath);
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (SerilogCsvFileLogger.LevelMappings.TryGetValue(logLevel, out LogEventLevel level))
            {
                string eventMessage = null;
                EventContext eventContext = state as EventContext;

                if (!string.IsNullOrWhiteSpace(eventId.Name))
                {
                    // Use the explicitly defined event name.
                    eventMessage = eventId.Name;
                }
                else if (eventContext == null)
                {
                    if (state != null && exception != null && formatter != null)
                    {
                        // A formatted error message is expected.
                        eventMessage = formatter.Invoke(state, exception);
                    }
                    else if (exception != null && state != null)
                    {
                        // State context and an exception were provided.
                        eventMessage = $"{exception.ToString(withCallStack: false, withErrorTypes: true)} {state}";
                    }
                    else if (exception != null)
                    {
                        // An exception was provided by itself.
                        eventMessage = exception.ToString(withCallStack: false, withErrorTypes: true);
                    }
                    else
                    {
                        // State context was provided by itself.
                        eventMessage = state.ToString();
                    }
                }

                MessageTemplate template = new MessageTemplateParser().Parse(eventMessage);
                List<LogEventProperty> properties = this.GetEventProperties(eventContext);
                LogEvent logEvent = new LogEvent(DateTime.Now, level, exception, template, properties);
                this.logger.Write(logEvent);
            }
        }

        private void AddHeadersToRollingCsvLogFile(IEnumerable<string> csvHeaders, string rollingLogFilePath)
        {
            if (!File.Exists(rollingLogFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(rollingLogFilePath));
                using (StreamWriter writer = new StreamWriter(rollingLogFilePath))
                {
                    int propertyIndex = 0;
                    foreach (string header in csvHeaders)
                    {
                        if (propertyIndex > 0)
                        {
                            writer.Write(",");
                        }

                        writer.Write(header.Replace(",", "  ")); 
                        propertyIndex++;
                    }

                    writer.Write("\n");
                }
            }
        }

        private List<LogEventProperty> GetEventProperties(EventContext context)
        {
            List<LogEventProperty> properties = new List<LogEventProperty>();
            if (context != null)
            {
                properties.Add(new LogEventProperty("transactionId", new ScalarValue(context.TransactionId)));
                properties.Add(new LogEventProperty("durationMs", new ScalarValue(context.DurationMs)));

                foreach (KeyValuePair<string, object> property in context.Properties.OrderBy(p => p.Key))
                {
                    properties.Add(new LogEventProperty(property.Key, new ScalarValue(property.Value != null ? property.Value : string.Empty)));
                }
            }

            return properties;
        }
    }
}
