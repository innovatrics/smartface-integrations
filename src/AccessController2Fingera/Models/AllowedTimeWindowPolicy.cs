﻿using System;

namespace SmartFace.Integrations.Fingera.Models
{
    public class AllowedTimeWindowPolicy
    {
        public bool Enabled                         { get; set; } = true;

        public AllowedTimeWindowPolicyDay[] Days    { get; set; }
    }

    public class AllowedTimeWindowPolicyDay
    {
        public TimeSpan From    { get; set; }
        public TimeSpan To      { get; set; }
    }
}
