﻿using System;
using System.Collections.Generic;
using System.IO;

namespace BugSplatUnity.Runtime.Settings
{
    internal interface IClientSettingsRepository
    {
        List<FileInfo> Attachments { get; }
        bool CaptureEditorLog { get; set; }
        bool CapturePlayerLog { get; set; }
        bool CaptureScreenshots { get; set; }
        bool PostExceptionsInEditor { get; set; }
        Func<Exception, bool> ShouldPostException { get; set; }
        string Description { get; set; }
        string Email { get; set; }
        string Key { get; set; }
        string Notes { get; set; }
        string User { get; set; }
    }
}
