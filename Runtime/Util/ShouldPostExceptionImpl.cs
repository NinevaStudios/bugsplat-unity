﻿using System;
using UnityEngine;

namespace BugSplatUnity.Runtime.Util
{
    public static class ShouldPostExceptionImpl
    {
        private static DateTime lastPost;

        public static bool DefaultShouldPostExceptionImpl(Exception ex = null)
        {
            var now = DateTime.Now;

            if (lastPost + TimeSpan.FromSeconds(60) > now)
            {
                Debug.Log("BugSplat info: Report rate-limiting triggered, skipping report...");
                return false;
            }

            lastPost = now;
            return true;
        }
    }
}
