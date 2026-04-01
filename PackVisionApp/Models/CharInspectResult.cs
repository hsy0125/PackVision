using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Models
{
	public class CharInspectResult
	{
		public char ExpectedChar { get; set; }
		public char ReadChar { get; set; }
		public Rectangle Box { get; set; } = Rectangle.Empty;

		public bool IsMatch
		{
			get { return ExpectedChar == ReadChar; }
		}
	}
}
