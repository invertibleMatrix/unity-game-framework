using System.Collections.Generic;
using UnityEngine.Events;

namespace AK.Core
{
	/// <summary>
	/// <see cref="PrefsProperty{TProp}"/> is a wrapper around <see cref="UniPrefs"/>'s API
	/// which save/load operations over a property... 
	/// </summary>
	/// <typeparam name="T">TypeOf property to wrap around, Make sure It's Serializable</typeparam>
	public sealed class PrefsProperty<T>
	{
		private T m_Current = default;
		private bool m_IsSyncWithPrefs = false;
		
		private readonly T m_Default = default;
		private readonly string m_SaveKey = default;

		/// <summary>
		/// Create & Returns the InstanceOf <see cref="PrefsProperty{TProp}"/> with the given SaveKey...
		/// </summary>
		/// <param name="saveKey"><see cref="string"/> Key To Use As Key In Database...</param>
		/// <param name="default">Default Value To Save On Creation...</param>
		public PrefsProperty(string saveKey, T @default = default)
		{
			m_SaveKey = saveKey;
			m_Default = @default;
			
			m_Current = @default;
			m_IsSyncWithPrefs = false;

			UniPrefs.OnReset += Reset;
		}

		~PrefsProperty() => UniPrefs.OnReset -= Reset;

		/// <summary>
		/// <see cref="Save"/> is going to save the given data in <see cref="UniPrefs"/>
		/// & also update <see cref="m_Current"/> runtime state...
		/// </summary>
		public void Save(T toSave = default)
		{
			if (toSave is not null)
			{
				m_Current = toSave;
			}

			m_IsSyncWithPrefs = true;
			UniPrefs.Set(m_SaveKey, m_Current);
		}

		/// <summary>
		/// <see cref="Read"/> this property data from save system & also update current runtime state...
		/// </summary>
		/// <returns>returns save data if exists...</returns>
		public T Read()
		{
			if (m_IsSyncWithPrefs) return m_Current;

			m_IsSyncWithPrefs = true;
			return m_Current = UniPrefs.Get(m_SaveKey, m_Current);
		}

		/// <summary>
		/// Reset this <see cref="PrefsProperty{T}"/> & delete it from <see cref="UniPrefs"/>...
		/// </summary>
		public void Reset()
		{
			if (UniPrefs.HasKey(m_SaveKey))
			{
				UniPrefs.Delete(m_SaveKey);
			}
			
			m_Current = m_Default;
			m_IsSyncWithPrefs = false;
		}

		/// <summary>
		/// <see cref="ToString"/> override for <see cref="PrefsProperty{TProp}"/> which converts <see cref="m_Current"/>'s
		/// <see cref="ToString"/> & Returns...
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return m_Current == null ? string.Empty : m_Current.ToString();
		}

		/// <summary>
		/// an implicit operator overload to get <see cref="m_Current"/> state of this property...
		/// </summary>
		public static implicit operator T(PrefsProperty<T> property) => property.Read();
	}
}