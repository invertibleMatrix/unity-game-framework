#if !ODIN_INSPECTOR
namespace Sirenix.OdinInspector
{
	[System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
		public class ShowIfAttribute : System.Attribute
	{
		public ShowIfAttribute() { }
		public ShowIfAttribute(string group) { }
		public ShowIfAttribute(string group,object val) { }
	}
}
#endif