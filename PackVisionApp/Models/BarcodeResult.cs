using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Models
{
    public class BarcodeResult
    {
        public bool Success { get; set; }
        public string Value { get; set; }
        public string Text { get; set; }
        public string FailReason { get; set; }

        public static BarcodeResult Ok(string value)
        {
            return new BarcodeResult
            {
                Success = true,
                Value = value,
                Text = value,
                FailReason = null
            };
        }

        public static BarcodeResult Fail(string reason)
        {
            return new BarcodeResult
            {
                Success = false,
                Value = null,
                Text = null,
                FailReason = reason
            };
        }
    }
}
