using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Innovatrics.SmartFace.Integrations.LockerMailer.Services;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Examples
{
    /// <summary>
    /// Example class to demonstrate and test the alarm trigger functionality
    /// </summary>
    public class AlarmTriggerTest
    {
        private readonly ILogger logger;

        public AlarmTriggerTest(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Test method to verify alarm configuration loading
        /// </summary>
        public void TestAlarmConfigurationLoading()
        {
            logger.Information("=== Testing Alarm Configuration Loading ===");

            // Create a test configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["LockerMailer:Alarms:0:AlarmName"] = "Alarm1",
                    ["LockerMailer:Alarms:0:AlarmTime"] = "18:00",
                    ["LockerMailer:Alarms:1:AlarmName"] = "Alarm2", 
                    ["LockerMailer:Alarms:1:AlarmTime"] = "17:00",
                    ["LockerMailer:Alarms:2:AlarmName"] = "Alarm3",
                    ["LockerMailer:Alarms:2:AlarmTime"] = "16:00",
                    ["LockerMailer:Templates:0:templateName"] = "Courier Item Reminder",
                    ["LockerMailer:Templates:0:templateId"] = "lockers-flow_3",
                    ["LockerMailer:Templates:0:templateDescription"] = "It is 5pm, if you currently have an courier locker assigned.",
                    ["LockerMailer:Templates:0:templateCheckGroup"] = "7thFloor",
                    ["LockerMailer:Templates:0:templateAlarm"] = "Alarm1",
                    ["LockerMailer:Templates:0:cancelTime"] = "17:00"
                })
                .Build();

            // Test alarm configuration loading
            var alarmsSection = configuration.GetSection("LockerMailer:Alarms");
            var alarmCount = 0;
            
            foreach (var alarm in alarmsSection.GetChildren())
            {
                var alarmName = alarm.GetValue<string>("AlarmName");
                var alarmTime = alarm.GetValue<string>("AlarmTime");
                
                if (!string.IsNullOrEmpty(alarmName) && !string.IsNullOrEmpty(alarmTime))
                {
                    alarmCount++;
                    logger.Information($"Found alarm: {alarmName} at {alarmTime}");
                }
            }

            logger.Information($"Total alarms loaded: {alarmCount}");

            // Test template configuration loading
            var templatesSection = configuration.GetSection("LockerMailer:Templates");
            var templateCount = 0;
            
            foreach (var template in templatesSection.GetChildren())
            {
                var templateId = template.GetValue<string>("templateId");
                var templateAlarm = template.GetValue<string>("templateAlarm");
                
                if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(templateAlarm))
                {
                    templateCount++;
                    logger.Information($"Found template: {templateId} with alarm {templateAlarm}");
                }
            }

            logger.Information($"Total alarm-triggered templates loaded: {templateCount}");
            logger.Information("=== Alarm Configuration Loading Test Complete ===");
        }

        /// <summary>
        /// Test method to verify time-based alarm triggering logic
        /// </summary>
        public void TestAlarmTriggeringLogic()
        {
            logger.Information("=== Testing Alarm Triggering Logic ===");

            // Test different times to see if alarms would trigger
            var testTimes = new[]
            {
                new DateTime(2024, 1, 1, 17, 58, 0), // 2 minutes before Alarm2 (17:00)
                new DateTime(2024, 1, 1, 17, 59, 0), // 1 minute before Alarm2 (17:00)
                new DateTime(2024, 1, 1, 18, 0, 0), // Exactly Alarm1 (18:00)
                new DateTime(2024, 1, 1, 18, 1, 0), // 1 minute after Alarm1 (18:00)
                new DateTime(2024, 1, 1, 16, 0, 0), // Exactly Alarm3 (16:00)
                new DateTime(2024, 1, 1, 12, 0, 0), // No alarm time
            };

            var alarmTimes = new Dictionary<string, TimeSpan>
            {
                ["Alarm1"] = new TimeSpan(18, 0, 0),
                ["Alarm2"] = new TimeSpan(17, 0, 0),
                ["Alarm3"] = new TimeSpan(16, 0, 0)
            };

            foreach (var testTime in testTimes)
            {
                logger.Information($"Testing time: {testTime:HH:mm:ss}");
                
                foreach (var alarm in alarmTimes)
                {
                    var timeDifference = Math.Abs((testTime.TimeOfDay - alarm.Value).TotalMinutes);
                    var wouldTrigger = timeDifference <= 1.0;
                    
                    logger.Information($"  {alarm.Key} ({alarm.Value:hh\\:mm}): {(wouldTrigger ? "WOULD TRIGGER" : "would not trigger")} (diff: {timeDifference:F1} min)");
                }
            }

            logger.Information("=== Alarm Triggering Logic Test Complete ===");
        }

        /// <summary>
        /// Simulate the alarm trigger process for testing
        /// </summary>
        public void SimulateAlarmTrigger()
        {
            logger.Information("=== Simulating Alarm Trigger ===");
            
            // Simulate current time being 18:00 (Alarm1 time)
            var currentTime = DateTime.Now.Date.AddHours(18); // 18:00 today
            var alarmTime = new TimeSpan(18, 0, 0);
            
            var timeDifference = Math.Abs((currentTime.TimeOfDay - alarmTime).TotalMinutes);
            var shouldTrigger = timeDifference <= 1.0;
            
            logger.Information($"Current time: {currentTime:HH:mm:ss}");
            logger.Information($"Alarm time: {alarmTime:hh\\:mm}");
            logger.Information($"Time difference: {timeDifference:F1} minutes");
            logger.Information($"Should trigger: {shouldTrigger}");
            
            if (shouldTrigger)
            {
                logger.Information("✓ Alarm1 would trigger at this time");
                logger.Information("✓ This would process lockers-flow_3 template for 7thFloor group");
                logger.Information("✓ Email would be sent to employees with active courier locker assignments");
            }
            else
            {
                logger.Information("✗ Alarm1 would not trigger at this time");
            }
            
            logger.Information("=== Alarm Trigger Simulation Complete ===");
        }
    }
}
