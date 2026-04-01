using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Models
{
    public class DateResult
    {
        public bool Success { get; set; }
        public string Value { get; set; }
        public string FailReason { get; set; }

        public static DateResult Ok(string value)
        {
            return new DateResult
            {
                Success = true,
                Value = value,
                FailReason = null
            };
        }

        public static DateResult Fail(string reason)
        {
            return new DateResult
            {
                Success = false,
                Value = null,
                FailReason = reason
            };
        }
    }
}
