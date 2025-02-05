using BugSplatUnity.Runtime.Settings;
using System;
using UnityEngine;


namespace BugSplatUnity.Runtime.Reporter
{
    internal interface IReportUploadGuardService
    {
        bool ShouldPostLogMessage(LogType type);
        bool ShouldPostException(Exception exception);
    }

    internal class ReportUploadGuardService : IReportUploadGuardService
    {
        private IClientSettingsRepository _clientSettingsRepository;
        public ReportUploadGuardService(IClientSettingsRepository clientSettingsRepository)
        {
            _clientSettingsRepository = clientSettingsRepository;
        }

        public bool ShouldPostException(Exception exception)
        {
            if (Application.isEditor && !_clientSettingsRepository.PostExceptionsInEditor)
            {
                return false;
            }

            return _clientSettingsRepository.ShouldPostException(exception);
        }

        public bool ShouldPostLogMessage(LogType type)
        {
            if (type != LogType.Exception)
            {
                return false;
            }

            return ShouldPostException(null);
        }
    }
}
