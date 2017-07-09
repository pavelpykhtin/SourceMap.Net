namespace SourceMap.Net
{
	public static class Base64
	{
		public static int ConvertChar(char charCode)
		{
			var bigA = 65;     // 'A'
			var bigZ = 90;     // 'Z'

			var littleA = 97;  // 'a'
			var littleZ = 122; // 'z'

			var zero = 48;     // '0'
			var nine = 57;     // '9'

			var plus = 43;     // '+'
			var slash = 47;    // '/'

			var littleOffset = 26;
			var numberOffset = 52;

			// 0 - 25: ABCDEFGHIJKLMNOPQRSTUVWXYZ
			if (bigA <= charCode && charCode <= bigZ)
			{
				return (charCode - bigA);
			}

			// 26 - 51: abcdefghijklmnopqrstuvwxyz
			if (littleA <= charCode && charCode <= littleZ)
			{
				return (charCode - littleA + littleOffset);
			}

			// 52 - 61: 0123456789
			if (zero <= charCode && charCode <= nine)
			{
				return (charCode - zero + numberOffset);
			}

			// 62: +
			if (charCode == plus)
			{
				return 62;
			}

			// 63: /
			if (charCode == slash)
			{
				return 63;
			}

			// Invalid base64 digit.
			return -1;
		}
	}
}