﻿using Abp.Application.Services;

namespace ASBicycle
{
    /// <summary>
    /// Derive your application services from this class.
    /// </summary>
    public abstract class ASBicycleAppServiceBase : ApplicationService
    {
        protected ASBicycleAppServiceBase()
        {
            LocalizationSourceName = ASBicycleConsts.LocalizationSourceName;
        }
    }
}