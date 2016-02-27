using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for DateHelper
/// </summary>
public class DateHelper
{
    public static int MonthsSince(DateTime startDate)
    {
        DateTime now = DateTime.Now;
        return 12 * (now.Year - startDate.Year) + now.Month - startDate.Month;
    }
}