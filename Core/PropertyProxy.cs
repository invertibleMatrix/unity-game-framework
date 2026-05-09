// ReSharper disable All

namespace AK.Core
{
	/// <summary>
	/// A class that wraps a property of type <typeparamref name="T"/> and provides an event that is invoked whenever the property changes.
	/// </summary>
	/// <typeparam name="T">The type of the property.</typeparam>
	public sealed class PropertyProxy<T>
	{
		private T m_Property = default;

		/// <summary>
		/// <see cref="Current"/> internal value of this propery...
		/// </summary>
		public T Current => m_Property;

		/// <summary>
		/// An event that is invoked whenever the value of the <see cref="Write"/> property changes.
		/// </summary>
		public readonly UnityEngine.Events.UnityEvent<T> OnChange = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="PropertyProxy{T}"/> class with the specified initial value.
		/// </summary>
		/// <param name="property">The initial value of the property.</param>
		public PropertyProxy(T property)
		{
			m_Property = property;
		}

		/// <summary>
		///Sets the value of the property. When the value is set, the <see cref="OnChange"/> event is invoked.
		/// </summary>
		public void Write(T value)
		{
			m_Property = value;
			OnChange.Invoke(m_Property);
		}

		/// <summary>
		/// Implicit conversion from <see cref="PropertyProxy{T}"/> to the underlying type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="reactiveProperty">The reactive property to convert.</param>
		public static implicit operator T(PropertyProxy<T> reactiveProperty) => reactiveProperty.Current;

		/// <summary>
		/// Implicit conversion from the underlying type <typeparamref name="T"/> to a <see cref="PropertyProxy{T}"/>.
		/// </summary>
		/// <param name="value">The value to wrap in a reactive property.</param>
		public static implicit operator PropertyProxy<T>(T value) => new(value);
	}
}