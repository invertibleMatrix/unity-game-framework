namespace AK.Core
{
	/// <summary>
	/// A value of type <typeparamref name="T"/> that announces every write through <see cref="OnChange"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	public sealed class PropertyProxy<T>
	{
		private T m_Property = default;

		/// <summary>The value last written, or the initial one.</summary>
		public T Current => m_Property;

		/// <summary>Invoked on every <see cref="Write"/>, with the value written, whether or not it changed.</summary>
		public readonly UnityEngine.Events.UnityEvent<T> OnChange = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="PropertyProxy{T}"/> class with the specified initial value.
		/// </summary>
		/// <param name="property">The initial value of the property.</param>
		public PropertyProxy(T property)
		{
			m_Property = property;
		}

		/// <summary>Sets the value and invokes <see cref="OnChange"/>.</summary>
		public void Write(T value)
		{
			m_Property = value;
			OnChange.Invoke(m_Property);
		}
	}
}
