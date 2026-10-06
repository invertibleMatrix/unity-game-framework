using AK.Editor;
using NUnit.Framework;

namespace AK.Tests.Integrations
{
	/// <summary>The Facebook SDK's define in Assets/csc.rsp: its line comes and goes, and the rest of the file stays.</summary>
	public class FacebookSdkDefineTests
	{
		private const string Line = "-define:UGFW_FACEBOOK_SDK";

		[Test]
		public void HasDefine_FindsTheLine_Trimmed()
		{
			Assert.IsFalse(FacebookSdkDefine.HasDefine(null));
			Assert.IsFalse(FacebookSdkDefine.HasDefine(""));
			Assert.IsFalse(FacebookSdkDefine.HasDefine("-define:UGFW_FACEBOOK_SDK_OLD\n"));
			Assert.IsTrue(FacebookSdkDefine.HasDefine(Line));
			Assert.IsTrue(FacebookSdkDefine.HasDefine("-nowarn:0618\r\n  " + Line + "  \r\n"));
		}

		[Test]
		public void WithDefine_StartsAFile()
		{
			Assert.AreEqual(Line + "\n", FacebookSdkDefine.WithDefine(null, true));
		}

		[Test]
		public void WithDefine_AddsTheLineAtTheEnd_WithTheFilesLineEndings()
		{
			Assert.AreEqual("-nowarn:0618\r\n" + Line + "\r\n", FacebookSdkDefine.WithDefine("-nowarn:0618\r\n", true));
			Assert.AreEqual("-nowarn:0618\n" + Line + "\n", FacebookSdkDefine.WithDefine("-nowarn:0618", true));
		}

		[Test]
		public void WithDefine_LeavesAFileWithTheLine_AsIs()
		{
			string text = Line + "\n-nowarn:0618\n";
			Assert.AreSame(text, FacebookSdkDefine.WithDefine(text, true));
		}

		[Test]
		public void WithDefine_RemovesOnlyTheLine()
		{
			Assert.AreEqual(
				"-nowarn:0618\r\n-define:OTHER\r\n",
				FacebookSdkDefine.WithDefine("-nowarn:0618\r\n" + Line + "\r\n-define:OTHER\r\n", false));
		}

		[Test]
		public void WithDefine_IsNull_WhenNothingElseIsLeft()
		{
			Assert.IsNull(FacebookSdkDefine.WithDefine(Line + "\n", false));
			Assert.IsNull(FacebookSdkDefine.WithDefine("\n" + Line + "\n\n", false));
			Assert.IsNull(FacebookSdkDefine.WithDefine(null, false));
		}
	}
}
