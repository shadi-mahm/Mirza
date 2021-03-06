﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Mirza.Web.Dto;

namespace Mirza.Web.Services.Report
{
    public interface IReportService
    {
        IEnumerable<WorkLogReportOutput> GetReport();
    }
}