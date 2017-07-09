/*
 * Copyright 2011 The Closure Compiler Authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace SourceMap.Net
{
	public static class Base64Vlq
	{
		// A Base64 VLQ digit can represent 5 bits, so it is base-32.
		private const int VLQ_BASE_SHIFT = 5;
		private const int VLQ_BASE = 1 << VLQ_BASE_SHIFT;

		// A mask of bits for a VLQ digit (11111), 31 decimal.
		private const int VLQ_BASE_MASK = VLQ_BASE - 1;

		// The continuation bit is the 6th bit.
		private const int VLQ_CONTINUATION_BIT = VLQ_BASE;

		/**
		 * Converts to a two-complement value from a value where the sign bit is
		 * is placed in the least significant bit.  For example, as decimals:
		 *   2 (10 binary) becomes 1, 3 (11 binary) becomes -1
		 *   4 (100 binary) becomes 2, 5 (101 binary) becomes -2
		 */
		private static int FromVLQSigned(int value)
		{
			var negate = (value & 1) == 1;
			value = value >> 1;
			return negate ? -value : value;
		}
		
		/**
		 * Decodes the next VLQValue from the provided CharIterator.
		 */
		public static void Decode(string src, ref int index, out int value)
		{
			var result = 0;
			bool continuation;
			var shift = 0;
			
			do
			{
				int digit = Base64.ConvertChar(src[index++]);
				continuation = (digit & VLQ_CONTINUATION_BIT) != 0;
				digit &= VLQ_BASE_MASK;
				result = result + (digit << shift);
				shift = shift + VLQ_BASE_SHIFT;

			} while (continuation);

			value = FromVLQSigned(result);
		}
	}
}