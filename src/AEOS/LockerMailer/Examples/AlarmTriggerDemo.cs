using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Innovatrics.SmartFace.Integrations.LockerMailer.Examples
{
    /// <summary>
    /// Demonstration program to show how the alarm trigger system works
    /// </summary>
    public class AlarmTriggerDemo
    {
        public static void RunDemo()
        {
            // Configure logging
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            logger.Information("=== LockerMailer Alarm Trigger System Demo ===");
            logger.Information("");

            // Show the configuration structure
            ShowConfigurationStructure(logger);
            
            logger.Information("");
            
            // Demonstrate how alarms work
            DemonstrateAlarmSystem(logger);
            
            logger.Information("");
            
            // Show the flow for lockers-flow_3
            DemonstrateLockersFlow3(logger);
            
            logger.Information("");
            logger.Information("=== Demo Complete ===");
        }

        private static void ShowConfigurationStructure(ILogger logger)
        {
            logger.Information("📋 CONFIGURATION STRUCTURE:");
            logger.Information("");
            logger.Information("Alarms are defined in appsettings.json:");
            logger.Information("```json");
            logger.Information("{");
            logger.Information("  \"LockerMailer\": {");
            logger.Information("    \"Alarms\": [");
            logger.Information("      {");
            logger.Information("        \"AlarmName\": \"Alarm1\",");
            logger.Information("        \"AlarmTime\": \"18:00\"");
            logger.Information("      },");
            logger.Information("      {");
            logger.Information("        \"AlarmName\": \"Alarm2\",");
            logger.Information("        \"AlarmTime\": \"17:00\"");
            logger.Information("      },");
            logger.Information("      {");
            logger.Information("        \"AlarmName\": \"Alarm3\",");
            logger.Information("        \"AlarmTime\": \"16:00\"");
            logger.Information("      }");
            logger.Information("    ],");
            logger.Information("    \"Templates\": [");
            logger.Information("      {");
            logger.Information("        \"templateName\": \"Courier Item Reminder\",");
            logger.Information("        \"templateId\": \"lockers-flow_3\",");
            logger.Information("        \"templateAlarm\": \"Alarm1\",");
            logger.Information("        \"templateCheckGroup\": \"7thFloor\"");
            logger.Information("      }");
            logger.Information("    ]");
            logger.Information("  }");
            logger.Information("}");
            logger.Information("```");
        }

        private static void DemonstrateAlarmSystem(ILogger logger)
        {
            logger.Information("⏰ HOW ALARM SYSTEM WORKS:");
            logger.Information("");
            logger.Information("1. AlarmTriggerService runs continuously in the background");
            logger.Information("2. Every 30 seconds, it checks the current time");
            logger.Information("3. If current time matches an alarm time (within 1 minute tolerance):");
            logger.Information("   ✓ Find all templates that use that alarm");
            logger.Information("   ✓ Get current locker assignment data");
            logger.Information("   ✓ Process each template for relevant assignments");
            logger.Information("   ✓ Send emails to affected employees");
            logger.Information("");
            logger.Information("🕐 EXAMPLE ALARM TIMES:");
            logger.Information("   • Alarm1: 18:00 (6:00 PM)");
            logger.Information("   • Alarm2: 17:00 (5:00 PM)");
            logger.Information("   • Alarm3: 16:00 (4:00 PM)");
        }

        private static void DemonstrateLockersFlow3(ILogger logger)
        {
            logger.Information("📧 LOCKERS-FLOW_3 PROCESS:");
            logger.Information("");
            logger.Information("Template Configuration:");
            logger.Information("  • templateId: 'lockers-flow_3'");
            logger.Information("  • templateAlarm: 'Alarm1' (triggers at 18:00)");
            logger.Information("  • templateCheckGroup: '7thFloor'");
            logger.Information("  • Description: 'It is 5pm, if you currently have an courier locker assigned.'");
            logger.Information("");
            logger.Information("When Alarm1 triggers at 18:00:");
            logger.Information("  📢 Terminal output:");
            logger.Information("    'lockers-flow_3 triggered as it is time'");
            logger.Information("    'lockers-flow_3 triggered'");
            logger.Information("    'Current locker assignments in '7thFloor' group:'");
            logger.Information("      '• Locker: Locker-001'");
            logger.Information("        'Name: John Doe'");
            logger.Information("        'Email: john.doe@company.com'");
            logger.Information("        'Assigned at: 2024-01-15 14:30:00'");
            logger.Information("");
            logger.Information("  1. 🔍 Find all current locker assignments in '7thFloor' group");
            logger.Information("  2. 📧 For each employee with an active courier locker:");
            logger.Information("     • Get their email address");
            logger.Information("     • Load the 'lockers-flow_3' template from Keila");
            logger.Information("     • Replace placeholders with their data");
            logger.Information("     • Send reminder email");
            logger.Information("");
            logger.Information("📝 EMAIL CONTENT EXAMPLE:");
            logger.Information("   Subject: 'Courier Item Reminder'");
            logger.Information("   Body: 'It is 5pm, if you currently have an courier locker assigned.'");
            logger.Information("   Recipient: Employee with active 7thFloor locker assignment");
        }
    }
}
